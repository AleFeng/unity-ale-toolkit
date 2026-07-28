using System;
using System.Collections.Generic;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 纯 C# 引用类型对象池（静态泛型）。用于池化<b>非 Unity 对象</b>的普通 <c>class</c> 实例，
    /// 复用而非反复 <c>new</c>，从而降低 GC 压力（如临时的命令 / 事件 / 上下文 / 累加器对象）。
    /// 与 <see cref="ToolkitGameObjectPool"/>（GameObject 池）彼此独立、互不依赖。
    ///
    /// <para><b>取用不自动 new：</b>池空时 <see cref="Spawn()"/> 返回 <c>null</c>，由调用方决定如何新建——
    /// 惯用写法 <c>var x = ToolkitClassPool&lt;Foo&gt;.Spawn() ?? new Foo();</c>。这样池只回答
    /// 「有没有可复用的」，构造逻辑（含带参构造）完全留给调用方，也与 Lean 的 <c>LeanClassPool</c> 一致。</para>
    ///
    /// <code>
    /// var ctx = ToolkitClassPool&lt;Ctx&gt;.Spawn() ?? new Ctx();
    /// ctx.Set(...);
    /// // ...用完...
    /// ToolkitClassPool&lt;Ctx&gt;.Despawn(ctx, c =&gt; c.Reset());   // 归还并清理
    /// </code>
    ///
    /// <para><b>关闭 Domain Reload 兼容：</b>各闭合泛型首次使用时把自身的清空动作登记到
    /// <see cref="ToolkitSingletonRegistry"/>，播放开始时统一复位，避免可复用实例跨会话残留。</para>
    /// </summary>
    /// <typeparam name="T">被池化的引用类型。</typeparam>
    public static class ToolkitClassPool<T> where T : class
    {
        private static readonly List<T> Pool = new List<T>();

        // 是否已向 ToolkitSingletonRegistry 登记过复位动作（每个闭合泛型各一份）。
        private static bool _resetHookRegistered;

        #region 取用

        /// <summary>取用一个可复用实例；池空返回 <c>null</c>。</summary>
        public static T Spawn()
        {
            EnsureResetHook();
            return Take();
        }

        /// <summary>取用一个可复用实例并对其执行 <paramref name="onSpawn"/>（复用时初始化）；池空返回 <c>null</c>。</summary>
        public static T Spawn(Action<T> onSpawn)
        {
            EnsureResetHook();
            var instance = Take();
            if (instance != null) onSpawn?.Invoke(instance);
            return instance;
        }

        /// <summary>取用<b>首个满足 <paramref name="match"/></b> 的可复用实例；无匹配返回 <c>null</c>。</summary>
        public static T Spawn(Predicate<T> match)
        {
            EnsureResetHook();
            return Take(match);
        }

        /// <summary>取用首个满足 <paramref name="match"/> 的实例并执行 <paramref name="onSpawn"/>；无匹配返回 <c>null</c>。</summary>
        public static T Spawn(Predicate<T> match, Action<T> onSpawn)
        {
            EnsureResetHook();
            var instance = Take(match);
            if (instance != null) onSpawn?.Invoke(instance);
            return instance;
        }

        #endregion

        #region 归还

        /// <summary>归还实例以供复用。<c>null</c> 或重复归还将被忽略。</summary>
        public static void Despawn(T instance)
        {
            EnsureResetHook();
            if (instance == null || Pool.Contains(instance)) return;
            Pool.Add(instance);
        }

        /// <summary>先对实例执行 <paramref name="onDespawn"/> 清理，再归还以供复用。<c>null</c> 或重复归还将被忽略。</summary>
        public static void Despawn(T instance, Action<T> onDespawn)
        {
            EnsureResetHook();
            if (instance == null || Pool.Contains(instance)) return;
            onDespawn?.Invoke(instance);
            Pool.Add(instance);
        }

        #endregion

        #region 查询 / 清理

        /// <summary>当前池中可复用实例数量。</summary>
        public static int Count => Pool.Count;

        /// <summary>清空池（丢弃全部可复用实例的引用）。</summary>
        public static void Clear() => Pool.Clear();

        #endregion

        #region 内部

        private static T Take()
        {
            int last = Pool.Count - 1;
            if (last < 0) return null;
            var instance = Pool[last];
            Pool.RemoveAt(last);
            return instance;
        }

        private static T Take(Predicate<T> match)
        {
            if (match == null) return Take();
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                if (match(Pool[i]))
                {
                    var instance = Pool[i];
                    Pool.RemoveAt(i);
                    return instance;
                }
            }
            return null;
        }

        private static void EnsureResetHook()
        {
            if (_resetHookRegistered) return;
            _resetHookRegistered = true;
            ToolkitSingletonRegistry.Register(ResetStatics);
        }

        private static void ResetStatics()
        {
            Pool.Clear();
            // _resetHookRegistered 保持 true：登记表同样跨会话存活，无需重复登记。
        }

        #endregion
    }
}
