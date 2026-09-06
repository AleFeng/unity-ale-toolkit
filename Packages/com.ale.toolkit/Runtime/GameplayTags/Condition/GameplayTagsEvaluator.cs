using System.Collections.Generic;
using Ale.Condition;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 内置判定器：主体与一组标签 <c>tags</c> 的关系——<c>mode</c> 取「任一 / 全部 / 皆无」（<see cref="GameplayTagMatchMode"/>），
    /// <c>exact</c> 为 true 时精确匹配。键 <c>Condition.GameplayTags</c>。
    /// 与 Condition System 自身的与或非组合即可覆盖 GAS <c>FGameplayTagQuery</c> 的全部表达力。
    /// </summary>
    [ConditionEvaluator("Condition.GameplayTags")]
    public sealed class GameplayTagsEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("tags",  ConditionParamType.String, true,  "标签"),
            GameplayTagMatchMode.CreateModeParam(),
            new ConditionParamDef("exact", ConditionParamType.Bool,   false, "精确匹配"),
        };

        public string Key => "Condition.GameplayTags";
        public string DisplayName => "标签集合";
        public string Category => "Condition";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var owned = GameplayTagConditionUtil.ResolveOwnedTags(ctx);
            if (owned == null) return false;

            var container = new GameplayTagContainer();
            var p = parameters.Find("tags");
            if (p != null)
                for (int i = 0; i < p.Count; i++)
                    container.AddTag(p.GetString(i));

            int  mode  = GameplayTagMatchMode.ReadMode(parameters);
            bool exact = parameters.Find("exact")?.GetBool() ?? false;
            return GameplayTagMatchMode.Matches(owned, container, mode, exact);
        }
    }
}
