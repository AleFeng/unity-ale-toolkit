using System.Collections.Generic;
using Ale.GameplayTags;
using Ale.Toolkit.Editor;
using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>效果模板面板（效果页左列）：名称 / 色点 / 默认效果定义（内联，从模板创建时复制）/ 自定义属性字段 schema。</summary>
    public sealed class EffectTemplatePanel : EffectMasterListPanel<EffectTemplate>
    {
        private readonly AttributeDefinitionListDrawer _schemaDrawer = new AttributeDefinitionListDrawer();
        private EffectTemplate _lastExpanded;

        protected override List<EffectTemplate> GetList(EffectDatabase db) => db.EffectTemplates;
        protected override string Noun => "效果模板";
        protected override bool   HasColorDot => true;
        protected override Color  RowColor(EffectTemplate item) => item.color;
        protected override string RowLabel(EffectTemplate item) => string.IsNullOrEmpty(item.name) ? Tr("(未命名)") : item.name;

        protected override EffectTemplate CreateNew(EffectDatabase db, List<EffectTemplate> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = "effect_template_" + n; n++; } while (Contains(list, name));
            var t = new EffectTemplate(name);
            t.Normalize();
            return t;
        }

        private static bool Contains(List<EffectTemplate> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        protected override void OnInvalidate() => _schemaDrawer.Invalidate();

        public override void DrawInspector(IEffectEditorContext ctx, EffectTemplate tmpl)
        {
            if (tmpl == null)
            {
                EditorGUILayout.LabelField(Tr("请选择或新建一个效果模板。"), ToolkitEditorStyles.Placeholder);
                return;
            }

            var db = ctx.Database;

            EditorGUILayout.LabelField(Tr("基础信息"), ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string newName  = EditorGUILayout.TextField(Tr("模板名称"), tmpl.name);
            Color  newColor = EditorGUILayout.ColorField(Tr("标识颜色"), tmpl.color);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改效果模板");
                tmpl.name  = newName;
                tmpl.color = newColor;
                ctx.MarkDirty();
            }
            var dups = ctx.DuplicateIdsOf(EEffectEntityKind.Template);
            if (dups != null && dups.Contains(string.IsNullOrWhiteSpace(tmpl.name) ? string.Empty : tmpl.name))
                EditorGUILayout.LabelField(Tr("⚠ 模板名称重复或为空"), ToolkitEditorStyles.StatusError);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Tr("默认效果定义（从模板创建时复制）"), ToolkitEditorStyles.Header);
            bool forceExpand = !ReferenceEquals(_lastExpanded, tmpl);
            _lastExpanded = tmpl;
            EffectEditorFields.InlineDefinition(ctx, Tr("默认效果定义"), so =>
            {
                var arr = so.FindProperty("effectTemplates");
                int idx = db.EffectTemplates.IndexOf(tmpl);
                return arr != null && idx >= 0 && idx < arr.arraySize ? arr.GetArrayElementAtIndex(idx).FindPropertyRelative("defaultDefinition") : null;
            }, forceExpand);

            EditorGUILayout.Space(6);
            _schemaDrawer.Draw(ctx, db, tmpl.attributes, Tr("自定义属性字段 schema"));
        }
    }

    /// <summary>
    /// Gameplay 标签面板（效果页左列）：绑定 <see cref="EffectDatabase.GameplayTags"/>；检视 名称（点分层级，非法红框）/ 注释，
    /// 并列出会被隐式登记的祖先。注册效果库时并入 toolkit 标签注册表（编辑态经效果目录 provider 进入标签目录）；运行时匹配不依赖它。
    /// </summary>
    public sealed class EffectGameplayTagPanel : EffectMasterListPanel<GameplayTagDefinition>
    {
        protected override List<GameplayTagDefinition> GetList(EffectDatabase db) => db.GameplayTags;
        protected override string Noun => "Gameplay 标签";

        protected override string HeaderHelp
            => Tr("本库声明的层级标签（如 Status.Buff.Might）。注册效果库时并入 toolkit 标签注册表，供效果的标签字段下拉与「未登记」提示；运行时匹配不依赖此表。");

        protected override string RowLabel(GameplayTagDefinition item)
            => string.IsNullOrEmpty(item.name) ? Tr("(空名)") : item.name;

        protected override GameplayTagDefinition CreateNew(EffectDatabase db, List<GameplayTagDefinition> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = "Tag.New" + n; n++; } while (Contains(list, name));
            return new GameplayTagDefinition(name);
        }

        private static bool Contains(List<GameplayTagDefinition> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        public override void DrawInspector(IEffectEditorContext ctx, GameplayTagDefinition tag)
        {
            if (tag == null)
            {
                EditorGUILayout.LabelField(Tr("请选择或新建一个 Gameplay 标签。"), ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField(Tr("基础信息"), ToolkitEditorStyles.Header);
            bool valid = GameplayTag.IsValidName(tag.name);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(Tr("名称"));
            string name = EditorGUILayout.TextField(tag.name ?? string.Empty, valid ? EditorStyles.textField : ToolkitEditorStyles.RedField);
            EditorGUILayout.EndHorizontal();
            string comment = EditorGUILayout.TextField(Tr("注释"), tag.comment ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改 Gameplay 标签");
                tag.name    = name;
                tag.comment = comment;
                ctx.MarkDirty();
            }

            if (!valid)
            {
                EditorGUILayout.LabelField(Tr("⚠ 名称不合法：段不能为空，段内不能含空白或 '/'"), ToolkitEditorStyles.StatusError);
                return;
            }

            string norm = GameplayTag.Normalize(tag.name);
            var segs = norm.Split('.');
            if (segs.Length > 1)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(Tr("隐式登记的祖先"), ToolkitEditorStyles.Header);
                for (int i = segs.Length - 1; i >= 1; i--)
                    EditorGUILayout.LabelField("• " + string.Join(".", segs, 0, i), EditorStyles.miniLabel);
            }
        }
    }

    /// <summary>枚举类型面板（效果页左列）：机制全部来自 toolkit <see cref="EditorEnumTypePanel{TDb}"/>，仅绑定 <see cref="EffectDatabase.EnumTypesList"/>（自定义属性的枚举字段用）。</summary>
    public sealed class EffectEnumTypePanel : EditorEnumTypePanel<EffectDatabase>
    {
        protected override List<EnumType> GetList(EffectDatabase db) => db.EnumTypesList;
    }
}
