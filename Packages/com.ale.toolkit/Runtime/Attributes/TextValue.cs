using System;
using UnityEngine;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 轻量「展示文本」值：<b>始终</b>携带一个纯文本 fallback，并在启用 <c>ATK_LOCALIZATION</c> 时额外内嵌一个
    /// Unity <see cref="UnityEngine.Localization.LocalizedString"/>（原生序列化 + 原生表/条目选择器）。
    /// 运行时经 <see cref="ResolveText"/> 优先取本地化文本、取不到回退 fallback。
    ///
    /// <para>相比用 <see cref="AttributeValue"/> 的 <see cref="EFieldType.Text"/> 承载展示文本，本类每实例仅一个
    /// string（+ 启用本地化时一个 LocalizedString），无 AttributeValue「预分配 6 个类型后备列表」的开销；
    /// 且<b>直接内嵌原生 LocalizedString</b>——Inspector 用 Unity 原生绘制器（表/条目可搜索下拉），选择即由原生
    /// 序列化正确保存（不再手工搬运扁平字符串，避免条目按 KeyId 引用时丢失）。可在任意组件/配置上直接声明使用。</para>
    ///
    /// <para>未启用 <c>ATK_LOCALIZATION</c> 时仅有 fallback 一项，<see cref="ResolveText"/> 即返回 fallback。</para>
    /// </summary>
    [Serializable]
    public class TextValue
    {
        [SerializeField] private string fallback = string.Empty;
#if ATK_LOCALIZATION
        [SerializeField] private UnityEngine.Localization.LocalizedString localized = new UnityEngine.Localization.LocalizedString();
#endif

        #region 构造

        public TextValue() { }

        /// <summary>以纯文本 fallback 初始化。</summary>
        public TextValue(string fallbackText)
        {
            fallback = fallbackText ?? string.Empty;
        }

        #endregion

        /// <summary>纯文本 fallback（始终存在）。</summary>
        public string Fallback
        {
            get => fallback ?? string.Empty;
            set => fallback = value ?? string.Empty;
        }

#if ATK_LOCALIZATION
        /// <summary>本地化引用（Unity <c>LocalizedString</c>；可空）。供代码读取 / 设置表与条目。</summary>
        public UnityEngine.Localization.LocalizedString Localized
        {
            get => localized;
            set => localized = value;
        }
#endif

        /// <summary>
        /// 解析显示文本：启用 <c>ATK_LOCALIZATION</c> 且本地化引用非空并能取到非空文本时返回本地化文本，
        /// 否则返回纯文本 fallback；均为空时返回空串。
        /// </summary>
        public string ResolveText()
        {
#if ATK_LOCALIZATION
            if (localized != null && !localized.IsEmpty)
            {
                string s = localized.GetLocalizedString();
                if (!string.IsNullOrEmpty(s)) return s;
            }
#endif
            return fallback ?? string.Empty;
        }

        /// <summary>fallback 与本地化引用是否均为空。</summary>
        public bool IsEmpty
        {
            get
            {
#if ATK_LOCALIZATION
                if (localized != null && !localized.IsEmpty) return false;
#endif
                return string.IsNullOrEmpty(fallback);
            }
        }

        /// <summary>深拷贝（fallback 复制；本地化引用另建一份并复制表/条目引用，互不影响）。</summary>
        public TextValue Clone()
        {
            var clone = new TextValue { fallback = fallback };
#if ATK_LOCALIZATION
            if (localized != null)
                clone.localized = new UnityEngine.Localization.LocalizedString
                {
                    TableReference      = localized.TableReference,
                    TableEntryReference = localized.TableEntryReference,
                };
#endif
            return clone;
        }
    }
}
