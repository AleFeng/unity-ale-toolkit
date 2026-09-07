using System;

namespace Ale.Effect
{
    /// <summary>
    /// 效果定义 <see cref="EffectDefinition.executions"/> 使用的内置阶段常量（<see cref="EffectGroup.phase"/> 的取值）。
    /// 容器在对应生命周期时刻以该阶段执行 <see cref="EffectRunner.Run"/>。
    ///
    /// <para>⚠️ <see cref="EffectRunner"/> 把<b>空 phase 当通配</b>：若定义里保留空阶段组，它会在每个阶段（每次周期、移除…）重复执行。
    /// 因此 <see cref="EffectDefinition.Normalize"/> 会把空 phase 改写为 <see cref="OnApply"/>。宿主自定义阶段（非本表所列）合法，
    /// 由宿主经容器的 <c>RunPhase</c> 触发。</para>
    /// </summary>
    public static class EffectPhases
    {
        /// <summary>新实例建立（瞬时效果亦然）。</summary>
        public const string OnApply = "onApply";

        /// <summary>叠到既有实例（此时不跑 <see cref="OnApply"/>）。</summary>
        public const string OnStack = "onStack";

        /// <summary>每次周期结算。</summary>
        public const string OnPeriod = "onPeriod";

        /// <summary>自然到期（随后紧接 <see cref="OnRemove"/>）。</summary>
        public const string OnExpire = "onExpire";

        /// <summary>任何移除（到期 / 显式 / 被标签移除）。</summary>
        public const string OnRemove = "onRemove";

        /// <summary>全部内置阶段（编辑器下拉用）。</summary>
        public static readonly string[] All = { OnApply, OnStack, OnPeriod, OnExpire, OnRemove };

        /// <summary>是否为内置阶段。</summary>
        public static bool IsBuiltIn(string phase) => !string.IsNullOrEmpty(phase) && Array.IndexOf(All, phase) >= 0;
    }
}
