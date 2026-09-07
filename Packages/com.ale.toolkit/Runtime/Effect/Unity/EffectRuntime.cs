using System.Collections.Generic;
using UnityEngine;

namespace Ale.Effect
{
    /// <summary>
    /// 运行时引导：分两阶段——<c>SubsystemRegistration</c> 清空全局效果定义注册表（与 toolkit 单例复位同阶段，先于任何注册）；
    /// <c>BeforeSceneLoad</c> 只做加法：自动发现并注册全部 <c>[EffectExecutor]</c> 执行器、安装「未注册键」警告，
    /// 并在 <see cref="AutoLoadFromResources"/> 为真时把 <c>Resources</c> 下的全部 <see cref="EffectDatabase"/> 注册进
    /// <see cref="EffectDataManager"/>（零配置默认；有自己数据管线 / Addressables / 二进制加载的宿主可关闭后显式 <see cref="EffectDataManager.Register"/>）。
    /// 拆分的原因：同一 LoadType 内跨程序集的回调顺序无保证，「清空」与「加法」若混在同一阶段会互相抹除。
    /// </summary>
    public static class EffectRuntime
    {
        private static readonly HashSet<string> _warned = new HashSet<string>();

        /// <summary>是否在启动时自动注册 <c>Resources</c> 目录下的全部 <see cref="EffectDatabase"/>（默认 true）。须在 BeforeSceneLoad 之前设置（如静态构造 / 更早的引导）。</summary>
        public static bool AutoLoadFromResources { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistries()
        {
            _warned.Clear();
            EffectDefinitionRegistry.Default.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var reg = EffectRegistry.Default;
            reg.AutoRegisterFromAssemblies();
            reg.MissingKeyWarning = key =>
            {
                if (_warned.Add(key))
                    Debug.LogWarning($"[EffectSystem] 未注册的执行器键：{key}");
            };

            if (AutoLoadFromResources)
                foreach (var db in Resources.LoadAll<EffectDatabase>(string.Empty))
                    if (db) EffectDataManager.Instance.Register(db);
        }

        /// <summary>显式注册一个执行器（补充自动发现）。</summary>
        public static void Register(IEffectExecutor executor) => EffectRegistry.Default.Register(executor);
    }
}
