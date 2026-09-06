using System;
using System.Collections.Generic;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 计数标签容器（运行时，不序列化；GAS <c>FGameplayTagCountContainer</c> 对应物）：拥有者「当前持有哪些标签、各几份」。
    /// 显式计数记录被直接添加的标签；隐式计数对该标签自身与每个祖先各 +1，因此 <see cref="HasMatchingTag"/> 是 O(1) 字典查表。
    ///
    /// <para>事件 <see cref="OnTagCountChanged"/> 在任一隐式计数变化后触发（先更新完全部计数，再逐个通知），
    /// 新计数为 0 即「不再持有」。效果容器据此重评「持续标签要求」。仅主线程。</para>
    /// </summary>
    public sealed class GameplayTagCountContainer
    {
        private readonly Dictionary<GameplayTag, int> _explicit = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<GameplayTag, int> _implicit = new Dictionary<GameplayTag, int>();

        /// <summary>某标签的隐式计数变化（tag, 新隐式计数）。一次添加 <c>A.B.C</c> 依次对 <c>A.B.C</c>、<c>A.B</c>、<c>A</c> 触发。</summary>
        public event Action<GameplayTag, int> OnTagCountChanged;

        /// <summary>显式标签种类数。</summary>
        public int Count => _explicit.Count;

        // ── 增删 ──────────────────────────────────────────────────────────────────

        /// <summary>添加 <paramref name="count"/> 份标签，返回其新显式计数；无效标签或 count ≤ 0 → 忽略并返回 0。</summary>
        public int AddTag(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count <= 0) return 0;

            _explicit.TryGetValue(tag, out int e);
            e += count;
            _explicit[tag] = e;

            for (var t = tag; t.IsValid; t = t.Parent)
            {
                _implicit.TryGetValue(t, out int c);
                _implicit[t] = c + count;
            }
            for (var t = tag; t.IsValid; t = t.Parent)
                OnTagCountChanged?.Invoke(t, GetTagCount(t));
            return e;
        }

        /// <summary>移除 <paramref name="count"/> 份标签（钳到 0，归零即删键），返回剩余显式计数。</summary>
        public int RemoveTag(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count <= 0) return GetExplicitTagCount(tag);
            if (!_explicit.TryGetValue(tag, out int e)) return 0;

            int removed = count < e ? count : e;
            e -= removed;
            if (e == 0) _explicit.Remove(tag);
            else _explicit[tag] = e;

            for (var t = tag; t.IsValid; t = t.Parent)
            {
                _implicit.TryGetValue(t, out int c);
                c -= removed;
                if (c <= 0) _implicit.Remove(t);
                else _implicit[t] = c;
            }
            for (var t = tag; t.IsValid; t = t.Parent)
                OnTagCountChanged?.Invoke(t, GetTagCount(t));
            return e;
        }

        /// <summary>把容器里的每个标签各添加一份。</summary>
        public void AddTags(GameplayTagContainer container)
        {
            if (container?.tags == null) return;
            for (int i = 0; i < container.tags.Count; i++)
                AddTag(new GameplayTag(container.tags[i]));
        }

        /// <summary>把容器里的每个标签各移除一份。</summary>
        public void RemoveTags(GameplayTagContainer container)
        {
            if (container?.tags == null) return;
            for (int i = 0; i < container.tags.Count; i++)
                RemoveTag(new GameplayTag(container.tags[i]));
        }

        /// <summary>清空全部计数；对每个此前持有（含隐式）的标签触发计数 0。</summary>
        public void Clear()
        {
            if (_implicit.Count == 0)
            {
                _explicit.Clear();
                return;
            }
            var keys = new List<GameplayTag>(_implicit.Keys);
            _explicit.Clear();
            _implicit.Clear();
            foreach (var k in keys)
                OnTagCountChanged?.Invoke(k, 0);
        }

        // ── 查询 ──────────────────────────────────────────────────────────────────

        /// <summary>层级：持有 <paramref name="tag"/> 自身或其任一后代（隐式计数 &gt; 0）。</summary>
        public bool HasMatchingTag(GameplayTag tag) => tag.IsValid && _implicit.TryGetValue(tag, out int c) && c > 0;

        /// <summary>精确：显式持有 <paramref name="tag"/> 本身。</summary>
        public bool HasExactTag(GameplayTag tag) => tag.IsValid && _explicit.ContainsKey(tag);

        /// <summary>隐式计数（自身 + 后代份数之和）。</summary>
        public int GetTagCount(GameplayTag tag) => tag.IsValid && _implicit.TryGetValue(tag, out int c) ? c : 0;

        /// <summary>显式计数。</summary>
        public int GetExplicitTagCount(GameplayTag tag) => tag.IsValid && _explicit.TryGetValue(tag, out int c) ? c : 0;

        /// <summary>层级：与 <paramref name="container"/> 任一标签匹配。空集 → false。</summary>
        public bool HasAny(GameplayTagContainer container)
        {
            if (container?.tags == null) return false;
            for (int i = 0; i < container.tags.Count; i++)
                if (HasMatchingTag(new GameplayTag(container.tags[i]))) return true;
            return false;
        }

        /// <summary>层级：与 <paramref name="container"/> 全部标签匹配。空集 → true。</summary>
        public bool HasAll(GameplayTagContainer container)
        {
            if (container?.tags == null) return true;
            for (int i = 0; i < container.tags.Count; i++)
                if (!HasMatchingTag(new GameplayTag(container.tags[i]))) return false;
            return true;
        }

        /// <summary>精确：显式持有 <paramref name="container"/> 任一标签。空集 → false。</summary>
        public bool HasAnyExact(GameplayTagContainer container)
        {
            if (container?.tags == null) return false;
            for (int i = 0; i < container.tags.Count; i++)
                if (HasExactTag(new GameplayTag(container.tags[i]))) return true;
            return false;
        }

        /// <summary>精确：显式持有 <paramref name="container"/> 全部标签。空集 → true。</summary>
        public bool HasAllExact(GameplayTagContainer container)
        {
            if (container?.tags == null) return true;
            for (int i = 0; i < container.tags.Count; i++)
                if (!HasExactTag(new GameplayTag(container.tags[i]))) return false;
            return true;
        }

        /// <summary>把显式标签（序数排序，结果稳定）写入容器。</summary>
        public void GetExplicitTags(GameplayTagContainer into)
        {
            if (into == null) return;
            var list = new List<GameplayTag>(_explicit.Keys);
            list.Sort();
            foreach (var t in list) into.AddTag(t);
        }

        /// <summary>把显式标签（序数排序）追加进列表。</summary>
        public void GetExplicitTags(List<GameplayTag> into)
        {
            if (into == null) return;
            int start = into.Count;
            into.AddRange(_explicit.Keys);
            into.Sort(start, into.Count - start, null);
        }
    }
}
