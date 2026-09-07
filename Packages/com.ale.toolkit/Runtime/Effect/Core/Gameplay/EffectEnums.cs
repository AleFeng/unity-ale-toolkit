namespace Ale.Effect
{
    // 本文件的全部枚举：显式赋值、自 0 连续（编辑器绘制器用 enumValueIndex），发布后承诺稳定，防止旧数据错位。

    /// <summary>时长策略（GAS DurationPolicy）。</summary>
    public enum EDurationPolicy
    {
        /// <summary>瞬时：修饰器永久落地（经 Sink 改基础值）+ 执行 onApply，不进入容器、无句柄。</summary>
        Instant = 0,

        /// <summary>有限时长：入容器，按 <c>duration</c> 倒计时，到期按叠加到期策略处理。</summary>
        HasDuration = 1,

        /// <summary>无限：入容器，不倒计时，直到显式移除。</summary>
        Infinite = 2,
    }

    /// <summary>叠加类型（GAS StackingType）。</summary>
    public enum EEffectStackingType
    {
        /// <summary>不叠加：每次施加各自成一个实例。</summary>
        None = 0,

        /// <summary>按来源聚合：同 id 且同来源标记的施加叠到同一实例。</summary>
        AggregateBySource = 1,

        /// <summary>按目标聚合：同 id 的施加不论来源都叠到同一实例。</summary>
        AggregateByTarget = 2,
    }

    /// <summary>叠层成功时是否刷新剩余时长。</summary>
    public enum EStackDurationRefreshPolicy
    {
        RefreshOnSuccessfulApplication = 0,
        NeverRefresh = 1,
    }

    /// <summary>叠层成功时是否重置周期计时。</summary>
    public enum EStackPeriodResetPolicy
    {
        ResetOnSuccessfulApplication = 0,
        NeverReset = 1,
    }

    /// <summary>有限时长效果到期时的叠层处理。</summary>
    public enum EStackExpirationPolicy
    {
        /// <summary>整个实例移除。</summary>
        ClearEntireStack = 0,

        /// <summary>减一层并刷新时长；减到 0 层即移除。</summary>
        RemoveSingleStackAndRefreshDuration = 1,

        /// <summary>只刷新时长、层数不变（直到显式减层 / 移除）。</summary>
        RefreshDuration = 2,
    }

    /// <summary>幅度来源（GAS MagnitudeCalculationType 的子集）。</summary>
    public enum EMagnitudeKind
    {
        /// <summary>可缩放常量：<c>baseValue + perLevel × (level − 1)</c>。</summary>
        Scalable = 0,

        /// <summary>基于属性：<c>coefficient × (属性当前值 + preAdd) + postAdd</c>，属性取自目标或来源（施加时快照）。</summary>
        AttributeBased = 1,

        /// <summary>由调用方按键传入（<c>EffectApplyRequest.SetByCaller</c>）；缺键回退 <c>baseValue</c>。</summary>
        SetByCaller = 2,
    }

    /// <summary>基于属性的幅度从谁身上取值。</summary>
    public enum EMagnitudeCaptureFrom
    {
        Target = 0,
        Source = 1,
    }

    /// <summary>线索（Cue）事件：随效果生命周期通知表现层。</summary>
    public enum EEffectCueEvent
    {
        /// <summary>效果实例建立（含叠层）。</summary>
        Applied = 0,

        /// <summary>瞬时效果落地 / 周期结算。</summary>
        Executed = 1,

        /// <summary>效果实例移除。</summary>
        Removed = 2,
    }

    /// <summary>一次施加请求的结果分类（不序列化，可稀疏）。&lt; 10 为成功。</summary>
    public enum EEffectApplyOutcome
    {
        /// <summary>已施加（瞬时落地 / 新实例建立）。</summary>
        Applied = 0,

        /// <summary>叠到既有实例并加了一层。</summary>
        Stacked = 1,

        /// <summary>叠到既有实例但已封顶，仅按策略刷新。</summary>
        Refreshed = 2,

        /// <summary>被目标身上激活效果的免疫标签阻断。</summary>
        BlockedByImmunity = 10,

        /// <summary>不满足施加标签要求。</summary>
        BlockedByTagRequirements = 11,

        /// <summary>不满足施加条件（Condition System）。</summary>
        BlockedByCondition = 12,

        /// <summary>概率未命中。</summary>
        BlockedByChance = 13,

        /// <summary>定义无效（为空 / 有限时长但时长不可求值或 ≤ 0）。</summary>
        Invalid = 20,

        /// <summary>按 id 找不到效果定义。</summary>
        DefinitionNotFound = 21,

        /// <summary>找不到目标的效果容器。</summary>
        NoContainer = 22,
    }

    /// <summary><see cref="EEffectApplyOutcome"/> 的便捷判断。</summary>
    public static class EffectApplyOutcomeExtensions
    {
        /// <summary>是否成功（Applied / Stacked / Refreshed）。</summary>
        public static bool IsSuccess(this EEffectApplyOutcome outcome) => (int)outcome < 10;
    }
}
