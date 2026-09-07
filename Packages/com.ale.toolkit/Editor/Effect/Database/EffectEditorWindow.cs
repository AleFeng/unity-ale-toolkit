using System;
using System.Collections.Generic;
using System.IO;
using Ale.Effect.Serialization;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 效果编辑器主窗口（IMGUI）：所有上层系统共用的效果库 <see cref="EffectDatabase"/> 在此配置——效果条目（显示字段 / 自定义属性 / GAS 式定义）、
    /// 效果模板、Gameplay 标签、枚举类型。外壳（数据文件字段 / 页签 / 导出按钮 / 状态栏 / Undo / 路径记忆 / 缓存刷新编排）来自
    /// toolkit 的 <see cref="EditorDatabaseWindowBase{TDb}"/>；上层系统的 Inspector 经 <see cref="EditorEffectRefListDrawer"/> 引用效果并跳转到本窗口。
    /// </summary>
    public sealed class EffectEditorWindow : EditorDatabaseWindowBase<EffectDatabase>, IEffectEditorContext
    {
        private readonly EffectSystemTab _effectTab = new EffectSystemTab();
        private IEditorSystemTab<EffectDatabase>[] _tabs;

        private Dictionary<EEffectEntityKind, HashSet<string>> _duplicateIds;

        private const string WindowTitle = "Effect Editor";
        private static readonly Vector2 WindowDefaultSize = new Vector2(1280f, 780f);

        [MenuItem("Tools/Ale Toolkit/Effect System/Effect Editor", priority = 3002)]
        public static void Open() => OpenWindow();

        /// <summary>打开窗口并载入指定效果库。</summary>
        public static void Open(EffectDatabase db)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            window.Focus();
        }

        /// <summary>打开窗口、载入效果库并定位到指定效果（下一帧 Layout 激活右列 Inspector）。</summary>
        public static void Open(EffectDatabase db, string effectId)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            var entry = db ? db.GetEffect(effectId) : null;
            if (entry != null) window._effectTab.RequestSelect(entry);
            window.Focus();
            window.Repaint();
        }

        /// <summary>新建效果库资产（含一个「通用」模板，便于立即创建效果），创建后在本窗口打开。返回资产或 null（取消）。</summary>
        public static EffectDatabase CreateDatabaseAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                Tr("创建效果库"), "EffectDatabase", "asset", Tr("请选择数据文件保存位置"));
            if (string.IsNullOrEmpty(path)) return null;

            var db = CreateInstance<EffectDatabase>();
            db.EffectTemplates.Add(new EffectTemplate(Tr("通用")));
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            Open(db);
            return db;
        }

        private static EffectEditorWindow OpenWindow()
        {
            bool isNew = !HasOpenInstances<EffectEditorWindow>();
            var window = GetWindow<EffectEditorWindow>(WindowTitle);
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

        protected override string EditorPrefKey => "EffectSystem.DatabasePath";

        protected override string[] SystemTabLabels => new[] { "效果" };

        protected override IEditorSystemTab<EffectDatabase>[] SystemTabs
            => _tabs ??= new IEditorSystemTab<EffectDatabase>[] { _effectTab };

        protected override string EmptyDatabaseHint => Tr("请创建或选择一个 EffectDatabase 效果库");

        protected override EffectDatabase CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                Tr("创建效果库"), "EffectDatabase", "asset", Tr("请选择数据文件保存位置"));
            if (string.IsNullOrEmpty(path)) return null;

            var db = CreateInstance<EffectDatabase>();
            db.EffectTemplates.Add(new EffectTemplate(Tr("通用")));
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            return db;
        }

        protected override void RefreshCaches(EffectDatabase db)
        {
            _duplicateIds = ScanDuplicates(db);
            if (db)
            {
                db.RebuildAllAttributes();   // 模板 schema 变动后同步各效果的自定义字段（幂等）
                db.NormalizeAll();           // 同步定义 id / 显示名、补 null、空阶段改写（幂等）
            }
        }

        protected override string BuildStatusMessage(EffectDatabase db)
        {
            if (_duplicateIds == null) return string.Empty;
            var parts = new List<string>();
            foreach (var kv in _duplicateIds)
                if (kv.Value.Count > 0)
                    parts.Add(Fmt("⚠ {0}重复：{1}（导出已禁用）", NounOf(kv.Key), string.Join(", ", kv.Value)));
            return string.Join("  |  ", parts);
        }

        protected override bool IsExportBlocked(EffectDatabase db)
        {
            if (_duplicateIds == null) return false;
            foreach (var kv in _duplicateIds)
                if (kv.Value.Count > 0) return true;
            return false;
        }

        protected override void DrawExportButtons(EffectDatabase db)
        {
            if (GUILayout.Button(Tr("导出 JSON"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                ExportJson(db);
            if (GUILayout.Button(Tr("导出二进制"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                ExportBinary(db);
        }

        // ── IEffectEditorContext ──────────────────────────────────────────────────

        public HashSet<string> DuplicateIdsOf(EEffectEntityKind kind)
        {
            _duplicateIds ??= ScanDuplicates(Database);
            return _duplicateIds.TryGetValue(kind, out var set) ? set : new HashSet<string>();
        }

        // ── 导出 ──────────────────────────────────────────────────────────────────

        private bool ValidateForExport(EffectDatabase db)
        {
            if (!db) return false;
            if (!db.Validate(out var errors))
            {
                EditorUtility.DisplayDialog(Tr("无法导出"), string.Join("\n", errors), Tr("确定"));
                return false;
            }
            return true;
        }

        private void ExportJson(EffectDatabase db)
        {
            if (!ValidateForExport(db)) return;
            string path = EditorUtility.SaveFilePanel(Tr("导出为 JSON"), Application.dataPath, db.name, "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, EffectConfigSerializer.ExportJson(db, Resolver));
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent(Tr("已导出 JSON")));
        }

        private void ExportBinary(EffectDatabase db)
        {
            if (!ValidateForExport(db)) return;
            string path = EditorUtility.SaveFilePanel(Tr("导出为二进制"), Application.dataPath, db.name, "bytes");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllBytes(path, EffectConfigSerializer.Export(db, Resolver));
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent(Tr("已导出二进制")));
        }

        // ── 查重 ──────────────────────────────────────────────────────────────────

        private static Dictionary<EEffectEntityKind, HashSet<string>> ScanDuplicates(EffectDatabase db)
        {
            var result = new Dictionary<EEffectEntityKind, HashSet<string>>();
            foreach (EEffectEntityKind k in Enum.GetValues(typeof(EEffectEntityKind)))
                result[k] = new HashSet<string>();
            if (!db) return result;

            result[EEffectEntityKind.Effect]   = EditorIdScanner.Scan(db.Effects,         x => x?.id);
            result[EEffectEntityKind.Template] = EditorIdScanner.Scan(db.EffectTemplates, x => x?.name);
            result[EEffectEntityKind.EnumType] = EditorIdScanner.Scan(db.EnumTypesList,   x => x?.name);
            return result;
        }

        private static string NounOf(EEffectEntityKind k)
        {
            switch (k)
            {
                case EEffectEntityKind.Effect:   return Tr("效果 id");
                case EEffectEntityKind.Template: return Tr("效果模板 name");
                default:                         return Tr("枚举类型 name");
            }
        }
    }
}
