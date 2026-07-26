using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// toolkit 编辑器加载检查器（<c>[InitializeOnLoad]</c>）。每次启动 / 域重载后延迟执行：
    /// ① <b>旧宏迁移</b>：把老项目已设的旧宏名（<c>IS_*</c>）一次性改写为新宏名（<c>ATK_*</c>）——
    ///    检测到仍启用的旧宏即补上对应新宏、移除旧宏（幂等，迁移后不再触发）；
    /// ② <b>包 / 宏一致性提示</b>：开了宏却没装对应包时在 Console 警告（仅提示，不强制修改）。
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
            MigrateLegacyDefines();
            CheckPackageConsistency();
        }

        /// <summary>把旧宏名 <c>IS_*</c> 迁移为 <c>ATK_*</c>：对每个仍启用的旧宏，补上对应新宏并移除旧宏。</summary>
        private static void MigrateLegacyDefines()
        {
            foreach (var kv in ToolkitDefines.LegacyRename)
            {
                string legacy = kv.Key;
                string modern = kv.Value;
                if (!ToolkitDefines.IsDefineEnabled(legacy)) continue;      // 旧宏未启用：无需迁移

                if (!ToolkitDefines.IsDefineEnabled(modern))
                    DefineUtils.ApplyDefine(true, modern);                  // 补上新宏
                DefineUtils.ApplyDefine(false, legacy);                     // 移除旧宏
                Debug.Log($"[Ale Toolkit] 已迁移旧宏 '{legacy}' → '{modern}'。");
            }
        }

        /// <summary>包 / 宏一致性检查（仅提示，不自动修改）。TMP 内置于 ugui 恒可用，无需检查。</summary>
        private static void CheckPackageConsistency()
        {
            WarnIfMismatch("Unity Localization", ToolkitDefines.Localization,
                ToolkitDefines.IsLocalizationPackageInstalled(), ToolkitDefines.PackageLocalization);
            WarnIfMismatch("Unity Addressables", ToolkitDefines.Addressable,
                ToolkitDefines.IsAddressablePackageInstalled(), ToolkitDefines.PackageAddressables);
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
