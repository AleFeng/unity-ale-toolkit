using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Ale.GameplayTags.Editor
{
    /// <summary>
    /// <see cref="GameplayTagTable"/> 的 Inspector：可排序条目列表（标签名 + 说明；提交时合法即归一，非法红底）、
    /// 逐项校验结果（非法 / 重复为错误，仅大小写不同为警告）、按名排序、登记到运行时注册表（编辑期预览下拉）、刷新目录。
    /// </summary>
    [CustomEditor(typeof(GameplayTagTable))]
    public sealed class GameplayTagTableEditor : UnityEditor.Editor
    {
        private ReorderableList _list;
        private SerializedProperty _entries;
        private readonly List<string> _messages = new List<string>();

        private void OnEnable()
        {
            _entries = serializedObject.FindProperty("entries");
            _list = new ReorderableList(serializedObject, _entries, true, true, true, true)
            {
                drawHeaderCallback = r =>
                {
                    float half = (r.width - 14f) * 0.5f;
                    EditorGUI.LabelField(new Rect(r.x + 14f, r.y, half, r.height), "标签名（点分层级）");
                    EditorGUI.LabelField(new Rect(r.x + 14f + half, r.y, half, r.height), "说明");
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var e       = _entries.GetArrayElementAtIndex(index);
                    var name    = e.FindPropertyRelative("name");
                    var comment = e.FindPropertyRelative("comment");
                    rect.y      += 1f;
                    rect.height  = EditorGUIUtility.singleLineHeight;
                    float half   = rect.width * 0.5f;

                    var nameRect = new Rect(rect.x, rect.y, half - 4f, rect.height);
                    EditorGUI.BeginChangeCheck();
                    string typed = EditorGUI.DelayedTextField(nameRect, name.stringValue);
                    if (EditorGUI.EndChangeCheck())
                        name.stringValue = GameplayTag.Normalize(typed) ?? typed;
                    if (!string.IsNullOrEmpty(name.stringValue) && !GameplayTag.IsValidName(name.stringValue))
                        EditorGUI.DrawRect(nameRect, GameplayTagSystemStyles.InvalidBg);

                    comment.stringValue = EditorGUI.TextField(new Rect(rect.x + half, rect.y, half, rect.height), comment.stringValue);
                },
                onAddCallback = l =>
                {
                    _entries.arraySize++;
                    var e = _entries.GetArrayElementAtIndex(_entries.arraySize - 1);
                    e.FindPropertyRelative("name").stringValue    = string.Empty;   // arraySize++ 会复制上一元素，需清空
                    e.FindPropertyRelative("comment").stringValue = string.Empty;
                },
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var table = (GameplayTagTable)target;

            EditorGUILayout.HelpBox(
                "声明本项目的 Gameplay 标签（点分层级，如 Status.Debuff.Mental；祖先自动登记，无需单独列出）。" +
                "放在 Resources 目录下即于启动时自动登记；编辑器下拉树会扫描工程内全部标签表。",
                MessageType.Info);

            _list.DoLayoutList();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("按名排序"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(table, "排序标签表");
                    table.Entries.Sort((a, b) => string.CompareOrdinal(a?.name, b?.name));
                    EditorUtility.SetDirty(table);
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button(new GUIContent("登记到注册表", "把本表登记进运行时默认注册表（编辑期预览；播放时会自动重建）")))
                {
                    int n = GameplayTagRuntime.Register(table);
                    Debug.Log($"[GameplayTagTable] 已登记 '{table.name}'：新增 {n} 条，注册表共 {GameplayTagRegistry.Default.Count} 条。", table);
                }
                if (GUILayout.Button("刷新目录"))
                    GameplayTagEditorCatalog.Rebuild();
            }

            _messages.Clear();
            bool ok = table.Validate(_messages);
            if (_messages.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", _messages), ok ? MessageType.Warning : MessageType.Error);
            else
                EditorGUILayout.LabelField($"共 {table.Entries.Count} 条，校验通过。", EditorStyles.miniLabel);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
