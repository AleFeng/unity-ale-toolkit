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
    /// <para><c>op</c> 存下拉索引，语义见 <see cref="ConditionCompare"/>。</para>
    /// </summary>
    [ConditionEvaluator("Condition.NumberCompare")]
    public sealed class NumberCompareEvaluator : IConditionEvaluator
    {
        // 比较符常量转发到 ConditionCompare。
        // 保留在此处是为了不破坏既有引用（含本包测试与下游代码）——它们是已发布的公开 API。
        /// <summary>大于。等价于 <see cref="ConditionCompare.Greater"/>。</summary>
        public const int Greater = ConditionCompare.Greater;

        /// <summary>大于等于。等价于 <see cref="ConditionCompare.GreaterOrEqual"/>。</summary>
        public const int GreaterOrEqual = ConditionCompare.GreaterOrEqual;

        /// <summary>等于。等价于 <see cref="ConditionCompare.Equal"/>。</summary>
        public const int Equal = ConditionCompare.Equal;

        /// <summary>小于等于。等价于 <see cref="ConditionCompare.LessOrEqual"/>。</summary>
        public const int LessOrEqual = ConditionCompare.LessOrEqual;

        /// <summary>小于。等价于 <see cref="ConditionCompare.Less"/>。</summary>
        public const int Less = ConditionCompare.Less;

        /// <summary>
        /// 本判定器「等于」所用的容差。
        ///
        /// <para>刻意<b>不</b>跟随 <see cref="ConditionCompare.DefaultEpsilon"/>（<c>1e-6</c>）：
        /// 本判定器自 1.4.0 起就是 <c>1e-9</c>，跟随默认值会放宽既有行为。
        /// 数值来自宿主的 <see cref="IConditionNumberSource"/>，本就是 <c>double</c>，
        /// 不像属性系统那样普遍存在 float 扩宽误差，用严格容差是合适的。</para>
        /// </summary>
        public const double Epsilon = 1e-9;

        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("id",     ConditionParamType.String, false, "数值ID"),
            ConditionCompare.CreateOpParam(),
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

            return ConditionCompare.Compare(value, amount, ConditionCompare.ReadOp(parameters), Epsilon);
        }
    }
}
