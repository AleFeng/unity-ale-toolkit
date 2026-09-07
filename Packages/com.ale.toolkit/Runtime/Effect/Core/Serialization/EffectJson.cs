using Newtonsoft.Json;

namespace Ale.Effect
{
    /// <summary>
    /// 效果表达式 / 效果定义的 JSON 序列化（Newtonsoft）。模型为纯 POCO（公开字段、无 Newtonsoft 特性），
    /// 因此可换用其它序列化器；服务端不被 Newtonsoft 绑死。内嵌的 gate / 施加条件（<c>ConditionExpression</c>）与标签容器随对象图一并往返。
    /// </summary>
    public static class EffectJson
    {
        /// <summary>序列化表达式为 JSON（<paramref name="pretty"/> 缩进）。</summary>
        public static string ToJson(EffectExpression expr, bool pretty = true)
            => JsonConvert.SerializeObject(expr, pretty ? Formatting.Indented : Formatting.None);

        /// <summary>从 JSON 反序列化表达式（空串返回一个空表达式）。</summary>
        public static EffectExpression FromJson(string json)
            => string.IsNullOrEmpty(json)
                ? new EffectExpression()
                : JsonConvert.DeserializeObject<EffectExpression>(json);

        /// <summary>序列化效果定义为 JSON。</summary>
        public static string ToJson(EffectDefinition definition, bool pretty = true)
            => JsonConvert.SerializeObject(definition, pretty ? Formatting.Indented : Formatting.None);

        /// <summary>从 JSON 反序列化效果定义（空串返回一个空定义）；反序列化后执行 <see cref="EffectDefinition.Normalize"/> 兜底 null。</summary>
        public static EffectDefinition DefinitionFromJson(string json)
        {
            var def = string.IsNullOrEmpty(json) ? new EffectDefinition() : JsonConvert.DeserializeObject<EffectDefinition>(json);
            def ??= new EffectDefinition();
            def.Normalize();
            return def;
        }
    }
}
