using System;
using Ale.Toolkit.Runtime.Serialization;

namespace Ale.Condition.Serialization
{
    /// <summary>
    /// 条件库序列化 DTO：与 <see cref="ConditionDatabase"/> 一一镜像。条件表达式 <see cref="ConditionExpression"/> 直接内嵌
    /// （JSON 导出可读、可 diff；二进制导出时改以 <see cref="ConditionJson"/> 串承载）；属性值 / 定义复用 toolkit 的
    /// <see cref="AttributeValueDto"/> 等。
    /// </summary>
    [Serializable]
    public class ConditionDatabaseDto
    {
        public int version = ConditionConfigSerializer.Version;
        public EnumTypeDto[]          enumTypes;
        public ConditionTemplateDto[] conditionTemplates;
        public ConditionEntryDto[]    conditions;
    }

    /// <summary>条件模板 DTO：名称 / 色点 / 属性字段 schema（基类）+ 默认条件表达式。</summary>
    [Serializable]
    public class ConditionTemplateDto : ConfigTemplateDto
    {
        public ConditionExpression defaultExpression;
    }

    /// <summary>条件条目 DTO。</summary>
    [Serializable]
    public class ConditionEntryDto
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
        /// <summary>条件表达式。</summary>
        public ConditionExpression expression;
    }
}
