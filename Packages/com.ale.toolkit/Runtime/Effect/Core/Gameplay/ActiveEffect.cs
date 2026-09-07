using System;
using System.Collections.Generic;
using Ale.Modifier;

namespace Ale.Effect
{
    /// <summary>
    /// 活动效果实例（GAS <c>FActiveGameplayEffect</c>）：容器里一条持续 / 无限效果的运行时状态——句柄、等级、剩余时长、周期计时、
    /// 层数、抑制、快照后的幅度与按层数缩放的 <see cref="ModifierDefinition"/> 列表。状态只由所属容器改动（internal setter）。
    ///
    /// <para><b>层数缩放</b>（<see cref="ScaleMagnitude"/>）：每条修饰器只产出<b>一条</b> <see cref="ModifierDefinition"/>——
    /// Add / PercentAdd → m × S；Multiply → (1 + m)^S − 1；Override → m。明细面板一条一行，与 GAS「聚合后按层数计」等价。</para>
    /// </summary>
    public sealed class ActiveEffect
    {
        private readonly Dictionary<string, float> _setByCaller;
        private readonly List<float> _baseMagnitudes = new List<float>();
        private readonly List<ModifierDefinition> _scaled = new List<ModifierDefinition>();

        public ActiveEffect(int handle, EffectDefinition definition, object target, object source, string sourceTag, int level,
            IReadOnlyDictionary<string, float> setByCaller = null)
        {
            Handle            = handle;
            Definition        = definition ?? throw new ArgumentNullException(nameof(definition));
            Target            = target;
            Source            = source;
            SourceTag         = string.IsNullOrEmpty(sourceTag) ? definition.DefaultSourceTag : sourceTag;
            ModifierSourceTag = SourceTag + "#" + handle;
            Level             = level < 1 ? 1 : level;
            _setByCaller      = setByCaller != null ? new Dictionary<string, float>(setByCaller) : new Dictionary<string, float>();
        }

        /// <summary>容器内唯一句柄（≥ 1，单调递增）。</summary>
        public int Handle { get; }

        /// <summary>效果定义。</summary>
        public EffectDefinition Definition { get; }

        /// <summary>来源标记（请求给定，或 <c>effect:{id}</c>）。</summary>
        public string SourceTag { get; }

        /// <summary>写进产出修饰器的来源标记：<c>{SourceTag}#{Handle}</c>。</summary>
        public string ModifierSourceTag { get; }

        /// <summary>施加者（存档恢复后为 null，宿主可按 <see cref="SourceTag"/> 重绑）。</summary>
        public object Source { get; internal set; }

        /// <summary>目标（容器 Owner）。</summary>
        public object Target { get; }

        /// <summary>等级。</summary>
        public int Level { get; internal set; }

        /// <summary>最近一次求值的总时长；无限 = −1。</summary>
        public float Duration { get; internal set; } = -1f;

        /// <summary>剩余时长；无限 = −1。</summary>
        public float Remaining { get; internal set; } = -1f;

        /// <summary>周期；≤ 0 = 非周期。</summary>
        public float Period { get; internal set; }

        /// <summary>距下次周期结算的时间。</summary>
        public float PeriodTimer { get; internal set; }

        /// <summary>已存活时间。</summary>
        public float Elapsed { get; internal set; }

        /// <summary>层数（≥ 1）。</summary>
        public int Stacks { get; internal set; } = 1;

        /// <summary>是否被抑制（持续标签要求不满足）。</summary>
        public bool IsInhibited { get; internal set; }

        /// <summary>是否仍在容器中（移除后为 false）。</summary>
        public bool IsActive { get; internal set; } = true;

        /// <summary>是否周期效果。</summary>
        public bool IsPeriodic => Period > 0f;

        /// <summary>是否有限时长。</summary>
        public bool HasDuration => Definition.durationPolicy == EDurationPolicy.HasDuration;

        /// <summary>SetByCaller 键值（施加与叠层时合并）。</summary>
        public IReadOnlyDictionary<string, float> SetByCaller => _setByCaller;

        /// <summary>每条修饰器的单层快照幅度（与 <see cref="EffectDefinition.modifiers"/> 同序）；求值失败的条目为 <see cref="float.NaN"/>。</summary>
        public IReadOnlyList<float> BaseMagnitudes => _baseMagnitudes;

        /// <summary>按当前层数缩放后的修饰器（容器持有的实例，勿改动）。</summary>
        public IReadOnlyList<ModifierDefinition> ScaledModifiers => _scaled;

        /// <summary>某条修饰器是否在施加时被跳过（幅度求值失败）。</summary>
        public static bool IsSkipped(float baseMagnitude) => float.IsNaN(baseMagnitude);

        /// <summary>按层数缩放单层幅度。</summary>
        public static float ScaleMagnitude(EModifierOperation operation, float magnitude, int stacks)
        {
            int s = stacks < 1 ? 1 : stacks;
            switch (operation)
            {
                case EModifierOperation.Multiply: return (float)Math.Pow(1.0 + magnitude, s) - 1f;
                case EModifierOperation.Override: return magnitude;
                default:                          return magnitude * s;   // Add / PercentAdd
            }
        }

        // ── 容器内部 ─────────────────────────────────────────────────────────────

        /// <summary>写入单层快照幅度并重建缩放列表。</summary>
        internal void SetBaseMagnitudes(IReadOnlyList<float> values)
        {
            _baseMagnitudes.Clear();
            if (values != null) _baseMagnitudes.AddRange(values);
            RebuildScaledModifiers();
        }

        /// <summary>合并 SetByCaller（新值覆盖旧值）。</summary>
        internal void MergeSetByCaller(IReadOnlyDictionary<string, float> values)
        {
            if (values == null) return;
            foreach (var kv in values) _setByCaller[kv.Key] = kv.Value;
        }

        /// <summary>按 <see cref="Stacks"/> 重建 <see cref="ScaledModifiers"/>。</summary>
        internal void RebuildScaledModifiers()
        {
            _scaled.Clear();
            var mods = Definition.modifiers;
            if (mods == null) return;
            for (int i = 0; i < mods.Count && i < _baseMagnitudes.Count; i++)
            {
                var m = mods[i];
                float b = _baseMagnitudes[i];
                if (m == null || IsSkipped(b)) continue;
                _scaled.Add(new ModifierDefinition(m.attributeId, m.operation, ScaleMagnitude(m.operation, b, Stacks), ModifierSourceTag));
            }
        }
    }
}
