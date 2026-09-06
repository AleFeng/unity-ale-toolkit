using System;
using System.Collections.Generic;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签注册表（<b>咨询性</b>目录）：记录项目「声明过」的标签名与说明，供编辑器下拉树与配置期校验取用。
    ///
    /// <para><b>运行时的任何匹配都不查注册表</b>——未登记的标签完全合法、照常匹配。注册表只回答「这个名字是不是已知的」，
    /// 目的是让编辑器能列出可选项、并在 <see cref="Validate"/> 里把拼错 / 仅大小写不同的手误抓出来。</para>
    ///
    /// <para>登记一条标签会自动登记其全部祖先（祖先无说明）。来源：Unity 桥在启动时载入的标签表资产、宿主数据库注册时并入的自定义标签。</para>
    /// </summary>
    public sealed class GameplayTagRegistry
    {
        /// <summary>共享默认实例（Unity 桥在启动时清空并重新填充）。</summary>
        public static GameplayTagRegistry Default { get; } = new GameplayTagRegistry();

        private readonly Dictionary<GameplayTag, string> _comments = new Dictionary<GameplayTag, string>();
        private List<GameplayTag> _sorted;

        /// <summary>内容变化（登记新项 / 清空）时触发。</summary>
        public event Action Changed;

        /// <summary>已登记标签数（含自动登记的祖先）。</summary>
        public int Count => _comments.Count;

        /// <summary>登记一条标签（自动补齐祖先）；返回是否有新项被加入。非法名 → false。</summary>
        public bool Register(string rawName, string comment = null)
        {
            var tag = new GameplayTag(rawName);
            if (!tag.IsValid) return false;

            bool added = false;
            for (var t = tag; t.IsValid; t = t.Parent)
            {
                if (_comments.ContainsKey(t)) continue;
                _comments[t] = null;
                added = true;
            }

            bool commentChanged = false;
            if (!string.IsNullOrEmpty(comment) && _comments[tag] != comment)
            {
                _comments[tag] = comment;
                commentChanged = true;
            }

            if (added) _sorted = null;
            if (added || commentChanged) Changed?.Invoke();
            return added;
        }

        /// <summary>登记一组定义条目；返回新增项数。</summary>
        public int Register(IEnumerable<GameplayTagDefinition> definitions)
        {
            if (definitions == null) return 0;
            int added = 0;
            foreach (var d in definitions)
                if (d != null && Register(d.name, d.comment)) added++;
            return added;
        }

        /// <summary>是否已登记（含自动登记的祖先）。</summary>
        public bool IsRegistered(GameplayTag tag) => tag.IsValid && _comments.ContainsKey(tag);

        /// <summary>取说明；未登记或无说明 → null。</summary>
        public string GetComment(GameplayTag tag) => tag.IsValid && _comments.TryGetValue(tag, out var c) ? c : null;

        /// <summary>全部已登记标签（序数排序的快照；内容变化后惰性重建）。</summary>
        public IReadOnlyList<GameplayTag> All
        {
            get
            {
                if (_sorted == null)
                {
                    _sorted = new List<GameplayTag>(_comments.Keys);
                    _sorted.Sort();
                }
                return _sorted;
            }
        }

        /// <summary>把 <paramref name="parent"/> 的直接子级追加进列表（序数有序）；parent 为 <see cref="GameplayTag.None"/> 时取全部根标签。</summary>
        public void GetChildren(GameplayTag parent, List<GameplayTag> into)
        {
            if (into == null) return;
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (parent.IsValid ? t.IsDirectChildOf(parent) : t.Depth == 1)
                    into.Add(t);
            }
        }

        /// <summary>清空。</summary>
        public void Clear()
        {
            if (_comments.Count == 0) return;
            _comments.Clear();
            _sorted = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// 配置期校验一个容器：非法名 → 错误；未登记 → 警告（前缀「警告:」）；未登记但存在仅大小写不同的已登记项 → 警告并指出。
        /// 返回是否无<b>错误</b>（警告不计）。
        /// </summary>
        public bool Validate(GameplayTagContainer container, List<string> messages, string path)
        {
            if (container?.tags == null) return true;
            bool ok = true;
            for (int i = 0; i < container.tags.Count; i++)
            {
                string raw = container.tags[i];
                var tag = new GameplayTag(raw);
                if (!tag.IsValid)
                {
                    messages?.Add($"{path}[{i}]：标签 '{raw}' 不合法");
                    ok = false;
                    continue;
                }
                if (IsRegistered(tag)) continue;

                var similar = FindCaseInsensitive(tag);
                messages?.Add(similar.IsValid
                    ? $"警告:{path}[{i}]：标签 '{tag}' 未登记，但已登记 '{similar}'（仅大小写不同）"
                    : $"警告:{path}[{i}]：标签 '{tag}' 未登记");
            }
            return ok;
        }

        private GameplayTag FindCaseInsensitive(GameplayTag tag)
        {
            foreach (var k in _comments.Keys)
                if (string.Equals(k.Name, tag.Name, StringComparison.OrdinalIgnoreCase)) return k;
            return GameplayTag.None;
        }
    }
}
