using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>效果库资产的 Inspector：顶部「在 Effect Editor 中编辑」按钮 + 概览，下方原始数据视图。</summary>
    [CustomEditor(typeof(EffectDatabase))]
    public sealed class EffectDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (EffectDatabase)target;

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!db))
            {
                if (GUILayout.Button(Tr("在 Effect Editor 中编辑"), GUILayout.Height(30)))
                    EffectEditorWindow.Open(db);
            }
            if (db)
                EditorGUILayout.LabelField(Fmt("效果 {0} · 模板 {1} · Gameplay 标签 {2} · 枚举 {3}",
                    db.Effects.Count, db.EffectTemplates.Count, db.GameplayTags.Count, db.EnumTypesList.Count), EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(Tr("推荐通过上方编辑器窗口进行配置；下方为原始数据视图。"), MessageType.None);
            EditorGUILayout.Space(6);

            DrawDefaultInspector();
        }
    }
}
