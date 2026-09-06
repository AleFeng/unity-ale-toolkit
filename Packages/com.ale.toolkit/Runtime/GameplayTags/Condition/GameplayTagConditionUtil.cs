using Ale.Condition;

namespace Ale.GameplayTags
{
    /// <summary>条件判定器取「主体标签容器」的统一解析。</summary>
    public static class GameplayTagConditionUtil
    {
        /// <summary>
        /// 解析顺序：上下文的 <see cref="IGameplayTagSource"/> 服务（按 <c>ctx.Subject</c> 查）→ <c>ctx.Subject as IGameplayTagOwner</c> → null。
        /// </summary>
        public static GameplayTagCountContainer ResolveOwnedTags(IConditionContext ctx)
        {
            if (ctx == null) return null;
            var owned = ctx.GetService<IGameplayTagSource>()?.GetOwnedTags(ctx.Subject);
            if (owned != null) return owned;
            return (ctx.Subject as IGameplayTagOwner)?.OwnedTags;
        }
    }
}
