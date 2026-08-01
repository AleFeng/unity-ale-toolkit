using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>使用方接线：对某数值 <c>id</c> 增量（<c>Ale.Condition.IConditionNumberSource</c> 的写侧对偶）。</summary>
    public interface IEffectNumberSink
    {
        void AddNumber(string id, double delta);
    }

    /// <summary>
    /// 内置执行器：对主体的数值 <c>id</c> 增加 <c>delta</c>（经上下文的 <see cref="IEffectNumberSink"/> 服务）。
    /// 与条件 <c>Condition.NumberCompare</c> 成读写对偶。键 <c>Effect.AdjustNumber</c>。
    /// </summary>
    [EffectExecutor("Effect.AdjustNumber")]
    public sealed class AdjustNumberEffect : IEffectExecutor
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("id",    EffectParamType.String, false, "数值ID"),
            new EffectParamDef("delta", EffectParamType.Float,  false, "增量"),
        };

        public string Key => "Effect.AdjustNumber";
        public string DisplayName => "调整数值";
        public string Category => "Effect";
        public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
        {
            var sink = ctx?.GetService<IEffectNumberSink>();
            if (sink == null) return EffectResult.Failed("缺少 IEffectNumberSink 服务");
            string id = parameters.Find("id")?.GetString();
            if (string.IsNullOrEmpty(id)) return EffectResult.Failed("id 为空");
            double delta = parameters.Find("delta")?.GetFloat() ?? 0d;
            sink.AddNumber(id, delta);
            return EffectResult.Applied;
        }
    }
}
