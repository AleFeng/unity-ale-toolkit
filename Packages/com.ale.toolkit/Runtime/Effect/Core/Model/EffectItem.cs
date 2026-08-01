using System;
using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Effect
{
    /// <summary>
    /// 原子效果：由一个执行器 <see cref="key"/> 加一组参数构成，另可挂一个可选<b>条件门控</b>
    /// <see cref="gate"/>（复用 Condition System）—— 门控为空则无条件执行，非空则满足才执行。
    /// </summary>
    [Serializable]
    public class EffectItem
    {
        /// <summary>执行器键（对应 <see cref="IEffectExecutor.Key"/> / <see cref="EffectExecutorAttribute"/>）。</summary>
        public string key;

        /// <summary>参数（由所选执行器的 <see cref="IEffectExecutor.ParamSchema"/> 决定）。</summary>
        public List<EffectParam> parameters = new List<EffectParam>();

        /// <summary>可选条件门控（Condition System；空 = 无条件执行）。满足才施加本效果（GAS Conditional Effects）。</summary>
        public ConditionExpression gate = new ConditionExpression();

        public EffectItem() { }

        public EffectItem(string key) { this.key = key; }

        public EffectItem Clone()
        {
            var c = new EffectItem
            {
                key  = key,
                gate = gate != null ? gate.Clone() : new ConditionExpression(),
            };
            c.parameters = new List<EffectParam>(parameters.Count);
            foreach (var p in parameters) c.parameters.Add(p?.Clone());
            return c;
        }
    }
}
