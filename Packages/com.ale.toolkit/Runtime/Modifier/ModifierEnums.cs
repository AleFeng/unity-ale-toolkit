namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 修饰器对目标数值的运算方式。求值时按固定分组顺序结算（见 <see cref="ModifierStackEvaluator"/>）：
    /// 先全部 <see cref="Add"/> → 再 <see cref="PercentAdd"/>（求和后一次性乘）→ 再逐个 <see cref="Multiply"/>
    /// → 最后 <see cref="Override"/>。
    /// <para>枚举值显式赋值且承诺稳定，防止旧数据损坏。</para>
    /// </summary>
    public enum EModifierOperation
    {
        /// <summary>加法：<c>running += magnitude</c>。</summary>
        Add = 0,

        /// <summary>百分比加法：所有该类 magnitude 求和为 P，整组一次性 <c>running *= (1 + P)</c>（magnitude 0.5 = +50%）。</summary>
        PercentAdd = 1,

        /// <summary>乘法：逐个 <c>running *= (1 + magnitude)</c>（与 <see cref="PercentAdd"/> 的区别是不求和、按传入顺序连乘）。</summary>
        Multiply = 2,

        /// <summary>覆盖：<c>running = magnitude</c>（存在多个时最后一个生效）。</summary>
        Override = 3,
    }

    /// <summary>
    /// 修饰器的时效。<see cref="ModifierStackEvaluator"/> 不解释此字段——某修饰器是否已到期、
    /// 是否应参与本次求值，由运行时在把修饰器交给求值器之前决定；此处仅承载配置数据。
    /// </summary>
    public enum EModifierDuration
    {
        /// <summary>瞬时：立即结算一次（运行时语义，常用于直接改基础值的场景）。</summary>
        Instant = 0,

        /// <summary>限时：存活 <see cref="ModifierDefinition.durationDays"/> 天后由运行时移除。</summary>
        Timed = 1,

        /// <summary>永久：不自动移除。</summary>
        Permanent = 2,
    }

    /// <summary>
    /// 同源修饰器再次施加时的叠加规则。同 <see cref="EModifierDuration"/>，<see cref="ModifierStackEvaluator"/>
    /// 不解释此字段；由运行时在构建「当前生效修饰器列表」时应用。
    /// </summary>
    public enum EStackRule
    {
        /// <summary>刷新：不叠层，仅刷新时长。</summary>
        Refresh = 0,

        /// <summary>叠加：新增一层（受 <see cref="ModifierDefinition.stackLimit"/> 限制）。</summary>
        Add = 1,

        /// <summary>每累计 X 层触发一次。</summary>
        EveryXStacks = 2,

        /// <summary>达到最大层数时触发。</summary>
        OnMaxStacks = 3,
    }
}
