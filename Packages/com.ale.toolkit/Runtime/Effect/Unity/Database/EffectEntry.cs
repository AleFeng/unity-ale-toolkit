using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Effect
{
    /// <summary>
    /// 效果条目：面向所有上层系统共用的效果配置实体——稳定引用键 <see cref="id"/>、面向玩家的本地化显示字段
    /// （名称 / 描述 / 图标，走 <see cref="AttributeValue"/>）、来自 <see cref="EffectTemplate"/> schema 的自定义属性值
    /// （任何系统可按需读取，如特效 / 音效 / UI 配色 / 业务参数），以及 GAS 式 <see cref="EffectDefinition"/>
    /// （时长 / 周期 / 叠加 / 标签 / 施加条件 / 修饰器 / 执行阶段 / 线索）。上层系统（技能「使用」、道具「使用」…）以 <see cref="id"/> 引用。
    ///
    /// <para><b>放置</b>：位于 <see cref="EffectDatabase.Effects"/> 顶层列表——定义内嵌的 执行表达式 → 组 → 项 → 条件 → 组 → 项 → 参数
    /// 已达 Unity 序列化深度上限，本条目不可再被别的类包一层。<see cref="Normalize"/> 把 <see cref="id"/> 与纯文本名同步进
    /// <see cref="definition"/>（定义自身也有 id / displayName，供 toolkit 容器与执行信息使用）。</para>
    /// </summary>
    [Serializable]
    public class EffectEntry : AttributeOwner
    {
        /// <summary>稳定引用键（与 <see cref="definition"/>.id 同步）。</summary>
        public string id;

        /// <summary>来源模板名（可为空）。</summary>
        public string templateRef;

        /// <summary>显示名（Text：纯文本 fallback + 可选本地化引用；为空时回退 <see cref="id"/>）。</summary>
        public AttributeValue displayText = new AttributeValue(EFieldType.Text);

        /// <summary>描述（Text）。</summary>
        public AttributeValue descriptionText = new AttributeValue(EFieldType.Text);

        /// <summary>图标（Sprite 对象类属性值：直接引用或 Addressable 授权 GUID）。</summary>
        public AttributeValue iconValue = new AttributeValue(EFieldType.Sprite);

        /// <summary>来自模板 schema 的自定义属性值（经 <see cref="RebuildAttributes"/> 对账）。</summary>
        public List<AttributeEntry> values = new List<AttributeEntry>();

        /// <summary>效果定义（toolkit GAS 层）。</summary>
        public EffectDefinition definition = new EffectDefinition();

        // 实现基类 AttributeOwner 的抽象属性，将 values 列表暴露给基类的懒加载字典缓存。
        protected override List<AttributeEntry> AttributeEntries => values;

        public EffectEntry()
        {
        }

        public EffectEntry(string id, EDurationPolicy durationPolicy = EDurationPolicy.Instant, string templateRef = null)
        {
            this.id          = id;
            this.templateRef = templateRef;
            definition       = new EffectDefinition(id, durationPolicy);
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

        /// <summary>归一（幂等）：Text / Sprite 字段类型正确、定义非空、定义 id / displayName 与本条目同步，并对定义 <see cref="EffectDefinition.Normalize"/>。</summary>
        public void Normalize()
        {
            displayText     = EnsureType(displayText,     EFieldType.Text);
            descriptionText = EnsureType(descriptionText, EFieldType.Text);
            iconValue       = EnsureType(iconValue,       EFieldType.Sprite);
            values ??= new List<AttributeEntry>();
            definition ??= new EffectDefinition();
            definition.id          = id;
            definition.displayName = PlainName();
            definition.Normalize();
        }

        /// <summary>
        /// 按来源模板 schema 对账自定义属性值：为模板新增字段追加默认值条目；移除模板已不存在的字段；
        /// 已存在字段保留现有值（类型 / 数组形态 / 枚举引用变化时重置为新类型默认值）。模板缺失时清空。
        /// </summary>
        public void RebuildAttributes(IEffectSchemaSource src)
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
        public EffectEntry Clone()
        {
            var clone = new EffectEntry
            {
                id              = id,
                templateRef     = templateRef,
                displayText     = displayText     != null ? displayText.Clone()     : new AttributeValue(EFieldType.Text),
                descriptionText = descriptionText != null ? descriptionText.Clone() : new AttributeValue(EFieldType.Text),
                iconValue       = iconValue       != null ? iconValue.Clone()       : new AttributeValue(EFieldType.Sprite),
                definition      = definition      != null ? definition.Clone()      : new EffectDefinition(),
            };
            if (values != null)
                foreach (var e in values)
                    if (e != null) clone.values.Add(e.Clone());
            return clone;
        }
    }
}
