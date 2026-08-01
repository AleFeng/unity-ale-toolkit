using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 效果系统的设置 / 概览窗口：说明用法、刷新执行器目录、列出当前发现的执行器（按 Category 分组）。
    /// 效果系统本身<b>不需要独立的配置 EditorWindow</b>——效果在各字段处内联编辑；本窗口只作总览与设置入口。
    /// </summary>
    public class EffectWelcomeWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/Ale Toolkit/Effect System/Welcome", priority = 3001)]
        public static void Open()
        {
            var w = GetWindow<EffectWelcomeWindow>("Effect System");
            w.minSize = new Vector2(360f, 320f);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("效果系统 · Effect System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在任意 MonoBehaviour / ScriptableObject 声明一个 [SerializeField] EffectExpression 字段，" +
                "即可在 Inspector 内联配置阶段组效果（每项可挂可选条件门控）。运行时用 EffectRunner.Run(expr, ctx, phase) 执行；" +
                "扩展效果 = 实现 IEffectExecutor 并打上 [EffectExecutor(\"Ns.Key\")]。",
                MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"已发现执行器：{EffectExecutorCatalog.All.Count}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新目录", GUILayout.Width(80)))
                    EffectExecutorCatalog.Rebuild();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            string lastCat = null;
            foreach (var ex in EffectExecutorCatalog.All)
            {
                string cat = string.IsNullOrEmpty(ex.Category) ? "其它" : ex.Category;
                if (cat != lastCat)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(cat, EditorStyles.boldLabel);
                    lastCat = cat;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(ex.DisplayName, GUILayout.Width(160));
                    EditorGUILayout.LabelField(ex.Key, EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
