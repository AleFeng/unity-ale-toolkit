namespace Ale.Effect
{
    /// <summary>
    /// 效果参数的值类型（<c>AttributeValue</c> 的极简化：仅标量 5 型，可经 <see cref="EffectParam.isArray"/> 变数组）。
    /// 与 <c>Ale.Condition.ConditionParamType</c> 同构、各自平行；显式赋值且承诺稳定。
    /// </summary>
    public enum EffectParamType
    {
        String = 0,
        Int    = 1,
        Float  = 2,
        Bool   = 3,
        Enum   = 4,
    }
}
