using Newtonsoft.Json;

namespace Ale.Effect
{
    /// <summary>
    /// 效果表达式的 JSON 序列化（Newtonsoft）。模型为纯 POCO（公开字段、无 Newtonsoft 特性），
    /// 因此可换用其它序列化器；服务端不被 Newtonsoft 绑死。内嵌的 gate（<c>ConditionExpression</c>）随对象图一并往返。
    /// </summary>
    public static class EffectJson
    {
        /// <summary>序列化为 JSON（<paramref name="pretty"/> 缩进）。</summary>
        public static string ToJson(EffectExpression expr, bool pretty = true)
            => JsonConvert.SerializeObject(expr, pretty ? Formatting.Indented : Formatting.None);

        /// <summary>从 JSON 反序列化（空串返回一个空表达式）。</summary>
        public static EffectExpression FromJson(string json)
            => string.IsNullOrEmpty(json)
                ? new EffectExpression()
                : JsonConvert.DeserializeObject<EffectExpression>(json);
    }
}
