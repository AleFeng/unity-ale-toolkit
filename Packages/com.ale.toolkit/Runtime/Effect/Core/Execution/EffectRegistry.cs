using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 执行器注册表（纯 C#，引擎无关）：键 → 执行器。可手动 <see cref="Register"/>，
    /// 或 <see cref="AutoRegisterFromAssemblies"/> 反射扫描 <see cref="EffectExecutorAttribute"/> 自动注册。
    /// 与 <c>Ale.Condition.ConditionRegistry</c> 同构。
    /// </summary>
    public sealed class EffectRegistry
    {
        private readonly Dictionary<string, IEffectExecutor> _map = new Dictionary<string, IEffectExecutor>();

        /// <summary>是否已经跑过一次自动注册（供 <see cref="EnsureAutoRegistered"/> 判重）。</summary>
        private bool _autoRegistered;

        /// <summary>共享默认实例（运行时由 Unity 桥在启动时 AutoRegister；服务端可手动填充）。</summary>
        public static EffectRegistry Default { get; } = new EffectRegistry();

        /// <summary>缺失键回调（可选；不硬依赖任何日志设施）。</summary>
        public Action<string> MissingKeyWarning;

        /// <summary>已注册执行器数。</summary>
        public int Count => _map.Count;

        /// <summary>全部已注册执行器。</summary>
        public IEnumerable<IEffectExecutor> All => _map.Values;

        /// <summary>注册（按 <see cref="IEffectExecutor.Key"/>；键空则忽略；同键覆盖）。</summary>
        public void Register(IEffectExecutor executor)
        {
            if (executor == null || string.IsNullOrEmpty(executor.Key)) return;
            _map[executor.Key] = executor;
        }

        /// <summary>注销指定键。</summary>
        public bool Unregister(string key) => key != null && _map.Remove(key);

        /// <summary>按键取执行器。</summary>
        public bool TryGet(string key, out IEffectExecutor executor)
        {
            if (string.IsNullOrEmpty(key)) { executor = null; return false; }
            return _map.TryGetValue(key, out executor);
        }

        /// <summary>
        /// 清空。
        /// <para>⚠️ 一并复位「已自动注册」标志，否则 <see cref="EnsureAutoRegistered"/> 在清空之后会
        /// 静默变成空操作，注册表永久为空。</para>
        /// </summary>
        public void Clear()
        {
            _map.Clear();
            _autoRegistered = false;
        }

        /// <summary>触发缺失键回调（运行器内部用）。</summary>
        internal void NotifyMissing(string key) => MissingKeyWarning?.Invoke(key);

        /// <summary>
        /// 幂等的自动注册兜底：首次调用等价于 <see cref="AutoRegisterFromAssemblies"/>，此后直接返回 false。
        /// 返回本次是否真的执行了扫描。与 <c>ConditionRegistry.EnsureAutoRegistered</c> 同款：
        /// Unity 侧的 <c>EffectRuntime</c> 只在运行时启动阶段填表，编辑器工具在非播放态执行 / 预览时注册表是空的，宿主据此兜底一次即可。
        /// <para><see cref="Clear"/> 会复位标志，因此「清空 → 再 Ensure」能正常重扫。</para>
        /// </summary>
        public bool EnsureAutoRegistered()
        {
            if (_autoRegistered) return false;
            AutoRegisterFromAssemblies();
            return true;
        }

        /// <summary>
        /// 反射扫描当前 AppDomain 所有程序集，实例化并注册：带 <see cref="EffectExecutorAttribute"/>、
        /// 实现 <see cref="IEffectExecutor"/>、且有公开无参构造的具体类。返回「新增」注册数（覆盖不计）。
        /// 纯 <see cref="System.Reflection"/>，引擎无关。
        /// <para>每次调用都全量重扫；只想兜底一次用 <see cref="EnsureAutoRegistered"/>。</para>
        /// </summary>
        public int AutoRegisterFromAssemblies()
        {
            _autoRegistered = true;
            int added = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; } // 某些程序集 GetTypes 会抛（缺依赖）；跳过

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsInterface) continue;
                    if (!typeof(IEffectExecutor).IsAssignableFrom(t)) continue;
                    if (Attribute.GetCustomAttribute(t, typeof(EffectExecutorAttribute)) == null) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                    var instance = Activator.CreateInstance(t) as IEffectExecutor;
                    if (instance == null || string.IsNullOrEmpty(instance.Key)) continue;

                    if (!_map.ContainsKey(instance.Key)) added++;
                    _map[instance.Key] = instance;
                }
            }
            return added;
        }
    }
}
