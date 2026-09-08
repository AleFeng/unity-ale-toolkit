using System;
using System.Collections.Generic;
using System.Text;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 「Effect Executors」页签：列出工程里全部效果执行器实现（<see cref="EffectExecutorIndex"/>），
    /// 支持搜索 / 分类过滤 / 只看有问题；右列给出实现类型、程序集、源码路径（可打开 IDE 或在 Project 中定位）、
    /// 参数 schema、被哪些配置引用（可跳到 Effect Database 页的对应效果），以及静默失效诊断；
    /// 底部是内置阶段与「新增一个执行器」的代码模板。
    ///
    /// <para>本页签内容来自<b>代码</b>而非效果库，因此 <see cref="EffectEditorWindow"/> 把它的
    /// <c>TabRequiresDatabase</c> 覆写为 false——没有效果库资产时照样可用。</para>
    /// </summary>
    public sealed class EffectExecutorTab : IEditorSystemTab<EffectDatabase>
    {
        private const float Padding        = 4f;
        private const float RightWidth     = 400f;
        private const float LeftMinWidth   = 300f;
        private const float ToolbarHeight  = 20f;
        private const float RowHeight      = 22f;
        private const float GroupHeight    = 18f;
        private const float BandHeight     = 20f;
        private const float CheatOpenH     = 260f;

        private static readonly Color IssueRowColor = new Color(0.90f, 0.30f, 0.30f, 0.14f);

        private string  _search = string.Empty;
        private string  _category;               // null = 全部
        private bool    _onlyIssues;
        private bool    _showDangling;
        private bool    _showCheatSheet;
        private Vector2 _leftScroll, _rightScroll;
        private EffectExecutorRow _selected;
        private Action  _pending;                // 延迟到本帧绘制结束后执行（跳转 / 打开脚本）

        private static GUIStyle _subStyle, _rightMini, _titleStyle, _dimStyle;

        // ── IEditorSystemTab ──────────────────────────────────────────────────────

        public void OnGUI(Rect rect, IEditorDbContext<EffectDatabase> ctx)
        {
            EnsureStyles();
            ToolkitEditorStyles.TrackMouseHover(ctx);
            var rows = EffectExecutorIndex.Rows;
            RebindSelection(rows);   // 目录重建后按键重新认领选中项

            var toolbarRect = new Rect(0, 0, rect.width, ToolbarHeight);
            GUILayout.BeginArea(toolbarRect, EditorStyles.toolbar);
            DrawToolbar(rows);
            GUILayout.EndArea();

            float y = ToolbarHeight;

            var dangling = EffectExecutorIndex.DanglingKeys;
            if (dangling.Count > 0)
            {
                float h = _showDangling ? Mathf.Min(140f, BandHeight + 4f + dangling.Count * 16f) : BandHeight;
                GUILayout.BeginArea(new Rect(0, y, rect.width, h));
                DrawDanglingBand(dangling);
                GUILayout.EndArea();
                y += h;
            }

            float cheatH = _showCheatSheet ? CheatOpenH : BandHeight;
            float bodyH  = Mathf.Max(80f, rect.height - y - cheatH - Padding);

            float rightW = Mathf.Clamp(rect.width - LeftMinWidth - Padding * 3, 240f, RightWidth);
            float leftW  = Mathf.Max(160f, rect.width - rightW - Padding * 3);

            DrawList(new Rect(Padding, y, leftW, bodyH), rows);
            DrawDetail(new Rect(Padding * 2 + leftW, y, rightW, bodyH));

            GUILayout.BeginArea(new Rect(Padding, y + bodyH, rect.width - Padding * 2, cheatH));
            DrawCheatSheet();
            GUILayout.EndArea();

            if (_pending != null)
            {
                var action = _pending;
                _pending = null;
                action();
            }
        }

        public void OnDatabaseChanged(IEditorDbContext<EffectDatabase> ctx) { }

        public void OnUndoRedo() { }

        // ── 工具栏 ────────────────────────────────────────────────────────────────

        private void DrawToolbar(IReadOnlyList<EffectExecutorRow> rows)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(Tr("搜索"), EditorStyles.miniLabel, GUILayout.Width(30));
            _search = GUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            _onlyIssues = GUILayout.Toggle(_onlyIssues, Tr("只看有问题"), EditorStyles.toolbarButton, GUILayout.Width(80));

            GUILayout.FlexibleSpace();
            GUILayout.Label(Fmt("已发现 {0} / 共 {1}", EffectExecutorIndex.DiscoveredCount, rows.Count),
                EditorStyles.miniLabel);
            if (GUILayout.Button(Tr("刷新"), EditorStyles.toolbarButton, GUILayout.Width(50)))
                Refresh();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>索引重建后旧的行对象会成为孤儿；按类型重新认领，认不回就清空选中。</summary>
        private void RebindSelection(IReadOnlyList<EffectExecutorRow> rows)
        {
            if (_selected == null) return;
            foreach (var r in rows)
            {
                if (ReferenceEquals(r, _selected)) return;
                if (r.Type == _selected.Type) { _selected = r; return; }
            }
            _selected = null;
        }

        private void Refresh()
        {
            EffectExecutorCatalog.Rebuild();
            EffectExecutorIndex.Rebuild();
            EditorScriptLocator.ClearCache();
            _selected = null;
        }

        // ── 悬空键告警条 ──────────────────────────────────────────────────────────

        private void DrawDanglingBand(IReadOnlyList<string> dangling)
        {
            var color = GUI.color;
            GUI.color = new Color(1f, 0.72f, 0.35f);
            _showDangling = EditorGUILayout.Foldout(_showDangling,
                Fmt("⚠ 配置里有 {0} 个执行器键没有对应实现（运行时会报「未注册的执行器键」）", dangling.Count), true);
            GUI.color = color;

            if (!_showDangling) return;
            foreach (var key in dangling)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(14f);
                GUILayout.Label(key, _subStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Fmt("{0} 处引用", CountUsages(key)), EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }
        }

        private static int CountUsages(string key)
        {
            int n = 0;
            foreach (var u in EffectExecutorIndex.Usages)
                if (string.Equals(u.Key, key, StringComparison.Ordinal)) n++;
            return n;
        }

        // ── 左列：分类过滤 + 分组列表 ─────────────────────────────────────────────

        private void DrawList(Rect rect, IReadOnlyList<EffectExecutorRow> allRows)
        {
            GUILayout.BeginArea(rect);

            _category = EditorFilterTabs.Draw(_category, EffectExecutorIndex.Categories, c => c);

            var visible = EffectExecutorIndex.Filter(allRows, _search, _category, _onlyIssues);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, EditorStyles.helpBox);
            if (visible.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(Tr("没有匹配的执行器"), EditorStyles.centeredGreyMiniLabel);
            }

            string lastCategory = null;
            foreach (var row in visible)
            {
                if (row.Category != lastCategory)
                {
                    lastCategory = row.Category;
                    var headRect = GUILayoutUtility.GetRect(0, GroupHeight, GUILayout.ExpandWidth(true));
                    GUI.Label(new Rect(headRect.x + 2f, headRect.y, headRect.width - 4f, headRect.height),
                        lastCategory, EditorStyles.boldLabel);
                }
                DrawRow(row);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRow(EffectExecutorRow row)
        {
            var rowRect = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));

            if (row == _selected)       ToolkitEditorStyles.DrawRowBackground(rowRect, ToolkitEditorStyles.SelectedColor);
            else if (row.HasProblem)    ToolkitEditorStyles.DrawRowBackground(rowRect, IssueRowColor);
            ToolkitEditorStyles.DrawRowHover(rowRect);

            float x     = rowRect.x + 6f;
            float right = rowRect.xMax - 6f;
            float nameW = Mathf.Clamp(rowRect.width * 0.42f, 90f, 190f);
            float badgeW = 46f;

            // 有真问题 → 红字 + ⚠；没问题但也不会被发现（抽象基类）→ 灰字；其余正常。
            string name = row.HasProblem ? "⚠ " + row.DisplayName : row.DisplayName;
            var nameStyle = row.HasProblem  ? ToolkitEditorStyles.StatusError
                          : row.Discovered ? EditorStyles.label
                                           : _dimStyle;
            GUI.Label(new Rect(x, rowRect.y + 3f, nameW, 16f), name, nameStyle);

            float keyX = x + nameW + 6f;
            GUI.Label(new Rect(keyX, rowRect.y + 4f, Mathf.Max(20f, right - keyX - badgeW - 4f), 15f), row.Key, _subStyle);

            if (row.UsageCount > 0)
                GUI.Label(new Rect(right - badgeW, rowRect.y + 4f, badgeW, 15f), Fmt("×{0}", row.UsageCount), _rightMini);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
            {
                if (e.clickCount >= 2)
                {
                    var target = row;
                    _pending = () => OpenScript(target);
                }
                else _selected = row;
                e.Use();
            }
        }

        // ── 右列：详情 ────────────────────────────────────────────────────────────

        private void DrawDetail(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);

            if (_selected == null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField(Tr("从左侧选择一个执行器"), ToolkitEditorStyles.Placeholder);
                GUILayout.EndArea();
                return;
            }

            var row = _selected;
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

            EditorGUILayout.LabelField(row.DisplayName, _titleStyle);
            EditorGUILayout.LabelField(row.Key, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(row.UsageCount > 0
                ? Fmt("分类 {0} · 被引用 {1} 处", row.Category, row.UsageCount)
                : Fmt("分类 {0} · 未被任何配置引用", row.Category), EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            DrawReadonlyRow(Tr("实现"),   row.Type != null ? row.Type.FullName : "-");
            DrawReadonlyRow(Tr("程序集"), row.AssemblyName);

            string scriptPath = EditorScriptLocator.PathOf(row.Type);
            DrawReadonlyRow(Tr("脚本"), !string.IsNullOrEmpty(scriptPath)
                ? scriptPath
                : Fmt("无源码（来自程序集 {0}）", row.AssemblyName));

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(scriptPath)))
            {
                if (GUILayout.Button(Tr("打开脚本"), GUILayout.Height(22)))
                {
                    var target = row;
                    _pending = () => OpenScript(target);
                }
                if (GUILayout.Button(Tr("在 Project 中定位"), GUILayout.Height(22)))
                {
                    var target = row;
                    _pending = () => EditorScriptLocator.Ping(target.Type);
                }
            }
            if (GUILayout.Button(Tr("复制 Key"), GUILayout.Width(70), GUILayout.Height(22)))
                EditorGUIUtility.systemCopyBuffer = row.Key ?? string.Empty;
            EditorGUILayout.EndHorizontal();

            DrawParamSchema(row);
            DrawUsages(row);
            DrawDiagnostics(row);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawReadonlyRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(54));
            EditorGUILayout.SelectableLabel(value ?? "-", EditorStyles.miniLabel, GUILayout.Height(15));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawParamSchema(EffectExecutorRow row)
        {
            var schema = row.ParamSchema;
            int count = schema?.Count ?? 0;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Fmt("参数（{0}）", count), EditorStyles.boldLabel);
            if (count == 0)
            {
                EditorGUILayout.LabelField(Tr("无参数"), EditorStyles.miniLabel);
                return;
            }

            foreach (var def in schema)
            {
                if (def == null) continue;
                var sb = new StringBuilder();
                sb.Append(def.type.ToString());
                if (def.isArray) sb.Append("[]");
                if (!string.IsNullOrEmpty(def.enumTypeRef)) sb.Append("  enum:").Append(def.enumTypeRef);
                if (def.choices != null && def.choices.Length > 0) sb.Append("  {").Append(string.Join(" / ", def.choices)).Append('}');

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(def.id, EditorStyles.miniBoldLabel, GUILayout.Width(90));
                GUILayout.Label(sb.ToString(), EditorStyles.miniLabel, GUILayout.Width(130));
                GUILayout.Label(def.label ?? string.Empty, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawUsages(EffectExecutorRow row)
        {
            var usages = EffectExecutorIndex.UsagesOf(row.Key);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Fmt("被引用（{0}）", usages.Count), EditorStyles.boldLabel);
            if (usages.Count == 0)
            {
                EditorGUILayout.LabelField(Tr("尚未被任何效果配置引用"), EditorStyles.miniLabel);
                return;
            }

            foreach (var usage in usages)
            {
                EditorGUILayout.BeginHorizontal();
                string label = usage.Asset ? usage.Asset.name : "-";
                if (!string.IsNullOrEmpty(usage.EffectId)) label += " · " + usage.EffectId;
                if (!string.IsNullOrEmpty(usage.Phase))    label += " · " + usage.Phase;
                GUILayout.Label(label, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(Tr("跳转"), EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    var target = usage;
                    _pending = () => Jump(target);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawDiagnostics(EffectExecutorRow row)
        {
            if (row.Issues == EExecutorIssue.None) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(row.HasProblem ? Tr("诊断") : Tr("说明"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(DescribeIssues(row),
                !row.HasProblem ? MessageType.Info
                                : row.Discovered ? MessageType.Warning : MessageType.Error);
        }

        /// <summary>把诊断标记译成人话（每条一行）。</summary>
        private static string DescribeIssues(EffectExecutorRow row)
        {
            var lines = new List<string>();

            if ((row.Issues & EExecutorIssue.MissingAttribute) != 0)
                lines.Add(Tr("实现了 IEffectExecutor 却没打 [EffectExecutor] 特性：编辑器目录与运行时注册表都发现不了它。"));
            if ((row.Issues & EExecutorIssue.AttributeKeyMismatch) != 0)
                lines.Add(Fmt("特性里写的是 '{0}'，实际生效的是 Key 属性的 '{1}'——特性字符串从不被读取，请改成一致。",
                    row.AttributeKey, row.Key));
            if ((row.Issues & EExecutorIssue.DuplicateKey) != 0)
                lines.Add(Fmt("键 '{0}' 有多个实现：编辑器目录取先发现的、运行时注册表取后注册的，两边可能不是同一个。", row.Key));
            if ((row.Issues & EExecutorIssue.EmptyKey) != 0)
                lines.Add(Tr("Key 属性为空：目录与注册表都会跳过它。"));
            if ((row.Issues & EExecutorIssue.AbstractType) != 0)
                lines.Add(Tr("抽象基类：自身不参与发现，由派生类打上 [EffectExecutor] 后被注册。这是执行器基类的正常形态，无需处理。"));
            if ((row.Issues & EExecutorIssue.NoDefaultCtor) != 0)
                lines.Add(Tr("缺少公开无参构造：目录与注册表都会跳过它。"));
            if ((row.Issues & EExecutorIssue.ConstructionFailed) != 0)
                lines.Add(Tr("实例化失败：请检查构造函数与静态初始化。"));

            return string.Join("\n", lines);
        }

        // ── 底部：用法速查 ────────────────────────────────────────────────────────

        private void DrawCheatSheet()
        {
            _showCheatSheet = EditorGUILayout.Foldout(_showCheatSheet, Tr("新增执行器速查"), true);
            if (!_showCheatSheet) return;

            EditorGUILayout.LabelField(
                Fmt("内置阶段：{0}（空阶段组视为通配，Normalize() 会改写为 onApply；宿主自定义阶段经 EffectContainer.RunPhase 触发）",
                    string.Join(" / ", EffectPhases.All)), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Tr("最小实现模板"), EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Tr("复制模板"), EditorStyles.miniButton, GUILayout.Width(70)))
            {
                EditorGUIUtility.systemCopyBuffer = Template;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.SelectableLabel(Template, EditorStyles.textArea, GUILayout.ExpandHeight(true));
        }

        // ── 动作 ──────────────────────────────────────────────────────────────────

        private static void OpenScript(EffectExecutorRow row)
        {
            if (row?.Type == null) return;
            if (!EditorScriptLocator.Open(row.Type))
                Debug.LogWarning("[EffectExecutorTab] " + Fmt("找不到 {0} 的源码脚本", row.Type.FullName));
        }

        private static void Jump(EffectKeyUsage usage)
        {
            if (!usage.Asset) return;
            if (usage.Asset is EffectDatabase db && !string.IsNullOrEmpty(usage.EffectId))
            {
                EffectEditorWindow.Open(db, usage.EffectId);
                return;
            }
            Selection.activeObject = usage.Asset;
            EditorGUIUtility.PingObject(usage.Asset);
        }

        // ── 样式 ──────────────────────────────────────────────────────────────────

        private static void EnsureStyles()
        {
            if (_subStyle != null) return;

            _subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.62f, 0.62f, 0.62f) },
                clipping = TextClipping.Clip,
            };
            _rightMini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _dimStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.58f, 0.58f, 0.58f) } };
        }

        private const string Template =
@"using System.Collections.Generic;
using Ale.Effect;

[EffectExecutor(""MySystem.DoThing"")]   // 特性只作发现标记；真正生效的键是下面的 Key 属性，两者务必一致
public sealed class DoThingExecutor : IEffectExecutor
{
    private static readonly EffectParamDef[] Schema =
    {
        new EffectParamDef(""targetId"", EffectParamType.String, false, ""目标ID""),
        new EffectParamDef(""amount"",   EffectParamType.Float,  false, ""数值""),
    };

    public string Key => ""MySystem.DoThing"";
    public string DisplayName => ""做点什么"";
    public string Category => ""我的系统"";
    public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

    public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
    {
        var sink = ctx?.GetService<IMyDomainSink>();      // 写侧服务由宿主注册进效果上下文
        if (sink == null) return EffectResult.Failed(""缺少 IMyDomainSink 服务"");

        string targetId = parameters.Find(""targetId"")?.GetString();
        if (string.IsNullOrEmpty(targetId)) return EffectResult.Failed(""targetId 为空"");
        double amount = parameters.Find(""amount"")?.GetFloat() ?? 0d;

        sink.DoThing(targetId, amount);
        return EffectResult.Applied;                      // Applied 是属性，不是方法
    }
}";
    }
}
