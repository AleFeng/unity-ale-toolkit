using Ale.Condition;

namespace Ale.Effect
{
    /// <summary>
    /// 通用效果上下文：<see cref="ConditionContext"/> 的服务袋 + 主体，同时满足 <see cref="IEffectContext"/>——
    /// 同一上下文既供施加条件 / 门控读服务，又供执行器与容器取写侧 Sink。用法与 <see cref="ConditionContext"/> 相同
    /// （按接口登记、按接口取用；刻意不提供全局默认实例）。
    /// </summary>
    public class EffectContext : ConditionContext, IEffectContext
    {
    }

    /// <summary>
    /// 带主体的一次性效果上下文包装：<see cref="Subject"/> 换成指定值，服务解析转发给内层上下文（镜像 <see cref="SubjectConditionContext"/>）。
    /// </summary>
    public sealed class SubjectEffectContext : IEffectContext
    {
        private readonly IConditionContext _inner;

        /// <param name="inner">提供服务解析的内层上下文；可为 null（则一切服务解析返回 null）。</param>
        /// <param name="subject">本次的主体。</param>
        public SubjectEffectContext(IConditionContext inner, object subject)
        {
            _inner  = inner;
            Subject = subject;
        }

        /// <summary>本次的主体。</summary>
        public object Subject { get; }

        /// <summary>转发给内层上下文；内层为 null 时返回 null。</summary>
        public T GetService<T>() where T : class => _inner?.GetService<T>();
    }
}
