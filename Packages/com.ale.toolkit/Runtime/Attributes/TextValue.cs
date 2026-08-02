using System;
using UnityEngine;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 轻量「展示文本」值：<b>始终</b>携带一个纯文本 fallback，并在启用 <c>ATK_LOCALIZATION</c> 时额外携带
    /// Unity Localization 引用（表 + 条目）。运行时经 <see cref="ResolveText"/> 优先取本地化文本、取不到回退纯文本。
    ///
    /// <para>本类是 <see cref="AttributeValue"/> 的 <see cref="EFieldType.Text"/> 的<b>独立轻量版</b>：同样以扁平字符串
    /// （fallback / tableRef / entryKey）承载，兼容原生序列化 / Undo 与后续 JSON / 二进制导出；但不背负 AttributeValue
    /// 「一实例预分配 6 个类型后备列表」的开销（本类每实例仅 3 个字符串）。可在任意组件 / 配置上直接声明使用，
    /// 配套 <c>[CustomPropertyDrawer(typeof(TextValue))]</c> 在 Inspector 中干净绘制（纯文本行 + 原生本地化选择器）。</para>
    ///
    /// <para>三个槽<b>始终序列化</b>（不随 <c>ATK_LOCALIZATION</c> 开关改变序列化形态，防止数据漂移 / 损坏）；
    /// 本地化引用仅在启用宏时于 <see cref="ResolveText"/> 中被使用。</para>
    /// </summary>
    [Serializable]
    public class TextValue
    {
        [SerializeField] private string fallback = string.Empty;
        [SerializeField] private string tableRef = string.Empty;
        [SerializeField] private string entryKey = string.Empty;

        #region 构造

        public TextValue() { }

        /// <summary>以纯文本 fallback 初始化。</summary>
        public TextValue(string fallbackText)
        {
            fallback = fallbackText ?? string.Empty;
        }

        #endregion

        #region 纯文本 fallback + 本地化引用

        /// <summary>纯文本 fallback（始终存在）。</summary>
        public string Fallback
        {
            get => fallback ?? string.Empty;
            set => fallback = value ?? string.Empty;
        }

        /// <summary>本地化引用（表 + 条目）。纯字符串读取，无需本地化包。</summary>
        public (string tableRef, string entryKey) GetLocalizedRef()
            => (tableRef ?? string.Empty, entryKey ?? string.Empty);

        /// <summary>设置本地化引用（表 + 条目）。</summary>
        public void SetLocalizedRef(string table, string entry)
        {
            tableRef = table ?? string.Empty;
            entryKey = entry ?? string.Empty;
        }

        #endregion

        #region 解析

        /// <summary>
        /// 解析显示文本：启用 <c>ATK_LOCALIZATION</c> 且本地化引用可解析出非空文本时返回本地化文本，
        /// 否则返回纯文本 fallback；均为空时返回空串。
        /// </summary>
        public string ResolveText()
        {
#if ATK_LOCALIZATION
            if (!string.IsNullOrEmpty(tableRef) || !string.IsNullOrEmpty(entryKey))
            {
                var ls = new UnityEngine.Localization.LocalizedString(tableRef, entryKey);
                string local = ls.GetLocalizedString();
                if (!string.IsNullOrEmpty(local)) return local;
            }
#endif
            return fallback ?? string.Empty;
        }

        /// <summary>fallback 与本地化引用是否均为空。</summary>
        public bool IsEmpty =>
            string.IsNullOrEmpty(fallback) && string.IsNullOrEmpty(tableRef) && string.IsNullOrEmpty(entryKey);

        #endregion

        #region 克隆

        /// <summary>深拷贝（全为字符串，浅拷即深拷）。</summary>
        public TextValue Clone() => new TextValue { fallback = fallback, tableRef = tableRef, entryKey = entryKey };

        #endregion
    }
}
