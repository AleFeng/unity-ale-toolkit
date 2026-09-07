using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 容器驱动执行 / 条件 / 线索时包在宿主上下文外面的一层：<see cref="Subject"/> 固定为目标（容器 Owner），
    /// 自身回答 <see cref="IEffectExecutionInfo"/> 的服务请求，其余服务转发给宿主上下文。
    /// 既保证施加条件 / 门控的主体是目标，又让执行器拿到定义 / 实例 / 来源 / 等级 / 阶段（对应 GAS 执行拿到 Spec）。
    /// </summary>
    internal sealed class EffectExecutionContext : IEffectContext, IEffectExecutionInfo
    {
        private readonly IEffectContext _inner;

        public EffectExecutionContext(IEffectContext inner, EffectDefinition definition, ActiveEffect effect,
            object source, object target, int level, string phase, IReadOnlyDictionary<string, float> setByCaller)
        {
            _inner      = inner;
            Definition  = definition;
            Effect      = effect;
            Source      = source;
            Target      = target;
            Level       = level < 1 ? 1 : level;
            Phase       = phase;
            SetByCaller = setByCaller;
        }

        public object Subject => Target;

        public T GetService<T>() where T : class
        {
            if (typeof(T) == typeof(IEffectExecutionInfo)) return (T)(object)this;
            return _inner?.GetService<T>();
        }

        public EffectDefinition Definition { get; }
        public ActiveEffect Effect { get; }
        public object Source { get; }
        public object Target { get; }
        public int Level { get; }
        public string Phase { get; }
        public IReadOnlyDictionary<string, float> SetByCaller { get; }
    }
}
