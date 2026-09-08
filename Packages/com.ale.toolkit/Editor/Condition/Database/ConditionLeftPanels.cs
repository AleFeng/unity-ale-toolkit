using System.Collections.Generic;
using Ale.Toolkit.Editor;
using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>条件模板面板（条件页左列）：名称 / 色点 / 默认条件表达式（内联，从模板创建时复制）/ 自定义属性字段 schema。</summary>
    public sealed class ConditionTemplatePanel : ConditionMasterListPanel<ConditionTemplate>
    {
        private readonly AttributeDefinitionListDrawer _schemaDrawer = new AttributeDefinitionListDrawer();
        private ConditionTemplate _lastExpanded;

        protected override List<ConditionTemplate> GetList(ConditionDatabase db) => db.ConditionTemplates;
        protected override string Noun => "条件模板";
        protected override bool   HasColorDot => true;
        protected override Color  RowColor(ConditionTemplate item) => item.color;
        protected override string RowLabel(ConditionTemplate item) => string.IsNullOrEmpty(item.name) ? Tr("(未命名)") : item.name;

        protected override ConditionTemplate CreateNew(ConditionDatabase db, List<ConditionTemplate> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = "condition_template_" + n; n++; } while (Contains(list, name));
            var t = new ConditionTemplate(name);
            t.Normalize();
            return t;
        }

        private static bool Contains(List<ConditionTemplate> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        protected override void OnInvalidate() => _schemaDrawer.Invalidate();

        public override void DrawInspector(IConditionEditorContext ctx, ConditionTemplate tmpl)
        {
            if (tmpl == null)
            {
                EditorGUILayout.LabelField(Tr("请选择或新建一个条件模板。"), ToolkitEditorStyles.Placeholder);
                return;
            }

            var db = ctx.Database;

            EditorGUILayout.LabelField(Tr("基础信息"), ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string newName  = EditorGUILayout.TextField(Tr("模板名称"), tmpl.name);
            Color  newColor = EditorGUILayout.ColorField(Tr("标识颜色"), tmpl.color);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改条件模板");
                tmpl.name  = newName;
                tmpl.color = newColor;
                ctx.MarkDirty();
            }
            var dups = ctx.DuplicateIdsOf(EConditionEntityKind.Template);
            if (dups != null && dups.Contains(string.IsNullOrWhiteSpace(tmpl.name) ? string.Empty : tmpl.name))
                EditorGUILayout.LabelField(Tr("⚠ 模板名称重复或为空"), ToolkitEditorStyles.StatusError);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Tr("默认条件表达式（从模板创建时复制）"), ToolkitEditorStyles.Header);
            bool forceExpand = !ReferenceEquals(_lastExpanded, tmpl);
            _lastExpanded = tmpl;
            ConditionEditorFields.InlineExpression(ctx, Tr("默认条件表达式"), so =>
            {
                var arr = so.FindProperty("conditionTemplates");
                int idx = db.ConditionTemplates.IndexOf(tmpl);
                return arr != null && idx >= 0 && idx < arr.arraySize ? arr.GetArrayElementAtIndex(idx).FindPropertyRelative("defaultExpression") : null;
            }, forceExpand);

            EditorGUILayout.Space(6);
            _schemaDrawer.Draw(ctx, db, tmpl.attributes, Tr("自定义属性字段 schema"));
        }
    }

    /// <summary>枚举类型面板（条件页左列）：机制全部来自 toolkit <see cref="EditorEnumTypePanel{TDb}"/>，仅绑定 <see cref="ConditionDatabase.EnumTypesList"/>（自定义属性的枚举字段用）。</summary>
    public sealed class ConditionEnumTypePanel : EditorEnumTypePanel<ConditionDatabase>
    {
        protected override List<EnumType> GetList(ConditionDatabase db) => db.EnumTypesList;
    }
}
