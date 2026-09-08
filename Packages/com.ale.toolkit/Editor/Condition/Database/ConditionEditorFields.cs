using System;
using System.Collections.Generic;
using Ale.Toolkit.Editor;
using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 条件编辑器的可复用 IMGUI 字段助手：ID 行（查重红框）、只读来源模板、按模板 schema 的自定义属性、
    /// 内联 <see cref="ConditionExpression"/>（交由 toolkit 的 <c>ConditionExpressionDrawer</c> 渲染）、校验摘要。
    /// 与效果侧的 <c>EffectEditorFields</c> 同构。
    /// </summary>
    public static class ConditionEditorFields
    {
        /// <summary>ID 输入行：重复 / 空 ID 红框高亮 + 下方提示；改动经 <paramref name="setId"/> 写回（内部 RecordUndo + MarkDirty）。</summary>
        public static void DrawIdField(IEditorContext ctx, string noun, string currentId, ICollection<string> duplicateIds, Action<string> setId)
        {
            bool isDup = duplicateIds != null && duplicateIds.Contains(string.IsNullOrWhiteSpace(currentId) ? string.Empty : currentId);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("ID");
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField(currentId ?? string.Empty, isDup ? ToolkitEditorStyles.RedField : EditorStyles.textField);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo(Fmt("修改{0} ID", noun));
                setId(newId);
                ctx.MarkDirty();
            }
            EditorGUILayout.EndHorizontal();

            if (isDup)
                EditorGUILayout.LabelField(Tr("⚠ ID 重复或为空"), ToolkitEditorStyles.StatusError);
        }

        /// <summary>只读「来源模板」行（创建后不可更改；为空显示「（无）」）。</summary>
        public static void DrawTemplateRefReadonly(string templateRef)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(Tr("来源模板"), string.IsNullOrEmpty(templateRef) ? Tr("（无）") : templateRef);
        }

        /// <summary>「自定义属性」区：逐条绘制实例来自模板 schema 的属性值（枚举字段按 <paramref name="templateAttrs"/> 解析枚举类型）。</summary>
        public static void DrawCustomAttributes(IConditionEditorContext ctx,
            List<AttributeEntry> values, List<AttributeDefinition> templateAttrs, string emptyHint)
        {
            EditorGUILayout.LabelField(Tr("自定义属性（来自模板 schema）"), ToolkitEditorStyles.Header);

            if (values == null || values.Count == 0)
            {
                EditorGUILayout.LabelField(emptyHint, ToolkitEditorStyles.Placeholder);
                return;
            }

            Dictionary<string, AttributeDefinition> defs = null;
            if (templateAttrs != null && templateAttrs.Count > 0)
            {
                defs = new Dictionary<string, AttributeDefinition>(templateAttrs.Count);
                foreach (var d in templateAttrs)
                    if (d != null && !string.IsNullOrEmpty(d.id)) defs[d.id] = d;
            }

            var db = ctx.Database;
            foreach (var entry in values)
            {
                if (entry == null || entry.value == null) continue;
                AttributeDefinition def = null;
                if (defs != null && !string.IsNullOrEmpty(entry.id)) defs.TryGetValue(entry.id, out def);
                var enumType = def != null && def.type == EFieldType.Enum && db != null ? db.GetEnumType(def.enumTypeRef) : null;
                AttributeFieldDrawer.Draw(ctx, entry.id, entry.value, enumType);
            }
        }

        /// <summary>
        /// 内联绘制一个 <see cref="ConditionExpression"/> 属性：经数据库 <c>SerializedObject</c> 由 <paramref name="resolve"/> 定位
        /// （越界 / 缺失返回 null 即降级为提示行），交由 toolkit <c>[CustomPropertyDrawer(ConditionExpression)]</c> 渲染；
        /// <paramref name="forceExpand"/> 为真时展开折叠。
        /// </summary>
        public static void InlineExpression(IEditorContext ctx, string label,
            Func<SerializedObject, SerializedProperty> resolve, bool forceExpand)
        {
            var so = ctx.Serialized;
            if (so == null)
            {
                EditorGUILayout.HelpBox(Tr("条件表达式编辑暂不可用（无序列化对象）。"), MessageType.None);
                return;
            }

            so.Update();
            var prop = resolve(so);
            if (prop == null)
            {
                EditorGUILayout.HelpBox(Tr("条件表达式编辑暂不可用。"), MessageType.None);
                return;
            }
            if (forceExpand) prop.isExpanded = true;

            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// 对单个条目做展示用校验（不改动数据）：表达式为空、条件项未选择判定器（空键在 <see cref="ConditionEngine"/> 恒判不通过）→ 错误；
        /// 表达式没有任何条件项 → 警告（求值恒通过，可能是漏配）。
        /// </summary>
        public static void ValidateForDisplay(ConditionEntry entry, List<string> errors, List<string> warnings)
        {
            if (entry == null) return;
            if (entry.expression == null) { errors.Add(Tr("表达式为空")); return; }

            var groups = entry.expression.groups;
            int items = 0;
            if (groups != null)
            {
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var list = groups[gi]?.items;
                    if (list == null) continue;
                    for (int ii = 0; ii < list.Count; ii++)
                    {
                        if (list[ii] == null) continue;
                        items++;
                        if (string.IsNullOrEmpty(list[ii].key))
                            errors.Add(Fmt("组{0} 第{1}项：未选择判定器（空键恒判不通过）", gi + 1, ii + 1));
                        else if (ConditionEvaluatorCatalog.Get(list[ii].key) == null)
                            errors.Add(Fmt("组{0} 第{1}项：判定器 '{2}' 没有对应实现", gi + 1, ii + 1, list[ii].key));
                    }
                }
            }
            if (items == 0)
                warnings.Add(Tr("表达式没有任何条件项，求值恒为通过。"));
        }

        /// <summary>校验摘要：错误（阻断导出）与警告（仅提示）各一条 HelpBox。</summary>
        public static void DrawValidationSummary(List<string> errors, List<string> warnings)
        {
            if ((errors == null || errors.Count == 0) && (warnings == null || warnings.Count == 0)) return;
            EditorGUILayout.Space(4);
            if (errors != null)   foreach (var m in errors)   EditorGUILayout.HelpBox(m, MessageType.Error);
            if (warnings != null) foreach (var m in warnings) EditorGUILayout.HelpBox(m, MessageType.Warning);
        }

        /// <summary>条件表达式的一行摘要：「N 组 · M 条 · AND/OR」。</summary>
        public static string Summary(ConditionExpression expr)
        {
            if (expr == null) return "—";
            int groups = expr.groups != null ? expr.groups.Count : 0;
            int items  = expr.TotalItemCount();
            return Fmt("{0} 组 · {1} 条 · {2}", groups, items,
                expr.groupOperator == ConditionLogicOp.Or ? "OR" : "AND");
        }
    }
}
