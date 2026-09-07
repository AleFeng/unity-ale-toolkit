using System;
using Ale.Toolkit.Runtime;

namespace Ale.Effect
{
    /// <summary>
    /// 效果模板（蓝图）：名称 / 色点 / 自定义属性字段 schema（来自 <see cref="ConfigTemplateBase"/>）+ 默认效果定义预设。
    /// 从模板创建 <see cref="EffectEntry"/> 时深拷贝 <see cref="defaultDefinition"/>，并按 schema 建立自定义属性值；
    /// 编辑器中列以模板作过滤维。
    ///
    /// <para>⚠ <see cref="defaultDefinition"/> 必须是模板的<b>直接字段</b>：数据库 → 模板 → 定义 → 执行表达式 → 组 → 项 → 条件 → 组 → 项 → 参数
    /// 已达 Unity 序列化深度上限，不可再多包一层。</para>
    /// </summary>
    [Serializable]
    public class EffectTemplate : ConfigTemplateBase
    {
        /// <summary>默认效果定义（从模板创建效果时深拷贝为预设；id / 显示名由效果条目同步，此处留空即可）。</summary>
        public EffectDefinition defaultDefinition = new EffectDefinition();

        public EffectTemplate()
        {
        }

        public EffectTemplate(string name) : base(name)
        {
        }

        /// <summary>归一（幂等）：补 null、定义归一。</summary>
        public void Normalize()
        {
            defaultDefinition ??= new EffectDefinition();
            defaultDefinition.Normalize();
        }

        /// <summary>深拷贝（名称 / 色点 / schema / 默认定义）。</summary>
        public EffectTemplate Clone()
        {
            var clone = new EffectTemplate();
            CopyTo(clone);
            clone.defaultDefinition = defaultDefinition != null ? defaultDefinition.Clone() : new EffectDefinition();
            return clone;
        }
    }
}
