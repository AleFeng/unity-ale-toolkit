using System.Collections.Generic;
using Ale.GameplayTags;

namespace Ale.Effect
{
    /// <summary>
    /// 内置执行器：移除主体身上 assetTags / grantedTags 命中 <c>tag</c>（层级）的全部效果——「驱散」。
    /// 容器经 <see cref="EffectApplier.ResolveContainer"/> 从上下文解析。键 <c>Effect.RemoveEffectsWithTag</c>。无匹配记 Skipped。
    /// </summary>
    [EffectExecutor("Effect.RemoveEffectsWithTag")]
    public sealed class RemoveEffectsWithTagExecutor : IEffectExecutor
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("tag", EffectParamType.String, false, "标签"),
        };

        public string Key => "Effect.RemoveEffectsWithTag";
        public string DisplayName => "按标签移除效果";
        public string Category => "Effect";
        public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
        {
            var tag = new GameplayTag(parameters.Find("tag")?.GetString());
            if (!tag.IsValid) return EffectResult.Failed("tag 非法或为空");
            var container = EffectApplier.ResolveContainer(ctx, ctx?.Subject);
            if (container == null) return EffectResult.Failed("找不到主体的效果容器");

            int n = container.RemoveEffectsWithTags(new GameplayTagContainer(tag.Name), ctx);
            return n > 0 ? EffectResult.Applied : EffectResult.Skipped("无匹配效果");
        }
    }
}
