using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 供上层系统 Inspector 使用的「条件 id 引用列表」绘制器（技能树解锁条件、特质获得条件…）：
    /// 标题行 + 「+」菜单（条件目录，按库分组）、可拖拽重排 / 删除的行（命中目录的行附「打开」按钮跳转到 Condition Editor 并定位；
    /// 未命中标「未找到」，不阻断——运行时按 id 经全局条件注册表解析）、底部自由输入行。Undo / 标脏经 <see cref="IEditorContext"/>。
    /// 与效果侧的 <c>EditorEffectRefListDrawer</c> 同构。
    /// </summary>
    public static class EditorConditionRefListDrawer
    {
        private static GUIStyle _missingStyle;
        private static GUIStyle MissingStyle => _missingStyle ??= new GUIStyle(EditorStyles.label)
            { normal = { textColor = new Color(0.92f, 0.62f, 0.35f) } };

        private static string _pendingId = string.Empty;

        public static void Draw(IEditorContext ctx, List<string> refs, EditorReorderableDrag drag,
            string header, string noun, string hint = null)
        {
            if (refs == null) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(header, ToolkitEditorStyles.Header);
            if (GUILayout.Button("+", GUILayout.Width(24)))
                ConditionEditorCatalog.BuildMenu(refs, id =>
                {
                    ctx.RecordUndo(Fmt("添加{0}", noun));
                    refs.Add(id);
                    ctx.MarkDirty();
                    ctx.Repaint();
                }).ShowAsContext();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(hint))
                EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);

            if (refs.Count == 0)
                EditorGUILayout.LabelField(Tr("（暂无引用条件）"), EditorStyles.miniLabel);
            else
                EditorDraggableRowList.Draw(ctx, refs, drag, noun, (i, id) =>
                {
                    EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(20));
                    if (ConditionEditorCatalog.TryFind(id, out var hit))
                    {
                        EditorGUILayout.LabelField(new GUIContent(ConditionEditorCatalog.LabelOf(hit.Entry), hit.Database.name));
                        if (GUILayout.Button(Tr("打开"), EditorStyles.miniButton, GUILayout.Width(44)))
                            ConditionEditorWindow.Open(hit.Database, id);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(new GUIContent(id + Tr("（未找到）"),
                            Tr("未在任何条件库中找到；运行时按 id 经全局条件注册表解析")), MissingStyle);
                    }
                });

            // 自由输入：引用尚未建库 / 其它来源的条件 id
            EditorGUILayout.BeginHorizontal();
            _pendingId = EditorGUILayout.TextField(_pendingId);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_pendingId) || refs.Contains(_pendingId.Trim())))
            {
                if (GUILayout.Button(Tr("添加 id"), GUILayout.Width(70)))
                {
                    ctx.RecordUndo(Fmt("添加{0}", noun));
                    refs.Add(_pendingId.Trim());
                    _pendingId = string.Empty;
                    ctx.MarkDirty();
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
