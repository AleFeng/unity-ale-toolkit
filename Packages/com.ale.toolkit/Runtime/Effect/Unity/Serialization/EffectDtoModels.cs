using System;
using Ale.GameplayTags;
using Ale.Toolkit.Runtime.Serialization;

namespace Ale.Effect.Serialization
{
    /// <summary>
    /// 效果库序列化 DTO：与 <see cref="EffectDatabase"/> 一一镜像。效果定义 <see cref="EffectDefinition"/> 直接内嵌（JSON 导出可读、可 diff；
    /// 二进制导出时改以 Effect System JSON 串承载）；属性值 / 定义复用 toolkit 的 <see cref="AttributeValueDto"/> 等。
    /// </summary>
    [Serializable]
    public class EffectDatabaseDto
    {
        public int version = EffectConfigSerializer.Version;
        public EnumTypeDto[]          enumTypes;
        public EffectTemplateDto[]    effectTemplates;
        public EffectEntryDto[]       effects;
        public GameplayTagDefinition[] gameplayTags;
    }

    /// <summary>效果模板 DTO：名称 / 色点 / 属性字段 schema（基类）+ 默认效果定义。</summary>
    [Serializable]
    public class EffectTemplateDto : ConfigTemplateDto
    {
        public EffectDefinition defaultDefinition;
    }

    /// <summary>效果条目 DTO。</summary>
    [Serializable]
    public class EffectEntryDto
    {
        public string id;
        public string templateRef;
        /// <summary>显示名（Text：纯文本 fallback + 本地化引用）。</summary>
        public AttributeValueDto displayText;
        /// <summary>描述（Text）。</summary>
        public AttributeValueDto descriptionText;
        /// <summary>图标（Sprite 对象类属性值：GUID / Addressable 地址由对象槽承载）。</summary>
        public AttributeValueDto iconValue;
        /// <summary>来自模板 schema 的自定义属性值。</summary>
        public AttributeEntryDto[] values;
        /// <summary>效果定义（导出前 id / displayName 已与条目同步）。</summary>
        public EffectDefinition definition;
    }
}
