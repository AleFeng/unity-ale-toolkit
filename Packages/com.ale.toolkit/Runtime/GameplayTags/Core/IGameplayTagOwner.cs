namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签拥有者：主体自身即可回答「我持有哪些标签」（角色 / 实体对象直接实现）。
    /// 条件判定器 / 效果容器在上下文没有 <see cref="IGameplayTagSource"/> 服务时，回落到 <c>ctx.Subject as IGameplayTagOwner</c>。
    /// </summary>
    public interface IGameplayTagOwner
    {
        /// <summary>当前持有的标签计数容器。</summary>
        GameplayTagCountContainer OwnedTags { get; }
    }

    /// <summary>
    /// 标签来源服务（上下文服务）：按主体解析其标签容器——多角色宿主只需登记一个共享服务，
    /// 由它按 <paramref name="subject"/>（如角色 id / 角色对象）查表。<paramref name="subject"/> 为 null 时取上下文默认主体。
    /// </summary>
    public interface IGameplayTagSource
    {
        /// <summary>取主体当前持有的标签容器；未知主体返回 null。</summary>
        GameplayTagCountContainer GetOwnedTags(object subject);
    }
}
