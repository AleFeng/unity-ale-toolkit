using System;
using System.Collections.Generic;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 某条修饰器对最终值的「边际贡献」，用于「基础 → 当前」明细展示与调试追溯。
    /// </summary>
    public readonly struct ModifierContribution
    {
        /// <summary>来源标识（<see cref="ModifierDefinition.sourceTag"/>）。</summary>
        public readonly string SourceTag;

        /// <summary>运算方式。</summary>
        public readonly EModifierOperation Operation;

        /// <summary>原始幅度。</summary>
        public readonly float Magnitude;

        /// <summary>
        /// 该修饰器在其结算阶段对运行值造成的「边际增量」（clamp 之前）。
        /// 所有 <see cref="Delta"/> 之和 = 未 clamp 的最终值 − 基础值。
        /// </summary>
        public readonly float Delta;

        public ModifierContribution(string sourceTag, EModifierOperation operation, float magnitude, float delta)
        {
            SourceTag = sourceTag;
            Operation = operation;
            Magnitude = magnitude;
            Delta     = delta;
        }
    }

    /// <summary>
    /// 一次修饰器栈求值的结果：基础值、未 clamp 的最终值、clamp 后的最终值，以及逐条来源明细。
    /// </summary>
    public readonly struct ModifierEvaluation
    {
        /// <summary>基础值。</summary>
        public readonly float BaseValue;

        /// <summary>未 clamp 的最终值（= <see cref="BaseValue"/> + Σ 明细 <see cref="ModifierContribution.Delta"/>）。</summary>
        public readonly float RawValue;

        /// <summary>clamp 到 <c>[min, max]</c> 后的最终值。</summary>
        public readonly float Value;

        /// <summary>逐条来源明细（结算顺序：Add → PercentAdd → Multiply → Override）；调用方要求不收集时为空列表。</summary>
        public readonly IReadOnlyList<ModifierContribution> Breakdown;

        public ModifierEvaluation(float baseValue, float rawValue, float value, IReadOnlyList<ModifierContribution> breakdown)
        {
            BaseValue = baseValue;
            RawValue  = rawValue;
            Value     = value;
            Breakdown = breakdown;
        }
    }

    /// <summary>
    /// 修饰器栈的纯函数求值器（无状态、无 Unity 依赖）。
    ///
    /// <para><b>结算顺序（固定）</b>：基础值
    /// → 先加上所有 <see cref="EModifierOperation.Add"/>
    /// → 再乘 <c>(1 + Σ PercentAdd)</c>（所有 <see cref="EModifierOperation.PercentAdd"/> 求和后一次性）
    /// → 再逐个乘 <c>(1 + magnitude)</c>（所有 <see cref="EModifierOperation.Multiply"/>，按传入顺序连乘）
    /// → 若存在 <see cref="EModifierOperation.Override"/> 则取最后一个覆盖值
    /// → 最后 clamp 到 <c>[min, max]</c>。</para>
    ///
    /// <para><b>契约</b>：传入的 <c>modifiers</c> 应已是「作用于同一目标值」的集合
    /// （按 <see cref="ModifierDefinition.targetAttributeId"/> 分组由调用方完成）；求值器不再按目标过滤，
    /// 也不解释时效 / 叠加（那些是运行时构建生效列表时的职责）。<c>null</c> 元素会被跳过。</para>
    /// </summary>
    public static class ModifierStackEvaluator
    {
        /// <summary>
        /// 完整求值：返回基础值、未 clamp 最终值、clamp 最终值与逐条明细。
        /// </summary>
        /// <param name="baseValue">基础值。</param>
        /// <param name="minValue">下限（不需要下限时传 <see cref="float.NegativeInfinity"/>）。</param>
        /// <param name="maxValue">上限（不需要上限时传 <see cref="float.PositiveInfinity"/>）。</param>
        /// <param name="modifiers">作用于同一目标值的修饰器集合（可为 null）。</param>
        /// <param name="collectBreakdown">是否收集逐条明细（false 时不分配明细列表，热路径用）。</param>
        public static ModifierEvaluation Evaluate(float baseValue, float minValue, float maxValue,
            IEnumerable<ModifierDefinition> modifiers, bool collectBreakdown = true)
        {
            List<ModifierContribution> breakdown = collectBreakdown ? new List<ModifierContribution>() : null;

            float sumAdd     = 0f;
            float sumPercent = 0f;
            List<ModifierDefinition> percentMods  = collectBreakdown ? new List<ModifierDefinition>() : null; // 仅明细需要
            List<ModifierDefinition> multiplyMods = null;
            ModifierDefinition overrideMod = null;

            if (modifiers != null)
            {
                foreach (var m in modifiers)
                {
                    if (m == null) continue;
                    switch (m.operation)
                    {
                        case EModifierOperation.Add:
                            sumAdd += m.magnitude;
                            breakdown?.Add(new ModifierContribution(m.sourceTag, EModifierOperation.Add, m.magnitude, m.magnitude));
                            break;
                        case EModifierOperation.PercentAdd:
                            sumPercent += m.magnitude;
                            percentMods?.Add(m);
                            break;
                        case EModifierOperation.Multiply:
                            (multiplyMods ??= new List<ModifierDefinition>()).Add(m);
                            break;
                        case EModifierOperation.Override:
                            overrideMod = m; // 最后一个生效
                            break;
                    }
                }
            }

            float afterAdd = baseValue + sumAdd;

            // PercentAdd：整组一次性乘 (1 + Σ)；明细按各自占比拆分 delta = afterAdd × magnitude_i。
            float afterPercent = afterAdd * (1f + sumPercent);
            if (breakdown != null && percentMods != null)
            {
                foreach (var pm in percentMods)
                    breakdown.Add(new ModifierContribution(pm.sourceTag, EModifierOperation.PercentAdd, pm.magnitude, afterAdd * pm.magnitude));
            }

            // Multiply：逐个 (1 + magnitude) 连乘；明细取每步边际 delta。
            float running = afterPercent;
            if (multiplyMods != null)
            {
                foreach (var mm in multiplyMods)
                {
                    float before = running;
                    running *= 1f + mm.magnitude;
                    breakdown?.Add(new ModifierContribution(mm.sourceTag, EModifierOperation.Multiply, mm.magnitude, running - before));
                }
            }

            // Override：最后一个覆盖值生效；明细 delta = 覆盖值 − 覆盖前的运行值。
            if (overrideMod != null)
            {
                float before = running;
                running = overrideMod.magnitude;
                breakdown?.Add(new ModifierContribution(overrideMod.sourceTag, EModifierOperation.Override, overrideMod.magnitude, running - before));
            }

            float clamped = running;
            if (clamped < minValue) clamped = minValue;
            if (clamped > maxValue) clamped = maxValue;

            IReadOnlyList<ModifierContribution> result =
                breakdown ?? (IReadOnlyList<ModifierContribution>)Array.Empty<ModifierContribution>();
            return new ModifierEvaluation(baseValue, running, clamped, result);
        }

        /// <summary>仅求 clamp 后的最终值（不分配明细列表，热路径用）。</summary>
        public static float EvaluateValue(float baseValue, float minValue, float maxValue,
            IEnumerable<ModifierDefinition> modifiers)
            => Evaluate(baseValue, minValue, maxValue, modifiers, collectBreakdown: false).Value;
    }
}
