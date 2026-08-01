using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>使用方接线：按 id 提供一个数值。</summary>
    public interface IConditionNumberSource
    {
        double GetNumber(string id);
    }

    /// <summary>
    /// 内置判定器：数值 <c>id</c> 与 <c>amount</c> 按 <c>op</c> 比较（大于 / 大于等于 / 等于 / 小于等于 / 小于），
    /// 数值经上下文的 <see cref="IConditionNumberSource"/> 取得。键 <c>Condition.NumberCompare</c>。
    /// <para><c>op</c> 存下拉索引，取值见 <see cref="Greater"/> …；顺序与 <see cref="OpLabels"/> 一致。</para>
    /// </summary>
    [ConditionEvaluator("Condition.NumberCompare")]
    public sealed class NumberCompareEvaluator : IConditionEvaluator
    {
        public const int Greater        = 0; // 大于
        public const int GreaterOrEqual = 1; // 大于等于
        public const int Equal          = 2; // 等于
        public const int LessOrEqual    = 3; // 小于等于
        public const int Less           = 4; // 小于

        private static readonly string[] OpLabels = { "大于", "大于等于", "等于", "小于等于", "小于" };

        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("id",     ConditionParamType.String, false, "数值ID"),
            new ConditionParamDef("op",     ConditionParamType.Int,    false, "比较", null, OpLabels),
            new ConditionParamDef("amount", ConditionParamType.Float,  false, "数值"),
        };

        public string Key => "Condition.NumberCompare";
        public string DisplayName => "数值比较";
        public string Category => "Condition";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IConditionNumberSource>();
            if (src == null) return false;

            string id = parameters.Find("id")?.GetString();
            if (string.IsNullOrEmpty(id)) return false;

            double value  = src.GetNumber(id);
            double amount = parameters.Find("amount")?.GetFloat() ?? 0d;
            int    op     = (int)(parameters.Find("op")?.GetInt() ?? GreaterOrEqual);

            switch (op)
            {
                case Greater:        return value >  amount;
                case GreaterOrEqual: return value >= amount;
                case Equal:          return Math.Abs(value - amount) < 1e-9;
                case LessOrEqual:    return value <= amount;
                case Less:           return value <  amount;
                default:             return value >= amount;
            }
        }
    }
}
