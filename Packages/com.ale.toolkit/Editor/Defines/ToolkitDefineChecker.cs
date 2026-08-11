using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// toolkit 编辑器加载检查器（<c>[InitializeOnLoad]</c>）。每次启动 / 域重载后延迟执行
    /// <b>包 / 宏一致性提示</b>：开了宏却没装对应包时在 Console 警告。
    ///
    /// <para><b>只提示，绝不改写 PlayerSettings。</b>宏的增删一律由用户经欢迎窗口显式操作——
    /// 自动改写会与其他插件的同名宏管理逻辑互相覆盖，每次写入都触发一次重编译，
    /// 编辑器会陷入「Compiling Scripts」死循环。</para>
    /// </summary>
    [InitializeOnLoad]
    public static class ToolkitDefineChecker
    {
        static ToolkitDefineChecker()
        {
            // 延迟到编辑器完全就绪后执行，避免在域初始化期间操作 PlayerSettings / UI。
            EditorApplication.delayCall += OnDelayedInit;
        }

        private static void OnDelayedInit()
        {
            EditorApplication.delayCall -= OnDelayedInit;
            CheckPackageConsistency();
        }

        /// <summary>包 / 宏一致性检查（仅提示，不自动修改）。TMP 内置于 ugui 恒可用，无需检查。</summary>
        private static void CheckPackageConsistency()
        {
            WarnIfMismatch("Unity Localization", ToolkitDefines.Localization,
                ToolkitDefines.IsLocalizationPackageInstalled(), ToolkitDefines.PackageLocalization);
            WarnIfMismatch("Unity Addressables", ToolkitDefines.Addressable,
                ToolkitDefines.IsAddressablePackageInstalled(), ToolkitDefines.PackageAddressables);
            WarnIfMismatch("Unity Input System", ToolkitDefines.InputSystem,
                ToolkitDefines.IsInputSystemPackageInstalled(), ToolkitDefines.PackageInputSystem);
        }

        private static void WarnIfMismatch(string title, string define, bool packageInstalled, string package)
        {
            if (ToolkitDefines.IsDefineEnabled(define) && !packageInstalled)
                Debug.LogWarning(
                    $"[Ale Toolkit] {title} 宏 '{define}' 已启用，但 {package} 包未安装。\n" +
                    "相关功能在运行时将无法生效。建议安装该包，或在 Ale Toolkit 欢迎窗口中关闭该宏。\n" +
                    "（Tools > Ale Toolkit > Welcome）");
        }
    }
}
