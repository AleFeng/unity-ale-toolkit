using System;
using System.Collections.Generic;
using Ale.GameplayTags;
using Ale.GameplayTags.Editor;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 编辑器效果目录：汇总工程内全部 <see cref="EffectDatabase"/> 资产（与放置位置无关）的效果条目，供上层系统的引用下拉、
    /// 「未找到」标注与「打开」跳转；同 id 先扫到者优先。资产增删改后经 <see cref="EffectDatabasePostprocessor"/> 失效重建。
    /// 同时向 <see cref="GameplayTagEditorCatalog"/> 提供各效果库声明的 Gameplay 标签（编辑态不再往运行时注册表灌标签）。
    /// </summary>
    [InitializeOnLoad]
    public static class EffectEditorCatalog
    {
        /// <summary>一条命中：所属效果库 + 条目。</summary>
        public readonly struct Hit
        {
            public readonly EffectDatabase Database;
            public readonly EffectEntry    Entry;

            public Hit(EffectDatabase database, EffectEntry entry)
            {
                Database = database;
                Entry    = entry;
            }
        }

        private static List<EffectDatabase>    _databases;
        private static List<Hit>               _all;
        private static Dictionary<string, Hit> _byId;

        static EffectEditorCatalog()
        {
            GameplayTagEditorCatalog.RegisterProvider(ProvideTags);
        }

        /// <summary>工程内全部效果库资产。</summary>
        public static IReadOnlyList<EffectDatabase> Databases
        {
            get { EnsureBuilt(); return _databases; }
        }

        /// <summary>全部效果条目（按库、再按列表顺序）。</summary>
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
        public static EffectEntry Find(string id) => TryFind(id, out var hit) ? hit.Entry : null;

        /// <summary>使缓存失效（下次访问重建），并连带失效标签目录。</summary>
        public static void Rebuild()
        {
            _databases = null;
            _all       = null;
            _byId      = null;
            GameplayTagEditorCatalog.Rebuild();
        }

        /// <summary>条目的下拉 / 行显示文本：显示名 (id)；无显示名或同 id 时只显示 id。</summary>
        public static string LabelOf(EffectEntry e)
        {
            if (e == null) return string.Empty;
            string name = e.PlainName();
            return string.IsNullOrEmpty(name) || name == e.id ? e.id : $"{name} ({e.id})";
        }

        /// <summary>纯索引构建（可测试）：按库顺序收集条目，同 id 先到者优先。</summary>
        public static void BuildIndex(IEnumerable<EffectDatabase> databases, List<Hit> all, Dictionary<string, Hit> byId)
        {
            all.Clear();
            byId.Clear();
            if (databases == null) return;
            foreach (var db in databases)
            {
                if (!db) continue;
                foreach (var e in db.Effects)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    var hit = new Hit(db, e);
                    all.Add(hit);
                    if (!byId.ContainsKey(e.id)) byId[e.id] = hit;
                }
            }
        }

        /// <summary>构建「添加效果」菜单：按库分组（库名/显示名 (id)），排除 <paramref name="exclude"/> 中的 id；目录为空时给出提示项。</summary>
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
                menu.AddDisabledItem(new GUIContent(Tr("（无可添加的效果；请先在 Effect Editor 中创建，或在下方输入 id）")));
            return menu;
        }

        private static IEnumerable<GameplayTagDefinition> ProvideTags()
        {
            EnsureBuilt();
            foreach (var db in _databases)
            {
                if (!db) continue;
                foreach (var t in db.GameplayTags)
                    if (t != null) yield return t;
            }
        }

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            _databases = new List<EffectDatabase>();
            foreach (var guid in AssetDatabase.FindAssets("t:EffectDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<EffectDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db) _databases.Add(db);
            }
            _all  = new List<Hit>();
            _byId = new Dictionary<string, Hit>();
            BuildIndex(_databases, _all, _byId);
        }
    }

    /// <summary>资产增删改（.asset）后失效效果目录。</summary>
    internal sealed class EffectDatabasePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(movedTo))
                EffectEditorCatalog.Rebuild();
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
