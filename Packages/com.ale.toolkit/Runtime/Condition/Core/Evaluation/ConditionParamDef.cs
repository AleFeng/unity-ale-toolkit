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

        /// <summary>
        /// 可选：固定选项标签。当非空且参数为 <see cref="ConditionParamType.Int"/> / <see cref="ConditionParamType.Enum"/> 标量时，
        /// 编辑器渲染为下拉，存所选<b>索引</b>（0 起）。用于「比较方式」这类固定枚举选项。
        /// </summary>
        public readonly string[] choices;

        /// <summary>
        /// 可选：<b>候选目录引用</b>（如 <c>"Chronicle.Trait"</c>）。判定器以此声明「这个字符串参数装的是哪一类 id」，
        /// 宿主经编辑器的 <c>ConditionDrawerHooks.RegisterProvider</c> 为该目录供给候选，绘制器便把裸文本框换成分组下拉。
        /// <para>与 <see cref="choices"/> 的区别：<see cref="choices"/> 是判定器代码里写死的固定选项（存<b>索引</b>），
        /// 本字段指向<b>工程数据</b>里的动态候选（存 id 本身）。为空 = 保持文本框，行为与 1.11.0 一致。</para>
        /// </summary>
        public readonly string catalogRef;

        public ConditionParamDef(string id, ConditionParamType type, bool isArray = false,
            string label = null, string enumTypeRef = null, string[] choices = null, string catalogRef = null)
        {
            this.id          = id;
            this.type        = type;
            this.isArray     = isArray;
            this.label       = string.IsNullOrEmpty(label) ? id : label;
            this.enumTypeRef = enumTypeRef;
            this.choices     = choices;
            this.catalogRef  = catalogRef;
        }
    }
}
