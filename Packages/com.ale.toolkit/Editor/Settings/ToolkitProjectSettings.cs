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
    /// Ale Toolkit 的<b>项目级</b>编辑器设置（版本控制友好）。经 <see cref="ScriptableSingleton{T}"/> 持久化到
    /// <c>ProjectSettings/AleToolkitSettings.asset</c>（随仓库提交，用 Unity 原生序列化，资源引用按 GUID 存）。
    ///
    /// <para>目前承载预制体生成向导的全局字体设定（默认 TMP 字体 / 本地化字体）——这类选择通常全项目一致，
    /// 故入库共享，分发到别的工程时各自维护自己的一份。相对地，<b>每人自定</b>的偏好（界面语言、启动自动显示）
    /// 仍存 <see cref="EditorPrefs"/>，不进本文件。</para>
    ///
    /// <para>宏未启用时对应字段不参与编译；此时本类即为无字段的空设置容器。</para>
    /// </summary>
    [FilePath("ProjectSettings/AleToolkitSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ToolkitProjectSettings : ScriptableSingleton<ToolkitProjectSettings>
    {
#if ATK_TMP
        [SerializeField, Tooltip("向导生成 Prefab 时套用于所有 TMP 文本节点的默认字体（留空则用 TMP 内置默认字体）。")]
        internal TMP_FontAsset defaultTmpFont;
#endif
#if ATK_TMP && ATK_LOCALIZATION
        [SerializeField, Tooltip("向导生成 Prefab 时赋给 LocalizedFontEvent 组件的本地化字体资源。")]
        internal LocalizedTmpFont localizedFont = new LocalizedTmpFont();
#endif

        /// <summary>
        /// 将当前设置写回 <c>ProjectSettings/AleToolkitSettings.asset</c>（文本序列化，便于版本管理 diff）。
        /// 原生序列化会正确处理 <c>LocalizedTmpFont</c> 内嵌套的 <c>TableReference</c> / <c>TableEntryReference</c>
        /// 回调，无需再手工拆解字段。
        /// </summary>
        public void SaveSettings() => Save(true);
    }
}
