namespace Ale.Condition
{
    /// <summary>条件项之间、条件组之间的逻辑连接方式。</summary>
    public enum ConditionLogicOp
    {
        /// <summary>与：全部成立才成立。</summary>
        And = 0,

        /// <summary>或：任一成立即成立。</summary>
        Or = 1,
    }
}
