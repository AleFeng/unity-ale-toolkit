using UnityEditor;
using UnityEngine;
using Ale.Toolkit.Runtime;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 「数据库配置编辑器」主窗口的泛型基类：抽出与具体系统无关的外壳——
    /// 顶部数据文件对象字段 + 导出/校验按钮区 + 系统页签条 + 页签派发 + 底部状态栏 +
    /// Undo 订阅 + 上次数据库路径记忆（EditorPrefs）+ Layout 阶段的缓存/查重编排。
    ///
    /// <para>宿主（如库存 / 编年史）派生本类并闭合 <typeparamref name="TDb"/>，只提供领域专有的少量钩子：
    /// 页签集合与名称、创建数据库、缓存刷新（RebuildAttributes / 查重）、状态栏文案、导出是否禁用、导出按钮。
    /// 本类实现 <see cref="IEditorDbContext{TDb}"/>，页签与绘制器通过它读写数据、记 Undo、标脏。</para>
    ///
    /// <para>本类仅依赖 <see cref="EditorWindow"/> 与 toolkit 通用件，无 Addressables / Localization 硬编译宏约束，
    /// 可被受宏约束的宿主程序集继承。</para>
    /// </summary>
    /// <typeparam name="TDb">宿主的数据库资产类型（<see cref="ScriptableObject"/>）。</typeparam>
    public abstract class EditorDatabaseWindowBase<TDb> : EditorWindow, IEditorDbContext<TDb>
        where TDb : ScriptableObject
    {
        private const float ToolbarHeight   = 26f;
        private const float TabRowHeight    = 24f;
        private const float StatusBarHeight = 22f;

        private TDb              _db;
        private SerializedObject _serialized;
        private int              _systemTab;
        private bool             _cachesDirty = true;
        private string           _snapStatusMessage;

        // ── 子类契约（必须实现）───────────────────────────────────────────────────

        /// <summary>记忆「上次数据库资产路径」用的 EditorPrefs 键（各宿主唯一）。</summary>
        protected abstract string EditorPrefKey { get; }

        /// <summary>系统页签名称（可由宿主本地化）。与 <see cref="SystemTabs"/> 同序、等长。</summary>
        protected abstract string[] SystemTabLabels { get; }

        /// <summary>系统页签实例（与 <see cref="SystemTabLabels"/> 同序、等长）。</summary>
        protected abstract IEditorSystemTab<TDb>[] SystemTabs { get; }

        /// <summary>无数据库时的占位提示文案。</summary>
        protected abstract string EmptyDatabaseHint { get; }

        /// <summary>创建并返回一个新数据库资产（含保存对话框）；用户取消返回 null。</summary>
        protected abstract TDb CreateNewDatabase();

        /// <summary>在 Layout 阶段、缓存标脏后调用：宿主在此做属性对账（RebuildAttributes）、重复 ID 扫描等。</summary>
        protected abstract void RefreshCaches(TDb db);

        /// <summary>状态栏文案（如重复 ID 警告）；无则返回空串。于 Layout 阶段、<see cref="RefreshCaches"/> 之后取。</summary>
        protected abstract string BuildStatusMessage(TDb db);

        /// <summary>导出是否应被禁用（如存在非空重复 ID）。</summary>
        protected abstract bool IsExportBlocked(TDb db);

        /// <summary>绘制宿主自己的导出/校验按钮（在工具栏右侧，已按 <see cref="IsExportBlocked"/> 与「无数据库」置灰）。</summary>
        protected abstract void DrawExportButtons(TDb db);

        // ── 子类契约（可选）───────────────────────────────────────────────────────

        /// <summary>窗口最小尺寸。</summary>
        protected virtual Vector2 MinWindowSize => new Vector2(900f, 520f);

        /// <summary>工具栏「数据文件」字段之后、导出按钮之前的额外内容（默认空）。</summary>
        protected virtual void DrawExtraToolbar(TDb db) { }

        // ── 访问器 ────────────────────────────────────────────────────────────────

        /// <summary>当前数据库（可能为 null）。</summary>
        protected TDb Db => _db;

        /// <summary>把窗口缓存标记为需刷新（下个 Layout 阶段调用 <see cref="RefreshCaches"/>）。</summary>
        protected void MarkCachesDirty() => _cachesDirty = true;

        /// <summary>切换当前数据库（重建 SerializedObject、记忆路径、通知各页签、标脏缓存）。</summary>
        protected void SetDatabase(TDb db)
        {
            _db          = db;
            _serialized  = db ? new SerializedObject(db) : null;
            _cachesDirty = true;
            if (db) EditorPrefs.SetString(EditorPrefKey, AssetDatabase.GetAssetPath(db));

            var tabs = SystemTabs;
            if (tabs != null)
                foreach (var t in tabs) t?.OnDatabaseChanged(this);
        }

        // ── IEditorDbContext<TDb> / IEditorContext ────────────────────────────────

        public TDb Database => _db;
        public SerializedObject Serialized => _serialized;

        public virtual IAssetRefResolver Resolver =>
            EditorExportResolver.Resolve(ToolkitDefines.IsAddressableEnabled());

        public void RecordUndo(string actionName)
        {
            if (_db) Undo.RecordObject(_db, actionName);
        }

        public void MarkDirty()
        {
            if (_db) EditorUtility.SetDirty(_db);
            _cachesDirty = true;
            _serialized?.Update();
        }

        // Repaint() 继承自 EditorWindow，已满足 IEditorContext。

        // ── 生命周期 ──────────────────────────────────────────────────────────────

        protected virtual void OnEnable()
        {
            minSize = MinWindowSize;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            string path = EditorPrefs.GetString(EditorPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                var db = AssetDatabase.LoadAssetAtPath<TDb>(path);
                if (db) SetDatabase(db);
            }
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            _serialized?.UpdateIfRequiredOrScript();
            var tabs = SystemTabs;
            if (tabs != null)
                foreach (var t in tabs) t?.OnUndoRedo();
            _cachesDirty = true;
            Repaint();
        }

        // ── 绘制 ──────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Layout 阶段刷新缓存与状态快照，保证 Repaint 时控件数量一致。
            if (Event.current.type == EventType.Layout)
            {
                if (_cachesDirty)
                {
                    _cachesDirty = false;
                    if (_db) RefreshCaches(_db);
                }
                _snapStatusMessage = _db ? BuildStatusMessage(_db) : string.Empty;
            }

            DrawToolbar();
            DrawSystemTabRow();

            var bodyRect = new Rect(0, ToolbarHeight + TabRowHeight, position.width,
                position.height - ToolbarHeight - TabRowHeight - StatusBarHeight);
            if (!_db) DrawNoDatabase(bodyRect);
            else      DrawBody(bodyRect);

            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            var rect = new Rect(0, 0, position.width, ToolbarHeight);
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("数据文件", GUILayout.Width(56));
            var newDb = (TDb)EditorGUILayout.ObjectField(_db, typeof(TDb), false, GUILayout.Width(240));
            if (newDb != _db) SetDatabase(newDb);

            DrawExtraToolbar(_db);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!_db))
            {
                bool blocked = _db != null && IsExportBlocked(_db);
                using (new EditorGUI.DisabledScope(blocked))
                    DrawExportButtons(_db);
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSystemTabRow()
        {
            var labels = SystemTabLabels;
            var rect = new Rect(0, ToolbarHeight, position.width, TabRowHeight);
            GUILayout.BeginArea(rect);
            if (labels != null && labels.Length > 0)
            {
                _systemTab = Mathf.Clamp(_systemTab, 0, labels.Length - 1);
                _systemTab = GUILayout.Toolbar(_systemTab, labels, GUILayout.Height(TabRowHeight - 2));
            }
            GUILayout.EndArea();
        }

        private void DrawBody(Rect rect)
        {
            var tabs = SystemTabs;
            if (tabs == null || tabs.Length == 0) return;
            int idx = Mathf.Clamp(_systemTab, 0, tabs.Length - 1);
            GUILayout.BeginArea(rect);
            tabs[idx]?.OnGUI(new Rect(0, 0, rect.width, rect.height), this);
            GUILayout.EndArea();
        }

        private void DrawNoDatabase(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.Width(360));
            EditorGUILayout.LabelField(EmptyDatabaseHint, ToolkitEditorStyles.Placeholder);
            EditorGUILayout.Space(8);
            if (GUILayout.Button("创建新的数据文件", GUILayout.Height(30)))
            {
                var db = CreateNewDatabase();
                if (db) SetDatabase(db);
            }
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawStatusBar()
        {
            var rect = new Rect(0, position.height - StatusBarHeight, position.width, StatusBarHeight);
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));
            if (!string.IsNullOrEmpty(_snapStatusMessage))
            {
                var labelRect = new Rect(rect.x + 8, rect.y + 2, rect.width - 16, rect.height - 4);
                GUI.Label(labelRect, _snapStatusMessage, ToolkitEditorStyles.StatusError);
            }
        }
    }
}
