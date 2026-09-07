using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 一次施加请求（GAS <c>GameplayEffectSpec</c> 的输入侧）：定义（或 id）、等级、来源 / 目标、来源标记、SetByCaller 键值。
    /// 非序列化模型，允许字典。
    /// </summary>
    public sealed class EffectApplyRequest
    {
        /// <summary>效果定义；为 null 时按 <see cref="EffectId"/> 经上下文的 <see cref="IEffectDefinitionSource"/> / 全局注册表解析。</summary>
        public EffectDefinition Definition;

        /// <summary>效果 id（<see cref="Definition"/> 为 null 时用于解析）。</summary>
        public string EffectId;

        /// <summary>等级（&lt; 1 按 1）。</summary>
        public int Level = 1;

        /// <summary>施加者（可空；AttributeBased 幅度的 Source 捕获、执行器读取来源用）。</summary>
        public object Source;

        /// <summary>目标（容器施加时会覆盖为容器的 Owner）。</summary>
        public object Target;

        /// <summary>来源标记：叠加键（按来源聚合时）与修饰器来源前缀；空 → <c>effect:{id}</c>。</summary>
        public string SourceTag;

        /// <summary>SetByCaller 键值（可空）。</summary>
        public Dictionary<string, float> SetByCaller;

        public EffectApplyRequest()
        {
        }

        public EffectApplyRequest(EffectDefinition definition, int level = 1)
        {
            Definition = definition;
            EffectId   = definition?.id;
            Level      = level;
        }

        public EffectApplyRequest(string effectId, int level = 1)
        {
            EffectId = effectId;
            Level    = level;
        }

        /// <summary>链式设置一个 SetByCaller 键值。</summary>
        public EffectApplyRequest WithSetByCaller(string key, float value)
        {
            if (string.IsNullOrEmpty(key)) return this;
            SetByCaller ??= new Dictionary<string, float>();
            SetByCaller[key] = value;
            return this;
        }

        /// <summary>解析后的效果 id（定义优先）。</summary>
        public string ResolvedEffectId => Definition != null ? Definition.id : EffectId;

        /// <summary>解析后的来源标记：显式值，或 <c>effect:{id}</c>。</summary>
        public string ResolveSourceTag()
            => string.IsNullOrEmpty(SourceTag) ? EffectDefinition.SourceTagPrefix + ResolvedEffectId : SourceTag;
    }
}
