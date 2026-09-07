using System;
using System.Collections.Generic;
using Ale.Modifier;

namespace Ale.Effect
{
    /// <summary>
    /// 效果定义里的一条修饰器（GAS Modifier）：对目标的某个属性以 <see cref="operation"/> 施加 <see cref="magnitude"/>。
    /// 持续 / 无限效果：施加时快照幅度、按层数缩放后作为 <see cref="ModifierDefinition"/> 参与宿主的属性汇流；
    /// 瞬时 / 周期效果：经 <see cref="IEffectAttributeSink"/> 永久落地到基础值。
    /// </summary>
    [Serializable]
    public class EffectModifier
    {
        /// <summary>目标属性 id（由宿主约定，如核心属性 id）。</summary>
        public string attributeId;

        /// <summary>运算方式（Add / PercentAdd / Multiply / Override）。</summary>
        public EModifierOperation operation = EModifierOperation.Add;

        /// <summary>幅度。</summary>
        public EffectMagnitude magnitude = new EffectMagnitude();

        public EffectModifier()
        {
        }

        public EffectModifier(string attributeId, EModifierOperation operation, EffectMagnitude magnitude)
        {
            this.attributeId = attributeId;
            this.operation   = operation;
            this.magnitude   = magnitude ?? new EffectMagnitude();
        }

        /// <summary>便捷：Scalable 常量幅度。</summary>
        public EffectModifier(string attributeId, EModifierOperation operation, float scalable)
            : this(attributeId, operation, EffectMagnitude.Scalable(scalable))
        {
        }

        /// <summary>深拷贝。</summary>
        public EffectModifier Clone() => new EffectModifier(attributeId, operation, magnitude?.Clone());

        /// <summary>校验：attributeId 空 → 错误；幅度自身校验。返回是否无错误。</summary>
        public bool Validate(List<string> errors, string path)
        {
            bool ok = true;
            if (string.IsNullOrWhiteSpace(attributeId))
            {
                errors?.Add($"{path}：attributeId 为空");
                ok = false;
            }
            if (magnitude != null) ok &= magnitude.Validate(errors, path + ".magnitude");
            return ok;
        }
    }
}
