using UnityEngine;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// 通用<b>顺序</b>虚拟滚动列表（一维纵向、单列）。在 <see cref="UiwVirtualListBase{TData,TCell}"/>
    /// 的虚拟滚动引擎之上，提供"单列纵向"布局策略：Content 高度 = 条目数 × 行距；实例锚点顶部横向拉伸，
    /// 逐行向下排布，滚动时循环复用。
    ///
    /// <para><b>「行距」与「格子高度」是两件事</b>：格子高度由 <c>cellPrefab</c> 量得，决定实例自身的尺寸；
    /// 行距由 <see cref="rowPitchScale"/> 在格子高度之上缩放而来，决定排布与滚动换算。倍率为 1（默认）时
    /// 两者相等，即逐行紧贴；大于 1 拉开间隙，小于 1 让相邻行重叠。</para>
    ///
    /// <para><b>排布方向可反转</b>：<see cref="reverseScrollDirection"/> 勾选后第 0 条排到最下方、最后一条在最上方，
    /// 「向下滚」于是从末条走向首条（聊天记录、日志这类自下而上追加的列表）。实现上只是把数据索引映射到
    /// 另一个<b>槽位</b>（见 <see cref="SlotOf"/>），Content 尺寸、锚点与滚动范围一概不变。</para>
    ///
    /// <para>各系统按需继承本类、闭合泛型并实现 <see cref="UiwVirtualListBase{TData,TCell}.BindCell"/> /
    /// <see cref="UiwVirtualListBase{TData,TCell}.ClearCell"/>（如仓库列表 <c>UiwInventoryItemOrderList</c>）。</para>
    /// </summary>
    public abstract class UiwVirtualOrderList<TData, TCell> : UiwVirtualListBase<TData, TCell>
        where TCell : Component
    {
        #region Inspector 配置

        [Header("布局")]
        [Tooltip("行距倍率：行距 = 格子高度 × 本倍率。1.0 = 逐行紧贴（默认）；\n" +
                 "大于 1 拉开间隙，小于 1 让相邻行重叠。格子自身的高度不受影响。")]
        [Min(0.01f)] public float rowPitchScale = 1f;

        [Tooltip("反向滚动：勾选后条目倒序排布——第 0 条在最下方，最后一条在最上方。\n" +
                 "于是「向下滚」是从末条走向首条，适合聊天记录、日志这类自下而上追加的列表。\n" +
                 "不勾选（默认）为正向：第 0 条在最上方，向下滚走向末条。")]
        public bool reverseScrollDirection;

        #endregion

        // 格子高度（像素）。与 cellPrefab 的 RectTransform 高度一致。
        private float _cellHeight = 120f;

        /// <summary>格子自身高度（由 <see cref="MeasureCell"/> 从 <c>cellPrefab</c> 量得）。只决定实例的尺寸。</summary>
        protected float CellHeight => _cellHeight;

        /// <summary>
        /// 行距（像素）= 格子高度 × <see cref="rowPitchScale"/>。定位、Content 高度与滚动换算<b>一律</b>用它，
        /// 不要用 <see cref="CellHeight"/>——那是格子自己的尺寸，与「隔多远排下一行」无关。
        /// </summary>
        protected float RowPitch => _cellHeight * Mathf.Max(0.01f, rowPitchScale);

        /// <summary>
        /// 测量 cellPrefab 的 RectTransform 高度，作为格子高度。若 prefab 高度为 0，则使用默认值 120。
        /// </summary>
        protected override void MeasureCell()
        {
            if (TryGetCellPrefabSize(out _, out float h) && h > 0f)
                _cellHeight = h;
        }

        #region 排布方向：索引 ↔ 槽位

        //
        // 【为什么用「索引 ↔ 槽位」映射，而不是把锚点翻到底部】
        // 槽位就是「从 Content 顶端往下数的第几行」，是纯几何量；索引是数据下标。正向时两者相等，
        // 倒序时 槽位 = 条目数-1-索引。所有定位、留白、滚动换算、焦点反解都只认槽位，于是倒序
        // 不需要动 Content 尺寸、锚点、滚动范围中的任何一个——它们本来就是按槽位算的。
        // 反过来若去翻锚点，上面每一处都要各写一套正 / 反分支，子类（焦点列表）还要再抄一遍。
        //
        // 两种做法的视觉结果完全一致：把第 0 条摆在最下方向上排，等价于让第 0 条占据最后一个槽位。
        //

        /// <summary>数据索引 → 槽位（从 Content 顶端往下数的第几行）。正向时即索引本身。</summary>
        protected int SlotOf(int index)
        {
            if (!reverseScrollDirection) return index;
            int count = items?.Count ?? 0;
            return count - 1 - index;
        }

        /// <summary>槽位 → 数据索引。映射是自反的（再映射一次就回去了），单独留一个名字只为读起来不拧巴。</summary>
        protected int IndexOfSlot(int slot) => SlotOf(slot);

        // 上一次生效的排布方向。运行期翻转时，已摆好的格子位置全部作废，须整体收回重排。
        private bool _reverseApplied;

        /// <summary>
        /// 排布方向变化时把所有格子收回重排。
        /// <para>放在 <see cref="RecomputeLayout"/> 里而不是 <c>OnValidate</c>：后者可能落在 Canvas Rebuild
        /// 循环内，就地回收 / 重排 UI 会报错；本方法由 <c>RebuildLayout</c> 在安全时机调用。</para>
        /// </summary>
        protected override void RecomputeLayout(Rect viewport)
        {
            base.RecomputeLayout(viewport);

            if (_reverseApplied == reverseScrollDirection) return;
            _reverseApplied = reverseScrollDirection;
            RegainAllInstances();
        }

        #endregion

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 上改了行距倍率后立刻生效（行距倍率通常是在 Play 模式里对着调的）。
        /// 只置脏、不直接重建——<c>OnValidate</c> 可能落在 Canvas Rebuild 循环内，就地改 UI 会报错。
        /// </summary>
        protected virtual void OnValidate()
        {
            if (Application.isPlaying) SetViewportDirty();
        }
#endif

        /// <summary>
        /// 设置 Content 高度 = 条目数 × 行距。Content 锚点顶部横向拉伸，纵向高度随条目数变化。
        /// </summary>
        /// <param name="count"></param>
        protected override void SetContentSize(int count)
        {
            var size = content.sizeDelta;
            size.y = count * RowPitch;
            content.sizeDelta = size;
        }

        /// <summary>
        /// 计算 可见区域需要的实例数 = 可见高度 / 行距 + 1 + bufferCount * 2。+1 是为了避免滚动到最后一行时出现空白。
        /// </summary>
        /// <param name="viewport"></param>
        /// <returns></returns>
        protected override int InstancesNeeded(Rect viewport)
            => Mathf.CeilToInt(viewport.height / RowPitch) + 1 + bufferCount * 2;

        /// <summary>
        /// 计算 第一个可见条目索引 = Content.anchoredPosition.y / 行距 - bufferCount。Content 向上移动时 anchoredPosition.y > 0（UGUI 坐标）。
        /// </summary>
        /// <param name="contentAnchoredPos"></param>
        /// <returns></returns>
        protected override int ComputeFirstIndex(Vector2 contentAnchoredPos)
        {
            // Content 向上移动时 anchoredPosition.y > 0（UGUI 坐标）。
            float scrollY = Mathf.Max(0f, contentAnchoredPos.y);
            int firstSlot = Mathf.FloorToInt(scrollY / RowPitch) - bufferCount;
            return FirstIndexOfSlotWindow(firstSlot);
        }

        /// <summary>
        /// 由「窗口起始<b>槽位</b>」求引擎要的「窗口起始<b>索引</b>」。
        /// <para>正向时两者相同。倒序时窗口内的索引是<b>降序</b>的——顶端槽位对应最大索引，
        /// 而引擎的窗口恒为 <c>[first, first + PoolTarget - 1]</c> 这样的升序区间，
        /// 故要交出的是窗口里最小的那个索引，即比顶端槽位对应的索引小一整个窗口跨度。</para>
        /// </summary>
        protected int FirstIndexOfSlotWindow(int firstSlot)
        {
            if (!reverseScrollDirection) return firstSlot;

            int count = items?.Count ?? 0;
            return count - firstSlot - PoolTarget;
        }

        /// <summary>
        /// 计算 第 index 个条目的纵向位置。格子轴心居中（见 <see cref="SetupInstanceRect"/>），
        /// 故这里给的是<b>该行槽位的中心</b>：-(index * 行距 + 半个行距)。
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected override Vector2 PositionOf(int index)
        {
            float pitch = RowPitch;
            return new Vector2(0f, -(SlotOf(index) * pitch + pitch * 0.5f));
        }

        /// <summary>
        /// 设置 实例 RectTransform：锚点顶部横向拉伸，纵向位置由 anchoredPosition.y 控制，宽度随 Content 变化，高度 = 格子高度。
        ///
        /// <para><b>轴心取正中 (0.5, 0.5)</b>，而非顶端。轴心即缩放中心，取顶端时放大的格子只向下长开，
        /// 视觉中心会比布局算出的行中心低 <c>(缩放 - 1) × 格高 / 2</c>——焦点列表配了缩放曲线后，
        /// 焦点条目就再也对不准焦点线。取正中则格子从自身中心向四周长开，任何缩放下视觉中心都不偏移。
        /// 代价是 <see cref="PositionOf"/> 要给出行中心而非行顶端，已在那里补偿。</para>
        /// </summary>
        /// <param name="inst"></param>
        protected override void SetupInstanceRect(TCell inst)
        {
            var rt = (RectTransform)inst.transform;
            // 锚点顶部左右拉伸，通过 anchoredPosition.y 控制纵向位置、宽度随 content 变化。
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, _cellHeight);
        }
    }
}
