using System;
using System.Collections.Generic;
using System.IO;
using Ale.Condition.Serialization;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 条件编辑器主窗口（IMGUI），两个页签：
    /// <list type="number">
    /// <item><b>Condition Evaluators</b>——工程里全部条件实现（<see cref="IConditionEvaluator"/>）的目录：搜索 / 分类 / 源码跳转 /
    /// 静默失效诊断 / 配置引用交叉核对。内容来自代码，<b>不需要条件库资产</b>。</item>
    /// <item><b>Condition Database</b>——所有上层系统共用的条件库 <see cref="ConditionDatabase"/>（可选）：条件条目
    /// （显示字段 / 自定义属性 / 两级 AND/OR 表达式）、条件模板、枚举类型。</item>
    /// </list>
    /// 外壳（数据文件字段 / 页签 / 导出按钮 / 状态栏 / Undo / 路径与页签记忆）来自 toolkit 的
    /// <see cref="EditorDatabaseWindowBase{TDb}"/>；上层系统的 Inspector 经 <see cref="EditorConditionRefListDrawer"/>
    /// 引用条件并跳转到本窗口的 Condition Database 页。与 Effect Editor 结构对称。
    /// </summary>
    public sealed class ConditionEditorWindow : EditorDatabaseWindowBase<ConditionDatabase>, IConditionEditorContext
    {
        /// <summary>页签索引：判定器目录（不需要条件库）。</summary>
        private const int TabEvaluators = 0;

        /// <summary>页签索引：条件库配置。</summary>
        private const int TabDatabase = 1;

        private readonly ConditionEvaluatorTab _evaluatorTab = new ConditionEvaluatorTab();
        private readonly ConditionSystemTab    _conditionTab = new ConditionSystemTab();
        private IEditorSystemTab<ConditionDatabase>[] _tabs;

        private Dictionary<EConditionEntityKind, HashSet<string>> _duplicateIds;

        private const string WindowTitle = "Condition Editor";
        private static readonly Vector2 WindowDefaultSize = new Vector2(1280f, 780f);

        [MenuItem("Tools/Ale Toolkit/Condition System/Condition Editor", priority = 2002)]
        public static void Open() => OpenWindow();

        /// <summary>打开窗口并切到「Condition Evaluators」页（判定器目录；不需要条件库）。</summary>
        public static void OpenEvaluators()
        {
            var window = OpenWindow();
            window.SelectSystemTab(TabEvaluators);
            window.Focus();
        }

        /// <summary>打开窗口、切到「Condition Database」页并载入指定条件库。</summary>
        public static void Open(ConditionDatabase db)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            window.SelectSystemTab(TabDatabase);
            window.Focus();
        }

        /// <summary>打开窗口、切到「Condition Database」页、载入条件库并定位到指定条件（下一帧 Layout 激活右列 Inspector）。</summary>
        public static void Open(ConditionDatabase db, string conditionId)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            window.SelectSystemTab(TabDatabase);
            var entry = db ? db.GetEntry(conditionId) : null;
            if (entry != null) window._conditionTab.RequestSelect(entry);
            window.Focus();
            window.Repaint();
        }

        /// <summary>新建条件库资产（含一个「通用」模板，便于立即创建条件），创建后在本窗口打开。返回资产或 null（取消）。</summary>
        public static ConditionDatabase CreateDatabaseAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                Tr("创建条件库"), "ConditionDatabase", "asset", Tr("请选择数据文件保存位置"));
            if (string.IsNullOrEmpty(path)) return null;

            var db = CreateInstance<ConditionDatabase>();
            db.ConditionTemplates.Add(new ConditionTemplate(Tr("通用")));
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            ConditionEditorCatalog.Rebuild();
            Open(db);
            return db;
        }

        private static ConditionEditorWindow OpenWindow()
        {
            bool isNew = !HasOpenInstances<ConditionEditorWindow>();
            var window = GetWindow<ConditionEditorWindow>(WindowTitle);
            if (isNew)
            {
                var main = EditorGUIUtility.GetMainWindowPosition();
                float x = main.x + (main.width  - WindowDefaultSize.x) * 0.5f;
                float y = main.y + (main.height - WindowDefaultSize.y) * 0.5f;
                window.position = new Rect(x, y, WindowDefaultSize.x, WindowDefaultSize.y);
            }
            window.Show();
            return window;
        }

        // ── 基类钩子 ──────────────────────────────────────────────────────────────

        protected override string EditorPrefKey => "ConditionSystem.DatabasePath";

        // 两个页签名是与类型名对齐的英文专名（ConditionDatabase / IConditionEvaluator），不走 Tr()。
        protected override string[] SystemTabLabels => new[] { "Condition Evaluators", "Condition Database" };

        protected override IEditorSystemTab<ConditionDatabase>[] SystemTabs
            => _tabs ??= new IEditorSystemTab<ConditionDatabase>[] { _evaluatorTab, _conditionTab };

        /// <summary>判定器目录读的是代码而非资产，没有条件库时照样可用。</summary>
        protected override bool TabRequiresDatabase(int tabIndex) => tabIndex != TabEvaluators;

        protected override string EmptyDatabaseHint => Tr("请创建或选择一个 ConditionDatabase 条件库");

        protected override ConditionDatabase CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                Tr("创建条件库"), "ConditionDatabase", "asset", Tr("请选择数据文件保存位置"));
            if (string.IsNullOrEmpty(path)) return null;

            var db = CreateInstance<ConditionDatabase>();
            db.ConditionTemplates.Add(new ConditionTemplate(Tr("通用")));
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            ConditionEditorCatalog.Rebuild();
            return db;
        }

        protected override void RefreshCaches(ConditionDatabase db)
        {
            _duplicateIds = ScanDuplicates(db);
            if (db)
            {
                db.RebuildAllAttributes();   // 模板 schema 变动后同步各条件的自定义字段（幂等）
                db.NormalizeAll();           // 补 null、剔除 null 组 / 项（幂等）
            }
        }

        protected override string BuildStatusMessage(ConditionDatabase db)
        {
            if (_duplicateIds == null) return string.Empty;
            var parts = new List<string>();
            foreach (var kv in _duplicateIds)
                if (kv.Value.Count > 0)
                    parts.Add(Fmt("⚠ {0}重复：{1}（导出已禁用）", NounOf(kv.Key), string.Join(", ", kv.Value)));
            return string.Join("  |  ", parts);
        }

        protected override bool IsExportBlocked(ConditionDatabase db)
        {
            if (_duplicateIds == null) return false;
            foreach (var kv in _duplicateIds)
                if (kv.Value.Count > 0) return true;
            return false;
        }

        protected override void DrawExportButtons(ConditionDatabase db)
        {
            if (GUILayout.Button(Tr("导出 JSON"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                ExportJson(db);
            if (GUILayout.Button(Tr("导出二进制"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                ExportBinary(db);
        }

        // ── IConditionEditorContext ───────────────────────────────────────────────

        public HashSet<string> DuplicateIdsOf(EConditionEntityKind kind)
        {
            _duplicateIds ??= ScanDuplicates(Database);
            return _duplicateIds.TryGetValue(kind, out var set) ? set : new HashSet<string>();
        }

        // ── 导出 ──────────────────────────────────────────────────────────────────

        private bool ValidateForExport(ConditionDatabase db)
        {
            if (!db) return false;
            if (!db.Validate(out var errors))
            {
                EditorUtility.DisplayDialog(Tr("无法导出"), string.Join("\n", errors), Tr("确定"));
                return false;
            }
            return true;
        }

        private void ExportJson(ConditionDatabase db)
        {
            if (!ValidateForExport(db)) return;
            string path = EditorUtility.SaveFilePanel(Tr("导出为 JSON"), Application.dataPath, db.name, "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, ConditionConfigSerializer.ExportJson(db, Resolver));
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent(Tr("已导出 JSON")));
        }

        private void ExportBinary(ConditionDatabase db)
        {
            if (!ValidateForExport(db)) return;
            string path = EditorUtility.SaveFilePanel(Tr("导出为二进制"), Application.dataPath, db.name, "bytes");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllBytes(path, ConditionConfigSerializer.Export(db, Resolver));
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent(Tr("已导出二进制")));
        }

        // ── 查重 ──────────────────────────────────────────────────────────────────

        private static Dictionary<EConditionEntityKind, HashSet<string>> ScanDuplicates(ConditionDatabase db)
        {
            var result = new Dictionary<EConditionEntityKind, HashSet<string>>();
            foreach (EConditionEntityKind k in Enum.GetValues(typeof(EConditionEntityKind)))
                result[k] = new HashSet<string>();
            if (!db) return result;

            result[EConditionEntityKind.Condition] = EditorIdScanner.Scan(db.Conditions,         x => x?.id);
            result[EConditionEntityKind.Template]  = EditorIdScanner.Scan(db.ConditionTemplates, x => x?.name);
            result[EConditionEntityKind.EnumType]  = EditorIdScanner.Scan(db.EnumTypesList,      x => x?.name);
            return result;
        }

        private static string NounOf(EConditionEntityKind k)
        {
            switch (k)
            {
                case EConditionEntityKind.Condition: return Tr("条件 id");
                case EConditionEntityKind.Template:  return Tr("条件模板 name");
                default:                             return Tr("枚举类型 name");
            }
        }
    }
}
