namespace Ale.Effect
{
    /// <summary>效果模板（自定义属性 schema）来源：<see cref="EffectDatabase"/> 与 <see cref="EffectDataManager"/> 实现，供 <see cref="EffectEntry.RebuildAttributes"/> 反查模板。</summary>
    public interface IEffectSchemaSource
    {
        /// <summary>按模板名取效果模板，未找到返回 null。</summary>
        EffectTemplate GetTemplate(string name);
    }
}
