using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 编辑器条件目录：汇总工程内全部 <see cref="ConditionDatabase"/> 资产（与放置位置无关）的条件条目，供上层系统的引用下拉、
    /// 「未找到」标注与「打开」跳转；同 id 先扫到者优先。资产增删改后经 <see cref="ConditionDatabasePostprocessor"/> 失效重建。
    /// 与效果侧的 <c>EffectEditorCatalog</c> 同构。
    /// </summary>
    public static class ConditionEditorCatalog
    {
        /// <summary>一条命中：所属条件库 + 条目。</summary>
        public readonly struct Hit
        {
            public readonly ConditionDatabase Database;
            public readonly ConditionEntry    Entry;

            public Hit(ConditionDatabase database, ConditionEntry entry)
            {
                Database = database;
                Entry    = entry;
            }
        }

        private static List<ConditionDatabase>  _databases;
        private static List<Hit>                _all;
        private static Dictionary<string, Hit>  _byId;

        /// <summary>工程内全部条件库资产。</summary>
        public static IReadOnlyList<ConditionDatabase> Databases
        {
            get { EnsureBuilt(); return _databases; }
        }

        /// <summary>全部条件条目（按库、再按列表顺序）。</summary>
        public static IReadOnlyList<Hit> All
        {
            get { EnsureBuilt(); return _all; }
        }

        /// <summary>按 id 查找（先扫到者优先）。</summary>
        public static bool TryFind(string id, out Hit hit)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out hit)) return true;
            hit = default;
            return false;
        }

        /// <summary>按 id 查找条目，未找到返回 null。</summary>
        public static ConditionEntry Find(string id) => TryFind(id, out var hit) ? hit.Entry : null;

        /// <summary>使缓存失效（下次访问重建）。</summary>
        public static void Rebuild()
        {
            _databases = null;
            _all       = null;
            _byId      = null;
        }

        /// <summary>条目的下拉 / 行显示文本：显示名 (id)；无显示名或同 id 时只显示 id。</summary>
        public static string LabelOf(ConditionEntry e)
        {
            if (e == null) return string.Empty;
            string name = e.PlainName();
            return string.IsNullOrEmpty(name) || name == e.id ? e.id : $"{name} ({e.id})";
        }

        /// <summary>纯索引构建（可测试）：按库顺序收集条目，同 id 先到者优先。</summary>
        public static void BuildIndex(IEnumerable<ConditionDatabase> databases, List<Hit> all, Dictionary<string, Hit> byId)
        {
            all.Clear();
            byId.Clear();
            if (databases == null) return;
            foreach (var db in databases)
            {
                if (!db) continue;
                foreach (var e in db.Conditions)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    var hit = new Hit(db, e);
                    all.Add(hit);
                    if (!byId.ContainsKey(e.id)) byId[e.id] = hit;
                }
            }
        }

        /// <summary>构建「添加条件」菜单：按库分组（库名/显示名 (id)），排除 <paramref name="exclude"/> 中的 id；目录为空时给出提示项。</summary>
        public static GenericMenu BuildMenu(ICollection<string> exclude, Action<string> onSelect)
        {
            EnsureBuilt();
            var menu = new GenericMenu();
            bool multiDb = _databases.Count > 1;
            bool any = false;
            foreach (var hit in _all)
            {
                string id = hit.Entry.id;
                if (exclude != null && exclude.Contains(id)) continue;
                any = true;
                string label = LabelOf(hit.Entry).Replace('/', '／');
                string path  = multiDb ? hit.Database.name + "/" + label : label;
                menu.AddItem(new GUIContent(path), false, () => onSelect?.Invoke(id));
            }
            if (!any)
                menu.AddDisabledItem(new GUIContent(Tr("（无可添加的条件；请先在 Condition Editor 中创建，或在下方输入 id）")));
            return menu;
        }

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            _databases = new List<ConditionDatabase>();
            foreach (var guid in AssetDatabase.FindAssets("t:ConditionDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<ConditionDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db) _databases.Add(db);
            }
            _all  = new List<Hit>();
            _byId = new Dictionary<string, Hit>();
            BuildIndex(_databases, _all, _byId);
        }
    }

    /// <summary>资产增删改（.asset）后失效条件目录。</summary>
    internal sealed class ConditionDatabasePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(movedTo))
                ConditionEditorCatalog.Rebuild();
        }

        private static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            foreach (var p in paths)
                if (p != null && p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
