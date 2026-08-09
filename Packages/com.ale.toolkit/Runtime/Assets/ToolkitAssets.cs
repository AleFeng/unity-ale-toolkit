using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 资源取用统一门面。无论底层是「直接实时引用」还是「Addressable 异步加载」，
    /// 调用方代码完全一致：
    /// <code>
    ///   ToolkitAssets.Bind&lt;Sprite&gt;(attrValue, image.gameObject, s =&gt; image.sprite = s);
    /// </code>
    /// 有实时引用时同步赋值；只有地址（运行时从导出数据加载）时异步加载完成后再赋值；
    /// 宿主 GameObject 销毁时自动释放对应句柄。
    ///
    /// core 程序集对 Addressables 零依赖：默认 <see cref="Loader"/> 为 <see cref="DirectAssetLoader"/>，
    /// 启用 ATK_ADDRESSABLE 后由 Addressable 运行时程序集在启动时替换为异步加载器。
    /// </summary>
    public static class ToolkitAssets
    {
        /// <summary>当前激活的加载器。默认直接模式；Addressable 程序集启动时会覆盖此值。</summary>
        public static IAssetLoader Loader = DirectAssetLoader.Instance;

        // ── Bind：跟踪宿主、自动释放 ────────────────────────────────────────────────

        /// <summary>绑定属性值的资源到宿主：加载完成后回调 <paramref name="set"/>，宿主销毁时自动释放。</summary>
        public static void Bind<T>(AttributeValue value, GameObject owner, Action<T> set, int index = 0) where T : Object
        {
            if (value == null || set == null) return;
            (Loader ?? DirectAssetLoader.Instance).Load(value, index, owner, set);
        }

        /// <summary>
        /// 绑定固定资源引用（配置类具名字段，如「图标」实时引用 + 其 Addressable 地址）到宿主：
        /// 直接模式同步返回 <paramref name="liveRef"/>；授权模式无实时引用时按 <paramref name="address"/> 异步加载。
        /// 宿主销毁时自动释放句柄。
        /// </summary>
        public static void Bind<T>(Object liveRef, string address, GameObject owner, Action<T> set) where T : Object
        {
            if (set == null) return;
            (Loader ?? DirectAssetLoader.Instance).Load(liveRef, address, owner, set);
        }

        // ── Load：不跟踪宿主，调用方自行管理生命周期 ────────────────────────────────

        /// <summary>
        /// 加载属性值的资源并回调，不绑定任何宿主。
        /// <para>Addressable 模式下句柄不随宿主自动释放，调用方用完后必须调用
        /// <see cref="Release(AttributeValue,int)"/>（传入同一 value 与 index）配对释放，否则句柄泄漏。
        /// 若能提供宿主 GameObject，优先用 <see cref="Bind{T}(AttributeValue,GameObject,Action{T},int)"/>（自动释放）。</para>
        /// </summary>
        public static void Load<T>(AttributeValue value, Action<T> onLoaded, int index = 0) where T : Object
        {
            if (value == null || onLoaded == null) return;
            (Loader ?? DirectAssetLoader.Instance).Load(value, index, null, onLoaded);
        }

        // ── 按地址取用：调用方只有一个运行时拼出来的地址串 ─────────────────────────────
        //
        // 与上面的 Bind / Load 的分工：那些都以 AttributeValue（或「实时引用 + 地址」二元组）为输入，
        // 有实时引用优先路径；这一组的输入只有一个地址字符串，用于「按名字拼地址」的动态取用
        // （如按角色名拼出 "…/Actors/{name}.prefab"）。直接模式下只能退到 Resources，详见
        // DirectAssetLoader.LoadByAddress 的说明。

        /// <summary>
        /// 按地址加载资源并回调。
        /// <para><paramref name="owner"/> 非空时句柄随宿主销毁自动释放；为空（默认）则调用方须用
        /// <see cref="ReleaseAddress"/> 传入<b>同一地址</b>配对释放，否则句柄泄漏。</para>
        /// <para>加载失败时回调传入 <c>null</c>，调用方需自行判空。</para>
        /// </summary>
        public static void LoadByAddress<T>(string address, Action<T> onLoaded, GameObject owner = null) where T : Object
        {
            if (onLoaded == null) return;
            (Loader ?? DirectAssetLoader.Instance).LoadByAddress(address, owner, onLoaded);
        }

        /// <summary>
        /// 按地址加载资源<b>并实例化</b>，回调传入的是新实例（而非源资源）。
        /// <para><b>两份生命周期各管各的</b>：实例归调用方所有，用完自行 <c>Destroy</c>；
        /// 源资源句柄不随实例销毁而释放，仍须调用方用 <see cref="ReleaseAddress"/> 按同一地址配对释放。</para>
        /// <para>加载失败时回调传入 <c>null</c>（不会实例化），调用方需自行判空。</para>
        /// </summary>
        /// <param name="address">资源地址。</param>
        /// <param name="onInstantiated">实例化完成回调，参数为新实例；失败时为 <c>null</c>。</param>
        /// <param name="parent">新实例的父节点，可空（缺省挂到当前活动场景根部）。</param>
        public static void InstantiateByAddress<T>(
            string address, Action<T> onInstantiated, Transform parent = null) where T : Object
        {
            if (onInstantiated == null) return;

            LoadByAddress<T>(address, asset =>
            {
                // 先上转型到 Object 再判空：泛型形参 T 拿不到 UnityEngine.Object 的 bool 重载。
                if (!(Object)asset) { onInstantiated(null); return; }
                onInstantiated(parent ? Object.Instantiate(asset, parent) : Object.Instantiate(asset));
            });
        }

        // ── Release ─────────────────────────────────────────────────────────────────

        /// <summary>释放宿主名下加载的全部资源句柄（直接模式为空操作）。</summary>
        public static void Release(GameObject owner)
        {
            (Loader ?? DirectAssetLoader.Instance).Release(owner);
        }

        /// <summary>
        /// 释放由无宿主 <see cref="Load{T}(AttributeValue,Action{T},int)"/> 加载的资源句柄（按属性值在 <paramref name="index"/> 处的地址）。
        /// 与该无宿主 Load 配对使用；直接模式为空操作。
        /// </summary>
        public static void Release(AttributeValue value, int index = 0)
        {
            if (value == null) return;
            (Loader ?? DirectAssetLoader.Instance).ReleaseAddress(value.GetObjAddress(index));
        }

        /// <summary>
        /// 按地址释放一次资源句柄（引用计数归零则真正卸载）。
        /// 与无宿主的 <see cref="LoadByAddress{T}"/> / <see cref="InstantiateByAddress{T}"/> 配对使用，
        /// 须传入与加载时<b>完全相同</b>的地址串；直接模式为空操作。
        /// <para>注意：只释放源资源句柄，<b>不</b>销毁已实例化出来的对象——那些由调用方自行 <c>Destroy</c>。</para>
        /// </summary>
        public static void ReleaseAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            (Loader ?? DirectAssetLoader.Instance).ReleaseAddress(address);
        }
    }
}
