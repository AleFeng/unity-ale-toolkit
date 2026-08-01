using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 条件表达式求值引擎（静态、无状态、引擎无关）。
    /// <para><b>结算</b>：组内按 <see cref="ConditionGroup.itemOperator"/> 合并各项（含每项 <see cref="ConditionItem.negate"/>）
    /// → 组按 <see cref="ConditionGroup.negate"/> 取反 → 顶层按 <see cref="ConditionExpression.groupOperator"/> 合并各组。
    /// 空表达式 / 全空组约定为「通过」。默认短路；<paramref name="collectAll"/> 关闭短路以尽量收集全部未满足键。</para>
    /// <para>未注册 / 空键的条件项视为「不通过」，并触发 <see cref="ConditionRegistry.MissingKeyWarning"/>。</para>
    /// </summary>
    public static class ConditionEngine
    {
        public static ConditionResult Evaluate(ConditionExpression expr, IConditionContext ctx,
            ConditionRegistry registry = null, bool collectAll = false)
        {
            registry = registry ?? ConditionRegistry.Default;
            if (expr?.groups == null || expr.groups.Count == 0)
                return ConditionResult.Pass;

            var  failed   = new List<string>();
            bool sawGroup = false;
            bool orAny    = false;
            bool andAll   = true;

            foreach (var group in expr.groups)
            {
                if (group?.items == null || group.items.Count == 0) continue; // 空组忽略
                sawGroup = true;

                bool groupPassed = EvaluateGroup(group, ctx, registry, collectAll, failed);

                if (expr.groupOperator == ConditionLogicOp.Or)
                {
                    if (groupPassed)
                    {
                        orAny = true;
                        if (!collectAll) return ConditionResult.Pass; // 短路：或——任一组成立
                    }
                }
                else // And
                {
                    if (!groupPassed)
                    {
                        andAll = false;
                        if (!collectAll) return ConditionResult.Fail(failed); // 短路：与——任一组不成立
                    }
                }
            }

            if (!sawGroup) return ConditionResult.Pass; // 全空组 = 通过

            bool passed = expr.groupOperator == ConditionLogicOp.And ? andAll : orAny;
            return passed ? ConditionResult.Pass : ConditionResult.Fail(failed);
        }

        private static bool EvaluateGroup(ConditionGroup group, IConditionContext ctx,
            ConditionRegistry registry, bool collectAll, List<string> failed)
        {
            bool sawItem = false;
            bool orAny   = false;
            bool andAll  = true;

            foreach (var item in group.items)
            {
                if (item == null) continue;
                sawItem = true;
                bool itemPassed = EvaluateItem(item, ctx, registry, failed);

                if (group.itemOperator == ConditionLogicOp.Or)
                {
                    if (itemPassed) { orAny = true; if (!collectAll) break; }
                }
                else
                {
                    if (!itemPassed) { andAll = false; if (!collectAll) break; }
                }
            }

            bool inner = !sawItem
                ? true
                : (group.itemOperator == ConditionLogicOp.And ? andAll : orAny);
            return group.negate ? !inner : inner;
        }

        private static bool EvaluateItem(ConditionItem item, IConditionContext ctx,
            ConditionRegistry registry, List<string> failed)
        {
            bool raw;
            if (string.IsNullOrEmpty(item.key))
            {
                raw = false; // 未配置键 → 不通过
            }
            else if (registry.TryGet(item.key, out var evaluator))
            {
                raw = evaluator.Evaluate(item.parameters, ctx);
            }
            else
            {
                registry.NotifyMissing(item.key); // 未注册键 → 告警 + 不通过
                raw = false;
            }

            bool result = item.negate ? !raw : raw;
            if (!result) failed.Add(item.key);
            return result;
        }
    }
}
