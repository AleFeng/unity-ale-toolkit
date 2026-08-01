using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 条件组：一组条件项按 <see cref="itemOperator"/>（And/Or）合并，可整体取反（<see cref="negate"/>）。
    /// </summary>
    [Serializable]
    public class ConditionGroup
    {
        /// <summary>取反：把本组合并结果 NOT 一下。</summary>
        public bool negate;

        /// <summary>组内条件项之间的连接（And/Or）。</summary>
        public ConditionLogicOp itemOperator = ConditionLogicOp.And;

        /// <summary>组内条件项。</summary>
        public List<ConditionItem> items = new List<ConditionItem>();

        public ConditionGroup Clone()
        {
            var c = new ConditionGroup { negate = negate, itemOperator = itemOperator };
            c.items = new List<ConditionItem>(items.Count);
            foreach (var i in items) c.items.Add(i?.Clone());
            return c;
        }
    }
}
