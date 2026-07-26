#if ATK_TMP
using TMPro;
#endif
#if ATK_TMP && ATK_LOCALIZATION
using System;
using Ale.Toolkit.Runtime.UI;
using UnityEngine.Localization.Tables;
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
        // 保存本地化字体引用的“原始序列化字段”，单键、以 '\n' 分隔："表引用\n条目KeyId\n条目Key"。
        // 表引用即 TableReference 序列化字符串（GUID 引用为 "GUID:<N 格式>"，名称引用为集合名）。
        private const string LocalizedFontKey = "AleToolkit.Wizard.LocalizedFont";
        private const string GuidTag = "GUID:";

        /// <summary>
        /// 向导生成 Prefab 时赋给 <c>LocalizedFontEvent</c> 的本地化字体引用（表 + 条目）。
        /// <para>持久化不走 <see cref="JsonUtility"/> 整体序列化 —— 后者不会触发嵌套的
        /// <see cref="TableReference"/> / <see cref="TableEntryReference"/> 结构体的
        /// <see cref="ISerializationCallbackReceiver"/>，读回时 <c>ReferenceType</c> 无法重建、引用变空。
        /// 改为存取其原始字段（见 <see cref="SaveLocalizedFont"/>），读取时经 <see cref="UnityEngine.Localization.LocalizedReference.SetReference"/>
        /// 重建（隐式转换会正确设置 <c>ReferenceType</c>），<c>m_TableCollectionName</c> 在后续原生序列化时按 GUID 重新生成。</para>
        /// </summary>
        public static LocalizedTmpFont LocalizedFont
        {
            get
            {
                var f = new LocalizedTmpFont();
                string raw = EditorPrefs.GetString(LocalizedFontKey, string.Empty);
                if (string.IsNullOrEmpty(raw)) return f;

                var parts = raw.Split('\n');
                if (parts.Length < 3) return f;
                string tableRef = parts[0];
                long.TryParse(parts[1], out long keyId);
                string key = parts[2];
                if (string.IsNullOrEmpty(tableRef)) return f;

                // 表引用：GUID 引用需自行剥离 "GUID:" 前缀解析（隐式 string→TableReference 会误判为“名称”）。
                TableReference table;
                if (tableRef.StartsWith(GuidTag, StringComparison.OrdinalIgnoreCase) &&
                    Guid.TryParseExact(tableRef.Substring(GuidTag.Length), "N", out var guid))
                    table = guid;         // Guid → Type.Guid
                else
                    table = tableRef;     // string → Type.Name

                // 条目引用：优先 KeyId（Id 类型），否则 Key 名称。
                TableEntryReference entry;
                if (keyId != SharedTableData.EmptyId)
                    entry = keyId;        // long → Type.Id
                else if (!string.IsNullOrEmpty(key))
                    entry = key;          // string → Type.Name
                else
                    return f;             // 表有、条目无 → 视为未配置

                f.SetReference(table, entry);
                return f;
            }
        }

        /// <summary>
        /// 持久化本地化字体引用的原始序列化字段。由欢迎窗口从其 <c>SerializedProperty</c> 直接读取
        /// <c>m_TableReference.m_TableCollectionName</c> / <c>m_TableEntryReference.m_KeyId</c> / <c>m_Key</c> 后传入。
        /// 与已存值一致则跳过写入，避免每帧重绘反复写 <see cref="EditorPrefs"/>。
        /// </summary>
        /// <param name="tableRef">TableReference 的序列化字符串（"GUID:..." 或集合名）。</param>
        /// <param name="keyId">条目 KeyId（<see cref="SharedTableData.EmptyId"/> 表示未按 Id 引用）。</param>
        /// <param name="key">条目 Key 名称（可空）。</param>
        public static void SaveLocalizedFont(string tableRef, long keyId, string key)
        {
            key ??= string.Empty;
            bool empty = string.IsNullOrEmpty(tableRef) ||
                         (keyId == SharedTableData.EmptyId && string.IsNullOrEmpty(key));
            string value = empty ? string.Empty : $"{tableRef}\n{keyId}\n{key}";
            if (value != EditorPrefs.GetString(LocalizedFontKey, string.Empty))
                EditorPrefs.SetString(LocalizedFontKey, value);
        }
#endif
    }
}
