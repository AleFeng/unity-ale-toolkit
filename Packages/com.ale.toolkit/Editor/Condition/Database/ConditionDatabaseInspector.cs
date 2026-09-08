using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>条件库资产的 Inspector：顶部「在 Condition Editor 中编辑」按钮 + 概览，下方原始数据视图。</summary>
    [CustomEditor(typeof(ConditionDatabase))]
    public sealed class ConditionDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (ConditionDatabase)target;

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!db))
            {
                if (GUILayout.Button(Tr("在 Condition Editor 中编辑"), GUILayout.Height(30)))
                    ConditionEditorWindow.Open(db);
            }
            if (db)
                EditorGUILayout.LabelField(Fmt("条件 {0} · 模板 {1} · 枚举 {2}",
                    db.Conditions.Count, db.ConditionTemplates.Count, db.EnumTypesList.Count), EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(Tr("推荐通过上方编辑器窗口进行配置；下方为原始数据视图。"), MessageType.None);
            EditorGUILayout.Space(6);

            DrawDefaultInspector();
        }
    }
}
