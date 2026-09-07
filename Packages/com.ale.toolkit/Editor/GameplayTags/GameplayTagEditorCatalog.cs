using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.GameplayTags.Editor
{
    /// <summary>
    /// 编辑期标签目录：<see cref="GameplayTagRegistry.Default"/>（宿主显式登记 / 播放中）并上工程内全部 <see cref="GameplayTagTable"/> 资产的条目
    /// （含自动补出的祖先），序数排序。惰性构建；注册表变化、标签表资产导入 / 删除（<see cref="GameplayTagTablePostprocessor"/>）时失效。
    /// <b>不要每帧扫描 AssetDatabase</b>——只在缓存失效后的下一次访问重建。
    /// </summary>
    public static class GameplayTagEditorCatalog
    {
        private static List<GameplayTag> _all;
        private static Dictionary<GameplayTag, string> _comments;
        private static HashSet<GameplayTag> _parents;

        static GameplayTagEditorCatalog()
        {
            GameplayTagRegistry.Default.Changed += Rebuild;
        }

        /// <summary>全部已知标签（序数排序）。</summary>
        public static IReadOnlyList<GameplayTag> All
        {
            get { EnsureBuilt(); return _all; }
        }

        /// <summary>目录里是否有该标签（含祖先）。</summary>
        public static bool IsKnown(GameplayTag tag)
        {
            EnsureBuilt();
            return tag.IsValid && _comments.ContainsKey(tag);
        }

        /// <summary>取说明；未知或无说明 → null。</summary>
        public static string CommentOf(GameplayTag tag)
        {
            EnsureBuilt();
            return tag.IsValid && _comments.TryGetValue(tag, out var c) ? c : null;
        }

        /// <summary>目录里是否存在该标签的子级。</summary>
        public static bool HasChildren(GameplayTag tag)
        {
            EnsureBuilt();
            return tag.IsValid && _parents.Contains(tag);
        }

        /// <summary>使缓存失效（下次访问重建）。</summary>
        public static void Rebuild()
        {
            _all      = null;
            _comments = null;
            _parents  = null;
        }

        // ── 标签提供者（编辑态其它目录 / 数据库贡献的标签；不经运行时注册表）──────────
        private static readonly List<Func<IEnumerable<GameplayTagDefinition>>> _providers
            = new List<Func<IEnumerable<GameplayTagDefinition>>>();

        /// <summary>登记一个标签提供者（去重），随即失效目录。宿主数据库 / 效果库在编辑态经此贡献标签，无需往运行时注册表灌数据。</summary>
        public static void RegisterProvider(Func<IEnumerable<GameplayTagDefinition>> provider)
        {
            if (provider == null || _providers.Contains(provider)) return;
            _providers.Add(provider);
            Rebuild();
        }

        /// <summary>移除一个标签提供者，随即失效目录。</summary>
        public static bool UnregisterProvider(Func<IEnumerable<GameplayTagDefinition>> provider)
        {
            bool removed = _providers.Remove(provider);
            if (removed) Rebuild();
            return removed;
        }

        /// <summary>
        /// 构建标签树菜单：路径按 <c>.</c> → <c>/</c> 分层；有子级的标签自身放进其子菜单首项「（叶名）」，避免同名叶项与文件夹并列。
        /// <paramref name="disabled"/> 中的标签以禁用项显示（容器「+ 从目录」用来标出已有项）。
        /// </summary>
        public static GenericMenu BuildTreeMenu(string currentName, Action<string> onSelect, bool includeClear = true,
            ICollection<string> disabled = null)
        {
            EnsureBuilt();
            var menu = new GenericMenu();
            if (includeClear)
            {
                menu.AddItem(new GUIContent("（清空）"), string.IsNullOrEmpty(currentName), () => onSelect?.Invoke(string.Empty));
                menu.AddSeparator(string.Empty);
            }
            if (_all.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("（目录为空：请创建 GameplayTagTable 资产或登记标签）"));
                return menu;
            }
            foreach (var t in _all)
            {
                string name    = t.Name;
                var    content = new GUIContent(MenuPathOf(t), _comments[t]);
                if (disabled != null && disabled.Contains(name))
                    menu.AddDisabledItem(content, name == currentName);
                else
                    menu.AddItem(content, name == currentName, () => onSelect?.Invoke(name));
            }
            return menu;
        }

        private static string MenuPathOf(GameplayTag t)
        {
            string path = t.Name.Replace(GameplayTag.Separator, '/');
            return _parents.Contains(t) ? path + "/（" + t.Leaf + "）" : path;
        }

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            _comments = new Dictionary<GameplayTag, string>();

            // ① 运行时注册表（宿主显式登记 / 播放中）
            var reg = GameplayTagRegistry.Default;
            foreach (var t in reg.All) _comments[t] = reg.GetComment(t);

            // ② 工程内全部标签表资产（与放置位置无关）
            foreach (var guid in AssetDatabase.FindAssets("t:GameplayTagTable"))
            {
                var table = AssetDatabase.LoadAssetAtPath<GameplayTagTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (!table) continue;
                foreach (var e in table.Entries)
                {
                    if (e == null) continue;
                    var tag = new GameplayTag(e.name);
                    if (!tag.IsValid) continue;
                    for (var a = tag; a.IsValid; a = a.Parent)
                        if (!_comments.ContainsKey(a)) _comments[a] = null;
                    if (!string.IsNullOrEmpty(e.comment)) _comments[tag] = e.comment;
                }
            }

            // ③ 编辑态提供者（效果库 / 宿主数据库声明的标签）
            foreach (var provider in _providers)
            {
                IEnumerable<GameplayTagDefinition> defs;
                try { defs = provider(); }
                catch (Exception ex) { Debug.LogWarning("[GameplayTagEditorCatalog] 标签提供者异常：" + ex.Message); continue; }
                if (defs == null) continue;
                foreach (var e in defs)
                {
                    if (e == null) continue;
                    var tag = new GameplayTag(e.name);
                    if (!tag.IsValid) continue;
                    for (var a = tag; a.IsValid; a = a.Parent)
                        if (!_comments.ContainsKey(a)) _comments[a] = null;
                    if (!string.IsNullOrEmpty(e.comment)) _comments[tag] = e.comment;
                }
            }

            _all = new List<GameplayTag>(_comments.Keys);
            _all.Sort();
            _parents = new HashSet<GameplayTag>();
            foreach (var t in _all)
            {
                var p = t.Parent;
                if (p.IsValid) _parents.Add(p);
            }
        }
    }

    /// <summary>标签表资产导入 / 删除 / 移动后使目录失效（惰性重建，开销可忽略）。</summary>
    internal sealed class GameplayTagTablePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(movedTo))
                GameplayTagEditorCatalog.Rebuild();
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
