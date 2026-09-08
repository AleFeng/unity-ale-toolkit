using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// 上下文菜单的一个条目（纯数据，由调用方每次弹出时现组装）。
    ///
    /// <para><see cref="Label"/> 用 <see cref="TextValue"/> 承载，故条目文案既可写死 fallback，
    /// 也可挂 Unity 本地化条目（启用 <c>ATK_LOCALIZATION</c> 时）。</para>
    /// </summary>
    public class UiwContextMenuItem
    {
        /// <summary>条目文案。</summary>
        public TextValue Label;
        /// <summary>条目图标（可空）。</summary>
        public Sprite Icon;
        /// <summary>是否可点击（false 时按钮置灰但仍显示）。</summary>
        public bool Interactable = true;
        /// <summary>点击回调（菜单会先关闭再回调，见 <see cref="UiwContextMenu"/>）。</summary>
        public Action OnClick;

        public UiwContextMenuItem() { }

        /// <summary>以纯文本文案构造（最常用形态）。</summary>
        public UiwContextMenuItem(string label, Action onClick, bool interactable = true, Sprite icon = null)
        {
            Label        = new TextValue(label);
            OnClick      = onClick;
            Interactable = interactable;
            Icon         = icon;
        }
    }

    /// <summary>
    /// 通用上下文菜单（右键菜单）：在光标处弹出一列条目，点选后执行回调。
    /// 条目<b>数据驱动</b>——本组件不认识任何业务概念，由调用方每次 <see cref="Open(IReadOnlyList{UiwContextMenuItem}, Vector2)"/>
    /// 时传入条目列表。
    ///
    /// <para>「遮罩 + 淡入淡出 + Cancel 键关闭 + 根节点启停」整套外壳来自 <see cref="UiwModalPopupBase"/>；
    /// 本类只额外负责<b>条目行的池化绑定</b>与<b>跟随光标定位</b>。</para>
    ///
    /// <para><b>关闭方式</b>：点击遮罩、按 Cancel 键（默认 ESC）、或点中任一条目。</para>
    ///
    /// <para><b>预制体约定</b>：本物体为根（可保存为未激活）；其下一个全屏遮罩图形，
    /// 一个面板（建议轴心取左上，使菜单自光标向右下展开），
    /// 面板内 <see cref="rowContainer"/> 挂 VerticalLayoutGroup + ContentSizeFitter。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class UiwContextMenu : UiwModalPopupBase
    {
        #region 配置

        [Header("条目")]
        [Tooltip("条目行的父容器（建议挂 VerticalLayoutGroup + ContentSizeFitter）。为空则用面板本身。")]
        public Transform rowContainer;
        [Tooltip("条目行预制体（UiwContextMenuRow）。")]
        public UiwContextMenuRow rowPrefab;

        [Header("定位")]
        [Tooltip("相对光标的像素偏移。面板轴心取左上时，用小的正 X / 负 Y 即可贴着光标展开。")]
        public Vector2 cursorOffset = new Vector2(2f, -2f);

        #endregion

        #region 状态

        private readonly UiwWidgetPool<UiwContextMenuRow> _rowPool = new UiwWidgetPool<UiwContextMenuRow>();

        // 本次弹出的条目与光标位置：Open 时缓存，OnOpening（根节点已激活）时才真正绑定与定位。
        private IReadOnlyList<UiwContextMenuItem> _pendingItems;
        private Vector2                           _pendingPos;

        #endregion

        #region 生命周期

        protected override void OnInit()
            => _rowPool.Configure(rowPrefab, rowContainer ? rowContainer : PanelRect);

        // virtual：子类（如库存的 UiwItemContextMenu）也要 OnDestroy 时，必须能 override 后调 base——
        // 两个类各自声明一个 private OnDestroy 的话，Unity 只会派发到最派生的那个，基类这句静默不执行。
        protected virtual void OnDestroy() => _rowPool.Clear();

        #endregion

        #region 打开

        /// <summary>
        /// 在光标处（屏幕坐标）弹出菜单。条目为空 / 全空时等同 <see cref="UiwModalPopupBase.Close"/>。
        ///
        /// <para>条目回调的执行时机是<b>先关菜单、再回调</b>：回调里可以安全地再弹别的窗
        /// （否则新窗会被本菜单的关闭流程连带影响）。</para>
        /// </summary>
        /// <param name="items">本次要显示的条目（按顺序）。</param>
        /// <param name="screenPos">光标屏幕坐标（通常取 <c>PointerEventData.position</c>）。</param>
        public void Open(IReadOnlyList<UiwContextMenuItem> items, Vector2 screenPos)
        {
            // 先把「有没有东西可显示」判掉，避免弹出一个空菜单再回头关它。
            int usable = 0;
            if (items != null)
                foreach (var item in items)
                    if (item != null) usable++;

            if (usable == 0) { Close(); return; }

            if (!rowPrefab)
            {
                Debug.LogWarning("[UiwContextMenu] 未配置条目行预制体 rowPrefab，菜单无法显示。", this);
                return;
            }

            _pendingItems = items;
            _pendingPos   = screenPos;
            Open();
        }

        /// <summary>绑定条目行并跟随光标定位（此时根节点已激活、尚未淡入）。</summary>
        protected override void OnOpening()
        {
            if (_pendingItems == null) return;

            _rowPool.Begin();
            int shown = 0;
            foreach (var item in _pendingItems)
            {
                if (item == null) continue;
                var row = _rowPool.Next();
                if (!row) break;

                var captured = item;                       // 闭包捕获：避免回调里读到循环变量的末值
                row.Bind(captured.Label != null ? captured.Label.ResolveText() : string.Empty,
                         captured.Icon, captured.Interactable,
                         () => InvokeItem(captured));
                row.transform.SetSiblingIndex(shown);      // 显示顺序与传入顺序一致（复用的行可能乱序）
                shown++;
            }
            _rowPool.End(RecycleRow);

            // 必须先把布局算实：菜单高度随条目数变化，布局未刷新时 PositionAtCursor 的夹取
            // 会按上一次（或预制体里）的尺寸算，靠近屏幕边缘时会错位。
            if (PanelRect) LayoutRebuilder.ForceRebuildLayoutImmediate(PanelRect);
            UIUtility.PositionAtCursor(PanelRect, _pendingPos, cursorOffset);
        }

        /// <summary>完全隐藏后回收条目行，并断开对条目列表的引用。</summary>
        protected override void OnClosed()
        {
            _rowPool.RecycleAll(RecycleRow);
            _pendingItems = null;
        }

        /// <summary>点中条目：先关菜单，再执行其回调。</summary>
        private void InvokeItem(UiwContextMenuItem item)
        {
            Close();
            item?.OnClick?.Invoke();
        }

        private static void RecycleRow(UiwContextMenuRow row)
        {
            if (row) row.Recycle();
        }

        #endregion
    }
}
