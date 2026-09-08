using System;
using Ale.Toolkit.Runtime;

namespace Ale.Condition
{
    /// <summary>
    /// 条件模板（蓝图）：名称 / 色点 / 自定义属性字段 schema（来自 <see cref="ConfigTemplateBase"/>）+ 默认条件表达式预设。
    /// 从模板创建 <see cref="ConditionEntry"/> 时深拷贝 <see cref="defaultExpression"/>，并按 schema 建立自定义属性值；
    /// 编辑器中列以模板作过滤维。
    ///
    /// <para>序列化深度：库 → 模板 → 表达式 → 组 → 项 → 参数 只有 5 层，余量充足
    /// （效果侧因为定义里又内嵌了完整条件表达式才逼近上限）。</para>
    /// </summary>
    [Serializable]
    public class ConditionTemplate : ConfigTemplateBase
    {
        /// <summary>默认条件表达式（从模板创建条件条目时深拷贝为预设）。</summary>
        public ConditionExpression defaultExpression = new ConditionExpression();

        public ConditionTemplate()
        {
        }

        public ConditionTemplate(string name) : base(name)
        {
        }

        /// <summary>归一（幂等）：补 null。</summary>
        public void Normalize()
        {
            defaultExpression ??= new ConditionExpression();
            defaultExpression.groups ??= new System.Collections.Generic.List<ConditionGroup>();
            defaultExpression.groups.RemoveAll(g => g == null);
        }

        /// <summary>深拷贝（名称 / 色点 / schema / 默认表达式）。</summary>
        public ConditionTemplate Clone()
        {
            var clone = new ConditionTemplate();
            CopyTo(clone);
            clone.defaultExpression = defaultExpression != null ? defaultExpression.Clone() : new ConditionExpression();
            return clone;
        }
    }
}
