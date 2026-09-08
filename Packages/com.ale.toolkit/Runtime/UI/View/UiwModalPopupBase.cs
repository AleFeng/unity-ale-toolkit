using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// 模态弹窗公共基类：把「全屏遮罩 + 淡入淡出 + Cancel 键关闭 + 关闭按钮 + 根节点启停」这一套外壳收口一处，
    /// 由右键菜单（<see cref="UiwContextMenu"/>）与各业务弹窗共用。
    ///
    /// <para><b>为什么不从 <see cref="UiwViewBase"/> 派生</b>：那个基类面向常驻界面——带标题文本、
    /// 抽象的 <c>Unsubscribe</c> / <c>Reopen</c>、以及「<c>Start</c> 时若 activeInHierarchy 就自动 Open」的语义，
    /// 对弹窗全是负担甚至是错的（弹窗不该自己开）。</para>
    ///
    /// <para><b>为什么不从 <see cref="UiwTooltipBase{TPayload}"/> 派生</b>：悬停弹窗在 Awake 里强制
    /// <c>blocksRaycasts = false</c> 且要求本物体常驻激活；模态弹窗恰恰相反——必须能被点击，
    /// 且关闭后要收起根节点，免得空弹窗一直拦着射线。</para>
    ///
    /// <para><b>子类契约</b>：带参数的对外 <c>Show(...)</c> 先把参数缓存到字段、再调 <see cref="Open"/>；
    /// 在 <see cref="OnOpening"/> 里写入内容（此时根节点已激活、尚未淡入，可安全测量布局 / 定位），
    /// 在 <see cref="OnClosed"/> 里清空内容（完全隐藏之后，用于释放图标句柄等）。</para>
    ///
    /// <para><b>Cancel 键</b>走 EventSystem 的 <see cref="ICancelHandler"/>（默认绑 ESC），
    /// 而非已被工程停用的旧版 <c>UnityEngine.Input</c>；代价是弹窗打开期间占用当前选中对象，关闭时还原。</para>
    /// </summary>
    public abstract class UiwModalPopupBase : MonoBehaviour, ICancelHandler
    {
        #region 配置

        [Header("结构")]
        [Tooltip("弹窗面板 RectTransform；为空则用本物体的 RectTransform。")]
        public RectTransform panel;
        [Tooltip("全屏遮罩图形（排在面板之下）：拦截射线，并可点击关闭。可空。")]
        public Graphic blocker;
        [Tooltip("关闭按钮（如面板右上角的「×」）。可空。")]
        public Button closeButton;

        [Header("淡入淡出")]
        [Tooltip("控制淡入淡出与射线阻挡的 CanvasGroup；为空则取本物体上的。")]
        public CanvasGroup canvasGroup;
        [Tooltip("淡入 / 淡出时长（秒）。")]
        [Min(0f)] public float fadeDuration = 0.12f;

        [Header("行为")]
        [Tooltip("点击遮罩关闭弹窗。")]
        public bool closeOnBlockerClick = true;
        [Tooltip("按 Cancel 键（默认 ESC）关闭弹窗。开启时弹窗打开会占用 EventSystem 的当前选中对象。")]
        public bool closeOnCancelKey = true;

        #endregion

        #region 状态

        /// <summary>弹窗当前是否处于打开状态（淡出过程中即视为已关闭）。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>弹窗关闭后派发（无论因何种方式关闭）。</summary>
        public event Action Closed;

        /// <summary>弹窗面板矩形（供子类定位 / 测量；未配置 <see cref="panel"/> 时即本物体的 RectTransform）。</summary>
        protected RectTransform PanelRect { get; private set; }

        private ToolkitTweenHandle _fade;
        private bool               _initialized;
        private GameObject         _prevSelected;   // 打开前 EventSystem 的选中对象，关闭时还原

        #endregion

        #region 生命周期

        protected virtual void Awake() => EnsureInit();

        /// <summary>
        /// 补齐引用、接好遮罩与关闭按钮。<b>幂等</b>：弹窗预制体常以未激活状态保存，此时 Awake 尚未执行，
        /// 故 <see cref="Open"/> 激活根节点后也会再调一次（SetActive 会同步跑 Awake，这里只是防御）。
        /// </summary>
        protected void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            PanelRect = panel ? panel : transform as RectTransform;
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

            // 遮罩点击关闭：运行时挂一个轻量转发器（同 UiwNumberCounter 对 +/- 按钮的做法），
            // 免去在预制体上为遮罩再配一个 Button。
            if (blocker)
            {
                blocker.raycastTarget = true;
                var relay = blocker.gameObject.GetComponent<PointerClickRelay>();
                if (!relay) relay = blocker.gameObject.AddComponent<PointerClickRelay>();
                relay.OnClick = HandleBlockerClick;
            }

            if (closeButton) closeButton.onClick.AddListener(Close);

            OnInit();
            ApplyHidden();
        }

        /// <summary>子类的一次性初始化（引用补齐、事件接线）。在遮罩 / 关闭按钮接好之后、隐藏之前调用。</summary>
        protected virtual void OnInit() { }

        // 本物体或其所在 Canvas 被停用时，复位为关闭态（否则再次打开会带着上次的 alpha / 射线状态）。
        protected virtual void OnDisable()
        {
            _fade.Kill();
            bool wasOpen = IsOpen;
            IsOpen = false;
            ApplyHidden();
            if (wasOpen) OnClosed();
        }

        #endregion

        #region 打开与关闭

        /// <summary>
        /// 打开弹窗（淡入）。已打开时会重新走一遍内容填充（<see cref="OnOpening"/>），便于带参 <c>Show(...)</c> 反复调用。
        /// </summary>
        public void Open()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            EnsureInit();

            IsOpen = true;
            OnOpening();          // 子类填内容 / 定位：根节点已激活，可安全测量布局

            _fade.Kill();
            if (canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable   = true;
                _fade = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, fadeDuration);
            }

            CaptureSelection();
        }

        /// <summary>关闭弹窗（淡出后隐藏根节点）。已关闭时为无操作。</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            RestoreSelection();

            // 立刻停止拦截射线：淡出还在进行时，弹窗不应再吃掉下方的点击。
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

        /// <summary>EventSystem 的 Cancel（默认 ESC）。</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (closeOnCancelKey) Close();
        }

        /// <summary>子类钩子：内容填充 / 定位。调用时根节点已激活、尚未淡入。</summary>
        protected virtual void OnOpening() { }

        /// <summary>子类钩子：完全隐藏后清空内容（释放图标句柄、作废未完成的异步回调等）。</summary>
        protected virtual void OnClosed() { }

        private void HandleBlockerClick()
        {
            if (closeOnBlockerClick) Close();
        }

        /// <summary>淡出结束：清空内容并收起根节点（避免空弹窗常驻参与布局 / 射线）。</summary>
        private void FinishClose()
        {
            if (IsOpen) return;               // 淡出期间又被打开：不要把新内容关掉
            if (canvasGroup) canvasGroup.alpha = 0f;
            OnClosed();
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

        #endregion

        #region EventSystem 选中态（供 Cancel 键派发）

        /// <summary>
        /// 记下当前选中对象并把选中权交给本弹窗 —— Cancel 键只会派发给 EventSystem 的当前选中对象，
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

        /// <summary>还原打开前的选中对象（仅当当前仍是本弹窗时才动，避免抢走别处已转移的焦点）。</summary>
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
        /// 轻量指针点击转发组件：运行时挂到遮罩上，把点击转发给弹窗。
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
