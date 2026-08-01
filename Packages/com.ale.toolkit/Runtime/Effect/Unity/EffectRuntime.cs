using System.Collections.Generic;
using UnityEngine;

namespace Ale.Effect
{
    /// <summary>
    /// 运行时桥：游戏启动时把纯 C# 的 <see cref="EffectRegistry.Default"/> 填满——反射扫描所有程序集里
    /// 带 <see cref="EffectExecutorAttribute"/> 的执行器，并把缺失键告警接到 <see cref="Debug"/>（去重）。
    /// </summary>
    public static class EffectRuntime
    {
        private static readonly HashSet<string> _warned = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            _warned.Clear();
            var reg = EffectRegistry.Default;
            reg.AutoRegisterFromAssemblies();
            reg.MissingKeyWarning = key =>
            {
                if (_warned.Add(key))
                    Debug.LogWarning($"[EffectSystem] 未注册的执行器键：{key}");
            };
        }

        /// <summary>向默认注册表注册一个执行器（便捷入口）。</summary>
        public static void Register(IEffectExecutor executor) => EffectRegistry.Default.Register(executor);
    }
}
