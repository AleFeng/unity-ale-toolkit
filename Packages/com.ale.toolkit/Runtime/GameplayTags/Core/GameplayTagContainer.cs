using System;
using System.Collections.Generic;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签容器（配置侧，<c>[Serializable]</c>）：一组去重、保序的标签名。唯一序列化字段是 <see cref="tags"/>
    /// （<c>List&lt;string&gt;</c>），Unity 原生序列化与 Newtonsoft 皆可直接往返。
    ///
    /// <para><b>查询语义</b>：<see cref="HasTag"/> 系列为层级匹配——容器里任一标签是查询标签自身或其后代即命中
    /// （<c>{ "A.1" }.HasTag("A") == true</c>）；<c>*Exact</c> 系列精确相等。
    /// <see cref="HasAll"/> 对空集恒 true、<see cref="HasAny"/> 对空集恒 false。</para>
    ///
    /// <para>无运行时缓存：定义里的容器规模通常不过十几项，逐项前缀比较即可；拥有者侧的热路径请用
    /// <see cref="GameplayTagCountContainer"/>。</para>
    /// </summary>
    [Serializable]
    public class GameplayTagContainer
    {
        /// <summary>标签名列表（已归一、去重、保序）。请经 <see cref="AddTag(GameplayTag)"/> 等方法改动。</summary>
        public List<string> tags = new List<string>();

        public GameplayTagContainer()
        {
        }

        public GameplayTagContainer(params string[] tags)
        {
            if (tags == null) return;
            foreach (var t in tags) AddTag(t);
        }

        /// <summary>标签数。</summary>
        public int Count => tags?.Count ?? 0;

        /// <summary>是否为空。</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>按下标取标签（无效名得到 <see cref="GameplayTag.None"/>）。</summary>
        public GameplayTag GetTag(int index) => new GameplayTag(tags[index]);

        /// <summary>把全部合法标签追加进 <paramref name="into"/>。</summary>
        public void GetTags(List<GameplayTag> into)
        {
            if (into == null || tags == null) return;
            for (int i = 0; i < tags.Count; i++)
            {
                var t = new GameplayTag(tags[i]);
                if (t.IsValid) into.Add(t);
            }
        }

        // ── 增删 ──────────────────────────────────────────────────────────────────

        /// <summary>添加标签；无效或已存在（精确）→ false。</summary>
        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            tags ??= new List<string>();
            if (IndexOfExact(tag) >= 0) return false;
            tags.Add(tag.Name);
            return true;
        }

        /// <summary>添加标签（原始字符串按归一规则整理）；非法或已存在 → false。</summary>
        public bool AddTag(string raw) => AddTag(new GameplayTag(raw));

        /// <summary>并入另一容器的全部标签，返回新增数。</summary>
        public int AddTags(GameplayTagContainer other)
        {
            if (other?.tags == null) return 0;
            int added = 0;
            for (int i = 0; i < other.tags.Count; i++)
                if (AddTag(new GameplayTag(other.tags[i]))) added++;
            return added;
        }

        /// <summary>精确移除一个标签（GAS 语义：不影响其后代）。</summary>
        public bool RemoveTag(GameplayTag tag)
        {
            int i = IndexOfExact(tag);
            if (i < 0) return false;
            tags.RemoveAt(i);
            return true;
        }

        /// <summary>移除 <paramref name="parent"/> 自身及其全部后代，返回移除数。</summary>
        public int RemoveTagsMatching(GameplayTag parent)
        {
            if (tags == null || !parent.IsValid) return 0;
            int removed = 0;
            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (new GameplayTag(tags[i]).MatchesTag(parent))
                {
                    tags.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>精确移除另一容器里的每个标签，返回移除数。</summary>
        public int RemoveTags(GameplayTagContainer other)
        {
            if (other?.tags == null) return 0;
            int removed = 0;
            for (int i = 0; i < other.tags.Count; i++)
                if (RemoveTag(new GameplayTag(other.tags[i]))) removed++;
            return removed;
        }

        /// <summary>清空。</summary>
        public void Clear() => tags?.Clear();

        // ── 查询 ──────────────────────────────────────────────────────────────────

        /// <summary>层级：容器里任一标签是 <paramref name="tag"/> 自身或其后代。</summary>
        public bool HasTag(GameplayTag tag)
        {
            if (tags == null || !tag.IsValid) return false;
            for (int i = 0; i < tags.Count; i++)
                if (new GameplayTag(tags[i]).MatchesTag(tag)) return true;
            return false;
        }

        /// <summary>精确：容器里含 <paramref name="tag"/> 本身。</summary>
        public bool HasTagExact(GameplayTag tag) => IndexOfExact(tag) >= 0;

        /// <summary>层级：与 <paramref name="other"/> 任一标签匹配。other 为空 → false。</summary>
        public bool HasAny(GameplayTagContainer other)
        {
            if (other?.tags == null) return false;
            for (int i = 0; i < other.tags.Count; i++)
                if (HasTag(new GameplayTag(other.tags[i]))) return true;
            return false;
        }

        /// <summary>层级：与 <paramref name="other"/> 全部标签匹配。other 为空 → true。</summary>
        public bool HasAll(GameplayTagContainer other)
        {
            if (other?.tags == null) return true;
            for (int i = 0; i < other.tags.Count; i++)
                if (!HasTag(new GameplayTag(other.tags[i]))) return false;
            return true;
        }

        /// <summary>精确：含 <paramref name="other"/> 任一标签。other 为空 → false。</summary>
        public bool HasAnyExact(GameplayTagContainer other)
        {
            if (other?.tags == null) return false;
            for (int i = 0; i < other.tags.Count; i++)
                if (HasTagExact(new GameplayTag(other.tags[i]))) return true;
            return false;
        }

        /// <summary>精确：含 <paramref name="other"/> 全部标签。other 为空 → true。</summary>
        public bool HasAllExact(GameplayTagContainer other)
        {
            if (other?.tags == null) return true;
            for (int i = 0; i < other.tags.Count; i++)
                if (!HasTagExact(new GameplayTag(other.tags[i]))) return false;
            return true;
        }

        /// <summary>取本容器中「是 <paramref name="other"/> 任一标签自身或其后代」的子集（新容器）。</summary>
        public GameplayTagContainer Filter(GameplayTagContainer other)
        {
            var result = new GameplayTagContainer();
            if (tags == null || other?.tags == null) return result;
            for (int i = 0; i < tags.Count; i++)
            {
                var t = new GameplayTag(tags[i]);
                if (!t.IsValid) continue;
                for (int j = 0; j < other.tags.Count; j++)
                {
                    if (t.MatchesTag(new GameplayTag(other.tags[j])))
                    {
                        result.AddTag(t);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>取本容器中精确出现在 <paramref name="other"/> 里的子集（新容器）。</summary>
        public GameplayTagContainer FilterExact(GameplayTagContainer other)
        {
            var result = new GameplayTagContainer();
            if (tags == null || other == null) return result;
            for (int i = 0; i < tags.Count; i++)
            {
                var t = new GameplayTag(tags[i]);
                if (t.IsValid && other.HasTagExact(t)) result.AddTag(t);
            }
            return result;
        }

        // ── 维护 ──────────────────────────────────────────────────────────────────

        /// <summary>深拷贝。</summary>
        public GameplayTagContainer Clone()
        {
            var c = new GameplayTagContainer();
            if (tags != null) c.tags = new List<string>(tags);
            return c;
        }

        /// <summary>归一：剔除非法与重复项、把写法整理为规范名。编辑器与校验前调用（幂等）。</summary>
        public void Normalize()
        {
            if (tags == null)
            {
                tags = new List<string>();
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < tags.Count;)
            {
                string n = GameplayTag.Normalize(tags[i]);
                if (n == null || !seen.Add(n))
                {
                    tags.RemoveAt(i);
                    continue;
                }
                tags[i] = n;
                i++;
            }
        }

        public override string ToString() => tags == null ? string.Empty : string.Join(", ", tags);

        private int IndexOfExact(GameplayTag tag)
        {
            if (tags == null || !tag.IsValid) return -1;
            for (int i = 0; i < tags.Count; i++)
                if (new GameplayTag(tags[i]).Equals(tag)) return i;
            return -1;
        }
    }
}
