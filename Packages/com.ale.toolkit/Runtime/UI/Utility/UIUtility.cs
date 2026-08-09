using System.Collections.Generic;
using UnityEngine;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// UI 共用工具方法集合（静态）。用于沉淀各处 UI 通用的辅助 / 工具方法，避免重复代码；后续可按主题分区扩充。
    /// </summary>
    public static class UIUtility
    {
        #region 悬停弹窗定位
        // 依据弹窗所在 Canvas 的 RenderMode 选择「屏幕坐标 → 世界坐标」换算：
        //   Overlay                        → 无定位相机（世界坐标即屏幕像素，直接落位）
        //   ScreenSpaceCamera / WorldSpace → Canvas.worldCamera（缺省回退 Camera.main）

        // GetWorldCorners 复用缓冲，避免每次定位产生 GC 分配（主线程同步调用，可安全共用）。
        private static readonly Vector3[] Corners = new Vector3[4];

        // 缓存「弹窗 RectTransform → 其根 Canvas」，避免每次定位都执行 GetComponentInParent（逐级向上遍历，开销较大）。
        // 只缓存这一步查找；renderMode / worldCamera / Camera.main 仍每次实时读取（开销极小），故相机变化时不会用到过期值。
        // 缓存的 Canvas 被销毁（如弹窗被改挂到别的 Canvas 下）时会自动重新查找。
        // 缓存的 Canvas 被销毁（如弹窗被改挂到别的 Canvas 下）时会自动重新查找；
        // 但**键**（RectTransform）不会自己消失——弹窗 / 格子销毁后条目仍留在表里，
        // 持有已销毁对象的托管包装，条目数只增不减。故在回填前按阈值清理死键。
        private static readonly Dictionary<RectTransform, Canvas> RootCanvasCache = new Dictionary<RectTransform, Canvas>();

        // 达到该条目数才做一次死键清扫（清扫是 O(n)，不必每次回填都做）。
        private const int CacheSweepThreshold = 64;

        // 清扫复用缓冲：避免每次清扫都分配（主线程同步调用，可安全共用）。
        private static readonly List<RectTransform> DeadKeys = new List<RectTransform>();

        /// <summary>条目数超阈值时，移除键已被销毁的缓存条目。</summary>
        private static void PruneCanvasCache()
        {
            if (RootCanvasCache.Count < CacheSweepThreshold) return;

            DeadKeys.Clear();
            foreach (var kv in RootCanvasCache)
                if (!kv.Key) DeadKeys.Add(kv.Key);

            for (int i = 0; i < DeadKeys.Count; i++)
                RootCanvasCache.Remove(DeadKeys[i]);
            DeadKeys.Clear();
        }

        /// <summary>
        /// 将 <paramref name="rt"/> 定位到光标处（<paramref name="screenPos"/> + <paramref name="cursorOffset"/> 像素偏移），
        /// 再夹取回屏幕内。换算所需的相机由 <paramref name="rt"/> 自身或父级的 Canvas 渲染模式决定。
        /// <paramref name="ignoreCache"/> 为 true 时忽略缓存、强制重新查找根 Canvas 并回填（默认 false，优先用缓存）。
        /// </summary>
        public static void PositionAtCursor(RectTransform rt, Vector2 screenPos, Vector2 cursorOffset, bool ignoreCache = false)
        {
            if (!rt) return;

            var cam = ResolveCanvasCamera(rt, ignoreCache);
            Vector2 target = screenPos + cursorOffset;

            if (!cam)
            {
                // Overlay：世界坐标即屏幕像素，直接落位（保持原 z 所在的画布平面）。
                var p = rt.position;
                rt.position = new Vector3(target.x, target.y, p.z);
            }
            else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, target, cam, out var world))
            {
                // ScreenSpaceCamera / WorldSpace：把屏幕点换算到 rt 所在平面的世界点。
                rt.position = world;
            }

            ClampToScreen(rt, cam);
        }

        /// <summary>
        /// 取 <paramref name="c"/> 自身或父级最近 Canvas 的<b>根</b> Canvas，未找到返回 null。
        /// <para>子 Canvas 继承根 Canvas 的渲染模式与相机，涉及坐标换算 / 层级挂载时一律以根为准。
        /// 拖拽残影挂载、指针坐标换算等三处此前各写了一遍这两行。</para>
        /// <para><b>不带缓存</b>：<c>GetComponentInParent</c> 会逐级向上遍历，请勿在每帧路径上调用；
        /// 每帧需要定位相机时用 <see cref="ResolveCanvasCamera"/>（带缓存）。</para>
        /// </summary>
        public static Canvas ResolveRootCanvas(Component c)
        {
            if (!c) return null;
            var canvas = c.GetComponentInParent<Canvas>();
            return canvas ? (canvas.rootCanvas ? canvas.rootCanvas : canvas) : null;
        }

        /// <summary>
        /// 取 <paramref name="rt"/> 自身或父级最近 Canvas 的定位相机：
        /// Overlay 返回 null；ScreenSpaceCamera / WorldSpace 返回 Canvas.worldCamera（缺省回退 <see cref="Camera.main"/>）。
        /// <paramref name="ignoreCache"/> 为 true 时忽略缓存、强制重新查找根 Canvas 并回填（默认 false）。
        /// </summary>
        public static Camera ResolveCanvasCamera(RectTransform rt, bool ignoreCache = false)
        {
            if (!rt) return null;

            // 命中且未被销毁 → 直接用；未缓存 / 已销毁 / 弹窗改换父级 / 显式忽略缓存 → 重新查找根 Canvas 并回填缓存。
            if (ignoreCache || !RootCanvasCache.TryGetValue(rt, out var root) || !root)
            {
                root = ResolveRootCanvas(rt);
                PruneCanvasCache();
                RootCanvasCache[rt] = root;
            }
            if (!root) return null;

            switch (root.renderMode)
            {
                case RenderMode.ScreenSpaceCamera:
                case RenderMode.WorldSpace:
                    return root.worldCamera ? root.worldCamera : Camera.main;
                default: // ScreenSpaceOverlay
                    return null;
            }
        }

        /// <summary>清空「RectTransform → 根 Canvas」缓存。一般无需手动调用；弹窗大规模销毁 / 重建后可用于释放残留条目。</summary>
        public static void ClearCanvasCache() => RootCanvasCache.Clear();

        /// <summary>
        /// 任一侧超出屏幕时，把弹窗整体移回并使该侧紧贴边界。越界量在屏幕像素空间计算，
        /// 再按定位相机换算回世界位移，因此对三种 RenderMode 均成立。
        /// </summary>
        private static void ClampToScreen(RectTransform rt, Camera cam)
        {
            if (!rt) return;

            rt.GetWorldCorners(Corners); // 世界坐标四角：0=左下 1=左上 2=右上 3=右下
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
            float minX = Mathf.Min(bl.x, tr.x), maxX = Mathf.Max(bl.x, tr.x);
            float minY = Mathf.Min(bl.y, tr.y), maxY = Mathf.Max(bl.y, tr.y);

            float w = Screen.width, h = Screen.height;
            float dx = 0f, dy = 0f;
            if (maxX > w)       dx = w - maxX;   // 右越界 → 左移贴右边界
            if (minX + dx < 0f) dx = -minX;      // 左越界（或比屏幕更宽）→ 贴左边界优先
            if (maxY > h)       dy = h - maxY;   // 上越界 → 下移贴上边界
            if (minY + dy < 0f) dy = -minY;      // 下越界（或比屏幕更高）→ 贴下边界优先
            if (dx == 0f && dy == 0f) return;

            if (!cam)
            {
                // Overlay：屏幕像素位移即世界位移。
                rt.position += new Vector3(dx, dy, 0f);
                return;
            }

            // ScreenSpaceCamera / WorldSpace：把当前锚点的屏幕位置加上位移后，换算回世界点。
            Vector2 curScreen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            Vector2 newScreen = curScreen + new Vector2(dx, dy);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, newScreen, cam, out var world))
                rt.position = world;
        }

        #endregion

        #region 世界坐标 → UI 坐标
        // 把世界空间中某个物体的位置换算成某个 Canvas 下的局部坐标，用于让 UI 挂件（血条 / 名牌 /
        // 操作菜单）"贴"在场景物体上。
        //
        // 与上方「悬停弹窗定位」的分工：那边的输入是屏幕像素（光标），直接写 rt.position 并夹取回屏内；
        // 这边的输入是世界坐标，返回值交由调用方赋给 localPosition，不做夹取。

        /// <summary>
        /// 世界坐标转为 UI 画布局部坐标，附加一段<b>世界空间</b>偏移。
        /// <para>偏移走世界空间而非屏幕像素：同样的像素偏移在不同分辨率下对应的世界距离不同，
        /// 会让挂件与目标物体的相对位置随分辨率漂移。</para>
        /// </summary>
        /// <param name="worldPos">世界空间位置。</param>
        /// <param name="offsetWorldSpace">世界空间位置偏移。</param>
        /// <param name="canvas">目标 Canvas。</param>
        /// <param name="rectTransform">换算所依据的矩形，缺省用 <paramref name="canvas"/> 自身的 RectTransform。</param>
        /// <returns>局部空间位置，可直接赋给 <c>rectTransform.localPosition</c>。</returns>
        public static Vector3 WorldPosToUILocalPos(
            Vector3 worldPos, Vector3 offsetWorldSpace, Canvas canvas, RectTransform rectTransform = null)
            => WorldPosToUILocalPos(worldPos + offsetWorldSpace, canvas, rectTransform);

        /// <summary>
        /// 世界坐标转为 UI 画布局部坐标。
        /// <para>WorldSpace Canvas 与场景同处一个世界空间，原样返回；ScreenSpace Canvas 先投影到屏幕，
        /// 再经 <see cref="ScreenPosToUILocalPos"/> 换算到局部坐标。</para>
        /// </summary>
        /// <param name="worldPos">世界空间位置。</param>
        /// <param name="canvas">目标 Canvas。</param>
        /// <param name="rectTransform">换算所依据的矩形，缺省用 <paramref name="canvas"/> 自身的 RectTransform。</param>
        /// <returns>局部空间位置，可直接赋给 <c>rectTransform.localPosition</c>。</returns>
        public static Vector3 WorldPosToUILocalPos(
            Vector3 worldPos, Canvas canvas, RectTransform rectTransform = null)
        {
            if (!canvas) return worldPos;

            // WorldSpace Canvas：UI 就在世界空间里，无需换算。
            if (canvas.renderMode == RenderMode.WorldSpace)
                return worldPos;

            // 投影游戏世界坐标必须用游戏主相机：canvas.worldCamera 是 UI 专用渲染相机，
            // 两者分离时拿它做投影会得到错误结果。canvas.worldCamera 只在 ScreenPosToUILocalPos
            // 内部用于 ScreenPointToLocalPointInRectangle。
            Camera camera = Camera.main ? Camera.main : canvas.worldCamera;

            Vector3 screenPos;
            if (camera)
            {
                screenPos = camera.WorldToScreenPoint(worldPos);
            }
            else
            {
                // 无相机可用：退回无相机投影（等价于 Overlay 语义，世界坐标即屏幕像素）。
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                screenPos = new Vector3(sp.x, sp.y, 0f);
            }

            return ScreenPosToUILocalPos(screenPos, canvas, rectTransform);
        }

        /// <summary>
        /// 屏幕坐标转为 UI 画布局部坐标。
        /// </summary>
        /// <param name="screenPos">屏幕像素坐标。</param>
        /// <param name="canvas">目标 Canvas。</param>
        /// <param name="rectTransform">换算所依据的矩形，缺省用 <paramref name="canvas"/> 自身的 RectTransform。</param>
        /// <returns>局部空间位置，可直接赋给 <c>rectTransform.localPosition</c>。</returns>
        public static Vector3 ScreenPosToUILocalPos(
            Vector2 screenPos, Canvas canvas, RectTransform rectTransform = null)
        {
            if (!canvas) return Vector3.zero;

            // 缺省以 Canvas 自身的 RectTransform 为换算基准。
            if (!rectTransform)
                rectTransform = canvas.transform as RectTransform;
            if (!rectTransform) return Vector3.zero;

            // Overlay 必须传 null 相机；其余模式用 Canvas 的 UI 相机。
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPos, uiCamera, out Vector2 localPoint))
                return localPoint;

            // 兜底：矩形换算失败（如矩形退化为零尺寸）时，退回屏幕点对应的世界坐标。
            return ScreenToWorld(screenPos, uiCamera);
        }

        /// <summary>屏幕坐标转世界坐标。仅供 <see cref="ScreenPosToUILocalPos"/> 换算失败时兜底。</summary>
        private static Vector3 ScreenToWorld(Vector2 screenPos, Camera camera)
        {
            Camera cam = camera ? camera : Camera.main;
            if (!cam) return Vector3.zero;

            // z 取相机自身 z 的绝对值：沿用 2D 工程「相机在 z 负方向、内容在 z=0 平面」的惯例。
            var sp = new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z));
            return cam.ScreenToWorldPoint(sp);
        }

        #endregion
    }
}
