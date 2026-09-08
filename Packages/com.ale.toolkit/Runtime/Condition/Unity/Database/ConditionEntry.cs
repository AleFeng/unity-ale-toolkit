using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Condition
{
    /// <summary>
    /// 条件条目：面向所有上层系统共用的具名条件配置——稳定引用键 <see cref="id"/>、面向玩家的本地化显示字段
    /// （名称 / 描述 / 图标，走 <see cref="AttributeValue"/>；用于「未满足时告诉玩家缺什么」这类 UI）、来自
    /// <see cref="ConditionTemplate"/> schema 的自定义属性值（任何系统可按需读取，如提示语气 / UI 配色 / 业务参数），
    /// 以及两级 AND/OR 的 <see cref="ConditionExpression"/>。上层系统（技能树解锁、特质获得、头衔取得…）以 <see cref="id"/> 引用。
    ///
    /// <para><b>为什么要包这一层</b>：<see cref="ConditionExpression"/> 是纯 POCO、没有 id 也不该有——它已发布、有 JSON 往返格式。
    /// 具名与显示信息由本条目承担，Core 模型保持不变。</para>
    /// </summary>
    [Serializable]
    public class ConditionEntry : AttributeOwner
    {
        /// <summary>稳定引用键。</summary>
        public string id;

        /// <summary>来源模板名（可为空）。</summary>
        public string templateRef;

        /// <summary>显示名（Text：纯文本 fallback + 可选本地化引用；为空时回退 <see cref="id"/>）。</summary>
        public AttributeValue displayText = new AttributeValue(EFieldType.Text);

        /// <summary>描述（Text）——一般写「未满足时给玩家看的那句话」。</summary>
        public AttributeValue descriptionText = new AttributeValue(EFieldType.Text);

        /// <summary>图标（Sprite 对象类属性值：直接引用或 Addressable 授权 GUID）。</summary>
        public AttributeValue iconValue = new AttributeValue(EFieldType.Sprite);

        /// <summary>来自模板 schema 的自定义属性值（经 <see cref="RebuildAttributes"/> 对账）。</summary>
        public List<AttributeEntry> values = new List<AttributeEntry>();

        /// <summary>条件表达式（两级 AND/OR）。</summary>
        public ConditionExpression expression = new ConditionExpression();

        // 实现基类 AttributeOwner 的抽象属性，将 values 列表暴露给基类的懒加载字典缓存。
        protected override List<AttributeEntry> AttributeEntries => values;

        public ConditionEntry()
        {
        }

        public ConditionEntry(string id, string templateRef = null)
        {
            this.id          = id;
            this.templateRef = templateRef;
        }

        /// <summary>显示名解析（本地化优先 → 纯文本，均空回退 <see cref="id"/>）。运行时 UI 用。</summary>
        public string ResolveDisplayName()
        {
            string s = displayText != null ? displayText.ResolveText() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>纯文本显示名（编辑期稳定，均空回退 <see cref="id"/>）。编辑器列表 / 下拉用。</summary>
        public string PlainName()
        {
            string s = displayText != null ? displayText.GetTextValue() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>归一（幂等）：Text / Sprite 字段类型正确、表达式与其组列表非空、剔除 null 组。</summary>
        public void Normalize()
        {
            displayText     = EnsureType(displayText,     EFieldType.Text);
            descriptionText = EnsureType(descriptionText, EFieldType.Text);
            iconValue       = EnsureType(iconValue,       EFieldType.Sprite);
            values ??= new List<AttributeEntry>();

            expression ??= new ConditionExpression();
            expression.groups ??= new List<ConditionGroup>();
            expression.groups.RemoveAll(g => g == null);
            foreach (var g in expression.groups)
            {
                g.items ??= new List<ConditionItem>();
                g.items.RemoveAll(i => i == null);
            }
        }

        /// <summary>
        /// 按来源模板 schema 对账自定义属性值：为模板新增字段追加默认值条目；移除模板已不存在的字段；
        /// 已存在字段保留现有值（类型 / 数组形态 / 枚举引用变化时重置为新类型默认值）。模板缺失时清空。
        /// </summary>
        public void RebuildAttributes(IConditionSchemaSource src)
        {
            var template = src?.GetTemplate(templateRef);
            AttributeSync.Sync(values, template != null ? template.attributes : null);
            InvalidateEntryCache();
        }

        private static AttributeValue EnsureType(AttributeValue v, EFieldType t)
        {
            if (v == null) return new AttributeValue(t);
            if (v.Type != t || v.IsArray) v.ChangeType(t, false);
            return v;
        }

        /// <summary>深拷贝。</summary>
        public ConditionEntry Clone()
        {
            var clone = new ConditionEntry
            {
                id              = id,
                templateRef     = templateRef,
                displayText     = displayText     != null ? displayText.Clone()     : new AttributeValue(EFieldType.Text),
                descriptionText = descriptionText != null ? descriptionText.Clone() : new AttributeValue(EFieldType.Text),
                iconValue       = iconValue       != null ? iconValue.Clone()       : new AttributeValue(EFieldType.Sprite),
                expression      = expression      != null ? expression.Clone()      : new ConditionExpression(),
            };
            if (values != null)
                foreach (var e in values)
                    if (e != null) clone.values.Add(e.Clone());
            return clone;
        }
    }
}
