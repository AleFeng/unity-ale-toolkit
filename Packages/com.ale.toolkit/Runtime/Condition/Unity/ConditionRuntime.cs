using System.Collections.Generic;
using UnityEngine;

namespace Ale.Condition
{
    /// <summary>
    /// 运行时桥：游戏启动时把纯 C# 的 <see cref="ConditionRegistry.Default"/> 填满——反射扫描所有程序集里
    /// 带 <see cref="ConditionEvaluatorAttribute"/> 的判定器，并把缺失键告警接到 <see cref="Debug"/>（去重）。
    /// </summary>
    public static class ConditionRuntime
    {
        private static readonly HashSet<string> _warned = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            _warned.Clear();
            var reg = ConditionRegistry.Default;
            reg.AutoRegisterFromAssemblies();
            reg.MissingKeyWarning = key =>
            {
                if (_warned.Add(key))
                    Debug.LogWarning($"[ConditionSystem] 未注册的判定器键：{key}");
            };
        }

        /// <summary>向默认注册表注册一个判定器（便捷入口）。</summary>
        public static void Register(IConditionEvaluator evaluator) => ConditionRegistry.Default.Register(evaluator);
    }
}
