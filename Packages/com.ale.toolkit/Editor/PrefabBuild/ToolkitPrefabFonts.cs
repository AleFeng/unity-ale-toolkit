#if ATK_TMP
using TMPro;
#endif
#if ATK_TMP && ATK_LOCALIZATION
using Ale.Toolkit.Runtime.UI;
#endif
using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 预制体生成向导使用的字体配置（<b>项目级全局设定</b>，经 <see cref="EditorPrefs"/> 持久化）。
    /// 由 Ale Toolkit 欢迎窗口的 TextMeshPro / Unity Localization 宏方块配置，供各插件的预制体生成向导读取——
    /// 字体选择通常全局一致，故与宏一样统一由 toolkit 保管。宏未启用时对应属性不参与编译。
    /// </summary>
    public static class ToolkitPrefabFonts
    {
#if ATK_TMP
        private const string DefaultTmpFontPathKey = "AleToolkit.Wizard.DefaultTmpFontPath";

        /// <summary>向导生成 Prefab 时套用于所有 TMP 文本节点的默认字体（留空则用 TMP 内置默认字体）。</summary>
        public static TMP_FontAsset DefaultTmpFont
        {
            get
            {
                string path = EditorPrefs.GetString(DefaultTmpFontPathKey, string.Empty);
                return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
            set
            {
                string path = value ? AssetDatabase.GetAssetPath(value) : string.Empty;
                EditorPrefs.SetString(DefaultTmpFontPathKey, path);
            }
        }
#endif

#if ATK_TMP && ATK_LOCALIZATION
        private const string LocalizedFontJsonKey = "AleToolkit.Wizard.LocalizedFontJson";

        /// <summary>
        /// 向导生成 Prefab 时赋给 <c>LocalizedFontEvent</c> 的本地化字体引用（表 + 条目）。
        /// 以 <see cref="JsonUtility"/> 序列化其 <c>LocalizedAsset</c> 内部字段持久化，读回时重建。
        /// </summary>
        public static LocalizedTmpFont LocalizedFont
        {
            get
            {
                var f = new LocalizedTmpFont();
                string json = EditorPrefs.GetString(LocalizedFontJsonKey, string.Empty);
                if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, f);
                return f;
            }
            set => EditorPrefs.SetString(LocalizedFontJsonKey,
                value != null ? JsonUtility.ToJson(value) : string.Empty);
        }
#endif
    }
}
