namespace Ale.Effect
{
    /// <summary>单条效果执行的结果类别。</summary>
    public enum EEffectOutcome
    {
        /// <summary>已施加。</summary>
        Applied = 0,

        /// <summary>被跳过（如 gate 条件不满足，或执行器判定无需施加）。</summary>
        Skipped = 1,

        /// <summary>执行失败（如缺少所需写侧服务、键未注册、参数非法）。</summary>
        Failed  = 2,
    }

    /// <summary>
    /// 一次效果执行的轻量结果（一次性触发，不含句柄）。持久 / 可撤效果由执行器转而压入现有 Modifier / Trait 系统实现。
    /// </summary>
    public readonly struct EffectResult
    {
        /// <summary>结果类别。</summary>
        public readonly EEffectOutcome Outcome;

        /// <summary>备注（可选：跳过 / 失败原因）。</summary>
        public readonly string Note;

        public EffectResult(EEffectOutcome outcome, string note = null)
        {
            Outcome = outcome;
            Note    = note;
        }

        /// <summary>是否已施加。</summary>
        public bool IsApplied => Outcome == EEffectOutcome.Applied;

        /// <summary>已施加。</summary>
        public static EffectResult Applied => new EffectResult(EEffectOutcome.Applied);

        /// <summary>被跳过（可带原因）。</summary>
        public static EffectResult Skipped(string note = null) => new EffectResult(EEffectOutcome.Skipped, note);

        /// <summary>执行失败（可带原因）。</summary>
        public static EffectResult Failed(string note = null) => new EffectResult(EEffectOutcome.Failed, note);
    }
}
