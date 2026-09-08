namespace Ale.Condition
{
    /// <summary>
    /// 具名条件来源：按 id 取一份条件表达式（宿主的条件库实现；多来源经 <see cref="ConditionDefinitionRegistry"/> 聚合）。
    /// 与效果侧的 <c>IEffectDefinitionSource</c> 对偶。
    /// </summary>
    public interface IConditionDefinitionSource
    {
        /// <summary>按 id 取条件表达式；未找到返回 null。</summary>
        ConditionExpression GetCondition(string id);
    }
}
