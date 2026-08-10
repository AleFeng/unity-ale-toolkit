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
            return Mathf.FloorToInt(scrollY / RowPitch) - bufferCount;
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
            return new Vector2(0f, -(index * pitch + pitch * 0.5f));
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
