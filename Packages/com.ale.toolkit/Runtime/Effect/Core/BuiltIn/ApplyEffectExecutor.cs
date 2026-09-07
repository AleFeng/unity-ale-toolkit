using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 内置执行器：对主体施加另一个效果（按 id；定义与容器经 <see cref="EffectApplier"/> 从上下文解析）。
    /// 用于效果组合——如「施加时顺带挂上一个持续 Buff」。来源沿用当前执行信息的来源。键 <c>Effect.ApplyEffect</c>。
    /// 被阻断（免疫 / 要求 / 条件 / 概率）记 Skipped；找不到容器或定义记 Failed。
    /// </summary>
    [EffectExecutor("Effect.ApplyEffect")]
    public sealed class ApplyEffectExecutor : IEffectExecutor
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("effectId",  EffectParamType.String, false, "效果ID"),
            new EffectParamDef("level",     EffectParamType.Int,    false, "等级"),
            new EffectParamDef("sourceTag", EffectParamType.String, false, "来源标记(可空)"),
        };

        public string Key => "Effect.ApplyEffect";
        public string DisplayName => "施加效果";
        public string Category => "Effect";
        public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
        {
            string effectId = parameters.Find("effectId")?.GetString();
            if (string.IsNullOrEmpty(effectId)) return EffectResult.Failed("effectId 为空");

            int level = (int)(parameters.Find("level")?.GetInt() ?? 1L);
            if (level < 1) level = 1;
            string sourceTag = parameters.Find("sourceTag")?.GetString();
            var info = ctx?.GetService<IEffectExecutionInfo>();

            var request = new EffectApplyRequest(effectId, level)
            {
                Source    = info?.Source,
                Target    = ctx?.Subject,
                SourceTag = string.IsNullOrEmpty(sourceTag) ? null : sourceTag,
            };
            var result = EffectApplier.Apply(request, ctx);
            if (result.IsSuccess) return EffectResult.Applied;
            switch (result.Outcome)
            {
                case EEffectApplyOutcome.NoContainer:
                case EEffectApplyOutcome.DefinitionNotFound:
                case EEffectApplyOutcome.Invalid:
                    return EffectResult.Failed(result.Note ?? result.Outcome.ToString());
                default:
                    return EffectResult.Skipped(result.Note ?? result.Outcome.ToString());
            }
        }
    }
}
