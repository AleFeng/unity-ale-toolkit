using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 资源取用加载器抽象。<see cref="ToolkitAssets"/> 门面把所有取用请求委派给当前激活的加载器。
    ///
    /// 默认实现为 <see cref="DirectAssetLoader"/>（同步返回 SO 里的实时引用，供编辑器/直接模式）；
    /// 启用 ATK_ADDRESSABLE 宏时，受约束的 Addressable 程序集会在运行时把激活加载器替换为
    /// 基于 Addressables 的异步实现（按地址加载 + 引用计数自动卸载）。core 程序集对 Addressables 零依赖。
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// 取用 <paramref name="value"/> 在 <paramref name="index"/> 处的对象资源。
        /// 加载完成（或同步可得）后回调 <paramref name="onLoaded"/>。
        /// <paramref name="owner"/> 为生命周期宿主（可空）：非空时加载的句柄随宿主销毁自动释放。
        /// </summary>
        void Load<T>(AttributeValue value, int index, GameObject owner, Action<T> onLoaded) where T : Object;

        /// <summary>
        /// 取用固定资源引用（配置类的具名字段，如 <c>Skill.icon</c>）：优先用实时引用 <paramref name="liveRef"/>，
        /// 无引用时回退 <paramref name="address"/> 走 Addressable 异步加载（直接模式仅返回 <paramref name="liveRef"/>）。
        /// </summary>
        void Load<T>(Object liveRef, string address, GameObject owner, Action<T> onLoaded) where T : Object;

        /// <summary>
        /// <b>纯地址</b>取用：调用方手上只有一个运行时拼出来的地址串，没有任何实时引用可回退
        /// （如按角色名拼出 <c>"…/Actors/{name}.prefab"</c>）。与上面两个 <c>Load</c> 的区别正在于此——
        /// 那两个都有实时引用优先路径，本方法没有。
        /// <para><paramref name="owner"/> 非空时句柄随宿主销毁自动释放；为空则调用方须自行用
        /// <see cref="ReleaseAddress"/> 按同一地址配对释放，否则句柄泄漏。</para>
        /// <para>加载失败或直接模式无法解析该地址时，回调传入 <c>null</c>。</para>
        /// </summary>
        void LoadByAddress<T>(string address, GameObject owner, Action<T> onLoaded) where T : Object;

        /// <summary>释放某宿主名下加载的全部资源句柄（直接模式为空操作）。</summary>
        void Release(GameObject owner);

        /// <summary>
        /// 释放一次由无宿主 <c>Load</c> 按地址加载的资源句柄（归零则卸载）。直接模式为空操作。
        /// 供无宿主加载（<paramref name="address"/> 取自 <see cref="AttributeValue.GetObjAddress"/>）配对释放。
        /// </summary>
        void ReleaseAddress(string address);
    }
}
