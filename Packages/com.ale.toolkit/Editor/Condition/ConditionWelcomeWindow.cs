using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 条件系统的概览 / 入口窗口：说明用法（内联字段 与 具名条件库两种用法），并给出通往
    /// <see cref="ConditionEditorWindow"/> 两个页签的入口。判定器清单见 Condition Editor 的「Condition Evaluators」页。
    /// </summary>
    public class ConditionWelcomeWindow : EditorWindow
    {
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

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "条件库（ConditionDatabase）：把条件配成有 id 的具名条目，多个系统按 id 共用同一份判断——" +
                "条目自带显示名 / 描述 / 图标（可直接喂给「未满足时」的玩家提示 UI）与模板驱动的自定义属性。" +
                "放在 Resources 下随启动自动注册，或由 ConditionDataManager.Register 显式注册；" +
                "求值经 ConditionResolver.Evaluate(id, ctx)（解析不到即判否）。",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开条件编辑器", GUILayout.Height(26)))
                    ConditionEditorWindow.Open();
                if (GUILayout.Button("新建条件库", GUILayout.Width(110), GUILayout.Height(26)))
                    ConditionEditorWindow.CreateDatabaseAsset();
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"已发现判定器：{ConditionEvaluatorCatalog.All.Count}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("查看全部判定器", GUILayout.Width(120), GUILayout.Height(22)))
                    ConditionEditorWindow.OpenEvaluators();
            }
            EditorGUILayout.LabelField(
                "（Condition Editor 的「Condition Evaluators」页：搜索 / 分类过滤 / 跳转源码 / 实现体检 / 配置引用交叉核对）",
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
