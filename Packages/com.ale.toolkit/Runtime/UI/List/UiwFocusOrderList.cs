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
    /// <para><b>滚轮由本类接管</b>，做两件原生 <c>ScrollRect</c> 做不到的事：<b>按整数条步进</b>
    /// （<see cref="wheelRowsPerNotch"/>，默认一档一条）与<b>平滑位移</b>（<see cref="scrollTweenDuration"/>，
    /// 默认 0.1 秒）。原生滚轮按 <c>Scroll Sensitivity</c> 走固定像素，只要该值不等于行距就必然停在两条之间；
    /// 且它直接改写 <c>content.anchoredPosition</c>，表现为整条列表瞬间跳一格、焦点缩放曲线跟着突变。
    /// 接管后一档恒好一条、且是滑过去的。<b><c>Scroll Sensitivity</c> 对本类不再有意义</b>——它仍会被取走并置 0
    /// （否则 <c>ScrollRect</c> 会与本类重复处理同一次滚轮），但位移量完全由行距与档位条数算出，
    /// 行距又由格子高度 × 行距倍率自动量得，不存在需要人工同步的第二份数值。
    /// 拖拽 / 惯性 / 边界回弹仍是 <c>ScrollRect</c> 原生的，本类不碰，拖拽开始时会取消进行中的补间。</para>
    ///
    /// <para><b>拖拽松手后吸附对齐</b>（<see cref="snapAfterDrag"/>，默认开）。焦点列表的语义是
    /// 「停在哪条就选中哪条」，停在两条之间时既没有明确的选中项，焦点缩放曲线还会让上下两条都呈半放大态。
    /// 松手后先让 <c>ScrollRect</c> 的惯性照常滑（「甩一下翻几条」的手感要留住），速度衰减到
    /// <see cref="snapVelocityThreshold"/> 以下再接管，补间到<b>当前焦点条目</b>正对焦点线的位置——
    /// 目标条目就是 <see cref="FocusedIndex"/> 已经指向的那条，故吸附过程中焦点不会跳变。</para>
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
    // IBeginDragHandler / IEndDragHandler 只用来「知道拖拽何时开始 / 结束」，不消费事件——
    // ExecuteEvents 会把拖拽派发给本物体上全部处理器，ScrollRect 照常收到并做它的拖拽与惯性。
    public abstract class UiwFocusOrderList<TData, TCell> : UiwVirtualOrderList<TData, TCell>,
                                                            IBeginDragHandler, IEndDragHandler
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
        [Tooltip("一档滚轮跨几条。默认 1 = 一档换一条焦点。\n" +
                 "本类的一档恒为「整数条」而非固定像素——焦点列表的语义是「停在哪条就选中哪条」，\n" +
                 "按像素走必然会停在两条之间。行距由格子高度 × 行距倍率自动算出，无需在别处再配一遍。\n" +
                 "注意 ScrollRect 的 Scroll Sensitivity 对本类不起作用（仍会被取走并置 0，见类注释）。")]
        [Min(1)] public int wheelRowsPerNotch = 1;

        [Tooltip("滚轮切换焦点条目时的平滑位移时长（秒）。0 = 不补间，一档即瞬间跳到位（原生 ScrollRect 的表现）。")]
        public float scrollTweenDuration = 0.1f;

        [Header("拖拽吸附")]
        [Tooltip("拖拽松手后吸附对齐：把当前焦点条目补间到正对焦点线的位置，不让列表停在两条之间。\n" +
                 "焦点列表的语义是「停在哪条就选中哪条」，停在两条之间既没有明确的选中项，\n" +
                 "焦点缩放曲线还会让上下两条都呈半放大态。\n" +
                 "取消勾选则完全交还 ScrollRect（拖到哪停哪，本版之前的表现）。")]
        public bool snapAfterDrag = true;

        [Tooltip("吸附补间时长（秒）。0 = 瞬间对齐。")]
        public float snapTweenDuration = 0.15f;

        [Tooltip("惯性滑行的速度（像素/秒）降到该值以下即开始吸附。\n" +
                 "松手后先让 ScrollRect 的惯性照常滑，「甩一下翻几条」的手感才留得住；\n" +
                 "但惯性是指数衰减，尾巴很长，等它自然归零会让吸附迟迟不来，故在此提前接管。\n" +
                 "调大 = 更早吸附（滑行更短促），调小 = 更贴近原生惯性。0 = 等惯性完全停下。\n" +
                 "ScrollRect 未开启 Inertia 时本项无效——松手即吸附。")]
        [Min(0f)] public float snapVelocityThreshold = 200f;

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
            // 待吸附同理——它一旦到点就会按「当时最近的那条」重新定位，把这次显式定位覆盖掉。
            _tweening    = false;
            _pendingSnap = false;
            // 惯性同样是位置来源之一：松手后的滑行若还没停，会立刻把刚定好的位置带跑。
            scrollRect.velocity = Vector2.zero;

            index = Mathf.Clamp(index, 0, count - 1);

            // 倒序时第 index 条占的是另一个槽位，故先经 SlotOf 换算再定位。
            float scrollY = ScrollYForSlot(SlotOf(index), pitch, scrollRect.viewport.rect.height);

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, scrollY);
            UpdateVisibleCells();
            UpdateFocusAndAppearance();
        }

        #endregion

        #region 滚轮接管与平滑位移

        // 补间状态。_tweening 为 false 时整条路径提前返回，静止期无逐帧开销。
        // 时长随每次启动传入（滚轮用 scrollTweenDuration，吸附用 snapTweenDuration），故须记在状态里。
        private float _tweenFrom, _tweenTo, _tweenElapsed, _tweenDuration;
        private bool  _tweening;

        /// <summary>是否正在播放滚轮补间。</summary>
        public bool IsScrollTweening => _tweening;

        /// <summary>
        /// 焦点列表<b>恒接管</b>滚轮，与反不反向无关——一档滚轮通常正好跨一整条，
        /// 交给原生 <c>ScrollRect</c> 会整条列表瞬间跳一格、焦点缩放曲线跟着突变，
        /// 故必须自己接过来做平滑位移。
        /// </summary>
        protected override bool NeedsScrollTakeOver => true;

        /// <summary>
        /// 滚轮：按<b>整数条</b>推进焦点，并（按需）补间过去。
        ///
        /// <para><b>一档 = 整数条，不是固定像素。</b>焦点列表的语义是「停在哪条就选中哪条」，
        /// 按像素走时只要步长不等于行距，就必然停在两条之间——既没有明确的选中项，
        /// 焦点缩放曲线还会让上下两条都呈半放大态。行距由格子高度 × 行距倍率自动算出，
        /// 一档跨几条由 <see cref="wheelRowsPerNotch"/> 定，两者都不需要在别处再配一份像素值。</para>
        /// </summary>
        public override void OnScroll(PointerEventData eventData)
        {
            if (!ScrollTakenOver) return;                                   // 未接管 → 保持 ScrollRect 原生处理
            if (!scrollRect || !scrollRect.viewport || !content) return;

            int count = items?.Count ?? 0;
            if (count <= 0) return;

            float pitch = RowPitch;
            if (pitch <= 0f) return;

            // 方向解析（含反向开关）交给基类，本类只管步进与补间。
            float delta = ResolveScrollDelta(eventData);
            if (Mathf.Approximately(delta, 0f)) return;

            // 滚轮是一次新的意图，作废上一次拖拽留下的待吸附——否则惯性一停，
            // 吸附会把滚轮刚推到的位置又拽回去。
            _pendingSnap = false;
            // 惯性同理：残余速度会在随后的帧里与补间抢写位置。
            scrollRect.velocity = Vector2.zero;

            float viewportHeight = scrollRect.viewport.rect.height;
            // 起点取「当前补间终点」而非当前位置：连滚数档时才会逐档累加，
            // 否则每档都从半路的实际位置重新起算，越滚越短。
            float from = _tweening ? _tweenTo : content.anchoredPosition.y;

            // 先把起点归到最近的整槽再整条整条地走。常态下起点本就对齐，归整不改变它；
            // 若因拖拽 / 外部写入停在半路（如关掉了拖拽吸附），这一步顺带把它拉回格上。
            int fromSlot = SlotAtFocusLine(from, pitch, viewportHeight);
            // 滚轮向下时 delta 为正，对应滚动量减小、走向更靠前的槽位，故取负号。
            int targetSlot = fromSlot - (delta > 0f ? 1 : -1) * Mathf.Max(1, wheelRowsPerNotch);
            targetSlot = Mathf.Clamp(targetSlot, 0, count - 1);

            float target = ScrollYForSlot(targetSlot, pitch, viewportHeight);
            if (Mathf.Approximately(target, content.anchoredPosition.y) && !_tweening) return;

            BeginScrollTween(target, scrollTweenDuration);
        }

        /// <summary>开始拖拽时取消补间与待吸附，把控制权交还给 <c>ScrollRect</c>，避免两者同时写位置。</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            _tweening    = false;
            _pendingSnap = false;
        }

        /// <summary>
        /// 松开拖拽：登记「待吸附」。此处<b>不</b>立刻吸附——惯性还没跑，立刻接管等于把
        /// <c>ScrollRect</c> 的惯性整个吞掉，「甩一下翻几条」就没了。实际接管时机见
        /// <see cref="TickSnapAfterDrag"/>。
        /// </summary>
        public void OnEndDrag(PointerEventData eventData) => _pendingSnap = snapAfterDrag;

        // 启动一次补间到指定滚动量。时长 ≤ 0 视为瞬移。
        private void BeginScrollTween(float target, float duration)
        {
            if (duration <= 0f)
            {
                _tweening = false;
                ApplyScrollY(target);
                return;
            }

            _tweenFrom     = content.anchoredPosition.y;
            _tweenTo       = target;
            _tweenElapsed  = 0f;
            _tweenDuration = duration;
            _tweening      = true;
        }

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
            float t = _tweenDuration > 0f ? Mathf.Clamp01(_tweenElapsed / _tweenDuration) : 1f;
            // 缓出（quad out）：起步快、收尾慢。0.1 秒这种短时长下比线性更跟手，停下时也不生硬。
            float y = Mathf.Lerp(_tweenFrom, _tweenTo, 1f - (1f - t) * (1f - t));

            if (t >= 1f) { y = _tweenTo; _tweening = false; }
            ApplyScrollY(y);
        }

        #endregion

        #region 拖拽松手后的吸附对齐

        //
        // 【为什么要等惯性、而不是松手即吸附】
        // ScrollRect 松手后会按 decelerationRate 指数衰减地继续滑（inertia）。松手当帧就接管，
        // 等于把这段惯性整个吞掉——「甩一下翻好几条」这个手感是拖拽列表的主要交互方式，不能没有。
        // 反过来，指数衰减的尾巴很长（ScrollRect 要衰减到 |v| < 1 才归零），等它自然停会让吸附
        // 迟迟不来，中间那段慢速蠕动毫无意义。故取「速度降到阈值以下」这个中间时机。
        //
        // 【为什么要给 ScrollRect.velocity 清零】
        // 接管后由本类的补间写 content.anchoredPosition，而 ScrollRect 自己的 LateUpdate 只要
        // velocity 非零就会继续往上叠加位移，两者会在同一帧抢写同一个值。清零后它那段直接短路
        // （movementType 为 Clamped 时 offset 恒为零，那个分支整个跳过），位置由补间独占。
        //

        // 松手后待吸附。等惯性衰减到阈值以下再真正接管，中途被新的拖拽 / 滚轮 / 数据替换作废。
        private bool _pendingSnap;

        /// <summary>是否正处于「松手后等待吸附」的状态（惯性仍在滑行）。</summary>
        public bool IsPendingSnap => _pendingSnap;

        // 每帧检查待吸附是否到点。无待吸附时立即返回，静止期无开销。
        private void TickSnapAfterDrag()
        {
            if (!_pendingSnap) return;
            if (!snapAfterDrag) { _pendingSnap = false; return; }
            if (!scrollRect || !scrollRect.viewport || !content) { _pendingSnap = false; return; }

            // 惯性未衰减到阈值 → 继续让 ScrollRect 滑。未开启 inertia 时 velocity 恒为零，松手即吸附。
            if (scrollRect.inertia && Mathf.Abs(scrollRect.velocity.y) > snapVelocityThreshold) return;

            _pendingSnap = false;
            SnapToFocusLine(snapTweenDuration);
        }

        /// <summary>
        /// 把<b>当前焦点条目</b>补间到正对焦点线的位置。
        /// <para>目标条目取的就是 <see cref="FocusedIndex"/> 已经指向的那条——两处用的是同一条反解
        /// （槽位中心 = 留白 + 槽位×行距 + 半行距 - 滚动量，令其等于焦点线），故吸附过程中
        /// 焦点<b>不会跳变</b>，只是把它从「最接近」挪到「正对」。</para>
        /// </summary>
        /// <param name="duration">补间时长（秒）。≤ 0 为瞬间对齐。</param>
        public void SnapToFocusLine(float duration)
        {
            if (!scrollRect || !scrollRect.viewport || !content) return;

            int count = items?.Count ?? 0;
            if (count <= 0) return;

            float pitch = RowPitch;
            if (pitch <= 0f) return;

            float viewportHeight = scrollRect.viewport.rect.height;
            float scrollY        = content.anchoredPosition.y;

            // 反解最近的槽位并夹取。槽位与索引同域（倒序时互为镜像），故夹槽位等价于夹索引，
            // 与 UpdateFocusAndAppearance 对 IndexOfSlot 结果的夹取结果一致。
            int slot = Mathf.Clamp(SlotAtFocusLine(scrollY, pitch, viewportHeight), 0, count - 1);
            float target = ScrollYForSlot(slot, pitch, viewportHeight);

            // 必须在补间之前：否则 ScrollRect 会带着残余速度与补间抢写位置。
            scrollRect.velocity = Vector2.zero;

            if (Mathf.Approximately(target, scrollY))
            {
                // 已经对齐（含滚到两端边界的情形——首尾留白保证了那里本就正对焦点线）。
                _tweening = false;
                return;
            }

            BeginScrollTween(target, duration);
        }

        #endregion

        #region 生命周期

        public override void SetItems(IReadOnlyList<TData> itemsParam)
        {
            // 数据整体替换会回到起点，进行中的补间与待吸附的目标都已失效
            _tweening    = false;
            _pendingSnap = false;
            base.SetItems(itemsParam);

            // 数据整体替换并回到起点，焦点必然要重算。先置为无效，保证随后必定抛一次 OnFocusChanged——
            // 调用方由此可以只订阅事件、不必再单独读一次初始焦点。
            _focusedIndex = -1;
            UpdateFocusAndAppearance();
        }

        protected override void LateUpdate()
        {
            TickSnapAfterDrag(); // 松手后的惯性衰减到位则启动吸附补间
            TickScrollTween();   // 推进补间（滚轮 / 吸附共用）写入滚动位置
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

        //
        // 「滚动量 ↔ 槽位」的两条互逆换算。焦点判定、滚轮步进、拖拽吸附、FocusIndex 四处全都要用，
        // 各写一遍必然会漂——本类此前就有三份同式的抄写。集中在这里，任何一处改动都同时作用于四者。
        //
        //   槽位 s 的中心距视口顶端 = 首留白 + s×行距 + 半行距 - 滚动量
        //   令其等于焦点线，即得两式。
        //

        /// <summary>由滚动量反解「此刻正对焦点线的槽位」，四舍五入到最近的整槽。<b>不</b>夹取，由调用方按需夹。</summary>
        private int SlotAtFocusLine(float scrollY, float rowPitch, float viewportHeight)
            => Mathf.RoundToInt((FocusLine(viewportHeight, rowPitch) + scrollY - _leadingPad - rowPitch * 0.5f) / rowPitch);

        /// <summary>
        /// 由槽位求「使该槽位正对焦点线」所需的滚动量，已夹到可滚范围内。
        /// <para>夹取不会带来偏差：首尾留白的定义保证了槽位 0 恰好落在滚动量 0、末槽位恰好落在 <see cref="MaxScroll"/>。</para>
        /// </summary>
        private float ScrollYForSlot(int slot, float rowPitch, float viewportHeight)
            => Mathf.Clamp(_leadingPad + slot * rowPitch + rowPitch * 0.5f - FocusLine(viewportHeight, rowPitch),
                           0f, MaxScroll());

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

                // 反解此刻正对焦点线的槽位，再映射回数据索引（倒序时两者不同）。
                int focusSlot = SlotAtFocusLine(scrollY, pitch, viewportHeight);
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
