using System.Collections.Generic;
using UnityEngine;

namespace Ale.Condition
{
    /// <summary>
    /// 运行时引导：分两阶段——<c>SubsystemRegistration</c> 清空全局具名条件注册表（与 toolkit 单例复位同阶段，先于任何注册）；
    /// <c>BeforeSceneLoad</c> 只做加法：反射发现并注册全部 <c>[ConditionEvaluator]</c> 判定器、安装「未注册键 / 未找到条件 id」告警，
    /// 并在 <see cref="AutoLoadFromResources"/> 为真时把 <c>Resources</c> 下的全部 <see cref="ConditionDatabase"/> 注册进
    /// <see cref="ConditionDataManager"/>（零配置默认；有自己数据管线 / Addressables / 二进制加载的宿主可关闭后显式
    /// <see cref="ConditionDataManager.Register"/>）。
    ///
    /// <para>拆分的原因：同一 LoadType 内跨程序集的回调顺序无保证，「清空」与「加法」若混在同一阶段会互相抹除。</para>
    /// </summary>
    public static class ConditionRuntime
    {
        private static readonly HashSet<string> _warned    = new HashSet<string>();
        private static readonly HashSet<string> _warnedIds = new HashSet<string>();

        /// <summary>是否在启动时自动注册 <c>Resources</c> 目录下的全部 <see cref="ConditionDatabase"/>（默认 true）。须在 BeforeSceneLoad 之前设置（如静态构造 / 更早的引导）。</summary>
        public static bool AutoLoadFromResources { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistries()
        {
            _warned.Clear();
            _warnedIds.Clear();
            ConditionDefinitionRegistry.Default.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var reg = ConditionRegistry.Default;
            reg.AutoRegisterFromAssemblies();
            reg.MissingKeyWarning = key =>
            {
                if (_warned.Add(key))
                    Debug.LogWarning($"[ConditionSystem] 未注册的判定器键：{key}");
            };

            ConditionResolver.MissingIdWarning = id =>
            {
                if (_warnedIds.Add(id))
                    Debug.LogWarning($"[ConditionSystem] 未找到具名条件：{id}（按不通过处理）");
            };

            if (AutoLoadFromResources)
                foreach (var db in Resources.LoadAll<ConditionDatabase>(string.Empty))
                    if (db) ConditionDataManager.Instance.Register(db);
        }

        /// <summary>向默认注册表注册一个判定器（便捷入口）。</summary>
        public static void Register(IConditionEvaluator evaluator) => ConditionRegistry.Default.Register(evaluator);
    }
}
