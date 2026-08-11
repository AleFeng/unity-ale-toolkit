using UnityEditor;
using UnityEditor.Build;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// toolkit 可选依赖宏（TextMeshPro / Unity Localization / Unity Addressables）的中枢：宏名常量、
    /// 对应 Unity 包名、宏启用状态 / 包安装状态检测。
    ///
    /// <para>这些宏是<b>项目级全局设定</b>（一个项目一般统一启用 / 不启用），统一由 toolkit 管理，供 toolkit
    /// 欢迎窗口的宏开关与各上层插件共享；不再由每个上层插件各自定义。宏的增删经 <see cref="DefineUtils"/>。</para>
    /// </summary>
    public static class ToolkitDefines
    {
        // ── 宏名常量 ──────────────────────────────────────────────────────────────
        /// <summary>TextMeshPro 支持宏。启用后 UI 文本组件使用 <c>TMP_Text</c>。</summary>
        public const string Tmp = "ATK_TMP";
        /// <summary>Unity Localization 支持宏。启用后属性字段可选 <c>LocalizedString</c>。</summary>
        public const string Localization = "ATK_LOCALIZATION";
        /// <summary>Unity Addressables 支持宏。启用后资源字段改用 <c>AssetReference</c> 授权（仅存 GUID）。</summary>
        public const string Addressable = "ATK_ADDRESSABLE";
        /// <summary>Unity Input System 支持宏。启用后可用 <c>ToolkitInputBinder</c> 按名把回调绑到输入上。</summary>
        public const string InputSystem = "ATK_INPUT_SYSTEM";

        // ── 包名（检测是否已安装）──────────────────────────────────────────────────
        /// <summary>TextMeshPro 包名（Unity 2021+ 已内置于 com.unity.ugui）。</summary>
        public const string PackageTmp          = "com.unity.ugui";
        public const string PackageLocalization = "com.unity.localization";
        public const string PackageAddressables = "com.unity.addressables";
        public const string PackageInputSystem  = "com.unity.inputsystem";

        // ── 探测用命名空间 ──────────────────────────────────────────────────────────
        private const string NsTmp          = "TMPro";
        private const string NsLocalization = "UnityEngine.Localization";
        private const string NsAddressable  = "UnityEngine.AddressableAssets";
        private const string NsInputSystem  = "UnityEngine.InputSystem";

        // ── 宏启用状态 ──────────────────────────────────────────────────────────────
        public static bool IsTmpEnabled()          => IsDefineEnabled(Tmp);
        public static bool IsLocalizationEnabled() => IsDefineEnabled(Localization);
        public static bool IsAddressableEnabled()  => IsDefineEnabled(Addressable);
        public static bool IsInputSystemEnabled()  => IsDefineEnabled(InputSystem);

        /// <summary>当前是否在 PlayerSettings（Standalone）启用了指定宏。</summary>
        public static bool IsDefineEnabled(string define)
        {
            try
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
                if (string.IsNullOrEmpty(defines)) return false;
                foreach (var d in defines.Split(';'))
                    if (d.Trim() == define) return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        // ── 包安装状态 ──────────────────────────────────────────────────────────────
        /// <summary>TextMeshPro（<c>TMPro</c> 命名空间）是否可用（Unity 2021+ 通常始终可用）。</summary>
        public static bool IsTmpPackageInstalled()          => DefineUtils.HasNamespace(NsTmp);
        /// <summary>com.unity.localization 是否已安装。</summary>
        public static bool IsLocalizationPackageInstalled() => DefineUtils.HasNamespace(NsLocalization);
        /// <summary>com.unity.addressables 是否已安装。</summary>
        public static bool IsAddressablePackageInstalled()  => DefineUtils.HasNamespace(NsAddressable);
        /// <summary>com.unity.inputsystem 是否已安装。</summary>
        public static bool IsInputSystemPackageInstalled()  => DefineUtils.HasNamespace(NsInputSystem);
    }
}
