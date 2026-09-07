using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 效果系统的概览 / 入口窗口：说明两层用法（效果定义 GameplayEffect 层 / 阶段组执行层）、内置阶段常量，
    /// 并给出通往 <see cref="EffectEditorWindow"/> 两个页签的入口。
    /// 效果既可在各字段处内联编辑，也可集中放进效果库；执行器清单见 Effect Editor 的「Effect Executors」页。
    /// </summary>
    public class EffectWelcomeWindow : EditorWindow
    {
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

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "效果库（EffectDatabase）：所有上层系统共用的效果配置——效果条目（显示名 / 描述 / 图标 + 模板驱动的自定义属性 + GAS 式定义）、" +
                "效果模板、Gameplay 标签、枚举类型。放在 Resources 下随启动自动注册，或由 EffectDataManager.Register 显式注册；" +
                "上层系统以效果 id 引用（EditorEffectRefListDrawer），并各自实现 [EffectExecutor] 执行器。",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开效果编辑器", GUILayout.Height(26)))
                    EffectEditorWindow.Open();
                if (GUILayout.Button("新建效果库", GUILayout.Width(110), GUILayout.Height(26)))
                    EffectEditorWindow.CreateDatabaseAsset();
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"已发现执行器：{EffectExecutorCatalog.All.Count}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("查看全部执行器", GUILayout.Width(120), GUILayout.Height(22)))
                    EffectEditorWindow.OpenExecutors();
            }
            EditorGUILayout.LabelField(
                "（Effect Editor 的「Effect Executors」页：搜索 / 分类过滤 / 跳转源码 / 实现体检 / 配置引用交叉核对）",
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
