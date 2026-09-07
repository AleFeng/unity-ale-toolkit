namespace Ale.Effect
{
    /// <summary>
    /// 容器来源服务（上下文服务）：按主体解析其 <see cref="EffectContainer"/>——多角色宿主登记一个共享服务，按主体（角色 id / 对象）查表。
    /// <see cref="EffectApplier"/> 与内置的 <c>Effect.ApplyEffect</c> / <c>Effect.RemoveEffectsWithTag</c> 执行器据此找到目标容器。
    /// </summary>
    public interface IEffectContainerSource
    {
        /// <summary>取主体的效果容器；未知主体返回 null。</summary>
        EffectContainer GetContainer(object subject);
    }
}
