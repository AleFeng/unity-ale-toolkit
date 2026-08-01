using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Effect
{
    /// <summary>
    /// 效果表达式执行器（静态、无状态、引擎无关）——Condition System 的 <c>ConditionEngine</c> 的写侧对偶。
    /// <para><b>执行</b>：按序遍历 <see cref="EffectExpression.groups"/>（<paramref name="phase"/> 过滤见下）→ 组内按序遍历
    /// <see cref="EffectGroup.items"/>；每项先求值可选 <see cref="EffectItem.gate"/>（走 <see cref="ConditionEngine"/>），
    /// 不满足则记 <see cref="EEffectOutcome.Skipped"/>；满足则按 <see cref="EffectItem.key"/> 取执行器 <see cref="IEffectExecutor.Execute"/>；
    /// 键为空 / 未注册记 <see cref="EEffectOutcome.Failed"/> 并触发 <see cref="EffectRegistry.MissingKeyWarning"/>。</para>
    /// <para><b>phase 过滤</b>：<paramref name="phase"/> 为 null 时执行全部组；非 null 时只执行「<see cref="EffectGroup.phase"/> 等于它」
    /// 或「<see cref="EffectGroup.phase"/> 为空（通配）」的组。</para>
    /// </summary>
    public static class EffectRunner
    {
        public static EffectRunReport Run(EffectExpression expr, IEffectContext ctx, string phase = null,
            EffectRegistry effectRegistry = null, ConditionRegistry conditionRegistry = null)
        {
            effectRegistry = effectRegistry ?? EffectRegistry.Default;
            if (expr?.groups == null || expr.groups.Count == 0)
                return EffectRunReport.Empty;

            int applied = 0, skipped = 0, failed = 0;
            var items = new List<EffectItemOutcome>();

            foreach (var group in expr.groups)
            {
                if (group?.items == null || group.items.Count == 0) continue;
                // phase 过滤：请求特定 phase 时，跳过「有非空 phase 且不匹配」的组；空 phase 组为通配，任意 phase 都执行。
                if (phase != null && !string.IsNullOrEmpty(group.phase) && group.phase != phase) continue;

                foreach (var item in group.items)
                {
                    if (item == null) continue;

                    // 条件门控：gate 非空且不满足 → 跳过。
                    if (item.gate != null && !item.gate.IsEmpty)
                    {
                        var gateResult = ConditionEngine.Evaluate(item.gate, ctx, conditionRegistry);
                        if (!gateResult.Passed)
                        {
                            skipped++;
                            items.Add(new EffectItemOutcome(item.key, EffectResult.Skipped("门控条件不满足")));
                            continue;
                        }
                    }

                    EffectResult result;
                    if (string.IsNullOrEmpty(item.key))
                    {
                        result = EffectResult.Failed("未配置执行器键");
                    }
                    else if (effectRegistry.TryGet(item.key, out var executor))
                    {
                        result = executor.Execute(item.parameters, ctx);
                    }
                    else
                    {
                        effectRegistry.NotifyMissing(item.key);
                        result = EffectResult.Failed("执行器键未注册");
                    }

                    switch (result.Outcome)
                    {
                        case EEffectOutcome.Applied: applied++; break;
                        case EEffectOutcome.Skipped: skipped++; break;
                        default:                     failed++;  break;
                    }
                    items.Add(new EffectItemOutcome(item.key, result));
                }
            }

            return new EffectRunReport(applied, skipped, failed, items);
        }
    }
}
