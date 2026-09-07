using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 内置执行器：移除主体身上定义 id 等于 <c>effectId</c> 的全部效果。容器从上下文解析。键 <c>Effect.RemoveEffectById</c>。无匹配记 Skipped。
    /// </summary>
    [EffectExecutor("Effect.RemoveEffectById")]
    public sealed class RemoveEffectByIdExecutor : IEffectExecutor
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("effectId", EffectParamType.String, false, "效果ID"),
        };

        public string Key => "Effect.RemoveEffectById";
        public string DisplayName => "按ID移除效果";
        public string Category => "Effect";
        public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
        {
            string effectId = parameters.Find("effectId")?.GetString();
            if (string.IsNullOrEmpty(effectId)) return EffectResult.Failed("effectId 为空");
            var container = EffectApplier.ResolveContainer(ctx, ctx?.Subject);
            if (container == null) return EffectResult.Failed("找不到主体的效果容器");

            int n = container.RemoveEffectsById(effectId, ctx);
            return n > 0 ? EffectResult.Applied : EffectResult.Skipped("无匹配效果");
        }
    }
}
