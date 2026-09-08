#if ATK_TMP
using UiText = TMPro.TMP_Text;
#else
using UiText = UnityEngine.UI.Text;
#endif

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime.UI
{
    /// <summary>
    /// 上下文菜单的一行（按钮 + 可选图标 + 文本）。由 <see cref="UiwContextMenu"/> 经
    /// <see cref="UiwWidgetPool{T}"/> 实例化与复用，不单独使用。
    ///
    /// <para>本组件不持有业务语义：条目数据（文本 / 图标 / 是否可点 / 点击回调）每次
    /// <see cref="Bind"/> 时整体写入，回调由菜单转发（点完即关）。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class UiwContextMenuRow : MonoBehaviour
    {
        [Header("UI组件")]
        [Tooltip("本行按钮（点击触发条目回调）。为空则取本物体上的。")]
        [SerializeField] private Button button;
        [Tooltip("条目文本。")]
        [SerializeField] private UiText labelText;
        [Tooltip("条目图标（可空；条目未提供图标时整体隐藏）。")]
        [SerializeField] private Image iconImage;

        // 本行当前绑定的点击回调（由菜单在 Bind 时写入，供按钮 onClick 转发）。
        private Action _onClick;
        private bool   _wired;

        /// <summary>
        /// 绑定一行的显示与回调。<paramref name="onClick"/> 由菜单包装（内含「点完即关」），
        /// 本组件只负责转发。
        /// </summary>
        public void Bind(string label, Sprite icon, bool interactable, Action onClick)
        {
            EnsureWired();

            _onClick = onClick;

            if (labelText) labelText.text = label ?? string.Empty;

            // 图标为可选：条目没给图标就整体隐藏该节点，避免留下一块空白占位。
            if (iconImage)
            {
                bool hasIcon = icon;
                if (iconImage.gameObject.activeSelf != hasIcon) iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon) iconImage.sprite = icon;
            }

            if (button) button.interactable = interactable;
        }

        /// <summary>回收本行：清空回调，避免复用前残留的条目回调被误触发。</summary>
        public void Recycle()
        {
            _onClick = null;
            if (button) button.interactable = true;
            gameObject.SetActive(false);
        }

        /// <summary>首次绑定时补齐按钮引用并挂一次 onClick（复用的行不会重复挂）。</summary>
        private void EnsureWired()
        {
            if (_wired) return;
            _wired = true;

            if (!button) button = GetComponent<Button>();
            if (button) button.onClick.AddListener(HandleClick);
        }

        private void HandleClick() => _onClick?.Invoke();
    }
}
