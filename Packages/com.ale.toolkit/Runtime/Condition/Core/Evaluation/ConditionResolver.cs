using System;

namespace Ale.Condition
{
    /// <summary>
    /// 按 id 求值具名条件的门面（效果侧 <c>EffectApplier</c> 的对偶）：把「条件 id」解析成一份
    /// <see cref="ConditionExpression"/> 再交给 <see cref="ConditionEngine"/>。
    ///
    /// <para><b>解析顺序</b>：上下文注册的 <see cref="IConditionDefinitionSource"/> → 显式传入的回落源 →
    /// <see cref="ConditionDefinitionRegistry.Default"/>。</para>
    ///
    /// <para><b>解析不到时 fail-closed</b>：返回不通过、并把该 id 放进 <see cref="ConditionResult.FailedKeys"/>，
    /// 同时经 <see cref="MissingIdWarning"/> 告警。门控场景下「id 写错」应当锁住内容而不是放行——
    /// 与 <see cref="ConditionEngine"/> 对未注册判定器键的处理一致。</para>
    /// </summary>
    public static class ConditionResolver
    {
        /// <summary>解析不到 id 时的告警回调（Unity 桥在启动时接到 <c>Debug.LogWarning</c> 并去重）。</summary>
        public static Action<string> MissingIdWarning;

        /// <summary>按 id 解析条件表达式；未找到返回 null（不告警，供调用方自行判断存在性）。</summary>
        public static ConditionExpression Resolve(string conditionId, IConditionContext ctx,
            IConditionDefinitionSource fallback = null)
        {
            if (string.IsNullOrEmpty(conditionId)) return null;

            var fromCtx = ctx?.GetService<IConditionDefinitionSource>()?.GetCondition(conditionId);
            if (fromCtx != null) return fromCtx;

            var fromFallback = fallback?.GetCondition(conditionId);
            if (fromFallback != null) return fromFallback;

            return ConditionDefinitionRegistry.Default.GetCondition(conditionId);
        }

        /// <summary>按 id 求值：解析不到即不通过（fail-closed）并告警。</summary>
        public static ConditionResult Evaluate(string conditionId, IConditionContext ctx,
            ConditionRegistry registry = null, bool collectAll = false, IConditionDefinitionSource fallback = null)
        {
            var expr = Resolve(conditionId, ctx, fallback);
            if (expr == null)
            {
                MissingIdWarning?.Invoke(conditionId);
                return ConditionResult.Fail(conditionId);
            }
            return ConditionEngine.Evaluate(expr, ctx, registry, collectAll);
        }

        /// <summary>按 id 求值，只要通过与否。</summary>
        public static bool IsSatisfied(string conditionId, IConditionContext ctx,
            ConditionRegistry registry = null, IConditionDefinitionSource fallback = null)
            => Evaluate(conditionId, ctx, registry, false, fallback).Passed;
    }
}
