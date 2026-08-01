using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 原子条件：由一个判定器 <see cref="key"/> 加一组参数构成，可整体取反（<see cref="negate"/>）。
    /// </summary>
    [Serializable]
    public class ConditionItem
    {
        /// <summary>取反：把判定器结果 NOT 一下。</summary>
        public bool negate;

        /// <summary>判定器键（对应 <see cref="IConditionEvaluator.Key"/> / <see cref="ConditionEvaluatorAttribute"/>）。</summary>
        public string key;

        /// <summary>参数（由所选判定器的 <see cref="IConditionEvaluator.ParamSchema"/> 决定）。</summary>
        public List<ConditionParam> parameters = new List<ConditionParam>();

        public ConditionItem() { }

        public ConditionItem(string key) { this.key = key; }

        public ConditionItem Clone()
        {
            var c = new ConditionItem { negate = negate, key = key };
            c.parameters = new List<ConditionParam>(parameters.Count);
            foreach (var p in parameters) c.parameters.Add(p?.Clone());
            return c;
        }
    }
}
