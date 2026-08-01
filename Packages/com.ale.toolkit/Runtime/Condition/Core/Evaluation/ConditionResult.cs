using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>一次条件求值的结果。</summary>
    public readonly struct ConditionResult
    {
        /// <summary>是否通过。</summary>
        public readonly bool Passed;

        /// <summary>未满足的判定器键（短路模式约 1 个，collectAll 模式尽量全量）。</summary>
        public readonly IReadOnlyList<string> FailedKeys;

        public ConditionResult(bool passed, IReadOnlyList<string> failedKeys)
        {
            Passed     = passed;
            FailedKeys = failedKeys ?? Array.Empty<string>();
        }

        /// <summary>通过（无失败键）。</summary>
        public static ConditionResult Pass => new ConditionResult(true, Array.Empty<string>());

        /// <summary>不通过（带失败键集）。</summary>
        public static ConditionResult Fail(IReadOnlyList<string> failedKeys) => new ConditionResult(false, failedKeys);

        /// <summary>不通过（单个失败键）。</summary>
        public static ConditionResult Fail(string key) => new ConditionResult(false, new[] { key });
    }
}
