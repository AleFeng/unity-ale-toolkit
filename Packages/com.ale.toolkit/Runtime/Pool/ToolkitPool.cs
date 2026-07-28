using System.Collections.Generic;
using UnityEngine;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 对象池静态门面。两大职责：
    /// <list type="number">
    ///   <item><b>按预制体自动建池</b>的便捷 <see cref="Spawn(GameObject)"/> /
    ///         <see cref="Despawn(GameObject, float)"/>——把 <c>Instantiate</c> / <c>Destroy</c>
    ///         就地替换即可，无需自己持有 <see cref="ToolkitGameObjectPool"/>。</item>
    ///   <item>维护<b>「克隆体 → 属主池」登记表</b> <see cref="Links"/>，使全局
    ///         <see cref="Despawn(GameObject, float)"/> 能把任意克隆体归还到它所属的池——
    ///         无论它是经本门面取用，还是经某个显式 <see cref="ToolkitGameObjectPool"/> 取用。</item>
    /// </list>
    ///
    /// <para><b>关闭 Domain Reload 兼容：</b>静态登记表在每次播放开始
    /// （<see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>）时清空，避免跨播放会话残留。</para>
    /// </summary>
    public static class ToolkitPool
    {
        /// <summary>克隆体 → 属主池 的登记表。由 <see cref="ToolkitGameObjectPool"/> 在取用 / 归还时维护。</summary>
        public static readonly Dictionary<GameObject, ToolkitGameObjectPool> Links
            = new Dictionary<GameObject, ToolkitGameObjectPool>();

        // 预制体 → 自动创建的池；仅供本门面的 Spawn(prefab) 便捷路径使用。
        private static readonly Dictionary<GameObject, ToolkitGameObjectPool> PrefabPools
            = new Dictionary<GameObject, ToolkitGameObjectPool>();

        // 承载自动创建的池的隐藏持久根。
        private static Transform _root;

        #region 取用（按预制体自动建池）

        /// <summary>按预制体取用一个克隆体（不改变父级 / 位姿）。</summary>
        public static GameObject Spawn(GameObject prefab)
            => FindOrCreatePool(prefab)?.Spawn();

        /// <summary>按预制体取用一个克隆体，设置局部位姿与父级。</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            => FindOrCreatePool(prefab)?.Spawn(position, rotation, parent);

        /// <summary>按预制体取用一个克隆体并挂到指定父级。</summary>
        public static GameObject Spawn(GameObject prefab, Transform parent, bool worldPositionStays = false)
            => FindOrCreatePool(prefab)?.Spawn(parent, worldPositionStays);

        /// <summary>
        /// 按<b>组件预制体</b>取用并直接返回该组件类型的实例（省去 <c>GetComponent</c>）。
        /// <paramref name="prefab"/> 可为 <see cref="GameObject"/> 或任意 <see cref="Component"/>。
        /// </summary>
        public static T Spawn<T>(T prefab) where T : Object
        {
            var pool = ResolvePool(prefab);
            return pool == null ? null : AsResult<T>(pool.Spawn());
        }

        /// <inheritdoc cref="Spawn{T}(T)"/>
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Object
        {
            var pool = ResolvePool(prefab);
            return pool == null ? null : AsResult<T>(pool.Spawn(position, rotation, parent));
        }

        /// <inheritdoc cref="Spawn{T}(T)"/>
        public static T Spawn<T>(T prefab, Transform parent, bool worldPositionStays = false) where T : Object
        {
            var pool = ResolvePool(prefab);
            return pool == null ? null : AsResult<T>(pool.Spawn(parent, worldPositionStays));
        }

        #endregion

        #region 归还 / 分离

        /// <summary>
        /// 把克隆体归还其属主池（按 <see cref="Links"/> 路由）。
        /// 返回 <c>true</c> = 已归还到某池；<c>false</c> = 未登记（非池化对象或已归还），此时按
        /// <paramref name="delay"/> 直接销毁兜底。
        /// </summary>
        public static bool Despawn(GameObject clone, float delay = 0f)
        {
            if (clone == null) return false;

            if (Links.TryGetValue(clone, out var pool) && pool != null)
            {
                pool.Despawn(clone, delay);
                return true;
            }

            if (delay > 0f) Object.Destroy(clone, delay);
            else Object.Destroy(clone);
            return false;
        }

        /// <summary>把组件所属克隆体归还其属主池。见 <see cref="Despawn(GameObject, float)"/>。</summary>
        public static bool Despawn(Component clone, float delay = 0f)
            => clone != null && Despawn(clone.gameObject, delay);

        /// <summary>归还当前全部已登记的激活克隆体（跨所有池）。</summary>
        public static void DespawnAll()
        {
            var clones = new List<GameObject>(Links.Keys);
            for (int i = 0; i < clones.Count; i++)
                Despawn(clones[i]);
        }

        /// <summary>让克隆体脱离池管理（属主池不再跟踪它，也从登记表移除）；此后由调用方自行负责其生命周期。</summary>
        public static void Detach(GameObject clone)
        {
            if (clone == null) return;
            if (Links.TryGetValue(clone, out var pool) && pool != null)
                pool.Forget(clone);
            Links.Remove(clone);
        }

        #endregion

        #region 内部

        private static ToolkitGameObjectPool ResolvePool(Object prefab)
        {
            var go = AsGameObject(prefab);
            return go == null ? null : FindOrCreatePool(go);
        }

        private static ToolkitGameObjectPool FindOrCreatePool(GameObject prefab)
        {
            if (prefab == null) return null;
            if (PrefabPools.TryGetValue(prefab, out var pool) && pool != null)
                return pool;

            EnsureRoot();
            var go = new GameObject($"ToolkitPool ({prefab.name})");
            go.transform.SetParent(_root, false);
            pool = go.AddComponent<ToolkitGameObjectPool>();
            pool.Prefab = prefab;
            PrefabPools[prefab] = pool;
            return pool;
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;
            var rootGo = new GameObject("ToolkitPoolRoot");
            Object.DontDestroyOnLoad(rootGo);
            _root = rootGo.transform;
        }

        private static GameObject AsGameObject(Object prefab)
        {
            switch (prefab)
            {
                case null:          return null;
                case GameObject go: return go;
                case Component c:   return c.gameObject;
                default:            return null;
            }
        }

        private static T AsResult<T>(GameObject clone) where T : Object
        {
            if (clone == null) return null;
            if (clone is T typedGo) return typedGo; // T == GameObject
            return clone.GetComponent<T>();          // T 为某组件 / 接口类型
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Links.Clear();
            PrefabPools.Clear();
            _root = null;
        }

        #endregion
    }
}
