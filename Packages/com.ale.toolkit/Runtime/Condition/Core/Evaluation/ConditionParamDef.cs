namespace Ale.Condition
{
    /// <summary>
    /// 判定器参数的 schema（供编辑器动态参数区与参数同步）。由判定器在代码中声明，不进入序列化。
    /// </summary>
    public sealed class ConditionParamDef
    {
        /// <summary>参数标识（与 <see cref="ConditionParam.id"/> 对应）。</summary>
        public readonly string id;

        /// <summary>值类型。</summary>
        public readonly ConditionParamType type;

        /// <summary>是否数组。</summary>
        public readonly bool isArray;

        /// <summary>显示标签（编辑器；默认取 <see cref="id"/>）。</summary>
        public readonly string label;

        /// <summary>Enum 类型引用（当 <see cref="type"/> 为 Enum；供编辑器下拉）。</summary>
        public readonly string enumTypeRef;

        public ConditionParamDef(string id, ConditionParamType type, bool isArray = false,
            string label = null, string enumTypeRef = null)
        {
            this.id          = id;
            this.type        = type;
            this.isArray     = isArray;
            this.label       = string.IsNullOrEmpty(label) ? id : label;
            this.enumTypeRef = enumTypeRef;
        }
    }
}
