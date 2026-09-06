using System.Collections.Generic;
using Ale.Condition;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 内置判定器：主体是否持有标签 <c>tag</c>（默认层级匹配——持有其后代亦算；<c>exact</c> 为 true 时精确）。
    /// 键 <c>Condition.HasGameplayTag</c>。主体标签经 <see cref="GameplayTagConditionUtil.ResolveOwnedTags"/> 解析，解析不到 → false。
    /// </summary>
    [ConditionEvaluator("Condition.HasGameplayTag")]
    public sealed class HasGameplayTagEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("tag",   ConditionParamType.String, false, "标签"),
            new ConditionParamDef("exact", ConditionParamType.Bool,   false, "精确匹配"),
        };

        public string Key => "Condition.HasGameplayTag";
        public string DisplayName => "持有标签";
        public string Category => "Condition";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var owned = GameplayTagConditionUtil.ResolveOwnedTags(ctx);
            if (owned == null) return false;

            var tag = new GameplayTag(parameters.Find("tag")?.GetString());
            if (!tag.IsValid) return false;

            bool exact = parameters.Find("exact")?.GetBool() ?? false;
            return exact ? owned.HasExactTag(tag) : owned.HasMatchingTag(tag);
        }
    }
}
