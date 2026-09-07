using System.Collections.Generic;
using Ale.Toolkit.Editor;
using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 「效果」页签（三列）：左=效果模板 / Gameplay 标签 / 枚举类型 子页签，中=效果列表（模板过滤 / 搜索 / 从模板添加 / 快速添加），
    /// 右=效果 Inspector（ID / 名称 / 描述 / 图标 / 来源模板 / 自定义属性 / 内联效果定义 / 校验摘要）或左列选中项的 Inspector。
    /// </summary>
    public sealed class EffectSystemTab : EffectThreeColumnTab<EffectEntry>
    {
        private readonly EffectTemplatePanel    _templatePanel = new EffectTemplatePanel();
        private readonly EffectGameplayTagPanel _tagPanel      = new EffectGameplayTagPanel();
        private readonly EffectEnumTypePanel    _enumPanel     = new EffectEnumTypePanel();
        private readonly EffectListPanel        _listPanel     = new EffectListPanel();
        private IEditorMasterListPanel<EffectDatabase>[] _leftPanels;

        protected override string[] LeftSubTabs => new[] { Tr("效果模板"), Tr("Gameplay 标签"), Tr("枚举类型") };

        protected override IEditorMasterListPanel<EffectDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<EffectDatabase>[] { _templatePanel, _tagPanel, _enumPanel };

        protected override string EntityNoun        => "效果";
        protected override float  DeleteButtonWidth => 68f;

        protected override List<EffectEntry> EntityList(EffectDatabase db) => db.Effects;

        protected override EffectEntry DrawEntityList(IEffectEditorContext ctx, EffectEntry displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override EffectEntry ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IEffectEditorContext ctx, EffectEntry entity)
            => EffectInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>效果列表面板（中列）：模板过滤 + 搜索 + 从模板添加（克隆模板默认定义、按 schema 建自定义属性）/ 快速添加，每行 id / 名称 / 策略 / 内容摘要。</summary>
    public sealed class EffectListPanel : EffectEntityListPanel<EffectEntry, EffectTemplate>
    {
        public EffectListPanel() : base("EffectEntryListDrag") { }

        protected override EEffectEntityKind Kind => EEffectEntityKind.Effect;
        protected override string Noun => "效果";
        protected override string NoTemplateHint => Tr("（无可用效果模板；请先在左侧「效果模板」中创建）");

        protected override List<EffectEntry>    Entities(EffectDatabase db)  => db.Effects;
        protected override List<EffectTemplate> Templates(EffectDatabase db) => db.EffectTemplates;
        protected override string TemplateName(EffectTemplate t) => t.name;
        protected override string TemplateRefOf(EffectEntry e)   => e.templateRef;
        protected override string IdOf(EffectEntry e)            => e.id;

        protected override Color RowDotColor(EffectDatabase db, EffectEntry e)
        {
            var t = db.GetTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override bool Matches(EffectDatabase db, EffectEntry e, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            term = term.ToLowerInvariant();
            if (!string.IsNullOrEmpty(e.id) && e.id.ToLowerInvariant().Contains(term)) return true;
            string name = e.PlainName();
            return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(term);
        }

        protected override EffectEntry AddFromTemplate(IEffectEditorContext ctx, string templateName)
        {
            var db   = ctx.Database;
            var tmpl = db.GetTemplate(templateName);

            ctx.RecordUndo("添加效果");
            var policy = tmpl?.defaultDefinition != null ? tmpl.defaultDefinition.durationPolicy : EDurationPolicy.Instant;
            var e = new EffectEntry(GenerateId(db, "effect_", id => db.GetEffect(id) != null), policy, tmpl != null ? tmpl.name : null);
            if (tmpl?.defaultDefinition != null)
                e.definition = tmpl.defaultDefinition.Clone();   // 模板预设（id / 显示名随后由 Normalize 同步）
            e.displayText.SetTextValue(0, Tr("新效果"));
            e.RebuildAttributes(db);
            e.Normalize();
            db.Effects.Add(e);
            ctx.MarkDirty();
            return e;
        }

        protected override EffectEntry QuickAdd(IEffectEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Effects.Count == 0)
                return AddFromTemplate(ctx, db.EffectTemplates.Count > 0 ? db.EffectTemplates[0].name : null);

            ctx.RecordUndo("快速添加效果");
            var clone = db.Effects[db.Effects.Count - 1].Clone();
            clone.id = GenerateId(db, "effect_", id => db.GetEffect(id) != null);
            clone.Normalize();
            db.Effects.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(EffectDatabase db, EffectEntry e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(100f, w * 0.28f);
            float polW  = 46f;
            float sumW  = Mathf.Min(130f, w * 0.30f);
            float nameX = contentX + idW + Pad;
            float sumX  = contentRight - sumW;
            float polX  = sumX - Pad - polW;
            float nameW = Mathf.Max(0f, polX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",        KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), Tr("名称"),   KeyStyle);
            GUI.Label(new Rect(polX,     keyRow.y, polW,  keyRow.height), Tr("策略"),   KeyStyle);
            GUI.Label(new Rect(sumX,     keyRow.y, sumW,  keyRow.height), Tr("内容"),   KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrWhiteSpace(e.id) ? Tr("(空 ID)") : e.id, IdStyle);
            string name = e.displayText != null ? e.displayText.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);
            GUI.Label(new Rect(polX,  valY, polW,  valH), EffectEditorFields.PolicyLabel(e.definition != null ? e.definition.durationPolicy : EDurationPolicy.Instant), SubStyle);
            GUI.Label(new Rect(sumX,  valY, sumW,  valH), Summary(e.definition), SubStyle);
        }

        private static string Summary(EffectDefinition d)
        {
            if (d == null) return "—";
            int mods = d.modifiers != null ? d.modifiers.Count : 0;
            int exec = 0;
            if (d.executions?.groups != null)
                foreach (var g in d.executions.groups)
                    if (g?.items != null) exec += g.items.Count;
            int tags = d.assetTags != null && d.assetTags.tags != null ? d.assetTags.tags.Count : 0;
            return Fmt("{0} 修饰 · {1} 执行 · {2} 标签", mods, exec, tags);
        }
    }

    /// <summary>效果 Inspector（右列）：ID / 名称 / 描述 / 图标 + 来源模板 + 自定义属性 + 内联效果定义 + 校验摘要。</summary>
    public static class EffectInspectorPanel
    {
        private static EffectEntry _lastExpanded;

        public static void Draw(IEffectEditorContext ctx, EffectEntry entry)
        {
            if (entry == null)
            {
                EditorGUILayout.LabelField(Tr("请选择或新建一个效果。"), ToolkitEditorStyles.Placeholder);
                return;
            }

            var db = ctx.Database;

            EditorGUILayout.LabelField(Tr("基础信息"), ToolkitEditorStyles.Header);
            EffectEditorFields.DrawIdField(ctx, Tr("效果"), entry.id, ctx.DuplicateIdsOf(EEffectEntityKind.Effect), v => entry.id = v);
            AttributeFieldDrawer.Draw(ctx, Tr("名称"), entry.displayText, null);
            AttributeFieldDrawer.Draw(ctx, Tr("描述"), entry.descriptionText, null);
            AttributeFieldDrawer.Draw(ctx, Tr("图标"), entry.iconValue, null);
            EffectEditorFields.DrawTemplateRefReadonly(entry.templateRef);

            EditorGUILayout.Space(6);
            var tmpl = db.GetTemplate(entry.templateRef);
            EffectEditorFields.DrawCustomAttributes(ctx, entry.values, tmpl?.attributes,
                Tr("（该效果暂无自定义属性字段；可在左侧「效果模板」的 schema 中添加）"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Tr("效果定义"), ToolkitEditorStyles.Header);
            EditorGUILayout.LabelField(Tr("定义内的 id / 显示名由上方 ID / 名称同步；上层系统以 ID 引用本效果。"), EditorStyles.miniLabel);
            bool forceExpand = !ReferenceEquals(_lastExpanded, entry);
            _lastExpanded = entry;
            EffectEditorFields.InlineDefinition(ctx, Tr("效果定义"), so =>
            {
                var arr = so.FindProperty("effects");
                int idx = db.Effects.IndexOf(entry);
                return arr != null && idx >= 0 && idx < arr.arraySize ? arr.GetArrayElementAtIndex(idx).FindPropertyRelative("definition") : null;
            }, forceExpand);

            var errors = new List<string>();
            var warns  = new List<string>();
            EffectEditorFields.ValidateForDisplay(entry, errors, warns);
            EffectEditorFields.DrawValidationSummary(errors, warns);
        }
    }
}
