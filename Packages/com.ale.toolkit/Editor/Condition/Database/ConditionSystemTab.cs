using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 「Condition Database」页签（三列）：左=条件模板 / 枚举类型 子页签，中=条件列表（模板过滤 / 搜索 / 从模板添加 / 快速添加），
    /// 右=条件 Inspector（ID / 名称 / 描述 / 图标 / 来源模板 / 自定义属性 / 内联条件表达式 / 校验摘要）或左列选中项的 Inspector。
    /// </summary>
    public sealed class ConditionSystemTab : ConditionThreeColumnTab<ConditionEntry>
    {
        private readonly ConditionTemplatePanel _templatePanel = new ConditionTemplatePanel();
        private readonly ConditionEnumTypePanel _enumPanel     = new ConditionEnumTypePanel();
        private readonly ConditionListPanel     _listPanel     = new ConditionListPanel();
        private IEditorMasterListPanel<ConditionDatabase>[] _leftPanels;

        protected override string[] LeftSubTabs => new[] { Tr("条件模板"), Tr("枚举类型") };

        protected override IEditorMasterListPanel<ConditionDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ConditionDatabase>[] { _templatePanel, _enumPanel };

        protected override string EntityNoun        => "条件";
        protected override float  DeleteButtonWidth => 68f;

        protected override List<ConditionEntry> EntityList(ConditionDatabase db) => db.Conditions;

        protected override ConditionEntry DrawEntityList(IConditionEditorContext ctx, ConditionEntry displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override ConditionEntry ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IConditionEditorContext ctx, ConditionEntry entity)
            => ConditionInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>条件列表面板（中列）：模板过滤 + 搜索 + 从模板添加（克隆模板默认表达式、按 schema 建自定义属性）/ 快速添加，每行 id / 名称 / 条数 / 摘要。</summary>
    public sealed class ConditionListPanel : ConditionEntityListPanel<ConditionEntry, ConditionTemplate>
    {
        public ConditionListPanel() : base("ConditionEntryListDrag") { }

        protected override EConditionEntityKind Kind => EConditionEntityKind.Condition;
        protected override string Noun => "条件";
        protected override string NoTemplateHint => Tr("（无可用条件模板；请先在左侧「条件模板」中创建）");

        protected override List<ConditionEntry>    Entities(ConditionDatabase db)  => db.Conditions;
        protected override List<ConditionTemplate> Templates(ConditionDatabase db) => db.ConditionTemplates;
        protected override string TemplateName(ConditionTemplate t) => t.name;
        protected override string TemplateRefOf(ConditionEntry e)   => e.templateRef;
        protected override string IdOf(ConditionEntry e)            => e.id;

        protected override Color RowDotColor(ConditionDatabase db, ConditionEntry e)
        {
            var t = db.GetTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override bool Matches(ConditionDatabase db, ConditionEntry e, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            term = term.ToLowerInvariant();
            if (!string.IsNullOrEmpty(e.id) && e.id.ToLowerInvariant().Contains(term)) return true;
            string name = e.PlainName();
            return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(term);
        }

        protected override ConditionEntry AddFromTemplate(IConditionEditorContext ctx, string templateName)
        {
            var db   = ctx.Database;
            var tmpl = db.GetTemplate(templateName);

            ctx.RecordUndo("添加条件");
            var e = new ConditionEntry(GenerateId(db, "condition_", id => db.GetEntry(id) != null), tmpl != null ? tmpl.name : null);
            if (tmpl?.defaultExpression != null)
                e.expression = tmpl.defaultExpression.Clone();   // 模板预设
            e.displayText.SetTextValue(0, Tr("新条件"));
            e.RebuildAttributes(db);
            e.Normalize();
            db.Conditions.Add(e);
            ctx.MarkDirty();
            return e;
        }

        protected override ConditionEntry QuickAdd(IConditionEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Conditions.Count == 0)
                return AddFromTemplate(ctx, db.ConditionTemplates.Count > 0 ? db.ConditionTemplates[0].name : null);

            ctx.RecordUndo("快速添加条件");
            var clone = db.Conditions[db.Conditions.Count - 1].Clone();
            clone.id = GenerateId(db, "condition_", id => db.GetEntry(id) != null);
            clone.Normalize();
            db.Conditions.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ConditionDatabase db, ConditionEntry e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(100f, w * 0.28f);
            float sumW  = Mathf.Min(150f, w * 0.34f);
            float nameX = contentX + idW + Pad;
            float sumX  = contentRight - sumW;
            float nameW = Mathf.Max(0f, sumX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",       KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), Tr("名称"), KeyStyle);
            GUI.Label(new Rect(sumX,     keyRow.y, sumW,  keyRow.height), Tr("内容"), KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrWhiteSpace(e.id) ? Tr("(空 ID)") : e.id, IdStyle);
            string name = e.displayText != null ? e.displayText.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);
            GUI.Label(new Rect(sumX,  valY, sumW,  valH), ConditionEditorFields.Summary(e.expression), SubStyle);
        }
    }

    /// <summary>条件 Inspector（右列）：ID / 名称 / 描述 / 图标 + 来源模板 + 自定义属性 + 内联条件表达式 + 校验摘要。</summary>
    public static class ConditionInspectorPanel
    {
        private static ConditionEntry _lastExpanded;

        public static void Draw(IConditionEditorContext ctx, ConditionEntry entry)
        {
            if (entry == null)
            {
                EditorGUILayout.LabelField(Tr("请选择或新建一个条件。"), ToolkitEditorStyles.Placeholder);
                return;
            }

            var db = ctx.Database;

            EditorGUILayout.LabelField(Tr("基础信息"), ToolkitEditorStyles.Header);
            ConditionEditorFields.DrawIdField(ctx, Tr("条件"), entry.id, ctx.DuplicateIdsOf(EConditionEntityKind.Condition), v => entry.id = v);
            AttributeFieldDrawer.Draw(ctx, Tr("名称"), entry.displayText, null);
            AttributeFieldDrawer.Draw(ctx, Tr("描述"), entry.descriptionText, null);
            AttributeFieldDrawer.Draw(ctx, Tr("图标"), entry.iconValue, null);
            ConditionEditorFields.DrawTemplateRefReadonly(entry.templateRef);

            EditorGUILayout.Space(6);
            var tmpl = db.GetTemplate(entry.templateRef);
            ConditionEditorFields.DrawCustomAttributes(ctx, entry.values, tmpl?.attributes,
                Tr("（该条件暂无自定义属性字段；可在左侧「条件模板」的 schema 中添加）"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Tr("条件表达式"), ToolkitEditorStyles.Header);
            EditorGUILayout.LabelField(Tr("上层系统以 ID 引用本条件；求值经 ConditionResolver 按 id 解析。"), EditorStyles.miniLabel);
            bool forceExpand = !ReferenceEquals(_lastExpanded, entry);
            _lastExpanded = entry;
            ConditionEditorFields.InlineExpression(ctx, Tr("条件表达式"), so =>
            {
                var arr = so.FindProperty("conditions");
                int idx = db.Conditions.IndexOf(entry);
                return arr != null && idx >= 0 && idx < arr.arraySize ? arr.GetArrayElementAtIndex(idx).FindPropertyRelative("expression") : null;
            }, forceExpand);

            var errors = new List<string>();
            var warns  = new List<string>();
            ConditionEditorFields.ValidateForDisplay(entry, errors, warns);
            ConditionEditorFields.DrawValidationSummary(errors, warns);
        }
    }
}
