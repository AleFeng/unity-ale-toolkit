namespace Ale.Condition
{
    /// <summary>
    /// 判定上下文：判定器借此读取游戏状态，保证跨端（客户端 / 服务端）一致。
    /// </summary>
    public interface IConditionContext
    {
        /// <summary>主体（如被判定的角色 / 实体）；判定器可选用。</summary>
        object Subject { get; }

        /// <summary>取一个游戏状态服务（如 <see cref="IConditionFlagSource"/> / 领域数据源）；无则返回 null。</summary>
        T GetService<T>() where T : class;
    }
}
