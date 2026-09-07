using UnityEditor;
using UnityEngine;

namespace Ale.GameplayTags.Editor
{
    /// <summary>
    /// 标签系统的概览窗口：说明用法、新建标签表、刷新目录、按层级缩进列出当前目录里的全部标签（含说明）。
    /// 标签在各字段处内联编辑（<c>[GameplayTagField]</c> / <see cref="GameplayTagContainer"/>），本窗口只作总览与入口。
    /// </summary>
    public class GameplayTagWelcomeWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/Ale Toolkit/GameplayTag System/Welcome", priority = 3101)]
        public static void Open()
        {
            var w = GetWindow<GameplayTagWelcomeWindow>("GameplayTag System");
            w.minSize = new Vector2(360f, 320f);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("标签系统 · GameplayTag System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "层级标签（如 Status.Debuff.Mental）：字段上打 [GameplayTagField]、或声明 GameplayTagContainer / GameplayTagRequirements 字段，" +
                "即可在 Inspector 内联选择。运行时用 GameplayTag.MatchesTag / GameplayTagCountContainer 匹配（持有后代即匹配祖先）；" +
                "标签表资产只是编辑器目录与校验来源，未登记的标签运行时照常匹配。",
                MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"目录标签：{GameplayTagEditorCatalog.All.Count}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("新建标签表...", GUILayout.Width(96)))
                    CreateTable();
                if (GUILayout.Button("刷新目录", GUILayout.Width(80)))
                    GameplayTagEditorCatalog.Rebuild();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            foreach (var t in GameplayTagEditorCatalog.All)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    float indent = (t.Depth - 1) * 14f;
                    GUILayout.Space(indent);
                    EditorGUILayout.LabelField(t.Leaf, GUILayout.Width(Mathf.Max(60f, 200f - indent)));
                    string comment = GameplayTagEditorCatalog.CommentOf(t);
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(comment) ? t.Name : $"{t.Name}  ·  {comment}", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void CreateTable()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建 Gameplay 标签表", "GameplayTagTable", "asset",
                "放在 Resources 目录下可于启动时自动登记");
            if (string.IsNullOrEmpty(path)) return;
            var table = CreateInstance<GameplayTagTable>();
            AssetDatabase.CreateAsset(table, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = table;
            GameplayTagEditorCatalog.Rebuild();
        }
    }
}
