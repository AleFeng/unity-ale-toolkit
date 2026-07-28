using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 通用 GameObject 预制体对象池（MonoBehaviour）。把频繁 <c>Instantiate</c> / <c>Destroy</c>
    /// 的预制体改为「取用（<see cref="Spawn()"/>）→ 归还（<see cref="Despawn"/>）→ 复用」，
    /// 降低实例化与 GC 开销。是 <see cref="ToolkitPool"/> 静态门面背后的实体池，也可单独挂到
    /// GameObject 上使用。
    ///
    /// <para><b>用法：</b></para>
    /// <code>
    /// var pool = go.AddComponent&lt;ToolkitGameObjectPool&gt;();
    /// pool.Prefab  = myPrefab;
    /// pool.Preload = 3;                    // 首次取用前预热 3 个
    /// var clone = pool.Spawn(pos, rot, parent);
    /// // ...
    /// pool.Despawn(clone);                 // 归还复用
    /// </code>
    ///
    /// <para><b>预热时机：</b><see cref="Preload"/> 采用<b>惰性</b>预热——在首次 <see cref="Spawn()"/>
    /// 时才按当时的 <see cref="Prefab"/> / <see cref="Preload"/> 生成，从而不依赖
    /// <c>AddComponent</c> 后属性赋值与 Unity 生命周期（<c>Awake</c>）的先后顺序。</para>
    ///
    /// <para><b>容量与回收：</b><see cref="Capacity"/> 为 0 表示不限；达到上限继续取用时，
    /// <see cref="Recycle"/> 为真则强制归还<b>最早取用</b>的克隆体来复用（<see cref="DespawnOldest"/>），
    /// 为假则仍新建、超出上限。</para>
    ///
    /// <para><b>通知：</b>取用 / 归还时按 <see cref="Notification"/> 通知克隆体（默认
    /// <see cref="PoolNotificationType.IPoolable"/>，回调 <see cref="IPoolable.OnSpawn"/> /
    /// <see cref="IPoolable.OnDespawn"/>）。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ale Toolkit/Toolkit Game Object Pool")]
    public class ToolkitGameObjectPool : MonoBehaviour
    {
        /// <summary>取用 / 归还时通知克隆体的方式。</summary>
        public enum PoolNotificationType
        {
            /// <summary>不通知，仅依赖对象自身的 <c>OnEnable</c> / <c>OnDisable</c>。</summary>
            None,

            /// <summary>对克隆体<b>根对象</b> <c>SendMessage("OnSpawn"/"OnDespawn")</c>（无接收者不报错）。</summary>
            SendMessage,

            /// <summary>对克隆体<b>及其子级</b> <c>BroadcastMessage("OnSpawn"/"OnDespawn")</c>（无接收者不报错）。</summary>
            BroadcastMessage,

            /// <summary>对克隆体<b>根对象</b>上实现 <see cref="IPoolable"/> 的组件回调 OnSpawn / OnDespawn。</summary>
            IPoolable,

            /// <summary>对克隆体<b>及其子级</b>上实现 <see cref="IPoolable"/> 的组件回调 OnSpawn / OnDespawn。</summary>
            BroadcastIPoolable,
        }

        #region 配置

        [Header("对象池")]
        [Tooltip("被池化的预制体。")]
        [SerializeField] private GameObject prefab;

        [Tooltip("预热数量：首次取用前预先实例化并隐藏的克隆体个数。")]
        [SerializeField] private int preload;

        [Tooltip("同时激活的克隆体上限；0 = 不限。")]
        [SerializeField] private int capacity;

        [Tooltip("达到 Capacity 上限继续取用时，是否强制回收最早取用的克隆体来复用（否则仍新建、超出上限）。")]
        [SerializeField] private bool recycle;

        [Tooltip("是否 DontDestroyOnLoad，使本池及其克隆体跨场景存活（仅对未被挂在其他父级下的池有效）。")]
        [SerializeField] private bool persist;

        [Tooltip("取用 / 归还时通知克隆体的方式。")]
        [SerializeField] private PoolNotificationType notification = PoolNotificationType.IPoolable;

        /// <summary>被池化的预制体。</summary>
        public GameObject Prefab { get => prefab; set => prefab = value; }

        /// <summary>预热数量：首次取用前预先实例化并隐藏的克隆体个数。</summary>
        public int Preload { get => preload; set => preload = value; }

        /// <summary>同时激活的克隆体上限；<c>0</c> = 不限。</summary>
        public int Capacity { get => capacity; set => capacity = value; }

        /// <summary>达到 <see cref="Capacity"/> 上限继续取用时，是否强制回收最早取用的克隆体来复用。</summary>
        public bool Recycle { get => recycle; set => recycle = value; }

        /// <summary>是否 <c>DontDestroyOnLoad</c> 使本池跨场景存活。运行时置真会立即应用（仅根对象生效）。</summary>
        public bool Persist
        {
            get => persist;
            set
            {
                persist = value;
                if (persist) ApplyPersist();
            }
        }

        /// <summary>取用 / 归还时通知克隆体的方式。</summary>
        public PoolNotificationType Notification { get => notification; set => notification = value; }

        #endregion

        #region 计数

        /// <summary>当前处于激活（已取用未归还）状态的克隆体数量。</summary>
        public int Spawned => _spawned.Count;

        /// <summary>当前处于隐藏（已归还可复用）状态的克隆体数量。</summary>
        public int Despawned => _despawned.Count;

        /// <summary>本池管理的克隆体总数（激活 + 隐藏）。</summary>
        public int Total => _spawned.Count + _despawned.Count;

        #endregion

        // 按取用顺序保存激活克隆体（供 DespawnOldest 回收最早的一个）。
        private readonly List<GameObject>  _spawned        = new List<GameObject>();
        // 可复用的隐藏克隆体。
        private readonly Stack<GameObject> _despawned      = new Stack<GameObject>();
        // IPoolable 回调复用缓冲，免每次分配。
        private readonly List<IPoolable>   _poolableBuffer = new List<IPoolable>();

        private bool _preloaded;

        #region Unity 生命周期

        protected virtual void Awake()
        {
            if (persist) ApplyPersist();
        }

        #endregion

        #region 取用

        /// <summary>取用一个克隆体（不改变父级 / 位姿，就地激活并返回）。</summary>
        public GameObject Spawn() => Spawn(transform, false);

        /// <summary>取用一个克隆体，设置<b>局部</b>位姿与父级并激活。</summary>
        /// <param name="position">相对 <paramref name="parent"/> 的局部位置。</param>
        /// <param name="rotation">相对 <paramref name="parent"/> 的局部旋转。</param>
        /// <param name="parent">父级；<c>null</c> 表示挂到场景根。</param>
        public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var clone = TakeClone();
            if (clone == null) return null;

            var t = clone.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            ActivateAndTrack(clone);
            return clone;
        }

        /// <summary>取用一个克隆体并挂到指定父级后激活。</summary>
        /// <param name="parent">父级。</param>
        /// <param name="worldPositionStays">是否保持世界位姿不变（同 <c>Transform.SetParent</c>）。</param>
        public GameObject Spawn(Transform parent, bool worldPositionStays = false)
        {
            var clone = TakeClone();
            if (clone == null) return null;

            clone.transform.SetParent(parent, worldPositionStays);
            ActivateAndTrack(clone);
            return clone;
        }

        // 取一个可用克隆体（处理容量 / 回收 / 复用 / 新建），尚未激活、尚未登记。
        private GameObject TakeClone()
        {
            EnsurePreloaded();

            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(ToolkitGameObjectPool)}] 未设置 Prefab，无法 Spawn。", this);
                return null;
            }

            // 达到容量上限且允许回收：先归还最早取用的一个，使其进入可复用栈。
            if (capacity > 0 && _spawned.Count >= capacity && recycle)
                DespawnOldest();

            // 复用隐藏栈中的实例（跳过已被外部销毁的 null）。
            while (_despawned.Count > 0)
            {
                var reused = _despawned.Pop();
                if (reused != null) return reused;
            }

            return Instantiate(prefab);
        }

        // 激活、登记、发通知。
        private void ActivateAndTrack(GameObject clone)
        {
            clone.SetActive(true);
            _spawned.Add(clone);
            ToolkitPool.Links[clone] = this;
            Notify(clone, true);
        }

        #endregion

        #region 归还

        /// <summary>把克隆体归还本池（隐藏复用）。<paramref name="delay"/> &gt; 0 时延迟归还。</summary>
        /// <param name="clone">要归还的克隆体（须由本池取用）。</param>
        /// <param name="delay">延迟秒数；&lt;= 0 立即归还。</param>
        public void Despawn(GameObject clone, float delay = 0f)
        {
            if (clone == null) return;

            if (delay > 0f)
            {
                // 组件未激活时无法起协程，退化为立即归还。
                if (isActiveAndEnabled) StartCoroutine(DespawnAfter(clone, delay));
                else DespawnNow(clone);
                return;
            }

            DespawnNow(clone);
        }

        /// <summary>归还最早取用的克隆体（无激活克隆体时无操作）。</summary>
        public void DespawnOldest()
        {
            if (_spawned.Count > 0) DespawnNow(_spawned[0]);
        }

        /// <summary>归还本池当前全部激活克隆体。</summary>
        public void DespawnAll()
        {
            // 快照后逐个归还，避免归还过程修改 _spawned 影响遍历。
            var snapshot = _spawned.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                DespawnNow(snapshot[i]);
        }

        private IEnumerator DespawnAfter(GameObject clone, float delay)
        {
            yield return new WaitForSeconds(delay);
            DespawnNow(clone);
        }

        private void DespawnNow(GameObject clone)
        {
            if (clone == null) return;

            int index = _spawned.IndexOf(clone);
            if (index < 0) return; // 非本池激活中的克隆体：已归还或不属于本池，忽略。

            _spawned.RemoveAt(index);

            Notify(clone, false);            // 先通知（此刻仍激活），再停用。
            clone.SetActive(false);
            clone.transform.SetParent(transform, false);
            _despawned.Push(clone);
            ToolkitPool.Links.Remove(clone);
        }

        #endregion

        #region 预热 / 清理

        /// <summary>按 <see cref="Preload"/> 预热到位（幂等）。未设置 <see cref="Prefab"/> 时无操作。</summary>
        public void PreloadAll()
        {
            if (prefab == null) return;
            _preloaded = true;
            while (Total < preload)
                _despawned.Push(CreateInactiveClone());
        }

        /// <summary>额外预热一个隐藏克隆体。未设置 <see cref="Prefab"/> 时无操作。</summary>
        public void PreloadOneMore()
        {
            if (prefab == null) return;
            _despawned.Push(CreateInactiveClone());
        }

        /// <summary>销毁全部<b>隐藏</b>克隆体（激活中的不动），释放其内存。</summary>
        public void Clean()
        {
            while (_despawned.Count > 0)
            {
                var clone = _despawned.Pop();
                if (clone != null) Destroy(clone);
            }
        }

        /// <summary>归还并销毁本池全部克隆体，重置到未预热状态。</summary>
        public void Clear()
        {
            DespawnAll();
            Clean();
            _preloaded = false;
        }

        // 惰性预热：首次取用时按当前 Prefab / Preload 生成。
        private void EnsurePreloaded()
        {
            if (_preloaded || prefab == null) return;
            _preloaded = true;
            for (int i = Total; i < preload; i++)
                _despawned.Push(CreateInactiveClone());
        }

        private GameObject CreateInactiveClone()
        {
            var clone = Instantiate(prefab, transform);
            clone.SetActive(false);
            return clone;
        }

        #endregion

        #region 内部

        // 供 ToolkitPool.Detach 使用：让本池不再跟踪某克隆体（不停用、不入回收栈）。
        internal void Forget(GameObject clone)
        {
            _spawned.Remove(clone);
        }

        private void ApplyPersist()
        {
            // DontDestroyOnLoad 仅对场景根对象有意义；被挂在其他父级下时跳过以免告警。
            if (Application.isPlaying && transform.parent == null)
                DontDestroyOnLoad(gameObject);
        }

        private void Notify(GameObject clone, bool spawn)
        {
            switch (notification)
            {
                case PoolNotificationType.None:
                    break;

                case PoolNotificationType.SendMessage:
                    clone.SendMessage(spawn ? "OnSpawn" : "OnDespawn", SendMessageOptions.DontRequireReceiver);
                    break;

                case PoolNotificationType.BroadcastMessage:
                    clone.BroadcastMessage(spawn ? "OnSpawn" : "OnDespawn", SendMessageOptions.DontRequireReceiver);
                    break;

                case PoolNotificationType.IPoolable:
                    clone.GetComponents(_poolableBuffer);
                    InvokePoolable(spawn);
                    break;

                case PoolNotificationType.BroadcastIPoolable:
                    clone.transform.GetComponentsInChildren(true, _poolableBuffer);
                    InvokePoolable(spawn);
                    break;
            }
        }

        private void InvokePoolable(bool spawn)
        {
            for (int i = 0; i < _poolableBuffer.Count; i++)
            {
                var poolable = _poolableBuffer[i];
                if (poolable == null) continue;
                if (spawn) poolable.OnSpawn();
                else poolable.OnDespawn();
            }
            _poolableBuffer.Clear();
        }

        #endregion
    }
}
