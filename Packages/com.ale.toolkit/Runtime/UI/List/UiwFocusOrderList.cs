using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
    /// <para><b>滚轮由本类接管并做平滑位移</b>（<see cref="scrollTweenDuration"/>，默认 0.1 秒）。
    /// 焦点列表一档滚轮通常正好跨一整条，原生 <c>ScrollRect</c> 直接改写 <c>content.anchoredPosition</c>，
    /// 表现为整条列表瞬间跳一格，焦点缩放曲线也跟着突变；补间后条目是滑过去的。
    /// 拖拽 / 惯性 / 边界回弹仍是 <c>ScrollRect</c> 原生的，本类不碰，拖拽开始时会取消进行中的补间。
    /// <b>仍不做释放后吸附对齐</b>——拖拽松手停在两条之间时，焦点缩放曲线会让上下两条都呈半放大态。</para>
    ///
    /// <para><b>首尾留白</b>：Content 在头尾各补一段空白，使<b>第一条与最后一条也能滚到焦点线上</b>。
    /// 没有这段留白时，条目是从 Content 顶端紧挨着排的，第一条的中心永远停在视口顶端附近、
    /// 够不到居中的焦点线；且条目少时 Content 比视口还矮，<c>ScrollRect</c> 干脆无从滚动——
    /// 表现为「所有条目挤在顶部且滚轮无反应」。补上留白后滚动量与焦点索引一一对应：
    /// 滚到 0 即焦点 0，滚到底即焦点末条。</para>
    ///
    /// <para><b>缩放以格子自身轴心为中心</b>：基类把格子的 pivot 设为正中 <c>(0.5, 0.5)</c>，
    /// 放大时格子从自己的正中向四周长开。这一点对本类是<b>必需</b>而非风格选择——轴心即缩放中心，
    /// 若取顶端，放大 s 倍的格子视觉中心会比布局算出的行中心低 <c>(s - 1) × 行距 / 2</c>，
    /// 焦点条目就对不准焦点线（缩放 1.5、行距 60 时正好偏 15 像素）。</para>
    ///
    /// <para><b>条目间距可调</b>：疏密由基类的 <c>rowPitchScale</c>（行距倍率）控制，本类的留白、焦点反解与
    /// 滚动换算一律以<b>行距</b>为单位，与格子自身高度无关。</para>
    ///
    /// <para>叶子类照常实现 <see cref="UiwVirtualListBase{TData,TCell}.BindCell"/> /
    /// <see cref="UiwVirtualListBase{TData,TCell}.ClearCell"/> 即可，本类不额外要求覆写任何钩子。</para>
    /// </summary>
    /// <typeparam name="TData">列表数据元素类型。</typeparam>
    /// <typeparam name="TCell">格子显示组件类型。</typeparam>
    // IScrollHandler 由基类实现，本类只覆写 OnScroll 加上平滑位移。
    public abstract class UiwFocusOrderList<TData, TCell> : UiwVirtualOrderList<TData, TCell>, IBeginDragHandler
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

        [Header("滚轮")]
        [Tooltip("滚轮切换焦点条目时的平滑位移时长（秒）。0 = 不补间，一档即瞬间跳到位（原生 ScrollRect 的表现）。\n" +
                 "一档滚轮的位移距离仍取自 ScrollRect 的 Scroll Sensitivity——本类只是把它改成滑过去而非跳过去。")]
        public float scrollTweenDuration = 0.1f;

        #endregion

        #region 布局：首尾留白

        // 头部 / 尾部留白（像素）。由视口高度与焦点锚点决定，使首尾两条也能停到焦点线上。
        private float _leadingPad;
        private float _trailingPad;

        /// <summary>Content 头部留白（像素）。第 0 条的顶端距 Content 顶端的距离。供子类做定位换算。</summary>
        protected float LeadingPad => _leadingPad;

        // 焦点线在视口内的位置决定了首尾各需要多少留白：
        // 头部留白 = 焦点线到视口顶端的距离 - 半个行距（把第 0 条的中心顶到焦点线上）；
        // 尾部留白同理，取焦点线到视口底端的距离。
        private void ComputePads(float viewportHeight, float rowPitch, out float leading, out float trailing)
        {
            float focusLine = FocusLine(viewportHeight, rowPitch);
            leading  = Mathf.Max(0f, focusLine - rowPitch * 0.5f);
            trailing = Mathf.Max(0f, viewportHeight - focusLine - rowPitch * 0.5f);
        }

        /// <summary>
        /// 视口尺寸变化时重算首尾留白。留白变了意味着所有行的位置整体平移，
        /// 故须把已分配的格子全部收回重排——基类的 <c>PositionOf</c> 只在「填充」时写一次位置，
        /// 不重排的话已在窗口内的格子会停在按旧留白算出的位置上。
        /// </summary>
        protected override void RecomputeLayout(Rect viewport)
        {
            base.RecomputeLayout(viewport);

            ComputePads(viewport.height, RowPitch, out float leading, out float trailing);
            if (Mathf.Approximately(leading, _leadingPad) && Mathf.Approximately(trailing, _trailingPad)) return;

            _leadingPad  = leading;
            _trailingPad = trailing;
            RegainAllInstances();
        }

        /// <summary>Content 高度 = 条目数 × 行距 + 首尾留白。空列表不留白（没有可聚焦的条目）。</summary>
        protected override void SetContentSize(int count)
        {
            var size = content.sizeDelta;
            size.y = count > 0 ? count * RowPitch + _leadingPad + _trailingPad : 0f;
            content.sizeDelta = size;
        }

        /// <summary>
        /// 第 index 条的纵向位置：在基类的逐行排布之上整体下移一个头部留白。
        /// 格子轴心居中，故给的是该行槽位的中心（含半个行距）。
        /// </summary>
        protected override Vector2 PositionOf(int index)
        {
            float pitch = RowPitch;
            return new Vector2(0f, -(_leadingPad + SlotOf(index) * pitch + pitch * 0.5f));
        }

        /// <summary>首个可见条目索引。滚动量需先扣掉头部留白，才是「越过了几个槽位」。</summary>
        protected override int ComputeFirstIndex(Vector2 contentAnchoredPos)
        {
            float pitch = RowPitch;
            if (pitch <= 0f) return 0;

            float scrollY = Mathf.Max(0f, contentAnchoredPos.y - _leadingPad);
            int firstSlot = Mathf.FloorToInt(scrollY / pitch) - bufferCount;
            return FirstIndexOfSlotWindow(firstSlot);
        }

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
        /// 立即把指定条目滚到焦点线上（瞬移，不做补间）。进行中的滚轮补间会被取消。
        /// <para>滚动量会夹取到可滚动范围内，避免把 content 推出边界后被 ScrollRect 的回弹拉回、导致焦点跳变。</para>
        /// </summary>
        public void FocusIndex(int index)
        {
            if (!scrollRect || !scrollRect.viewport || !content) return;

            int count = items?.Count ?? 0;
            if (count <= 0) return;

            float pitch = RowPitch;
            if (pitch <= 0f) return;

            // 本方法是瞬移，与补间是两个互斥的位置来源；不取消的话补间会在随后的帧里把位置拉回去。
            _tweening = false;

            index = Mathf.Clamp(index, 0, count - 1);

            float viewportHeight = scrollRect.viewport.rect.height;
            // 由「第 index 条的中心落在焦点线上」反解所需滚动量。倒序时它占的是另一个槽位，故按槽位算。
            float scrollY = _leadingPad + SlotOf(index) * pitch + pitch * 0.5f - FocusLine(viewportHeight, pitch);
            scrollY = Mathf.Clamp(scrollY, 0f, MaxScroll());

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, scrollY);
            UpdateVisibleCells();
            UpdateFocusAndAppearance();
        }

        #endregion

        #region 滚轮接管与平滑位移

        // 补间状态。_tweening 为 false 时整条路径提前返回，静止期无逐帧开销。
        private float _tweenFrom, _tweenTo, _tweenElapsed;
        private bool  _tweening;

        /// <summary>是否正在播放滚轮补间。</summary>
        public bool IsScrollTweening => _tweening;

        /// <summary>
        /// 焦点列表<b>恒接管</b>滚轮，与反不反向无关——一档滚轮通常正好跨一整条，
        /// 交给原生 <c>ScrollRect</c> 会整条列表瞬间跳一格、焦点缩放曲线跟着突变，
        /// 故必须自己接过来做平滑位移。
        /// </summary>
        protected override bool NeedsScrollTakeOver => true;

        /// <summary>滚轮：按一档步长推进目标滚动量，并（按需）补间过去。</summary>
        public override void OnScroll(PointerEventData eventData)
        {
            if (!ScrollTakenOver) return;                                   // 未接管 → 保持 ScrollRect 原生处理
            if (!scrollRect || !scrollRect.viewport || !content) return;

            float pitch = RowPitch;
            if (pitch <= 0f) return;

            // 方向解析（含反向开关）交给基类，本类只管补间。
            float delta = ResolveScrollDelta(eventData);
            if (Mathf.Approximately(delta, 0f)) return;

            float step = ScrollStep > 0f ? ScrollStep : pitch;
            // 起点取「当前补间终点」而非当前位置：连滚数档时才会逐档累加，
            // 否则每档都从半路的实际位置重新起算，越滚越短、最后停在两条之间。
            float from   = _tweening ? _tweenTo : content.anchoredPosition.y;
            float target = Mathf.Clamp(from - delta * step, 0f, MaxScroll());

            if (Mathf.Approximately(target, content.anchoredPosition.y) && !_tweening) return;

            if (scrollTweenDuration <= 0f)
            {
                _tweening = false;
                ApplyScrollY(target);
                return;
            }

            _tweenFrom    = content.anchoredPosition.y;
            _tweenTo      = target;
            _tweenElapsed = 0f;
            _tweening     = true;
        }

        /// <summary>开始拖拽时取消补间，把控制权交还给 <c>ScrollRect</c>，避免两者同时写位置。</summary>
        public void OnBeginDrag(PointerEventData eventData) => _tweening = false;

        /// <summary>当前可滚动范围上限。覆写基类：本类的 Content 高度还含首尾留白。</summary>
        protected override float MaxScroll()
        {
            if (!scrollRect || !scrollRect.viewport) return 0f;
            int count = items?.Count ?? 0;
            return Mathf.Max(0f, count * RowPitch + _leadingPad + _trailingPad - scrollRect.viewport.rect.height);
        }

        // 写入滚动量。直接改 anchoredPosition 不会触发 ScrollRect 的 onValueChanged，
        // 故须显式刷一次可见格——与 FocusIndex 同一套写法。
        private void ApplyScrollY(float y)
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
            UpdateVisibleCells();
        }

        // 推进补间。用 unscaledDeltaTime：timeScale 为 0 时 UI 仍应可滚（与基类的限速填充一致）。
        private void TickScrollTween()
        {
            if (!_tweening) return;
            if (!content) { _tweening = false; return; }

            _tweenElapsed += Time.unscaledDeltaTime;
            float t = scrollTweenDuration > 0f ? Mathf.Clamp01(_tweenElapsed / scrollTweenDuration) : 1f;
            // 缓出（quad out）：起步快、收尾慢。0.1 秒这种短时长下比线性更跟手，停下时也不生硬。
            float y = Mathf.Lerp(_tweenFrom, _tweenTo, 1f - (1f - t) * (1f - t));

            if (t >= 1f) { y = _tweenTo; _tweening = false; }
            ApplyScrollY(y);
        }

        #endregion

        #region 生命周期

        public override void SetItems(IReadOnlyList<TData> itemsParam)
        {
            _tweening = false;   // 数据整体替换会回到起点，进行中的补间目标已失效
            base.SetItems(itemsParam);

            // 数据整体替换并回到起点，焦点必然要重算。先置为无效，保证随后必定抛一次 OnFocusChanged——
            // 调用方由此可以只订阅事件、不必再单独读一次初始焦点。
            _focusedIndex = -1;
            UpdateFocusAndAppearance();
        }

        protected override void LateUpdate()
        {
            TickScrollTween();   // 先推进滚轮补间写入滚动位置
            base.LateUpdate();   // 视口尺寸变化重建 + 限速填充
            UpdateFocusAndAppearance();
        }

        #endregion

        #region 焦点与外观计算

        /// <summary>焦点线距视口顶端的距离（像素）。<paramref name="rowPitch"/> 为行距，非格子高度。</summary>
        private float FocusLine(float viewportHeight, float rowPitch)
        {
            switch (focusAnchor)
            {
                case EFocusAnchor.Top:    return rowPitch * 0.5f;
                case EFocusAnchor.Bottom: return viewportHeight - rowPitch * 0.5f;
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

            float pitch = RowPitch;
            if (pitch <= 0f) return;

            _updatingFocus = true;
            try
            {
                int   count          = items?.Count ?? 0;
                float viewportHeight = scrollRect.viewport.rect.height;
                float scrollY        = content.anchoredPosition.y;
                float focusLine      = FocusLine(viewportHeight, pitch);

                // 槽位 s 的中心距视口顶端 = pad + s*p + p/2 - scrollY；令其等于焦点线，反解 s 并取最近的整数，
                // 再映射回数据索引（倒序时两者不同）。
                int focusSlot = Mathf.RoundToInt((focusLine + scrollY - _leadingPad - pitch * 0.5f) / pitch);
                int newFocus = count > 0
                    ? Mathf.Clamp(IndexOfSlot(focusSlot), 0, count - 1)
                    : -1;

                if (newFocus != _focusedIndex)
                {
                    int previous = _focusedIndex;
                    _focusedIndex = newFocus;
                    OnFocusChanged?.Invoke(previous, newFocus);
                }

                ApplyFocusAppearance(count, pitch, viewportHeight, scrollY, focusLine);
            }
            finally
            {
                _updatingFocus = false;
            }
        }

        private void ApplyFocusAppearance(
            int count, float rowPitch, float viewportHeight, float scrollY, float focusLine)
        {
            bool hasScale  = HasKeys(focusScaleCurve);
            bool hasOffset = HasKeys(focusOffsetCurve);
            if (!hasScale && !hasOffset) return;
            if (count <= 0 || viewportHeight <= 0f) return;

            // 只遍历视口覆盖到的数据索引（两端各留一格富余），逐个向基类要其活跃格子。
            // 不自建「索引 → 格子」映射，避免与基类的回收 / 复用循环产生第二份需要同步的状态。
            float relScroll = scrollY - _leadingPad;   // 扣掉头部留白后才是「越过了几个槽位」
            int firstSlot = Mathf.FloorToInt(relScroll / rowPitch) - 1;
            int lastSlot  = Mathf.CeilToInt((relScroll + viewportHeight) / rowPitch) + 1;
            // 槽位区间 → 数据索引区间。倒序时映射是反序的，故两端要对调后再夹取。
            int first = IndexOfSlot(reverseContentOrder ? lastSlot  : firstSlot);
            int last  = IndexOfSlot(reverseContentOrder ? firstSlot : lastSlot);
            first = Mathf.Max(0, first);
            last  = Mathf.Min(count - 1, last);

            // 归一化基准取视口半高：视口上 / 下边缘恰好落在曲线定义域的 ∓1 / ±1。
            float halfSpan = viewportHeight * 0.5f;

            for (int i = first; i <= last; i++)
            {
                if (!TryGetActiveCell(i, out var cell)) continue;
                if (!(Component)cell) continue;

                float centerY = _leadingPad + SlotOf(i) * rowPitch + rowPitch * 0.5f - scrollY;   // 该格中心距视口顶端
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
