using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>内置判定器：无参，恒 <c>true</c>（占位 / 调试）。键 <c>Condition.AlwaysTrue</c>。</summary>
    [ConditionEvaluator("Condition.AlwaysTrue")]
    public sealed class AlwaysTrueEvaluator : IConditionEvaluator
    {
        public string Key => "Condition.AlwaysTrue";
        public string DisplayName => "总是成立";
        public string Category => "Condition";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
    }
}
