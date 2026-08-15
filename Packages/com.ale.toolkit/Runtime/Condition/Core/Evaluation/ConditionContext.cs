using System;
using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>
    /// 通用判定上下文：按类型登记领域服务，供判定器经 <see cref="GetService{T}"/> 取用。
    /// 宿主不必再为「把数据源交给判定器」手写一个上下文类。
    ///
    /// <para><b>刻意不提供全局默认实例。</b>「哪个是默认上下文」是宿主的策略，
    /// 由 toolkit 提供一个全局实例只会和宿主自己的注册表形成两套并列的东西，让人搞不清该往哪注册。
    /// 各宿主自行持有一个静态实例即可。</para>
    ///
    /// <para><b>成员刻意不做 virtual。</b><see cref="GetService{T}"/> 在条件求值的热路径上
    /// （一次链接评估可能调几十次）。需要自定义解析策略的宿主（例如要把服务表放在别处、
    /// 或要按「自定义优先、内置回落」分层的），<b>直接实现 <see cref="IConditionContext"/> 即可</b>，
    /// 不必继承本类。</para>
    /// </summary>
    public class ConditionContext : IConditionContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// 主体（如被判定的角色 / 实体）；判定器可选用。
        /// <para>只判定一次、不想改动共享实例状态时，用 <see cref="SubjectConditionContext"/> 包一层。</para>
        /// </summary>
        public object Subject { get; set; }

        /// <summary>注册一个领域服务。同类型后注册覆盖先注册；传 null 等同于注销。</summary>
        public void RegisterService<T>(T service) where T : class
        {
            if (service == null) { UnregisterService<T>(); return; }
            _services[typeof(T)] = service;
        }

        /// <summary>注销指定类型的领域服务。返回是否确有其项。</summary>
        public bool UnregisterService<T>() where T : class => _services.Remove(typeof(T));

        /// <summary>是否已注册指定类型的领域服务。</summary>
        public bool HasService<T>() where T : class => _services.ContainsKey(typeof(T));

        /// <summary>
        /// 取一个领域服务；未注册返回 null。
        /// <para>⚠️ 按 <c>typeof(T)</c> <b>精确</b>查表，不做可赋值匹配：
        /// 用 <c>RegisterService&lt;IFoo&gt;(impl)</c> 登记的，只能用 <c>GetService&lt;IFoo&gt;()</c> 取到，
        /// <c>GetService&lt;FooImpl&gt;()</c> 取不到。判定器一律按接口取用，登记时也请按接口登记。</para>
        /// </summary>
        public T GetService<T>() where T : class =>
            _services.TryGetValue(typeof(T), out object service) ? service as T : null;

        /// <summary>清空所有服务与主体。</summary>
        public void Clear()
        {
            _services.Clear();
            Subject = null;
        }
    }

    /// <summary>
    /// 带主体的一次性上下文包装：<see cref="Subject"/> 换成指定值，服务解析仍转发给内层上下文。
    ///
    /// <para>用于「判定这个对象是否满足条件」这类场景 —— 不必改动共享上下文的
    /// <see cref="ConditionContext.Subject"/>，因而多处求值（乃至嵌套求值）之间不会互相踩。</para>
    /// </summary>
    public sealed class SubjectConditionContext : IConditionContext
    {
        private readonly IConditionContext _inner;

        /// <param name="inner">提供服务解析的内层上下文；可为 null（则一切服务解析返回 null）。</param>
        /// <param name="subject">本次判定的主体。</param>
        public SubjectConditionContext(IConditionContext inner, object subject)
        {
            _inner  = inner;
            Subject = subject;
        }

        /// <summary>本次判定的主体。</summary>
        public object Subject { get; }

        /// <summary>转发给内层上下文；内层为 null 时返回 null。</summary>
        public T GetService<T>() where T : class => _inner?.GetService<T>();
    }
}
