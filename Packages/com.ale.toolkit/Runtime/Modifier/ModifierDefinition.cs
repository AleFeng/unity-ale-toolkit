using System;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 一条属性修饰器的配置定义（GAS / ModiBuff 范式）：对某个「目标数值」施加可叠加、可撤销的增减。
    ///
    /// <para>行为与 Buff 不直接改基础值，而是附加带来源、时长、堆叠规则的 <see cref="ModifierDefinition"/>；
    /// 「当前值」由 <see cref="ModifierStackEvaluator"/> 用「基础值 + 一组修饰器」现场求出——可完整追溯、可整组撤销。</para>
    ///
    /// <para><b>领域无关</b>：本类不认识「角色 / 属性系统」等上层概念。<see cref="targetAttributeId"/>
    /// 只是一个由使用方约定的字符串键；使用方（如角色系统的属性汇流）负责把修饰器按目标键分组后交给求值器。</para>
    /// </summary>
    [Serializable]
    public class ModifierDefinition
    {
        /// <summary>作用目标的标识键（由使用方约定，如某核心属性 id）。求值器本身不解释其含义。</summary>
        public string targetAttributeId;

        /// <summary>运算方式（Add / PercentAdd / Multiply / Override）。</summary>
        public EModifierOperation operation = EModifierOperation.Add;

        /// <summary>幅度。语义随 <see cref="operation"/>：Add=加数；PercentAdd / Multiply=百分比（0.5 = +50%）；Override=覆盖值。</summary>
        public float magnitude;

        /// <summary>时效（求值器不解释，运行时据此到期移除）。</summary>
        public EModifierDuration duration = EModifierDuration.Permanent;

        /// <summary><see cref="EModifierDuration.Timed"/> 时的存活天数（世界内天数）。</summary>
        public float durationDays;

        /// <summary>来源标识（如 <c>trait:勇敢</c> / <c>race:elf</c>）。用于成组撤销与「为什么是这个值」的追溯。</summary>
        public string sourceTag;

        /// <summary>最大叠加层数（求值器不解释，运行时构建生效列表时应用）。</summary>
        public int stackLimit = 1;

        /// <summary>叠加规则（求值器不解释，运行时应用）。</summary>
        public EStackRule stackRule = EStackRule.Refresh;

        public ModifierDefinition()
        {
        }

        public ModifierDefinition(string targetAttributeId, EModifierOperation operation, float magnitude, string sourceTag = null)
        {
            this.targetAttributeId = targetAttributeId;
            this.operation         = operation;
            this.magnitude         = magnitude;
            this.sourceTag         = sourceTag;
        }

        /// <summary>深拷贝。</summary>
        public ModifierDefinition Clone()
        {
            return new ModifierDefinition
            {
                targetAttributeId = targetAttributeId,
                operation         = operation,
                magnitude         = magnitude,
                duration          = duration,
                durationDays      = durationDays,
                sourceTag         = sourceTag,
                stackLimit        = stackLimit,
                stackRule         = stackRule,
            };
        }
    }
}
