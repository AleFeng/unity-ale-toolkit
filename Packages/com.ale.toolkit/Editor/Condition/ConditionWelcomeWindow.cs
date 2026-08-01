using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 条件系统的设置 / 概览窗口：说明用法、刷新判定器目录、列出当前发现的判定器（按 Category 分组）。
    /// 条件系统本身<b>不需要独立的配置 EditorWindow</b>——条件在各字段处内联编辑；本窗口只作总览与设置入口。
    /// </summary>
    public class ConditionWelcomeWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/Ale Toolkit/Condition System/Welcome", priority = 2001)]
        public static void Open()
        {
            var w = GetWindow<ConditionWelcomeWindow>("Condition System");
            w.minSize = new Vector2(360f, 320f);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("条件系统 · Condition System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在任意 MonoBehaviour / ScriptableObject 声明一个 [SerializeField] ConditionExpression 字段，" +
                "即可在 Inspector 内联配置两级 AND/OR 条件。运行时用 ConditionEngine.Evaluate(expr, ctx) 判定；" +
                "扩展条件 = 实现 IConditionEvaluator 并打上 [ConditionEvaluator(\"Ns.Key\")]。",
                MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"已发现判定器：{ConditionEvaluatorCatalog.All.Count}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新目录", GUILayout.Width(80)))
                    ConditionEvaluatorCatalog.Rebuild();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            string lastCat = null;
            foreach (var ev in ConditionEvaluatorCatalog.All)
            {
                string cat = string.IsNullOrEmpty(ev.Category) ? "其它" : ev.Category;
                if (cat != lastCat)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(cat, EditorStyles.boldLabel);
                    lastCat = cat;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(ev.DisplayName, GUILayout.Width(160));
                    EditorGUILayout.LabelField(ev.Key, EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
