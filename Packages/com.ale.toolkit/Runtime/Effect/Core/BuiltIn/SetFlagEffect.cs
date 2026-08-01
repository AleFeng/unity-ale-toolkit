using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>使用方接线：写入 / 清除某标记（<c>Ale.Condition.IConditionFlagSource</c> 的写侧对偶）。</summary>
    public interface IEffectFlagSink
    {
        void SetFlag(string flag, bool value);
    }

    /// <summary>
    /// 内置执行器：给主体置 / 清标记 <c>flag</c>（经上下文的 <see cref="IEffectFlagSink"/> 服务）。
    /// 与条件 <c>Condition.HasFlag</c> 成读写对偶。键 <c>Effect.SetFlag</c>。
    /// </summary>
    [EffectExecutor("Effect.SetFlag")]
    public sealed class SetFlagEffect : IEffectExecutor
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("flag",  EffectParamType.String, false, "标记"),
            new EffectParamDef("value", EffectParamType.Bool,   false, "置为"),
        };

        public string Key => "Effect.SetFlag";
        public string DisplayName => "置标记";
        public string Category => "Effect";
        public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
        {
            var sink = ctx?.GetService<IEffectFlagSink>();
            if (sink == null) return EffectResult.Failed("缺少 IEffectFlagSink 服务");
            string flag = parameters.Find("flag")?.GetString();
            if (string.IsNullOrEmpty(flag)) return EffectResult.Failed("flag 为空");
            bool value = parameters.Find("value")?.GetBool() ?? true;
            sink.SetFlag(flag, value);
            return EffectResult.Applied;
        }
    }
}
