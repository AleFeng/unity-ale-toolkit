using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 条件表达式：固定两级结构（表达式 → 组 → 项 → 参数），各条件组按 <see cref="groupOperator"/>（And/Or）合并。
    /// 无多态、无 Dictionary —— Unity 原生序列化 + Undo 直接可用，Newtonsoft JSON 亦可往返。
    /// <para>空表达式（无组 / 组皆空）约定为「通过」。</para>
    /// </summary>
    [Serializable]
    public class ConditionExpression
    {
        /// <summary>各条件组之间的连接（And/Or）。</summary>
        public ConditionLogicOp groupOperator = ConditionLogicOp.And;

        /// <summary>条件组列表。</summary>
        public List<ConditionGroup> groups = new List<ConditionGroup>();

        /// <summary>是否为空（不含任何条件项）。空表达式求值恒为通过。</summary>
        public bool IsEmpty => TotalItemCount() == 0;

        /// <summary>全部条件项总数（用于列表列 / 折叠标题摘要）。</summary>
        public int TotalItemCount()
        {
            int n = 0;
            if (groups != null)
                foreach (var g in groups)
                    if (g?.items != null) n += g.items.Count;
            return n;
        }

        public ConditionExpression Clone()
        {
            var c = new ConditionExpression { groupOperator = groupOperator };
            c.groups = new List<ConditionGroup>(groups.Count);
            foreach (var g in groups) c.groups.Add(g?.Clone());
            return c;
        }

        /// <summary>便捷求值：转调 <see cref="ConditionEngine.Evaluate"/>。</summary>
        public ConditionResult Evaluate(IConditionContext ctx, ConditionRegistry registry = null, bool collectAll = false)
            => ConditionEngine.Evaluate(this, ctx, registry, collectAll);
    }
}
