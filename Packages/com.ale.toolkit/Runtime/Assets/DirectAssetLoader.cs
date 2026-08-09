using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 默认加载器：直接返回 SO 中保存的实时对象引用，同步回调。
    /// 适用于编辑器内、或运行时直接加载 SO（引用未被剥离）的场景。
    /// 不持有任何句柄，<see cref="Release"/> 为空操作。
    /// </summary>
    public sealed class DirectAssetLoader : IAssetLoader
    {
        public static readonly DirectAssetLoader Instance = new DirectAssetLoader();

        public void Load<T>(AttributeValue value, int index, GameObject owner, Action<T> onLoaded) where T : Object
        {
            if (onLoaded == null) return;
            var obj = value != null ? value.GetObject(index) : null;
            onLoaded(obj as T);
        }

        public void Load<T>(Object liveRef, string address, GameObject owner, Action<T> onLoaded) where T : Object
        {
            // 直接模式：仅返回实时引用（address 忽略）。
            onLoaded?.Invoke(liveRef as T);
        }

        public void LoadByAddress<T>(string address, GameObject owner, Action<T> onLoaded) where T : Object
        {
            if (onLoaded == null) return;
            if (string.IsNullOrEmpty(address)) { onLoaded(null); return; }

            // 直接模式没有实时引用可回退，纯地址在运行时唯一可解析的途径就是 Resources。
            // Resources 要求「相对某个 Resources 目录、且不带扩展名」的路径，而地址串通常带扩展名
            // （如 "…/Actor_Test_1.prefab"），故先去掉扩展名再试——这是最常见的一处不匹配。
            T asset = Resources.Load<T>(StripExtension(address));
            // 先上转型到 Object 再判空：泛型形参 T 拿不到 UnityEngine.Object 的 bool 重载
            // （`if (asset)` 无法编译），而 `asset != null` 走的是引用比较、会绕过 Unity 的「伪 null」判定。
            if ((Object)asset) { onLoaded(asset); return; }

            Debug.LogWarning(
                $"[Toolkit.Assets] 直接模式无法按地址取用资源：{address}\n" +
                "直接模式只认实时引用；纯地址加载需要 Addressables —— 请安装 com.unity.addressables " +
                "并在 Ale Toolkit 欢迎窗口启用 ATK_ADDRESSABLE 宏，或把资源放进 Resources 目录并改用其相对路径。");
            onLoaded(null);
        }

        public void Release(GameObject owner)
        {
            // 直接模式无句柄，无需释放。
        }

        public void ReleaseAddress(string address)
        {
            // 直接模式无句柄，无需释放。
        }

        /// <summary>去掉地址末段的扩展名（无扩展名则原样返回）。只看最后一段，避免误伤目录名里的点。</summary>
        private static string StripExtension(string address)
        {
            int slash = address.LastIndexOf('/');
            int dot = address.LastIndexOf('.');
            return dot > slash ? address.Substring(0, dot) : address;
        }
    }
}
