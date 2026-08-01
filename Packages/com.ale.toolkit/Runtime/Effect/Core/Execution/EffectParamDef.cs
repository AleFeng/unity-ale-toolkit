namespace Ale.Effect
{
    /// <summary>
    /// 执行器参数的 schema（供编辑器动态参数区与参数同步）。由执行器在代码中声明，不进入序列化。
    /// 与 <c>Ale.Condition.ConditionParamDef</c> 同构、各自平行。
    /// </summary>
    public sealed class EffectParamDef
    {
        /// <summary>参数标识（与 <see cref="EffectParam.id"/> 对应）。</summary>
        public readonly string id;

        /// <summary>值类型。</summary>
        public readonly EffectParamType type;

        /// <summary>是否数组。</summary>
        public readonly bool isArray;

        /// <summary>显示标签（编辑器；默认取 <see cref="id"/>）。</summary>
        public readonly string label;

        /// <summary>Enum 类型引用（当 <see cref="type"/> 为 Enum；供编辑器下拉）。</summary>
        public readonly string enumTypeRef;

        /// <summary>
        /// 可选：固定选项标签。当非空且参数为 <see cref="EffectParamType.Int"/> / <see cref="EffectParamType.Enum"/> 标量时，
        /// 编辑器渲染为下拉，存所选<b>索引</b>（0 起）。用于「目标选择（随机/最近/最远）」这类固定枚举选项。
        /// </summary>
        public readonly string[] choices;

        public EffectParamDef(string id, EffectParamType type, bool isArray = false,
            string label = null, string enumTypeRef = null, string[] choices = null)
        {
            this.id          = id;
            this.type        = type;
            this.isArray     = isArray;
            this.label       = string.IsNullOrEmpty(label) ? id : label;
            this.enumTypeRef = enumTypeRef;
            this.choices     = choices;
        }
    }
}
