using Ale.Condition;

namespace Ale.Effect
{
    /// <summary>
    /// 效果执行上下文：执行器借此拿到<b>主体</b>与<b>写侧服务</b>（Sink：授予/移除特质、压/弹修饰器、置标志…）来施加突变。
    /// <para>继承 <see cref="IConditionContext"/>（<c>Subject</c> + <c>GetService&lt;T&gt;()</c>），故同一个上下文既能供
    /// 效果项的 gate 条件<b>读</b>服务，又能供效果执行器<b>写</b>服务——运行时（角色实例 / ECS 世界）提供一个即可。</para>
    /// </summary>
    public interface IEffectContext : IConditionContext
    {
    }
}
