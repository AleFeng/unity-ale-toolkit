using System.Collections.Generic;
using NUnit.Framework;
using Ale.GameplayTags;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 计数标签容器的门槛：显式 / 隐式（祖先）计数、钳零删键、层级查询、事件顺序与新计数、容器批量增删、显式标签排序输出、清空。
    /// </summary>
    public class GameplayTagCountContainerTests
    {
        private static GameplayTag T(string s) => new GameplayTag(s);

        [Test]
        public void AddTag_IncrementsSelfAndAncestors()
        {
            var c = new GameplayTagCountContainer();
            Assert.AreEqual(1, c.AddTag(T("A.B.C")));
            Assert.AreEqual(2, c.AddTag(T("A.B.C")));
            Assert.AreEqual(1, c.AddTag(T("A.B")));
            Assert.AreEqual(2, c.Count);                    // 显式种类：A.B.C、A.B
            Assert.AreEqual(2, c.GetExplicitTagCount(T("A.B.C")));
            Assert.AreEqual(3, c.GetTagCount(T("A.B")));   // 2（后代）+ 1（自身）
            Assert.AreEqual(3, c.GetTagCount(T("A")));
            Assert.AreEqual(0, c.GetTagCount(T("A.B.C.D")));
            Assert.AreEqual(0, c.AddTag(GameplayTag.None));
            Assert.AreEqual(0, c.AddTag(T("X"), 0));
            Assert.IsFalse(c.HasMatchingTag(T("X")));
        }

        [Test]
        public void RemoveTag_ClampsAtZero_And_RemovesKey()
        {
            var c = new GameplayTagCountContainer();
            c.AddTag(T("A.B"), 2);
            Assert.AreEqual(1, c.RemoveTag(T("A.B")));
            Assert.IsTrue(c.HasExactTag(T("A.B")));
            Assert.AreEqual(0, c.RemoveTag(T("A.B"), 5));   // 钳到 0
            Assert.IsFalse(c.HasExactTag(T("A.B")));
            Assert.IsFalse(c.HasMatchingTag(T("A")));
            Assert.AreEqual(0, c.GetTagCount(T("A")));
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(0, c.RemoveTag(T("Nope")));
        }

        [Test]
        public void HasMatchingTag_ViaImplicitParent()
        {
            var c = new GameplayTagCountContainer();
            c.AddTag(T("Status.Debuff.Mental"));
            Assert.IsTrue(c.HasMatchingTag(T("Status")));
            Assert.IsTrue(c.HasMatchingTag(T("Status.Debuff")));
            Assert.IsTrue(c.HasMatchingTag(T("Status.Debuff.Mental")));
            Assert.IsFalse(c.HasMatchingTag(T("Status.Buff")));
            Assert.IsFalse(c.HasExactTag(T("Status")));
            Assert.IsTrue(c.HasExactTag(T("Status.Debuff.Mental")));
            Assert.IsFalse(c.HasMatchingTag(GameplayTag.None));
        }

        [Test]
        public void Event_FiresForEachAffectedTag_WithNewCount()
        {
            var c = new GameplayTagCountContainer();
            var log = new List<string>();
            c.OnTagCountChanged += (t, n) => log.Add($"{t}={n}");

            c.AddTag(T("A.B.C"));
            CollectionAssert.AreEqual(new[] { "A.B.C=1", "A.B=1", "A=1" }, log);
            log.Clear();
            c.AddTag(T("A.B"));
            CollectionAssert.AreEqual(new[] { "A.B=2", "A=2" }, log);
            log.Clear();
            c.RemoveTag(T("A.B.C"));
            CollectionAssert.AreEqual(new[] { "A.B.C=0", "A.B=1", "A=1" }, log);
        }

        [Test]
        public void AddTags_RemoveTags_Container_Symmetric()
        {
            var c = new GameplayTagCountContainer();
            var tags = new GameplayTagContainer("A.1", "B");
            c.AddTags(tags);
            Assert.IsTrue(c.HasAll(tags));
            Assert.IsTrue(c.HasMatchingTag(T("A")));
            c.RemoveTags(tags);
            Assert.AreEqual(0, c.Count);
            Assert.IsFalse(c.HasAny(tags));
            Assert.AreEqual(0, c.GetTagCount(T("A")));
        }

        [Test]
        public void HasAny_HasAll_Exact_Variants_And_EmptySets()
        {
            var c = new GameplayTagCountContainer();
            c.AddTag(T("A.1"));
            c.AddTag(T("B"));
            var empty = new GameplayTagContainer();
            Assert.IsFalse(c.HasAny(empty));
            Assert.IsTrue(c.HasAll(empty));
            Assert.IsFalse(c.HasAnyExact(empty));
            Assert.IsTrue(c.HasAllExact(empty));
            Assert.IsFalse(c.HasAny(null));
            Assert.IsTrue(c.HasAll(null));
            Assert.IsTrue(c.HasAny(new GameplayTagContainer("A", "Z")));
            Assert.IsTrue(c.HasAll(new GameplayTagContainer("A", "B")));
            Assert.IsFalse(c.HasAll(new GameplayTagContainer("A", "Z")));
            Assert.IsFalse(c.HasAnyExact(new GameplayTagContainer("A")));
            Assert.IsTrue(c.HasAnyExact(new GameplayTagContainer("A.1")));
            Assert.IsTrue(c.HasAllExact(new GameplayTagContainer("A.1", "B")));
            Assert.IsFalse(c.HasAllExact(new GameplayTagContainer("A.1", "A")));
        }

        [Test]
        public void GetExplicitTags_SortedOrdinal()
        {
            var c = new GameplayTagCountContainer();
            c.AddTag(T("B"));
            c.AddTag(T("A.2"));
            c.AddTag(T("A.1"), 3);
            var into = new GameplayTagContainer();
            c.GetExplicitTags(into);
            CollectionAssert.AreEqual(new[] { "A.1", "A.2", "B" }, into.tags);

            var list = new List<GameplayTag> { T("Z") };
            c.GetExplicitTags(list);
            CollectionAssert.AreEqual(new[] { T("Z"), T("A.1"), T("A.2"), T("B") }, list);   // 只排追加段
        }

        [Test]
        public void Clear_FiresZeroForAllKeys()
        {
            var c = new GameplayTagCountContainer();
            c.AddTag(T("A.B"));
            var zeroed = new HashSet<string>();
            c.OnTagCountChanged += (t, n) => { if (n == 0) zeroed.Add(t.Name); };
            c.Clear();
            CollectionAssert.AreEquivalent(new[] { "A.B", "A" }, zeroed);
            Assert.AreEqual(0, c.Count);
            Assert.IsFalse(c.HasMatchingTag(T("A")));
            c.Clear();   // 空表再清不抛
        }
    }
}
