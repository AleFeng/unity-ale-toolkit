using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>使用方接线：按 id 提供一个数值。</summary>
    public interface IConditionNumberSource
    {
        double GetNumber(string id);
    }

    /// <summary>
    /// 内置判定器：数值 <c>id</c> ≥ <c>amount</c>（经上下文的 <see cref="IConditionNumberSource"/> 服务）。
    /// 键 <c>Condition.NumberAtLeast</c>。
    /// </summary>
    [ConditionEvaluator("Condition.NumberAtLeast")]
    public sealed class NumberAtLeastEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("id",     ConditionParamType.String, false, "数值ID"),
            new ConditionParamDef("amount", ConditionParamType.Float,  false, "下限"),
        };

        public string Key => "Condition.NumberAtLeast";
        public string DisplayName => "数值不低于";
        public string Category => "Condition";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IConditionNumberSource>();
            if (src == null) return false;
            string id = parameters.Find("id")?.GetString();
            if (string.IsNullOrEmpty(id)) return false;
            double amount = parameters.Find("amount")?.GetFloat() ?? 0d;
            return src.GetNumber(id) >= amount;
        }
    }
}
