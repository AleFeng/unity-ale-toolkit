namespace Ale.Condition
{
    /// <summary>
    /// 条件参数的值类型（<c>AttributeValue</c> 的极简化：仅标量 5 型，可经 <see cref="ConditionParam.isArray"/> 变数组）。
    /// 显式赋值且承诺稳定。
    /// </summary>
    public enum ConditionParamType
    {
        String = 0,
        Int    = 1,
        Float  = 2,
        Bool   = 3,
        Enum   = 4,
    }
}
