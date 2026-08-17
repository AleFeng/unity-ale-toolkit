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
        //
        // 两个 PositionAt* 的差别（名字像，语义不同，别混）：
        //   PositionAtCursor    输入屏幕像素 → 写 rt.position（世界），并夹取回屏内
        //   PositionAtWorldPos  输入世界坐标 → 写 rt.localPosition（以父级为基准），不夹取

        /// <summary>
        /// 把 <paramref name="rt"/> 摆到世界坐标 <paramref name="worldPos"/> 在 UI 上对应的位置。
        ///
        /// <para><b>基准取父级而非 Canvas</b>：结果写的是 <c>localPosition</c>，而它的原点就是父级的轴心。
        /// 两者只在「父级恰好与 Canvas 原点重合」时等价——中间层一旦带了偏移或换了锚点，
        /// 以 Canvas 为基准算出的值会整体错开那段偏移，且错法酷似「相机没对上」，极难查。
        /// 直接用本方法就不存在「基准与赋值目标不一致」这种配错法。</para>
        ///
        /// <para>父级不是 RectTransform（或 <paramref name="rt"/> 无父级）时退回以 Canvas 为基准。</para>
        /// </summary>
        /// <param name="rt">要定位的矩形。</param>
        /// <param name="worldPos">世界空间位置。</param>
        /// <param name="canvas">目标 Canvas。</param>
        /// <param name="worldCamera">把世界坐标投影到屏幕所用的相机，见 <see cref="ResolveWorldCamera"/>。</param>
        public static void PositionAtWorldPos(RectTransform rt, Vector3 worldPos, Canvas canvas, Camera worldCamera = null)
        {
            if (!rt) return;

            rt.localPosition = WorldPosToUILocalPos(worldPos, canvas, rt.parent as RectTransform, worldCamera);
        }

        /// <summary>
        /// 世界坐标转为 UI 画布局部坐标，附加一段<b>世界空间</b>偏移。
        /// <para>偏移走世界空间而非屏幕像素：同样的像素偏移在不同分辨率下对应的世界距离不同，
        /// 会让挂件与目标物体的相对位置随分辨率漂移。</para>
        /// </summary>
        /// <param name="worldPos">世界空间位置。</param>
        /// <param name="offsetWorldSpace">世界空间位置偏移。</param>
        /// <param name="canvas">目标 Canvas。</param>
        /// <param name="rectTransform">换算所依据的矩形，缺省用 <paramref name="canvas"/> 自身的 RectTransform。</param>
        /// <param name="worldCamera">把世界坐标投影到屏幕所用的相机，见 <see cref="ResolveWorldCamera"/>。</param>
        /// <returns>局部空间位置，可直接赋给 <c>rectTransform.localPosition</c>。</returns>
        public static Vector3 WorldPosToUILocalPos(
            Vector3 worldPos, Vector3 offsetWorldSpace, Canvas canvas,
            RectTransform rectTransform = null, Camera worldCamera = null)
            => WorldPosToUILocalPos(worldPos + offsetWorldSpace, canvas, rectTransform, worldCamera);

        /// <summary>
        /// 世界坐标转为 UI 画布局部坐标。
        /// <para>WorldSpace Canvas 与场景同处一个世界空间，原样返回；ScreenSpace Canvas 先投影到屏幕，
        /// 再经 <see cref="ScreenPosToUILocalPos"/> 换算到局部坐标。</para>
        /// </summary>
        /// <param name="worldPos">世界空间位置。</param>
        /// <param name="canvas">目标 Canvas。</param>
        /// <param name="rectTransform">换算所依据的矩形，缺省用 <paramref name="canvas"/> 自身的 RectTransform。</param>
        /// <param name="worldCamera">把世界坐标投影到屏幕所用的相机，见 <see cref="ResolveWorldCamera"/>。</param>
        /// <returns>局部空间位置，可直接赋给 <c>rectTransform.localPosition</c>。</returns>
        public static Vector3 WorldPosToUILocalPos(
            Vector3 worldPos, Canvas canvas, RectTransform rectTransform = null, Camera worldCamera = null)
        {
            if (!canvas) return worldPos;

            // WorldSpace Canvas：UI 就在世界空间里，无需换算（也就用不到相机）。
            if (canvas.renderMode == RenderMode.WorldSpace)
                return worldPos;

            Camera camera = ResolveWorldCamera(worldCamera, canvas);

            Vector3 screenPos;
            if (camera)
            {
                screenPos = camera.WorldToScreenPoint(worldPos);
            }
            else
            {
                WarnNoWorldCameraOnce(canvas);
                // 无相机可用：退回无相机投影（等价于 Overlay 语义，世界坐标即屏幕像素）。
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                screenPos = new Vector3(sp.x, sp.y, 0f);
            }

            return ScreenPosToUILocalPos(screenPos, canvas, rectTransform);
        }

        /// <summary>
        /// 解析「把世界坐标投影到屏幕」所用的相机：显式传入的优先，其次 <see cref="Camera.main"/>，
        /// 最后才兜底到 <c>canvas.worldCamera</c>。
        ///
        /// <para><b>务必显式传入</b>：<c>Camera.main</c> 依赖「场景里有且仅有一台打了 MainCamera 标签的
        /// 相机」这条隐含约定——标签漏打时它直接是 <c>null</c>，分屏 / 多相机 / RenderTexture 下则取错那台。
        /// 调用方通常明确知道是哪台相机在渲染目标物体，交进来即可，也就不必再依赖场景标签。</para>
        ///
        /// <para><c>canvas.worldCamera</c> 排在最后且仅作兜底：它是 <b>UI 的</b>渲染相机，与渲染游戏世界的
        /// 那台分离时拿它做世界投影会得到错误结果；Overlay 模式下它按定义恒为 <c>null</c>。</para>
        /// </summary>
        public static Camera ResolveWorldCamera(Camera worldCamera, Canvas canvas)
        {
            if (worldCamera) return worldCamera;
            if (Camera.main) return Camera.main;
            return canvas ? canvas.worldCamera : null;
        }

        // 已就「取不到世界相机」报过警的 Canvas 实例 ID。
        private static readonly HashSet<int> NoWorldCameraWarned = new HashSet<int>();

        /// <summary>
        /// 就某个 Canvas 上「三个相机来源全空」告警一次。
        ///
        /// <para><b>为什么值得单独报</b>：此时换算退化成「世界坐标的 XY 直接当屏幕像素」——2D 工程里世界坐标
        /// 通常是个位数，于是所有挂件都塌到屏幕左下角那一小撮，彼此只差不到一个像素。这是纯粹的配置事故
        /// （相机没打 MainCamera 标签、调用方也没把相机交进来），但退化本身悄无声息，症状又酷似
        /// 「UI 预制体的锚点 / 轴心配错」，不报出来只会往错的方向查。</para>
        ///
        /// <para><b>必须去重</b>：跟随物体的挂件是每帧定位的，不去重会刷屏。
        /// 去重表随程序域存活，域重载（改代码、进退播放模式）后重新计数。</para>
        /// </summary>
        private static void WarnNoWorldCameraOnce(Canvas canvas)
        {
            if (!canvas || !NoWorldCameraWarned.Add(canvas.GetInstanceID())) return;

            Debug.LogWarning(
                $"[Toolkit.UI] Canvas '{canvas.name}' 上做世界坐标 → UI 坐标换算时取不到相机：" +
                $"调用方未传入、场景中没有 MainCamera 标签的相机、Canvas.worldCamera 也为空" +
                $"（Overlay 模式下它恒为空）。本次换算已退化为「世界坐标当屏幕像素」，" +
                $"UI 会整体塌向屏幕左下角。请把渲染该世界物体的相机传给换算方法，" +
                $"或给场景相机打上 MainCamera 标签。（同一 Canvas 只报一次）", canvas);
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
