namespace Ale.Effect
{
    /// <summary>一次施加请求的结果：分类、句柄（0 = 无实例：瞬时或被拒）、当前层数、备注。</summary>
    public readonly struct EffectApplyResult
    {
        /// <summary>结果分类。</summary>
        public readonly EEffectApplyOutcome Outcome;

        /// <summary>活动效果句柄（0 = 无实例）。</summary>
        public readonly int Handle;

        /// <summary>施加后的层数（无实例为 0）。</summary>
        public readonly int Stacks;

        /// <summary>备注（阻断原因 / 警告等，可空）。</summary>
        public readonly string Note;

        public EffectApplyResult(EEffectApplyOutcome outcome, int handle = 0, int stacks = 0, string note = null)
        {
            Outcome = outcome;
            Handle  = handle;
            Stacks  = stacks;
            Note    = note;
        }

        /// <summary>是否成功（Applied / Stacked / Refreshed）。</summary>
        public bool IsSuccess => Outcome.IsSuccess();

        /// <summary>被阻断 / 无效的结果。</summary>
        public static EffectApplyResult Blocked(EEffectApplyOutcome outcome, string note = null)
            => new EffectApplyResult(outcome, 0, 0, note);

        public override string ToString()
            => Handle > 0 ? $"{Outcome}(#{Handle} ×{Stacks})" : string.IsNullOrEmpty(Note) ? Outcome.ToString() : $"{Outcome}: {Note}";
    }
}
