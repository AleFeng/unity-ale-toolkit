using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>使用方接线：提供「是否持有某标记」。</summary>
    public interface IConditionFlagSource
    {
        bool HasFlag(string flag);
    }

    /// <summary>
    /// 内置判定器：主体是否持有标记 <c>flag</c>（经上下文的 <see cref="IConditionFlagSource"/> 服务）。
    /// 键 <c>Condition.HasFlag</c>。
    /// </summary>
    [ConditionEvaluator("Condition.HasFlag")]
    public sealed class HasFlagEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("flag", ConditionParamType.String, false, "标记"),
        };

        public string Key => "Condition.HasFlag";
        public string DisplayName => "持有标记";
        public string Category => "Condition";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IConditionFlagSource>();
            if (src == null) return false;
            string flag = parameters.Find("flag")?.GetString();
            return !string.IsNullOrEmpty(flag) && src.HasFlag(flag);
        }
    }
}
