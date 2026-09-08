using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        /// <summary>点击回调（菜单会先关闭再回调，见 <see cref="UiwContextMenu.Open"/>）。</summary>
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
    /// 条目<b>数据驱动</b>——本组件不认识任何业务概念，由调用方每次 <see cref="Open"/> 时传入条目列表。
    ///
    /// <para><b>与 <see cref="UiwTooltipBase{TPayload}"/> 的区别</b>：悬停弹窗在 Awake 里强制
    /// <c>blocksRaycasts = false</c>（不遮挡下方条目的悬停判定），而菜单必须<b>能被点击</b>，
    /// 故本类不从它派生，自带一套「定位 + 淡入淡出 + 射线开关」。</para>
    ///
    /// <para><b>关闭方式</b>：点击遮罩（<see cref="blocker"/>，全屏透明图，排在面板之下）、
    /// 按 Cancel 键（默认 ESC，经 EventSystem 的 <see cref="ICancelHandler"/> 派发，
    /// 不走已被工程停用的旧版 <c>UnityEngine.Input</c>）、或点中任一条目。</para>
    ///
    /// <para><b>预制体约定</b>：本物体为根（可保存为未激活）；其下一个全屏 <see cref="blocker"/> 图形，
    /// 一个 <see cref="panel"/> 面板（建议轴心取左上，使菜单自光标向右下展开），
    /// 面板内 <see cref="rowContainer"/> 挂 VerticalLayoutGroup + ContentSizeFitter。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class UiwContextMenu : MonoBehaviour, ICancelHandler
    {
        #region 配置

        [Header("结构")]
        [Tooltip("菜单面板 RectTransform（跟随光标定位）。为空则用本物体的 RectTransform。")]
        public RectTransform panel;
        [Tooltip("条目行的父容器（建议挂 VerticalLayoutGroup + ContentSizeFitter）。为空则用 panel。")]
        public Transform rowContainer;
        [Tooltip("条目行预制体（UiwContextMenuRow）。")]
        public UiwContextMenuRow rowPrefab;
        [Tooltip("全屏遮罩图形：点击它关闭菜单。可空（为空则只能靠 ESC / 点条目关闭）。")]
        public Graphic blocker;

        [Header("定位与淡入淡出")]
        [Tooltip("控制淡入淡出与射线阻挡的 CanvasGroup；为空则取本物体上的。")]
        public CanvasGroup canvasGroup;
        [Tooltip("相对光标的像素偏移。面板轴心取左上时，用小的正 X / 负 Y 即可贴着光标展开。")]
        public Vector2 cursorOffset = new Vector2(2f, -2f);
        [Tooltip("淡入 / 淡出时长（秒）。")]
        [Min(0f)] public float fadeDuration = 0.1f;

        [Header("行为")]
        [Tooltip("按 Cancel 键（默认 ESC）关闭菜单。开启时菜单弹出会占用 EventSystem 的当前选中对象。")]
        public bool closeOnCancelKey = true;

        #endregion

        #region 状态

        /// <summary>菜单当前是否处于打开状态（淡出过程中即视为已关闭）。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>菜单关闭后派发（无论因何种方式关闭；条目回调在此之后执行）。</summary>
        public event Action Closed;

        private readonly UiwWidgetPool<UiwContextMenuRow> _rowPool = new UiwWidgetPool<UiwContextMenuRow>();

        private RectTransform      _rt;
        private ToolkitTweenHandle _fade;
        private bool               _initialized;
        private GameObject         _prevSelected;   // 弹出前 EventSystem 的选中对象，关闭时还原

        #endregion

        #region 生命周期

        private void Awake() => Init();

        /// <summary>
        /// 补齐引用并接好遮罩点击。<b>幂等</b>：预制体根节点常以未激活状态保存，此时 Awake 尚未执行，
        /// 故 <see cref="Open"/> 激活本物体后也会再调一次（Unity 的 SetActive 会同步跑 Awake，
        /// 这里的重复调用只是防御）。
        /// </summary>
        private void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _rt = panel ? panel : transform as RectTransform;
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            _rowPool.Configure(rowPrefab, rowContainer ? rowContainer : panel);

            // 遮罩点击关闭：运行时挂一个轻量转发器（同 UiwNumberCounter 对 +/- 按钮的做法），
            // 免去预制体上再配一个 Button。
            if (blocker)
            {
                blocker.raycastTarget = true;
                var relay = blocker.gameObject.GetComponent<PointerClickRelay>();
                if (!relay) relay = blocker.gameObject.AddComponent<PointerClickRelay>();
                relay.OnClick = Close;
            }

            ApplyHidden();
        }

        // 本物体或其所在 Canvas 被停用时，复位为关闭态（否则再次弹出会带着上次的 alpha / 射线状态）。
        private void OnDisable()
        {
            _fade.Kill();
            IsOpen = false;
            ApplyHidden();
            _rowPool.RecycleAll(RecycleRow);
        }

        private void OnDestroy() => _rowPool.Clear();

        #endregion

        #region 打开与关闭

        /// <summary>
        /// 在光标处（屏幕坐标）弹出菜单。条目为空 / 全空时等同 <see cref="Close"/>。
        ///
        /// <para>条目回调的执行时机是<b>先关菜单、再回调</b>：回调里可以安全地再弹别的窗
        /// （否则新窗会被本菜单的关闭流程连带影响）。</para>
        /// </summary>
        /// <param name="items">本次要显示的条目（按顺序）。</param>
        /// <param name="screenPos">光标屏幕坐标（通常取 <c>PointerEventData.position</c>）。</param>
        public void Open(IReadOnlyList<UiwContextMenuItem> items, Vector2 screenPos)
        {
            if (items == null || items.Count == 0) { Close(); return; }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Init();

            if (!rowPrefab)
            {
                Debug.LogWarning("[UiwContextMenu] 未配置条目行预制体 rowPrefab，菜单无法显示。", this);
                return;
            }

            // ── 条目 ──────────────────────────────────────────────────────────
            _rowPool.Begin();
            int shown = 0;
            foreach (var item in items)
            {
                if (item == null) continue;
                var row = _rowPool.Next();
                if (!row) break;

                var captured = item;                       // 闭包捕获：避免回调里读到循环变量的末值
                row.Bind(captured.Label != null ? captured.Label.ResolveText() : string.Empty,
                         captured.Icon, captured.Interactable,
                         () => Invoke(captured));
                row.transform.SetSiblingIndex(shown);      // 保证显示顺序与传入顺序一致（复用行可能乱序）
                shown++;
            }
            _rowPool.End(RecycleRow);

            if (shown == 0) { Close(); return; }

            // ── 定位 ──────────────────────────────────────────────────────────
            // 必须先把布局算实：菜单高度随条目数变化，布局未刷新时 PositionAtCursor 的夹取
            // 会按上一次（或预制体里）的尺寸算，靠近屏幕边缘时会错位。
            if (_rt) LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
            UIUtility.PositionAtCursor(_rt, screenPos, cursorOffset);

            // ── 显示 ──────────────────────────────────────────────────────────
            IsOpen = true;
            _fade.Kill();
            if (canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable   = true;
                _fade = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, fadeDuration);
            }

            CaptureSelection();
        }

        /// <summary>关闭菜单（淡出后隐藏）。已关闭时为无操作。</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            RestoreSelection();

            // 立刻停止拦截射线：淡出还在进行时，菜单不应再吃掉下方的点击。
            if (canvasGroup)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable   = false;
            }

            _fade.Kill();
            if (canvasGroup && fadeDuration > 0f && isActiveAndEnabled)
                _fade = ToolkitTween.FadeCanvasGroup(canvasGroup, 0f, fadeDuration, onComplete: FinishClose);
            else
                FinishClose();

            Closed?.Invoke();
        }

        /// <summary>EventSystem 的 Cancel（默认 ESC）：关闭菜单。</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (closeOnCancelKey) Close();
        }

        /// <summary>点中条目：先关菜单，再执行其回调。</summary>
        private void Invoke(UiwContextMenuItem item)
        {
            Close();
            item?.OnClick?.Invoke();
        }

        /// <summary>淡出结束：回收条目行并隐藏根节点（避免常驻的空菜单参与布局 / 射线）。</summary>
        private void FinishClose()
        {
            if (IsOpen) return;               // 淡出期间又被打开：不要把新菜单关掉
            _rowPool.RecycleAll(RecycleRow);
            if (canvasGroup) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>立即置为关闭态（无动画）。</summary>
        private void ApplyHidden()
        {
            if (!canvasGroup) return;
            canvasGroup.alpha          = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable   = false;
        }

        private static void RecycleRow(UiwContextMenuRow row)
        {
            if (row) row.Recycle();
        }

        #endregion

        #region EventSystem 选中态（供 Cancel 键派发）

        /// <summary>
        /// 记下当前选中对象并把选中权交给本菜单 —— Cancel 键只会派发给 EventSystem 的当前选中对象，
        /// 不这样做则 ESC 收不到。
        /// </summary>
        private void CaptureSelection()
        {
            if (!closeOnCancelKey) return;
            var es = EventSystem.current;
            if (!es) return;

            _prevSelected = es.currentSelectedGameObject;
            es.SetSelectedGameObject(gameObject);
        }

        /// <summary>还原弹出前的选中对象（仅当当前仍是本菜单时才动，避免抢走别处已转移的焦点）。</summary>
        private void RestoreSelection()
        {
            var es = EventSystem.current;
            if (es && es.currentSelectedGameObject == gameObject)
                es.SetSelectedGameObject(_prevSelected);
            _prevSelected = null;
        }

        #endregion

        #region 类定义

        /// <summary>
        /// 轻量指针点击转发组件：运行时挂到遮罩上，把点击转发给菜单（关闭）。
        /// 与 <see cref="UiwNumberCounter"/> 的 <c>PointerHoldRelay</c> 同一手法，
        /// 免去在预制体上为遮罩再配一个 Button。
        /// </summary>
        private sealed class PointerClickRelay : MonoBehaviour, IPointerClickHandler
        {
            public Action OnClick;

            public void OnPointerClick(PointerEventData eventData) => OnClick?.Invoke();
        }

        #endregion
    }
}
