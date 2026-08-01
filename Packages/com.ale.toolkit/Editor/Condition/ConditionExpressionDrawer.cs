using System;
using Ale.Condition;
using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// <see cref="ConditionExpression"/> 的真·PropertyDrawer：任意脚本声明该字段即在 Inspector 内联出现
    /// 两级 AND/OR 编辑器（组 / 项 / 参数增删重排、And·Or 切换、NOT、按 Category 分组的判定器下拉、按 schema 的动态参数区）。
    /// 全程 <see cref="SerializedProperty"/> 读写，Undo / 脏标记由 <see cref="SerializedObject"/> 自动处理。
    /// </summary>
    [CustomPropertyDrawer(typeof(ConditionExpression))]
    public class ConditionExpressionDrawer : PropertyDrawer
    {
        private const float Indent = 14f;
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        // ── 高度 ──────────────────────────────────────────────────────────────────
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => CountRows(property) * RowH;

        private static int CountRows(SerializedProperty property)
        {
            if (!property.isExpanded) return 1;
            int rows = 1 + 1; // 标题 + 控制行
            var groups = property.FindPropertyRelative("groups");
            for (int gi = 0; gi < groups.arraySize; gi++)
            {
                rows += 1; // 组标题
                var items = groups.GetArrayElementAtIndex(gi).FindPropertyRelative("items");
                for (int ii = 0; ii < items.arraySize; ii++)
                {
                    rows += 1; // 项标题
                    var ps = items.GetArrayElementAtIndex(ii).FindPropertyRelative("parameters");
                    for (int pi = 0; pi < ps.arraySize; pi++)
                        rows += ParamRows(ps.GetArrayElementAtIndex(pi));
                }
            }
            return rows;
        }

        private static int ParamRows(SerializedProperty pProp)
        {
            if (!pProp.FindPropertyRelative("isArray").boolValue) return 1;
            var type = (ConditionParamType)pProp.FindPropertyRelative("type").enumValueIndex;
            return 1 + BackingArray(pProp, type).arraySize; // 数量行 + 各元素行
        }

        // ── 绘制 ──────────────────────────────────────────────────────────────────
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var groups  = property.FindPropertyRelative("groups");
            var groupOp = property.FindPropertyRelative("groupOperator");

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
            string summary = $"{label.text}   ·  [{OpName(groupOp)}] {TotalItems(groups)} 条";
            property.isExpanded = EditorGUI.Foldout(row(left), property.isExpanded, summary, true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }

            // 结构性操作延迟到绘制后应用（每帧至多一个）
            bool addGroup = false;
            int  delGroup = -1, addItemGroup = -1, delItemGroup = -1, delItemIndex = -1;

            // 控制行：组间连接 + 添加组
            var ctrl = row(left + Indent);
            DrawOpToggle(new Rect(ctrl.x, ctrl.y, 130f, LH), groupOp, "组间");
            if (GUI.Button(new Rect(ctrl.xMax - 84f, ctrl.y, 84f, LH), "+ 添加组", EditorStyles.miniButton)) addGroup = true;

            for (int gi = 0; gi < groups.arraySize; gi++)
            {
                var gProp   = groups.GetArrayElementAtIndex(gi);
                var gNeg    = gProp.FindPropertyRelative("negate");
                var gItemOp = gProp.FindPropertyRelative("itemOperator");
                var items   = gProp.FindPropertyRelative("items");

                float gx = left + Indent;
                var gh = row(gx);
                EditorGUI.DrawRect(new Rect(gh.x, gh.y - 1, gh.width, LH + 2), ConditionSystemStyles.GroupBg);
                float cx = gh.x + 2;
                gNeg.boolValue = GUI.Toggle(new Rect(cx, gh.y, 40f, LH), gNeg.boolValue, "NOT", EditorStyles.miniButton); cx += 44;
                EditorGUI.LabelField(new Rect(cx, gh.y, 38f, LH), $"组{gi + 1}"); cx += 40;
                DrawOpToggle(new Rect(cx, gh.y, 120f, LH), gItemOp, "组内");
                if (GUI.Button(new Rect(gh.xMax - 128f, gh.y, 62f, LH), "+ 条件", EditorStyles.miniButton)) addItemGroup = gi;
                if (GUI.Button(new Rect(gh.xMax - 62f, gh.y, 62f, LH), "删除组", EditorStyles.miniButton)) delGroup = gi;

                for (int ii = 0; ii < items.arraySize; ii++)
                {
                    var itProp  = items.GetArrayElementAtIndex(ii);
                    var itNeg   = itProp.FindPropertyRelative("negate");
                    var keyProp = itProp.FindPropertyRelative("key");
                    var ps      = itProp.FindPropertyRelative("parameters");

                    float ix = gx + Indent;
                    var ih = row(ix);
                    float dx = ih.x;
                    itNeg.boolValue = GUI.Toggle(new Rect(dx, ih.y, 40f, LH), itNeg.boolValue, "NOT", EditorStyles.miniButton); dx += 44;

                    var keyRect = new Rect(dx, ih.y, Mathf.Max(60f, ih.xMax - dx - 60f), LH);
                    string curKey = keyProp.stringValue;
                    if (GUI.Button(keyRect, ConditionEvaluatorCatalog.DisplayNameOf(curKey), EditorStyles.popup))
                    {
                        var so = property.serializedObject;
                        string keyPath = keyProp.propertyPath, itemPath = itProp.propertyPath;
                        ConditionEvaluatorCatalog.BuildKeyMenu(curKey, newKey =>
                        {
                            so.Update();
                            so.FindProperty(keyPath).stringValue = newKey;
                            ConditionEvaluatorCatalog.SyncParameters(so.FindProperty(itemPath),
                                ConditionEvaluatorCatalog.Get(newKey)?.ParamSchema);
                            so.ApplyModifiedProperties();
                        }).DropDown(keyRect);
                    }
                    if (GUI.Button(new Rect(ih.xMax - 56f, ih.y, 56f, LH), "删除", EditorStyles.miniButton))
                    { delItemGroup = gi; delItemIndex = ii; }

                    var schema = ConditionEvaluatorCatalog.Get(keyProp.stringValue)?.ParamSchema;
                    for (int pi = 0; pi < ps.arraySize; pi++)
                    {
                        var pProp = ps.GetArrayElementAtIndex(pi);
                        var def   = FindDef(schema, pProp.FindPropertyRelative("id").stringValue);
                        DrawParam(pProp, def, ix + Indent, row);
                    }
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

        // ── 参数 ──────────────────────────────────────────────────────────────────
        private static void DrawParam(SerializedProperty pProp, ConditionParamDef def, float x, Func<float, Rect> row)
        {
            var    type    = (ConditionParamType)pProp.FindPropertyRelative("type").enumValueIndex;
            bool   isArray = pProp.FindPropertyRelative("isArray").boolValue;
            string label   = def != null ? def.label : pProp.FindPropertyRelative("id").stringValue;
            const float labelW = 92f;

            // 选项下拉：标量 Int/Enum + schema 提供了 choices（如「比较方式」）
            if (!isArray && def?.choices != null && def.choices.Length > 0
                && (type == ConditionParamType.Int || type == ConditionParamType.Enum))
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

        private static void DrawScalarField(Rect r, SerializedProperty pProp, ConditionParamType type, int index)
        {
            switch (type)
            {
                case ConditionParamType.String:
                {
                    var arr = pProp.FindPropertyRelative("strings"); Ensure(arr, index + 1);
                    var el = arr.GetArrayElementAtIndex(index);
                    el.stringValue = EditorGUI.TextField(r, el.stringValue);
                    break;
                }
                case ConditionParamType.Float:
                {
                    var arr = pProp.FindPropertyRelative("floats"); Ensure(arr, index + 1);
                    var el = arr.GetArrayElementAtIndex(index);
                    el.doubleValue = EditorGUI.DoubleField(r, el.doubleValue);
                    break;
                }
                case ConditionParamType.Bool:
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
        private static SerializedProperty BackingArray(SerializedProperty pProp, ConditionParamType type)
        {
            switch (type)
            {
                case ConditionParamType.String: return pProp.FindPropertyRelative("strings");
                case ConditionParamType.Float:  return pProp.FindPropertyRelative("floats");
                default:                        return pProp.FindPropertyRelative("ints");
            }
        }

        private static void Ensure(SerializedProperty arr, int size) { if (arr.arraySize < size) arr.arraySize = size; }

        private static ConditionParamDef FindDef(System.Collections.Generic.IReadOnlyList<ConditionParamDef> schema, string id)
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

        private static string OpName(SerializedProperty op)
            => (ConditionLogicOp)op.enumValueIndex == ConditionLogicOp.And ? "AND" : "OR";

        private static void DrawOpToggle(Rect r, SerializedProperty op, string prefix)
        {
            var v = (ConditionLogicOp)op.enumValueIndex;
            if (GUI.Button(r, $"{prefix}: {(v == ConditionLogicOp.And ? "并且 AND" : "或者 OR")}", EditorStyles.miniButton))
                op.enumValueIndex = v == ConditionLogicOp.And ? (int)ConditionLogicOp.Or : (int)ConditionLogicOp.And;
        }

        private static void InitGroup(SerializedProperty g)
        {
            g.FindPropertyRelative("negate").boolValue = false;
            g.FindPropertyRelative("itemOperator").enumValueIndex = (int)ConditionLogicOp.And;
            g.FindPropertyRelative("items").ClearArray();
        }

        private static void InitItem(SerializedProperty it)
        {
            it.FindPropertyRelative("negate").boolValue = false;
            it.FindPropertyRelative("key").stringValue = "";
            it.FindPropertyRelative("parameters").ClearArray();
        }
    }
}
