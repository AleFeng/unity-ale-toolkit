using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// <see cref="EffectExpression"/> 的真·PropertyDrawer：任意脚本声明该字段即在 Inspector 内联出现
    /// 阶段组编辑器（阶段组 / 效果项 / 参数增删、按 Category 分组的执行器下拉、按 schema 的动态参数区、
    /// 以及每项<b>可选条件门控</b>——内嵌渲染 <c>ConditionExpression</c>，其 UI 由条件系统的 PropertyDrawer 自动提供）。
    /// 全程 <see cref="SerializedProperty"/> 读写，Undo / 脏标记由 <see cref="SerializedObject"/> 自动处理。
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectExpression))]
    public class EffectExpressionDrawer : PropertyDrawer
    {
        private const float Indent = 14f;
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        // ── 高度（固定行按 RowH 计数 + 每项 gate 的像素高单独累加）──────────────────────
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return RowH;

            int   rows  = 1 + 1; // 标题 + 控制行
            float extra = 0f;    // 各项门控的像素高

            var groups = property.FindPropertyRelative("groups");
            for (int gi = 0; gi < groups.arraySize; gi++)
            {
                rows += 1; // 组头
                var items = groups.GetArrayElementAtIndex(gi).FindPropertyRelative("items");
                for (int ii = 0; ii < items.arraySize; ii++)
                {
                    var it = items.GetArrayElementAtIndex(ii);
                    rows += 1; // 项头
                    var ps = it.FindPropertyRelative("parameters");
                    for (int pi = 0; pi < ps.arraySize; pi++)
                        rows += ParamRows(ps.GetArrayElementAtIndex(pi));
                    extra += EditorGUI.GetPropertyHeight(it.FindPropertyRelative("gate"), true) + V; // 门控
                }
            }
            return rows * RowH + extra;
        }

        private static int ParamRows(SerializedProperty pProp)
        {
            if (!pProp.FindPropertyRelative("isArray").boolValue) return 1;
            var type = (EffectParamType)pProp.FindPropertyRelative("type").enumValueIndex;
            return 1 + BackingArray(pProp, type).arraySize; // 数量行 + 各元素行
        }

        // ── 绘制 ──────────────────────────────────────────────────────────────────
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var groups = property.FindPropertyRelative("groups");

            float y     = position.y;
            float left  = position.x;
            float right = position.xMax;

            Func<float, Rect> row = ix =>
            {
                var r = new Rect(ix, y, right - ix, LH);
                y += RowH;
                return r;
            };

            // 标题（折叠 + 摘要）
            string summary = $"{label.text}   ·  {TotalItems(groups)} 效果";
            property.isExpanded = EditorGUI.Foldout(row(left), property.isExpanded, summary, true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }

            // 结构性操作延迟到绘制后应用（每帧至多一个）
            bool addGroup = false;
            int  delGroup = -1, addItemGroup = -1, delItemGroup = -1, delItemIndex = -1;

            // 控制行：添加阶段组
            var ctrl = row(left + Indent);
            EditorGUI.LabelField(new Rect(ctrl.x, ctrl.y, 180f, LH), "阶段组（按序执行）", EditorStyles.miniLabel);
            if (GUI.Button(new Rect(ctrl.xMax - 96f, ctrl.y, 96f, LH), "+ 添加阶段组", EditorStyles.miniButton)) addGroup = true;

            for (int gi = 0; gi < groups.arraySize; gi++)
            {
                var gProp = groups.GetArrayElementAtIndex(gi);
                var phase = gProp.FindPropertyRelative("phase");
                var items = gProp.FindPropertyRelative("items");

                float gx = left + Indent;
                var gh = row(gx);
                EditorGUI.DrawRect(new Rect(gh.x, gh.y - 1, gh.width, LH + 2), EffectSystemStyles.GroupBg);
                float cx = gh.x + 4;
                EditorGUI.LabelField(new Rect(cx, gh.y, 46f, LH), $"阶段{gi + 1}"); cx += 48;
                EditorGUI.LabelField(new Rect(cx, gh.y, 32f, LH), "时机"); cx += 34;
                float phaseRight = gh.xMax - 130f;
                phase.stringValue = EditorGUI.TextField(new Rect(cx, gh.y, Mathf.Max(60f, phaseRight - cx), LH), phase.stringValue);
                if (GUI.Button(new Rect(gh.xMax - 126f, gh.y, 60f, LH), "+ 效果", EditorStyles.miniButton)) addItemGroup = gi;
                if (GUI.Button(new Rect(gh.xMax - 62f, gh.y, 62f, LH), "删除组", EditorStyles.miniButton)) delGroup = gi;

                for (int ii = 0; ii < items.arraySize; ii++)
                {
                    var itProp  = items.GetArrayElementAtIndex(ii);
                    var keyProp = itProp.FindPropertyRelative("key");
                    var ps      = itProp.FindPropertyRelative("parameters");
                    var gate    = itProp.FindPropertyRelative("gate");

                    float ix = gx + Indent;
                    var ih = row(ix);
                    float dx = ih.x;

                    var keyRect = new Rect(dx, ih.y, Mathf.Max(60f, ih.xMax - dx - 60f), LH);
                    string curKey = keyProp.stringValue;
                    if (GUI.Button(keyRect, EffectExecutorCatalog.DisplayNameOf(curKey), EditorStyles.popup))
                    {
                        var so = property.serializedObject;
                        string keyPath = keyProp.propertyPath, itemPath = itProp.propertyPath;
                        EffectExecutorCatalog.BuildKeyMenu(curKey, newKey =>
                        {
                            so.Update();
                            so.FindProperty(keyPath).stringValue = newKey;
                            EffectExecutorCatalog.SyncParameters(so.FindProperty(itemPath),
                                EffectExecutorCatalog.Get(newKey)?.ParamSchema);
                            so.ApplyModifiedProperties();
                        }).DropDown(keyRect);
                    }
                    if (GUI.Button(new Rect(ih.xMax - 56f, ih.y, 56f, LH), "删除", EditorStyles.miniButton))
                    { delItemGroup = gi; delItemIndex = ii; }

                    var schema = EffectExecutorCatalog.Get(keyProp.stringValue)?.ParamSchema;
                    for (int pi = 0; pi < ps.arraySize; pi++)
                    {
                        var pProp = ps.GetArrayElementAtIndex(pi);
                        var def   = FindDef(schema, pProp.FindPropertyRelative("id").stringValue);
                        DrawParam(pProp, def, ix + Indent, row);
                    }

                    // 门控条件（可选）：内联渲染内嵌的 ConditionExpression，UI 由条件系统 PropertyDrawer 提供。
                    float gateH = EditorGUI.GetPropertyHeight(gate, true);
                    var gateRect = new Rect(ix + Indent, y, right - (ix + Indent), gateH);
                    y += gateH + V;
                    EditorGUI.PropertyField(gateRect, gate, new GUIContent("门控条件（可选）"), true);
                }
            }

            // 应用延迟操作（优先级：加组 > 删组 > 加项 > 删项）
            if (addGroup) { groups.arraySize++; InitGroup(groups.GetArrayElementAtIndex(groups.arraySize - 1)); }
            else if (delGroup >= 0) groups.DeleteArrayElementAtIndex(delGroup);
            else if (addItemGroup >= 0)
            {
                var it = groups.GetArrayElementAtIndex(addItemGroup).FindPropertyRelative("items");
                it.arraySize++; InitItem(it.GetArrayElementAtIndex(it.arraySize - 1));
            }
            else if (delItemGroup >= 0 && delItemIndex >= 0)
                groups.GetArrayElementAtIndex(delItemGroup).FindPropertyRelative("items").DeleteArrayElementAtIndex(delItemIndex);

            EditorGUI.EndProperty();
        }

        // ── 参数（镜像 ConditionExpressionDrawer.DrawParam）──────────────────────────
        private static void DrawParam(SerializedProperty pProp, EffectParamDef def, float x, Func<float, Rect> row)
        {
            var    type    = (EffectParamType)pProp.FindPropertyRelative("type").enumValueIndex;
            bool   isArray = pProp.FindPropertyRelative("isArray").boolValue;
            string label   = def != null ? def.label : pProp.FindPropertyRelative("id").stringValue;
            const float labelW = 92f;

            // 选项下拉：标量 Int/Enum + schema 提供了 choices（如「目标选择：随机/最近/最远」）
            if (!isArray && def?.choices != null && def.choices.Length > 0
                && (type == EffectParamType.Int || type == EffectParamType.Enum))
            {
                var rc = row(x);
                EditorGUI.LabelField(new Rect(rc.x, rc.y, labelW, LH), label);
                var arr = pProp.FindPropertyRelative("ints"); Ensure(arr, 1);
                var el = arr.GetArrayElementAtIndex(0);
                int cur = Mathf.Clamp((int)el.longValue, 0, def.choices.Length - 1);
                el.longValue = EditorGUI.Popup(new Rect(rc.x + labelW, rc.y, rc.width - labelW, LH), cur, def.choices);
                return;
            }

            if (!isArray)
            {
                var r = row(x);
                EditorGUI.LabelField(new Rect(r.x, r.y, labelW, LH), label);
                DrawScalarField(new Rect(r.x + labelW, r.y, r.width - labelW, LH), pProp, type, 0);
            }
            else
            {
                var backing = BackingArray(pProp, type);
                var r = row(x);
                EditorGUI.LabelField(new Rect(r.x, r.y, labelW, LH), label + " []");
                int size    = backing.arraySize;
                int newSize = Mathf.Max(0, EditorGUI.DelayedIntField(new Rect(r.x + labelW, r.y, 90f, LH), size));
                if (newSize != size) backing.arraySize = newSize;

                for (int i = 0; i < backing.arraySize; i++)
                {
                    var er = row(x + Indent);
                    EditorGUI.LabelField(new Rect(er.x, er.y, 24f, LH), i.ToString());
                    DrawScalarField(new Rect(er.x + 26f, er.y, er.width - 26f, LH), pProp, type, i);
                }
            }
        }

        private static void DrawScalarField(Rect r, SerializedProperty pProp, EffectParamType type, int index)
        {
            switch (type)
            {
                case EffectParamType.String:
                {
                    var arr = pProp.FindPropertyRelative("strings"); Ensure(arr, index + 1);
                    var el = arr.GetArrayElementAtIndex(index);
                    el.stringValue = EditorGUI.TextField(r, el.stringValue);
                    break;
                }
                case EffectParamType.Float:
                {
                    var arr = pProp.FindPropertyRelative("floats"); Ensure(arr, index + 1);
                    var el = arr.GetArrayElementAtIndex(index);
                    el.doubleValue = EditorGUI.DoubleField(r, el.doubleValue);
                    break;
                }
                case EffectParamType.Bool:
                {
                    var arr = pProp.FindPropertyRelative("ints"); Ensure(arr, index + 1);
                    var el = arr.GetArrayElementAtIndex(index);
                    el.longValue = EditorGUI.Toggle(r, el.longValue != 0) ? 1 : 0;
                    break;
                }
                default: // Int / Enum（Enum 暂以原始整数值编辑；宿主可后续注入枚举下拉）
                {
                    var arr = pProp.FindPropertyRelative("ints"); Ensure(arr, index + 1);
                    var el = arr.GetArrayElementAtIndex(index);
                    el.longValue = EditorGUI.LongField(r, el.longValue);
                    break;
                }
            }
        }

        // ── 小工具 ────────────────────────────────────────────────────────────────
        private static SerializedProperty BackingArray(SerializedProperty pProp, EffectParamType type)
        {
            switch (type)
            {
                case EffectParamType.String: return pProp.FindPropertyRelative("strings");
                case EffectParamType.Float:  return pProp.FindPropertyRelative("floats");
                default:                     return pProp.FindPropertyRelative("ints");
            }
        }

        private static void Ensure(SerializedProperty arr, int size) { if (arr.arraySize < size) arr.arraySize = size; }

        private static EffectParamDef FindDef(IReadOnlyList<EffectParamDef> schema, string id)
        {
            if (schema == null) return null;
            for (int i = 0; i < schema.Count; i++)
                if (schema[i].id == id) return schema[i];
            return null;
        }

        private static int TotalItems(SerializedProperty groups)
        {
            int n = 0;
            for (int gi = 0; gi < groups.arraySize; gi++)
                n += groups.GetArrayElementAtIndex(gi).FindPropertyRelative("items").arraySize;
            return n;
        }

        private static void InitGroup(SerializedProperty g)
        {
            g.FindPropertyRelative("phase").stringValue = "";
            g.FindPropertyRelative("items").ClearArray();
        }

        private static void InitItem(SerializedProperty it)
        {
            it.FindPropertyRelative("key").stringValue = "";
            it.FindPropertyRelative("parameters").ClearArray();
            // 重置内嵌门控条件（arraySize++ 会复制上一元素，需清空 gate 的 groups）。
            var gate = it.FindPropertyRelative("gate");
            gate.FindPropertyRelative("groupOperator").enumValueIndex = 0; // And
            gate.FindPropertyRelative("groups").ClearArray();
        }
    }
}
