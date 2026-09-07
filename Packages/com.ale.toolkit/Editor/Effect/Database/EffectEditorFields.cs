using System;
using System.Collections.Generic;
using Ale.Toolkit.Editor;
using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 效果编辑器的可复用 IMGUI 字段助手：ID 行（查重红框）、只读来源模板、按模板 schema 的自定义属性、
    /// 内联 <see cref="EffectDefinition"/>（交由 toolkit 绘制器渲染，隐藏被条目同步的 id / 显示名两行）、校验摘要。
    /// </summary>
    public static class EffectEditorFields
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
        public static void DrawCustomAttributes(IEffectEditorContext ctx,
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
        /// 内联绘制一个 <see cref="EffectDefinition"/> 属性：经数据库 <c>SerializedObject</c> 由 <paramref name="resolve"/> 定位
        /// （越界 / 缺失返回 null 即降级为提示行），交由 toolkit <c>[CustomPropertyDrawer(EffectDefinition)]</c> 渲染；
        /// 绘制期间隐藏定义内的 id / 显示名两行；<paramref name="forceExpand"/> 为真时展开折叠。
        /// </summary>
        public static void InlineDefinition(IEditorContext ctx, string label,
            Func<SerializedObject, SerializedProperty> resolve, bool forceExpand)
        {
            var so = ctx.Serialized;
            if (so == null)
            {
                EditorGUILayout.HelpBox(Tr("效果定义编辑暂不可用（无序列化对象）。"), MessageType.None);
                return;
            }

            so.Update();
            var prop = resolve(so);
            if (prop == null)
            {
                EditorGUILayout.HelpBox(Tr("效果定义编辑暂不可用。"), MessageType.None);
                return;
            }
            if (forceExpand) prop.isExpanded = true;

            bool prevShow = EffectDefinitionDrawerHooks.ShowIdentityFields;
            EffectDefinitionDrawerHooks.ShowIdentityFields = false;
            try
            {
                EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
            }
            finally
            {
                EffectDefinitionDrawerHooks.ShowIdentityFields = prevShow;
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>对「已同步 id / 显示名并归一」的定义副本做校验（不改动数据），返回错误与警告（警告已去掉前缀）。</summary>
        public static void ValidateForDisplay(EffectEntry entry, List<string> errors, List<string> warnings)
        {
            if (entry?.definition == null) { errors.Add(Tr("定义为空")); return; }
            var def = entry.definition.Clone();
            def.id          = entry.id;
            def.displayName = entry.PlainName();
            def.Normalize();
            var msgs = new List<string>();
            def.Validate(msgs);
            foreach (var m in msgs)
            {
                if (EffectDefinition.IsWarning(m)) warnings.Add(m.Substring(EffectDefinition.WarningPrefix.Length).Trim());
                else                               errors.Add(m);
            }
        }

        /// <summary>校验摘要：错误（阻断导出）与警告（仅提示）各一条 HelpBox。</summary>
        public static void DrawValidationSummary(List<string> errors, List<string> warnings)
        {
            if ((errors == null || errors.Count == 0) && (warnings == null || warnings.Count == 0)) return;
            EditorGUILayout.Space(4);
            if (errors != null)   foreach (var m in errors)   EditorGUILayout.HelpBox(m, MessageType.Error);
            if (warnings != null) foreach (var m in warnings) EditorGUILayout.HelpBox(m, MessageType.Warning);
        }

        /// <summary>时长策略的显示名。</summary>
        public static string PolicyLabel(EDurationPolicy policy)
        {
            switch (policy)
            {
                case EDurationPolicy.HasDuration: return Tr("持续");
                case EDurationPolicy.Infinite:    return Tr("无限");
                default:                          return Tr("瞬时");
            }
        }
    }
}
