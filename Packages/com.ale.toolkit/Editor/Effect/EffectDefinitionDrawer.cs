using System;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// <see cref="EffectDefinition"/> 的 PropertyDrawer（IMGUI）：折叠标题带摘要，展开为分节——
    /// 基本 / 时长与周期（按策略显隐）/ 叠加（非瞬时）/ 标签 / 施加（要求、概率、条件）/ 修饰器 / 执行。
    /// 嵌套的标签容器 / 要求 / 条件 / 表达式 / 幅度经 <see cref="EditorGUI.PropertyField(Rect, SerializedProperty, GUIContent, bool)"/>
    /// 自动落到各自的绘制器。<b>高度与绘制共用同一套布局代码</b>（<see cref="Layout"/>，量算模式不画），保证不错位。
    /// 结构性操作（增删修饰器）延迟到绘制后应用。
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectDefinition))]
    public sealed class EffectDefinitionDrawer : PropertyDrawer
    {
        private const float Indent = 14f;
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        private static readonly GUIContent DurationLabel   = new GUIContent("时长", "宿主时间单位（世界日 / 秒…）");
        private static readonly GUIContent PeriodLabel     = new GUIContent("周期", "≤ 0 = 非周期；每周期把修饰器永久落地并执行 onPeriod");
        private static readonly GUIContent RequireLabel    = new GUIContent("施加标签要求");
        private static readonly GUIContent OngoingLabel    = new GUIContent("持续标签要求", "不满足时抑制：修饰器不汇流、周期冻结、撤回授予标签；时长照走");
        private static readonly GUIContent ConditionLabel  = new GUIContent("施加条件（可选）");
        private static readonly GUIContent ExecutionsLabel = new GUIContent("执行阶段", "阶段：onApply / onStack / onPeriod / onExpire / onRemove");
        private static readonly GUIContent ChanceLabel     = new GUIContent("施加概率");
        private static readonly GUIContent AssetTagsLabel  = new GUIContent("资产标签", "描述本效果：免疫 / 按标签移除的匹配对象");
        private static readonly GUIContent GrantedLabel    = new GUIContent("授予标签", "激活期间授予目标（每实例一次）");
        private static readonly GUIContent RemoveLabel     = new GUIContent("移除带标签的效果", "成功施加后移除目标身上命中的其它效果");
        private static readonly GUIContent ImmunityLabel   = new GUIContent("授予免疫标签", "激活期间：新效果 assetTags 命中任一 → 免疫");
        private static readonly GUIContent CueLabel        = new GUIContent("线索标签", "随 Applied / Executed / Removed 通知表现层");
        private static readonly GUIContent ModifierLabel   = GUIContent.none;

        /// <summary>布局游标：量算模式只推进 y，绘制模式返回可画的矩形。</summary>
        private sealed class Cursor
        {
            public float X, Y, Width;
            public bool Draw;
            public Rect Row(float h = -1f)
            {
                if (h < 0f) h = LH;
                var r = new Rect(X, Y, Width, h);
                Y += h + V;
                return r;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var c = new Cursor { X = 0f, Y = 0f, Width = 1000f, Draw = false };
            Layout(c, property, label);
            return c.Y - V;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var c = new Cursor { X = position.x, Y = position.y, Width = position.width, Draw = true };
            Layout(c, property, label);
            EditorGUI.EndProperty();
        }

        // ── 共用布局 ───────────────────────────────────────────────────────────────
        private static void Layout(Cursor c, SerializedProperty p, GUIContent label)
        {
            var idProp   = p.FindPropertyRelative("id");
            var policy   = p.FindPropertyRelative("durationPolicy");
            var mods     = p.FindPropertyRelative("modifiers");
            var execs    = p.FindPropertyRelative("executions");
            var isInstant = (EDurationPolicy)policy.enumValueIndex == EDurationPolicy.Instant;
            var hasDuration = (EDurationPolicy)policy.enumValueIndex == EDurationPolicy.HasDuration;

            // 标题
            string summary = $"{label.text}   ·  {(string.IsNullOrEmpty(idProp.stringValue) ? "(无 id)" : idProp.stringValue)}" +
                             $"  ·  {policy.enumDisplayNames[Mathf.Clamp(policy.enumValueIndex, 0, policy.enumDisplayNames.Length - 1)]}" +
                             $"  ·  {mods.arraySize} 修饰  ·  {TotalItems(execs)} 执行";
            var head = c.Row();
            if (c.Draw) p.isExpanded = EditorGUI.Foldout(head, p.isExpanded, summary, true);
            if (!p.isExpanded) return;

            float left = c.X;
            c.X += Indent;
            c.Width -= Indent;

            int addModifier = 0, delModifier = -1;

            // ① 基本（宿主实体自带 id / 名称并同步进定义时可经钩子隐藏）
            if (EffectDefinitionDrawerHooks.ShowIdentityFields)
            {
                Section(c, "基本");
                Field(c, p.FindPropertyRelative("id"), "id");
                Field(c, p.FindPropertyRelative("displayName"), "显示名");
            }

            // ② 时长与周期
            Section(c, "时长与周期");
            Field(c, policy, "时长策略");
            if (hasDuration) Nested(c, p.FindPropertyRelative("duration"), DurationLabel);
            if (!isInstant)
            {
                Nested(c, p.FindPropertyRelative("period"), PeriodLabel);
                Field(c, p.FindPropertyRelative("executePeriodicOnApplication"), "施加即结算周期");
            }

            // ③ 叠加
            if (!isInstant)
            {
                Section(c, "叠加");
                var stacking = p.FindPropertyRelative("stackingType");
                Field(c, stacking, "叠加类型");
                if ((EEffectStackingType)stacking.enumValueIndex != EEffectStackingType.None)
                {
                    Field(c, p.FindPropertyRelative("stackLimit"), "层数上限（≤0 不限）");
                    Field(c, p.FindPropertyRelative("stackDurationRefreshPolicy"), "叠层刷新时长");
                    Field(c, p.FindPropertyRelative("stackPeriodResetPolicy"), "叠层重置周期");
                    Field(c, p.FindPropertyRelative("stackExpirationPolicy"), "到期处理");
                }
            }

            // ④ 标签
            Section(c, "标签");
            Nested(c, p.FindPropertyRelative("assetTags"), AssetTagsLabel);
            if (!isInstant)
            {
                Nested(c, p.FindPropertyRelative("grantedTags"), GrantedLabel);
                Nested(c, p.FindPropertyRelative("grantedApplicationImmunityTags"), ImmunityLabel);
            }
            Nested(c, p.FindPropertyRelative("removeEffectsWithTags"), RemoveLabel);
            Nested(c, p.FindPropertyRelative("cueTags"), CueLabel);

            // ⑤ 施加
            Section(c, "施加");
            Nested(c, p.FindPropertyRelative("applicationTagRequirements"), RequireLabel);
            if (!isInstant) Nested(c, p.FindPropertyRelative("ongoingTagRequirements"), OngoingLabel);
            var chance = c.Row();
            if (c.Draw) EditorGUI.Slider(chance, p.FindPropertyRelative("chanceToApply"), 0f, 1f, ChanceLabel);
            Nested(c, p.FindPropertyRelative("applicationCondition"), ConditionLabel);

            // ⑥ 修饰器
            var modHead = Section(c, $"修饰器（{mods.arraySize}）");
            if (c.Draw && GUI.Button(new Rect(modHead.xMax - 96f, modHead.y, 96f, LH), "+ 添加修饰器", EditorStyles.miniButton)) addModifier++;
            for (int i = 0; i < mods.arraySize; i++)
            {
                var el = mods.GetArrayElementAtIndex(i);
                float h = EditorGUI.GetPropertyHeight(el, ModifierLabel, true);
                var r = c.Row(h);
                if (c.Draw)
                {
                    EditorGUI.PropertyField(new Rect(r.x, r.y, r.width - 30f, h), el, ModifierLabel, true);
                    if (GUI.Button(new Rect(r.xMax - 26f, r.y, 26f, LH), "×", EditorStyles.miniButton)) delModifier = i;
                }
            }

            // ⑦ 执行
            Section(c, "执行");
            Nested(c, execs, ExecutionsLabel);

            c.X = left;
            c.Width += Indent;

            // 延迟应用结构操作（每帧至多一个）
            if (!c.Draw) return;
            if (addModifier > 0)
            {
                mods.arraySize++;
                InitModifier(mods.GetArrayElementAtIndex(mods.arraySize - 1));
            }
            else if (delModifier >= 0)
            {
                mods.DeleteArrayElementAtIndex(delModifier);
            }
        }

        private static Rect Section(Cursor c, string title)
        {
            var r = c.Row();
            if (c.Draw)
            {
                EditorGUI.DrawRect(new Rect(r.x, r.y - 1, r.width, LH + 2), EffectSystemStyles.GroupBg);
                EditorGUI.LabelField(new Rect(r.x + 4, r.y, r.width - 8, LH), title, EditorStyles.miniBoldLabel);
            }
            return r;
        }

        private static void Field(Cursor c, SerializedProperty prop, string label)
        {
            var r = c.Row();
            if (c.Draw) EditorGUI.PropertyField(r, prop, new GUIContent(label), false);
        }

        private static void Nested(Cursor c, SerializedProperty prop, GUIContent label)
        {
            float h = EditorGUI.GetPropertyHeight(prop, label, true);
            var r = c.Row(h);
            if (c.Draw) EditorGUI.PropertyField(r, prop, label, true);
        }

        private static int TotalItems(SerializedProperty expression)
        {
            var groups = expression.FindPropertyRelative("groups");
            int n = 0;
            for (int gi = 0; gi < groups.arraySize; gi++)
                n += groups.GetArrayElementAtIndex(gi).FindPropertyRelative("items").arraySize;
            return n;
        }

        // arraySize++ 会复制上一元素，需清零。
        private static void InitModifier(SerializedProperty m)
        {
            m.FindPropertyRelative("attributeId").stringValue = string.Empty;
            m.FindPropertyRelative("operation").enumValueIndex = 0;
            var mag = m.FindPropertyRelative("magnitude");
            mag.FindPropertyRelative("kind").enumValueIndex = 0;
            mag.FindPropertyRelative("baseValue").floatValue = 0f;
            mag.FindPropertyRelative("perLevel").floatValue = 0f;
            mag.FindPropertyRelative("attributeId").stringValue = string.Empty;
            mag.FindPropertyRelative("captureFrom").enumValueIndex = 0;
            mag.FindPropertyRelative("coefficient").floatValue = 1f;
            mag.FindPropertyRelative("preAdd").floatValue = 0f;
            mag.FindPropertyRelative("postAdd").floatValue = 0f;
            mag.FindPropertyRelative("setByCallerKey").stringValue = string.Empty;
        }
    }
}
