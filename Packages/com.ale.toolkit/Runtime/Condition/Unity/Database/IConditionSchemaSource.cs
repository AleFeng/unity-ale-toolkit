namespace Ale.Condition
{
    /// <summary>条件模板（自定义属性 schema）来源：<see cref="ConditionDatabase"/> 与 <see cref="ConditionDataManager"/> 实现，供 <see cref="ConditionEntry.RebuildAttributes"/> 反查模板。</summary>
    public interface IConditionSchemaSource
    {
        /// <summary>按模板名取条件模板，未找到返回 null。</summary>
        ConditionTemplate GetTemplate(string name);
    }
}
