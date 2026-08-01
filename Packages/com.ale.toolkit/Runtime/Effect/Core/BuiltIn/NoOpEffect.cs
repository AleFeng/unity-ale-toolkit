using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 内置执行器：无操作占位（<c>Ale.Condition</c> 的 <c>AlwaysTrueEvaluator</c> 的写侧对偶）。
    /// 无参、恒返回 <see cref="EffectResult.Applied"/>；用于占位 / 调试 / 目录自检。键 <c>Effect.NoOp</c>。
    /// </summary>
    [EffectExecutor("Effect.NoOp")]
    public sealed class NoOpEffect : IEffectExecutor
    {
        public string Key => "Effect.NoOp";
        public string DisplayName => "空操作";
        public string Category => "Effect";
        public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
            => EffectResult.Applied;
    }
}
