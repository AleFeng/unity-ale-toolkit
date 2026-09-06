using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ale.GameplayTags;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// GameplayTags 引擎无关核心的门槛：归一规则（<b>数据格式，发布后冻结</b>——本文件钉死）、层级匹配、
    /// 容器增删查、标签要求、咨询性注册表。
    /// </summary>
    public class GameplayTagTests
    {
        private static GameplayTag T(string s) => new GameplayTag(s);

        // ── 归一规则（数据格式）──
        [Test]
        public void Normalize_TrimsWholeAndSegments()
        {
            Assert.AreEqual("A.B", GameplayTag.Normalize(" A . B "));
            Assert.AreEqual("Status.Buff", GameplayTag.Normalize("  Status.Buff "));
            Assert.AreEqual("Status.Buff", T("Status.Buff").Name);   // 已规范：原样
        }

        [Test]
        public void Normalize_EmptySegment_IsInvalid()
        {
            foreach (var s in new[] { "A..B", ".A", "A.", ".", "", "   ", null })
                Assert.IsNull(GameplayTag.Normalize(s), $"'{s}' 应非法");
            Assert.IsFalse(T("A..B").IsValid);
        }

        [Test]
        public void Normalize_WhitespaceOrSlashInsideSegment_IsInvalid()
        {
            Assert.IsNull(GameplayTag.Normalize("A B.C"));
            Assert.IsNull(GameplayTag.Normalize("A/B"));
            Assert.IsNull(GameplayTag.Normalize("A.B/C"));
            Assert.IsNull(GameplayTag.Normalize("A.B C"));
        }

        [Test]
        public void Normalize_AllowsCjkAndSymbols()
        {
            Assert.AreEqual("状态.减益.精神异常", GameplayTag.Normalize("状态.减益.精神异常"));
            Assert.AreEqual("Status.Buff_1.x-y:z", GameplayTag.Normalize("Status.Buff_1.x-y:z"));
        }

        [Test]
        public void Normalize_IsCaseSensitive_Frozen()
        {
            Assert.AreNotEqual(T("a.b"), T("A.B"));
            Assert.IsFalse(T("a.b").MatchesTag(T("A")));
            Assert.AreEqual("A.B", GameplayTag.Normalize("A.B"));   // 不折叠大小写
        }

        // ── 结构 ──
        [Test]
        public void ParentDepthLeafRoot()
        {
            var t = T("A.B.C");
            Assert.AreEqual(3, t.Depth);
            Assert.AreEqual(T("A.B"), t.Parent);
            Assert.AreEqual(T("A"), t.Root);
            Assert.AreEqual("C", t.Leaf);
            Assert.AreEqual(GameplayTag.None, T("A").Parent);
            Assert.AreEqual(T("A"), T("A").Root);
            Assert.AreEqual(0, GameplayTag.None.Depth);
            Assert.AreEqual(string.Empty, GameplayTag.None.Leaf);
        }

        [Test]
        public void GetAncestors_NearToFar()
        {
            var list = new List<GameplayTag>();
            T("A.B.C").GetAncestors(list);
            CollectionAssert.AreEqual(new[] { T("A.B"), T("A") }, list);
            list.Clear();
            T("A.B.C").GetAncestors(list, includeSelf: true);
            CollectionAssert.AreEqual(new[] { T("A.B.C"), T("A.B"), T("A") }, list);
        }

        // ── 匹配 ──
        [Test]
        public void MatchesTag_SelfAndDescendant_True_Ancestor_False()
        {
            Assert.IsTrue(T("A.B").MatchesTag(T("A")));
            Assert.IsTrue(T("A.B.C").MatchesTag(T("A")));
            Assert.IsTrue(T("A").MatchesTag(T("A")));
            Assert.IsFalse(T("A").MatchesTag(T("A.B")));
            Assert.IsTrue(T("A.B").IsDescendantOf(T("A")));
            Assert.IsFalse(T("A").IsDescendantOf(T("A")));
            Assert.IsTrue(T("A.B").IsDirectChildOf(T("A")));
            Assert.IsFalse(T("A.B.C").IsDirectChildOf(T("A")));
            Assert.IsTrue(T("A.B").MatchesTagExact(T("A.B")));
            Assert.IsFalse(T("A.B").MatchesTagExact(T("A")));
        }

        [Test]
        public void MatchesTag_PrefixWithoutSeparator_False()
        {
            Assert.IsFalse(T("AB").MatchesTag(T("A")));
            Assert.IsFalse(T("A.BC").MatchesTag(T("A.B")));
        }

        [Test]
        public void Invalid_NeverMatches_And_EqualsNone()
        {
            Assert.AreEqual(GameplayTag.None, default(GameplayTag));
            Assert.AreEqual(GameplayTag.None, T(".."));
            Assert.IsFalse(GameplayTag.None.MatchesTag(GameplayTag.None));
            Assert.IsFalse(T("A").MatchesTag(GameplayTag.None));
            Assert.IsFalse(GameplayTag.None.MatchesTag(T("A")));
            Assert.IsFalse(GameplayTag.None.MatchesTagExact(GameplayTag.None));
            Assert.AreEqual(string.Empty, GameplayTag.None.Name);
        }

        [Test]
        public void TryParse_Parse_Behaviour()
        {
            Assert.IsTrue(GameplayTag.TryParse(" A.B ", out var t) && t.Name == "A.B");
            Assert.IsFalse(GameplayTag.TryParse("A..B", out var bad));
            Assert.AreEqual(GameplayTag.None, bad);
            Assert.AreEqual("A.B", GameplayTag.Parse("A.B").Name);
            Assert.Throws<ArgumentException>(() => GameplayTag.Parse("A..B"));
            Assert.IsTrue(GameplayTag.IsValidName("A"));
            Assert.IsFalse(GameplayTag.IsValidName("A/B"));
        }

        [Test]
        public void Equality_Hash_Compare_Ordinal()
        {
            Assert.IsTrue(T("A.B") == T(" A . B "));
            Assert.IsTrue(T("A") != T("B"));
            Assert.AreEqual(T("A.B").GetHashCode(), T("A.B").GetHashCode());
            Assert.Less(T("A").CompareTo(T("B")), 0);
            Assert.Less(T("A.B").CompareTo(T("A.C")), 0);
            Assert.AreEqual("A.B", T("A.B").ToString());
        }

        // ── 容器 ──
        [Test]
        public void Container_AddDedupAndKeepsOrder()
        {
            var c = new GameplayTagContainer();
            Assert.IsTrue(c.AddTag("B.1"));
            Assert.IsTrue(c.AddTag("A"));
            Assert.IsFalse(c.AddTag(" B . 1 "));   // 归一后重复
            Assert.IsFalse(c.AddTag("A..B"));      // 非法
            CollectionAssert.AreEqual(new[] { "B.1", "A" }, c.tags);
            Assert.AreEqual(2, c.Count);
            Assert.IsFalse(c.IsEmpty);
            Assert.AreEqual(T("B.1"), c.GetTag(0));
        }

        [Test]
        public void Container_HasTag_Hierarchical_HasTagExact_Strict()
        {
            var c = new GameplayTagContainer("A.1", "B");
            Assert.IsTrue(c.HasTag(T("A")));        // 持有后代 A.1 → 匹配 A
            Assert.IsTrue(c.HasTag(T("A.1")));
            Assert.IsFalse(c.HasTag(T("A.1.x")));   // 只持有祖先不算
            Assert.IsFalse(c.HasTagExact(T("A")));
            Assert.IsTrue(c.HasTagExact(T("A.1")));
            Assert.IsFalse(c.HasTag(GameplayTag.None));
        }

        [Test]
        public void Container_HasAll_EmptyOther_True_HasAny_EmptyOther_False()
        {
            var c = new GameplayTagContainer("A.1", "B");
            var empty = new GameplayTagContainer();
            Assert.IsTrue(c.HasAll(empty));
            Assert.IsFalse(c.HasAny(empty));
            Assert.IsTrue(c.HasAll(null));
            Assert.IsFalse(c.HasAny(null));
            Assert.IsTrue(c.HasAny(new GameplayTagContainer("A", "Z")));
            Assert.IsTrue(c.HasAll(new GameplayTagContainer("A", "B")));
            Assert.IsFalse(c.HasAll(new GameplayTagContainer("A", "Z")));
            Assert.IsTrue(c.HasAnyExact(new GameplayTagContainer("A.1")));
            Assert.IsFalse(c.HasAnyExact(new GameplayTagContainer("A")));
            Assert.IsTrue(c.HasAllExact(new GameplayTagContainer("A.1", "B")));
            Assert.IsFalse(c.HasAllExact(new GameplayTagContainer("A.1", "A")));
        }

        [Test]
        public void Container_RemoveTag_Exact_RemoveTagsMatching_Subtree()
        {
            var c = new GameplayTagContainer("A", "A.1", "A.1.x", "B");
            Assert.IsFalse(c.RemoveTag(T("A.9")));
            Assert.IsTrue(c.RemoveTag(T("A")));                   // 精确：只移除 A，不动 A.1
            CollectionAssert.AreEqual(new[] { "A.1", "A.1.x", "B" }, c.tags);
            Assert.AreEqual(2, c.RemoveTagsMatching(T("A.1")));   // 子树
            CollectionAssert.AreEqual(new[] { "B" }, c.tags);
            Assert.AreEqual(1, c.RemoveTags(new GameplayTagContainer("B", "Z")));
            Assert.IsTrue(c.IsEmpty);
        }

        [Test]
        public void Container_AddTags_Filter_Clone_Independence()
        {
            var c = new GameplayTagContainer("A.1", "A.2", "B");
            Assert.AreEqual(1, c.AddTags(new GameplayTagContainer("A.1", "C")));
            var f = c.Filter(new GameplayTagContainer("A"));
            CollectionAssert.AreEqual(new[] { "A.1", "A.2" }, f.tags);
            var fe = c.FilterExact(new GameplayTagContainer("A", "B"));
            CollectionAssert.AreEqual(new[] { "B" }, fe.tags);
            var clone = c.Clone();
            clone.AddTag("D");
            Assert.AreEqual(4, c.Count);
            Assert.AreEqual(5, clone.Count);
        }

        [Test]
        public void Container_Normalize_DropsInvalidAndDuplicates()
        {
            var c = new GameplayTagContainer();
            c.tags.Add(" A . B ");
            c.tags.Add("A.B");
            c.tags.Add("bad..");
            c.tags.Add(null);
            c.Normalize();
            CollectionAssert.AreEqual(new[] { "A.B" }, c.tags);

            var n = new GameplayTagContainer { tags = null };   // Newtonsoft 可能给出 null
            n.Normalize();
            Assert.IsNotNull(n.tags);
            Assert.IsTrue(n.AddTag("X"));

            var l = new List<GameplayTag>();
            c.GetTags(l);
            CollectionAssert.AreEqual(new[] { T("A.B") }, l);
        }

        // ── 要求 ──
        [Test]
        public void Requirements_IsMet_RequireIgnore_EmptyRules()
        {
            var req = new GameplayTagRequirements();
            req.requireTags.AddTag("A");
            req.ignoreTags.AddTag("X");

            var owned = new GameplayTagCountContainer();
            Assert.IsFalse(req.IsMet(owned));                 // 缺 A
            owned.AddTag(T("A.1"));
            Assert.IsTrue(req.IsMet(owned));                  // 层级满足
            owned.AddTag(T("X.y"));
            Assert.IsFalse(req.IsMet(owned));                 // 持有 X 后代 → 不满足

            Assert.IsFalse(req.IsMet((GameplayTagCountContainer)null));   // 有 require，空集不满足
            var onlyIgnore = new GameplayTagRequirements();
            onlyIgnore.ignoreTags.AddTag("X");
            Assert.IsTrue(onlyIgnore.IsMet((GameplayTagCountContainer)null));

            Assert.IsTrue(new GameplayTagRequirements().IsEmpty);
            Assert.IsTrue(new GameplayTagRequirements().IsMet(owned));
            Assert.IsTrue(req.IsMet(new GameplayTagContainer("A.1")));
            Assert.IsFalse(req.IsMet(new GameplayTagContainer("A.1", "X")));
        }

        [Test]
        public void Requirements_Validate_RejectsOverlap_And_InvalidNames()
        {
            var req = new GameplayTagRequirements();
            req.requireTags.AddTag("A");
            req.ignoreTags.AddTag("A");
            var errors = new List<string>();
            Assert.IsFalse(req.Validate(errors, "req"));
            Assert.AreEqual(1, errors.Count);

            var bad = new GameplayTagRequirements();
            bad.requireTags.tags.Add("A..B");
            errors.Clear();
            Assert.IsFalse(bad.Validate(errors, "bad"));
            StringAssert.Contains("bad.requireTags[0]", errors[0]);

            var ok = new GameplayTagRequirements();
            ok.requireTags.AddTag("A");
            ok.ignoreTags.AddTag("A.1");   // 精确不同 → 允许（需 A 但不能是 A.1）
            errors.Clear();
            Assert.IsTrue(ok.Validate(errors, "ok"));

            var clone = ok.Clone();
            clone.requireTags.AddTag("Z");
            Assert.AreEqual(1, ok.requireTags.Count);
        }

        // ── 注册表 ──
        [Test]
        public void Registry_RegisterAddsAncestors_ChildrenAndAllSorted()
        {
            var reg = new GameplayTagRegistry();
            int changed = 0;
            reg.Changed += () => changed++;

            Assert.IsTrue(reg.Register("Status.Buff.Might", "力量增益"));
            Assert.IsTrue(reg.Register("Status.Debuff"));
            Assert.IsFalse(reg.Register("Status"));          // 已由祖先登记
            Assert.IsFalse(reg.Register("A..B"));
            Assert.AreEqual(4, reg.Count);
            Assert.IsTrue(reg.IsRegistered(T("Status")));
            Assert.AreEqual("力量增益", reg.GetComment(T("Status.Buff.Might")));
            Assert.IsNull(reg.GetComment(T("Status.Buff")));
            CollectionAssert.AreEqual(
                new[] { T("Status"), T("Status.Buff"), T("Status.Buff.Might"), T("Status.Debuff") }, reg.All);

            var kids = new List<GameplayTag>();
            reg.GetChildren(T("Status"), kids);
            CollectionAssert.AreEqual(new[] { T("Status.Buff"), T("Status.Debuff") }, kids);
            kids.Clear();
            reg.GetChildren(GameplayTag.None, kids);
            CollectionAssert.AreEqual(new[] { T("Status") }, kids);
            Assert.AreEqual(2, changed);

            Assert.AreEqual(1, reg.Register(new[]
            {
                new GameplayTagDefinition("Status.Buff.Might", "改注"),
                new GameplayTagDefinition("Immunity.Mental"),
            }));
            Assert.AreEqual("改注", reg.GetComment(T("Status.Buff.Might")));
        }

        [Test]
        public void Registry_Validate_FlagsUnregisteredAndCaseVariants()
        {
            var reg = new GameplayTagRegistry();
            reg.Register("Status.Buff");

            var c = new GameplayTagContainer("Status.Buff", "status.buff", "Other");
            c.tags.Add("A..B");
            var msgs = new List<string>();
            Assert.IsFalse(reg.Validate(c, msgs, "tags"));      // 含非法 → 有错误
            Assert.AreEqual(3, msgs.Count);
            StringAssert.StartsWith("警告:", msgs[0]);
            StringAssert.Contains("仅大小写不同", msgs[0]);
            StringAssert.StartsWith("警告:", msgs[1]);
            StringAssert.Contains("未登记", msgs[1]);
            Assert.IsFalse(msgs[2].StartsWith("警告:"));
            StringAssert.Contains("不合法", msgs[2]);

            var good = new GameplayTagContainer("Status.Buff");
            msgs.Clear();
            Assert.IsTrue(reg.Validate(good, msgs, "tags"));
            Assert.AreEqual(0, msgs.Count);
        }

        [Test]
        public void Registry_Clear_ResetsAndFiresChanged()
        {
            var reg = new GameplayTagRegistry();
            reg.Register("A.B");
            int changed = 0;
            reg.Changed += () => changed++;
            reg.Clear();
            Assert.AreEqual(0, reg.Count);
            Assert.AreEqual(0, reg.All.Count);
            Assert.AreEqual(1, changed);
            reg.Clear();
            Assert.AreEqual(1, changed);   // 空表再清不触发
        }
    }
}
