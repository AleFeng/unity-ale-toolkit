using Newtonsoft.Json;

namespace Ale.Condition
{
    /// <summary>
    /// 条件表达式的 JSON 序列化（Newtonsoft）。模型为纯 POCO（公开字段、无 Newtonsoft 特性），
    /// 因此可换用其它序列化器；服务端不被 Newtonsoft 绑死。
    /// </summary>
    public static class ConditionJson
    {
        /// <summary>序列化为 JSON（<paramref name="pretty"/> 缩进）。</summary>
        public static string ToJson(ConditionExpression expr, bool pretty = true)
            => JsonConvert.SerializeObject(expr, pretty ? Formatting.Indented : Formatting.None);

        /// <summary>从 JSON 反序列化（空串返回一个空表达式）。</summary>
        public static ConditionExpression FromJson(string json)
            => string.IsNullOrEmpty(json)
                ? new ConditionExpression()
                : JsonConvert.DeserializeObject<ConditionExpression>(json);
    }
}
