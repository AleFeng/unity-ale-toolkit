using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>单条效果项的执行结果（键 + 结果），用于运行报告逐项回看。</summary>
    public readonly struct EffectItemOutcome
    {
        public readonly string Key;
        public readonly EffectResult Result;

        public EffectItemOutcome(string key, EffectResult result)
        {
            Key    = key;
            Result = result;
        }
    }

    /// <summary>
    /// 一次 <see cref="EffectRunner.Run"/> 的聚合报告：各类别计数 + 逐项结果（<c>ConditionResult</c> 的对偶，写侧）。
    /// </summary>
    public readonly struct EffectRunReport
    {
        /// <summary>已施加数。</summary>
        public readonly int Applied;

        /// <summary>被跳过数（gate 不满足 / 执行器判定跳过）。</summary>
        public readonly int Skipped;

        /// <summary>失败数（缺服务 / 键未注册 / 空键）。</summary>
        public readonly int Failed;

        /// <summary>逐项结果（按执行顺序）。</summary>
        public readonly IReadOnlyList<EffectItemOutcome> Items;

        public EffectRunReport(int applied, int skipped, int failed, IReadOnlyList<EffectItemOutcome> items)
        {
            Applied = applied;
            Skipped = skipped;
            Failed  = failed;
            Items   = items ?? Array.Empty<EffectItemOutcome>();
        }

        /// <summary>处理的效果项总数。</summary>
        public int Total => Applied + Skipped + Failed;

        /// <summary>空报告（未处理任何效果项）。</summary>
        public static EffectRunReport Empty => new EffectRunReport(0, 0, 0, Array.Empty<EffectItemOutcome>());
    }
}
