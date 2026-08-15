using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 比较符范式：下拉选项 + 索引常量 + 比较函数。供各判定器复用，不必各自抄一份。
    ///
    /// <para><b>⚠️ <see cref="Labels"/> 是通信格式，不是 UI 文案。</b>
    /// 它以字符串形式进入使用方的配置与脚本 —— 例如经 VN Framework 的桥接后，
    /// Dialogue System 的对话条件里写的是 <c>AleCond_Xxx("大于等于", 3)</c>，运行时再按标签反查索引。
    /// 因此：<b>永远不要本地化它</b>（跟着编辑器语言变会让已写好的剧本集体失配），
    /// 也<b>永远不要调整顺序</b>（配置里存的是索引，调序会让已配置的条件集体错位）。</para>
    /// </summary>
    public static class ConditionCompare
    {
        /// <summary>大于。</summary>
        public const int Greater = 0;

        /// <summary>大于等于。</summary>
        public const int GreaterOrEqual = 1;

        /// <summary>等于。</summary>
        public const int Equal = 2;

        /// <summary>小于等于。</summary>
        public const int LessOrEqual = 3;

        /// <summary>小于。</summary>
        public const int Less = 4;

        /// <summary>比较符下拉标签。下标即存储值。⚠️ 顺序与文案发布后冻结，理由见类注释。</summary>
        public static readonly string[] Labels = { "大于", "大于等于", "等于", "小于等于", "小于" };

        /// <summary>比较符参数的默认 id。</summary>
        public const string DefaultParamId = "op";

        /// <summary>
        /// 浮点比较的默认容差。
        ///
        /// <para>取 <c>1e-6</c> 而不是更严的值，是因为常见来源是 <c>float</c> 扩宽成 <c>double</c>：
        /// <c>10.1f</c> 实际是 <c>10.100000381469727</c>，与配置里存的 <c>double 10.1</c> 相差约 <c>3.8e-7</c>。
        /// 容差比这个小，「等于」在浮点属性上就几乎永远判不成立。</para>
        /// </summary>
        public const double DefaultEpsilon = 1e-6;

        /// <summary>
        /// 构造统一的「比较」参数定义（<see cref="ConditionParamType.Int"/> + <see cref="Labels"/> 下拉，存索引）。
        /// 各判定器一律调本方法，杜绝手抄标签数组抄歪。
        /// </summary>
        public static ConditionParamDef CreateOpParam(string label = "比较", string id = DefaultParamId) =>
            new ConditionParamDef(id, ConditionParamType.Int, false, label, null, Labels);

        /// <summary>
        /// 从参数表读比较符。缺省 <see cref="GreaterOrEqual"/>。
        /// </summary>
        public static int ReadOp(IReadOnlyList<ConditionParam> parameters, string id = DefaultParamId) =>
            (int)(parameters.Find(id)?.GetInt() ?? GreaterOrEqual);

        /// <summary>
        /// 按比较符比较两个整数（精确比较，不涉及容差）。
        /// 未知比较符按 <see cref="GreaterOrEqual"/> 处理。
        /// </summary>
        public static bool Compare(long value, long amount, int op)
        {
            switch (op)
            {
                case Greater:        return value >  amount;
                case GreaterOrEqual: return value >= amount;
                case Equal:          return value == amount;
                case LessOrEqual:    return value <= amount;
                case Less:           return value <  amount;
                default:             return value >= amount;
            }
        }

        /// <summary>
        /// 按比较符比较两个浮点数。「等于」用 <paramref name="epsilon"/> 容差。
        /// 未知比较符按 <see cref="GreaterOrEqual"/> 处理。
        /// </summary>
        /// <param name="epsilon">
        /// 「等于」的容差，默认 <see cref="DefaultEpsilon"/>。
        /// 需要更严格的语义时显式传入 —— 但要清楚这会让 float 来源的数值几乎判不出相等。
        /// </param>
        public static bool Compare(double value, double amount, int op, double epsilon = DefaultEpsilon)
        {
            switch (op)
            {
                case Greater:        return value >  amount;
                case GreaterOrEqual: return value >= amount;
                case Equal:          return Math.Abs(value - amount) < epsilon;
                case LessOrEqual:    return value <= amount;
                case Less:           return value <  amount;
                default:             return value >= amount;
            }
        }
    }
}
