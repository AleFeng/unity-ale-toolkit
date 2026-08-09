using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ale.Toolkit.Runtime.UI
{
    // ── 焦点停靠位置 ──────────────────────────────────────────────────────────────
    /// <summary>焦点在视口中的停靠位置。滚动时，中心最接近该位置的条目即为「焦点条目」。</summary>
    public enum EFocusAnchor
    {
        /// <summary>视口顶端（第一个完整可见的格子）。</summary>
        Top,
        /// <summary>视口正中。</summary>
        Center,
        /// <summary>视口底端。</summary>
        Bottom,
    }

    // ── 聚焦式顺序虚拟列表 ────────────────────────────────────────────────────────
    /// <summary>
    /// 带「焦点条目」语义的顺序虚拟滚动列表。在 <see cref="UiwVirtualOrderList{TData,TCell}"/>
    /// 的单列纵向布局之上补两件事：
    ///
    /// <list type="number">
    /// <item><b>焦点跟踪</b>——视口中的某个位置（<see cref="focusAnchor"/>）被定为焦点线，
    /// 中心最接近它的条目即焦点条目；滚动导致焦点改变时抛 <see cref="OnFocusChanged"/>。
    /// 这让「滚到哪儿就选中哪儿」的转盘 / 拨轮式交互不必额外接线：选中态直接由滚动位置派生，
    /// 不需要点击，也不需要在格子上挂选中逻辑。</item>
    /// <item><b>随焦点距离变化的外观</b>——可选的两条曲线按「该格中心离焦点线有多远」驱动缩放与横向偏移，
    /// 得到中间大两头小、并向两侧让开的弧形排布。</item>
    /// </list>
    ///
    /// <para>不改动基类的滚动模型：滚动仍由 <c>ScrollRect</c> 驱动（拖拽 / 滚轮 / 惯性都是它原生的），
    /// 本类只读取滚动位置来派生焦点与外观，<b>不</b>接管输入、<b>不</b>做释放后吸附对齐。</para>
    ///
    /// <para><b>缩放以格子自身轴心为中心</b>：基类把格子的 pivot 设为顶端居中 <c>(0.5, 1)</c>，
    /// 因此放大时格子是从自己的上边缘向下、向两侧长开的，而不是从格子正中向四周扩张。</para>
    ///
    /// <para>叶子类照常实现 <see cref="UiwVirtualListBase{TData,TCell}.BindCell"/> /
    /// <see cref="UiwVirtualListBase{TData,TCell}.ClearCell"/> 即可，本类不额外要求覆写任何钩子。</para>
    /// </summary>
    /// <typeparam name="TData">列表数据元素类型。</typeparam>
    /// <typeparam name="TCell">格子显示组件类型。</typeparam>
    public abstract class UiwFocusOrderList<TData, TCell> : UiwVirtualOrderList<TData, TCell>
        where TCell : Component
    {
        #region Inspector 配置

        [Header("焦点")]
        [Tooltip("焦点在视口中的停靠位置。中心最接近该位置的条目即为焦点条目。")]
        public EFocusAnchor focusAnchor = EFocusAnchor.Center;

        [Tooltip("焦点缩放曲线。x = 该格中心相对焦点线的归一化距离（[-1,1]，±1 对应视口上 / 下边缘），" +
                 "y = 该格的 localScale。留空（无关键帧）则完全不改动缩放。")]
        public AnimationCurve focusScaleCurve;

        [Tooltip("焦点横向偏移曲线。x 同上，y = 该格的横向偏移（像素，正值向右）。" +
                 "留空（无关键帧）则完全不改动位置。")]
        public AnimationCurve focusOffsetCurve;

        #endregion

        #region 焦点

        /// <summary>当前焦点条目的数据索引；列表为空时为 -1。</summary>
        public int FocusedIndex => _focusedIndex;

        /// <summary>
        /// 焦点条目改变时触发，参数为 (上一个索引, 当前索引)。列表为空时当前索引为 -1。
        /// <para>数据整体替换（<see cref="SetItems"/>）后必定触发一次，便于调用方以此初始化选中态。</para>
        /// </summary>
        public event Action<int, int> OnFocusChanged;

        private int _focusedIndex = -1;

        /// <summary>取当前焦点条目对应的活跃格子。焦点无效、或该条目尚未分配到格子时返回 false。</summary>
        public bool TryGetFocusedCell(out TCell cell)
        {
            cell = null;
            if (_focusedIndex < 0) return false;
            if (!TryGetActiveCell(_focusedIndex, out cell)) return false;

            // 上转型到 Component 再判空：泛型形参 TCell 拿不到 UnityEngine.Object 的 bool 重载。
            if ((Component)cell) return true;

            cell = null;
            return false;
        }

        /// <summary>
        /// 立即把指定条目滚到焦点线上（瞬移，不做补间）。
        /// <para>滚动量会夹取到可滚动范围内，避免把 content 推出边界后被 ScrollRect 的回弹拉回、导致焦点跳变。</para>
        /// </summary>
        public void FocusIndex(int index)
        {
            if (!scrollRect || !scrollRect.viewport || !content) return;

            int count = items?.Count ?? 0;
            if (count <= 0) return;

            float cellHeight = CellHeight;
            if (cellHeight <= 0f) return;

            index = Mathf.Clamp(index, 0, count - 1);

            float viewportHeight = scrollRect.viewport.rect.height;
            // 由「第 index 条的中心落在焦点线上」反解所需滚动量。
            float scrollY = index * cellHeight + cellHeight * 0.5f - FocusLine(viewportHeight, cellHeight);
            float maxScroll = Mathf.Max(0f, count * cellHeight - viewportHeight);
            scrollY = Mathf.Clamp(scrollY, 0f, maxScroll);

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, scrollY);
            UpdateVisibleCells();
            UpdateFocusAndAppearance();
        }

        #endregion

        #region 生命周期

        public override void SetItems(IReadOnlyList<TData> itemsParam)
        {
            base.SetItems(itemsParam);

            // 数据整体替换并回到起点，焦点必然要重算。先置为无效，保证随后必定抛一次 OnFocusChanged——
            // 调用方由此可以只订阅事件、不必再单独读一次初始焦点。
            _focusedIndex = -1;
            UpdateFocusAndAppearance();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();   // 视口尺寸变化重建 + 限速填充
            UpdateFocusAndAppearance();
        }

        #endregion

        #region 焦点与外观计算

        /// <summary>焦点线距视口顶端的距离（像素）。</summary>
        private float FocusLine(float viewportHeight, float cellHeight)
        {
            switch (focusAnchor)
            {
                case EFocusAnchor.Top:    return cellHeight * 0.5f;
                case EFocusAnchor.Bottom: return viewportHeight - cellHeight * 0.5f;
                default:                  return viewportHeight * 0.5f;
            }
        }

        // 防重入：OnFocusChanged 的订阅方若在回调里改数据（如 SetItems），会重新走进本方法；
        // 不拦住则可能形成事件递归。嵌套调用直接返回——外层这一趟走完后状态即已一致。
        private bool _updatingFocus;

        private void UpdateFocusAndAppearance()
        {
            if (_updatingFocus) return;
            if (!scrollRect || !scrollRect.viewport || !content) return;

            float cellHeight = CellHeight;
            if (cellHeight <= 0f) return;

            _updatingFocus = true;
            try
            {
                int   count          = items?.Count ?? 0;
                float viewportHeight = scrollRect.viewport.rect.height;
                float scrollY        = content.anchoredPosition.y;
                float focusLine      = FocusLine(viewportHeight, cellHeight);

                // 第 i 条的中心距视口顶端 = i*h + h/2 - scrollY；令其等于焦点线，反解 i 并取最近的整数。
                int newFocus = count > 0
                    ? Mathf.Clamp(
                        Mathf.RoundToInt((focusLine + scrollY - cellHeight * 0.5f) / cellHeight), 0, count - 1)
                    : -1;

                if (newFocus != _focusedIndex)
                {
                    int previous = _focusedIndex;
                    _focusedIndex = newFocus;
                    OnFocusChanged?.Invoke(previous, newFocus);
                }

                ApplyFocusAppearance(count, cellHeight, viewportHeight, scrollY, focusLine);
            }
            finally
            {
                _updatingFocus = false;
            }
        }

        private void ApplyFocusAppearance(
            int count, float cellHeight, float viewportHeight, float scrollY, float focusLine)
        {
            bool hasScale  = HasKeys(focusScaleCurve);
            bool hasOffset = HasKeys(focusOffsetCurve);
            if (!hasScale && !hasOffset) return;
            if (count <= 0 || viewportHeight <= 0f) return;

            // 只遍历视口覆盖到的数据索引（两端各留一格富余），逐个向基类要其活跃格子。
            // 不自建「索引 → 格子」映射，避免与基类的回收 / 复用循环产生第二份需要同步的状态。
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollY / cellHeight) - 1);
            int last  = Mathf.Min(count - 1, Mathf.CeilToInt((scrollY + viewportHeight) / cellHeight) + 1);

            // 归一化基准取视口半高：视口上 / 下边缘恰好落在曲线定义域的 ∓1 / ±1。
            float halfSpan = viewportHeight * 0.5f;

            for (int i = first; i <= last; i++)
            {
                if (!TryGetActiveCell(i, out var cell)) continue;
                if (!(Component)cell) continue;

                float centerY = i * cellHeight + cellHeight * 0.5f - scrollY;   // 该格中心距视口顶端
                float t = Mathf.Clamp((centerY - focusLine) / halfSpan, -1f, 1f);

                var rt = (RectTransform)cell.transform;

                if (hasScale)
                {
                    float scale = focusScaleCurve.Evaluate(t);
                    var current = rt.localScale;
                    // 仅在确有变化时回写：写 transform 会让 UGUI 标脏并触发画布重建，
                    // 静止时逐帧写入同样的值等于每帧白重建一次画布。
                    if (!Mathf.Approximately(current.x, scale) || !Mathf.Approximately(current.y, scale))
                        rt.localScale = new Vector3(scale, scale, 1f);
                }

                if (hasOffset)
                {
                    float offsetX = focusOffsetCurve.Evaluate(t);
                    var current = rt.anchoredPosition;
                    // 只改 x：y 是基类按 PositionOf(i) 定的行位置，覆盖它会把整个虚拟滚动的定位打乱。
                    if (!Mathf.Approximately(current.x, offsetX))
                        rt.anchoredPosition = new Vector2(offsetX, current.y);
                }
            }
        }

        /// <summary>曲线是否真的配置过。Inspector 上「留空」的 AnimationCurve 是零关键帧而非 null，故两者都要判。</summary>
        private static bool HasKeys(AnimationCurve curve) => curve != null && curve.length > 0;

        #endregion
    }
}
