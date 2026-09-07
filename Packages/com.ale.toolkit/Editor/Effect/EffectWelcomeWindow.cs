using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 效果系统的设置 / 概览窗口：说明两层用法（效果定义 GameplayEffect 层 / 阶段组执行层）、内置阶段常量、
    /// 刷新执行器目录、列出当前发现的执行器（按 Category 分组）。
    /// 效果系统本身<b>不需要独立的配置 EditorWindow</b>——效果在各字段处内联编辑；本窗口只作总览与设置入口。
    /// </summary>
    public class EffectWelcomeWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/Ale Toolkit/Effect System/Welcome", priority = 3001)]
        public static void Open()
        {
            var w = GetWindow<EffectWelcomeWindow>("Effect System");
            w.minSize = new Vector2(360f, 360f);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("效果系统 · Effect System", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "效果定义（EffectDefinition，GAS GameplayEffect）：声明 [SerializeField] EffectDefinition 字段或新建 Ale/Effect/Effect Definition 资产，" +
                "在 Inspector 分节配置时长策略 / 周期 / 叠加 / 标签 / 施加条件与概率 / 修饰器 / 各阶段执行。" +
                "运行时由 EffectContainer.ApplyEffect(def, ctx) 施加、Tick(delta, ctx) 推进；持续效果的修饰器经 CollectModifiers 汇入宿主属性，" +
                "瞬时 / 周期修饰器经 IEffectAttributeSink 永久落地。\n\n" +
                "执行层（EffectExpression）：阶段组 + 效果项（可挂条件门控）。扩展效果 = 实现 IEffectExecutor 并打上 [EffectExecutor(\"Ns.Key\")]。",
                MessageType.Info);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("内置阶段：" + string.Join(" / ", EffectPhases.All) +
                                       "（空阶段组视为通配，Normalize() 会改写为 onApply；宿主自定义阶段经 RunPhase 触发）", EditorStyles.wordWrappedMiniLabel);

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
