using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Condition;
using Ale.Condition.Editor;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 条件编辑器索引与目录的门槛：判定器诊断标记（漏打特性 / 特性键与 Key 不一致 / 重复键 / 空键 / 抽象 / 缺无参构造 / 实例化失败）、
    /// 「哪一个会被真正发现」的判定、抽象基类只作说明、判定器键收集（跨组跨项、跳过空键、带回下标）、搜索 / 分类 / 只看有问题的过滤，
    /// 以及条件目录的索引构建（同 id 先到者优先、空 id 跳过）。
    ///
    /// <para>测试用的假判定器<b>一律不打 <c>[ConditionEvaluator]</c></b>——打了会混进工程的真实判定器目录与绘制器下拉。
    /// 特性键改由 <see cref="ConditionEvaluatorIndex.BuildRows"/> 的三元组参数显式传入。</para>
    /// </summary>
    public class ConditionEditorIndexTests
    {
        // ── 假判定器 ──────────────────────────────────────────────────────────────

        private static readonly ConditionParamDef[] OneParam =
        {
            new ConditionParamDef("amount", ConditionParamType.Float, false, "数值"),
        };

        private abstract class AbstractEvaluator : IConditionEvaluator
        {
            public string Key => "Test.Abstract";
            public string DisplayName => "抽象";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => OneParam;
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class GoodEvaluator : IConditionEvaluator
        {
            public GoodEvaluator() { }
            public string Key => "Test.Good";
            public string DisplayName => "好用的";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => OneParam;
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class DuplicateEvaluator : IConditionEvaluator
        {
            public DuplicateEvaluator() { }
            public string Key => "Test.Good";                  // 与 GoodEvaluator 同键
            public string DisplayName => "撞键的";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class MismatchEvaluator : IConditionEvaluator
        {
            public MismatchEvaluator() { }
            public string Key => "Test.Mismatch";              // 与传入的特性键不一致
            public string DisplayName => "键不一致";
            public string Category => "别的分类";
            public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class NoAttrEvaluator : IConditionEvaluator
        {
            public NoAttrEvaluator() { }
            public string Key => "Test.NoAttr";
            public string DisplayName => "漏打特性";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class NoCtorEvaluator : IConditionEvaluator
        {
            public NoCtorEvaluator(int unused) { }             // 只有带参构造
            public string Key => "Test.NoCtor";
            public string DisplayName => "缺无参构造";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class EmptyKeyEvaluator : IConditionEvaluator
        {
            public EmptyKeyEvaluator() { }
            public string Key => string.Empty;
            public string DisplayName => "空键";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private sealed class ThrowingEvaluator : IConditionEvaluator
        {
            public ThrowingEvaluator() => throw new InvalidOperationException("测试用：构造失败");
            public string Key => "Test.Throwing";
            public string DisplayName => "构造失败";
            public string Category => "测试";
            public IReadOnlyList<ConditionParamDef> ParamSchema => Array.Empty<ConditionParamDef>();
            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx) => true;
        }

        private static ConditionEvaluatorRow Row(List<ConditionEvaluatorRow> rows, Type type)
        {
            foreach (var r in rows) if (r.Type == type) return r;
            Assert.Fail("未找到行：" + type.Name);
            return null;
        }

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        private ConditionDatabase NewDb(string name, params string[] ids)
        {
            var db = ScriptableObject.CreateInstance<ConditionDatabase>();
            db.name = name;
            foreach (var id in ids)
            {
                var e = new ConditionEntry(id);
                e.displayText.SetTextValue(0, name + ":" + id);
                db.Conditions.Add(e);
            }
            _created.Add(db);
            return db;
        }

        /// <summary>按目录供给固定候选的假提供者。</summary>
        private sealed class Provider : IConditionParamCatalogProvider
        {
            private readonly List<(string id, string display)> _items;
            public Provider(string system, string catalogRef, params (string id, string display)[] items)
            {
                SystemName = system;
                CatalogRef = catalogRef;
                _items = new List<(string, string)>(items);
            }
            public string SystemName { get; }
            public string CatalogRef { get; }
            public IEnumerable<(string id, string display)> GetItems() => _items;
        }

        [TearDown]
        public void Cleanup()
        {
            ConditionDrawerHooks.ClearProviders();
            foreach (var o in _created) if (o) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ── 参数候选注入点 ────────────────────────────────────────────────────────

        [Test]
        public void DrawerHooks_Providers_Register_AggregateByCatalog_Unregister()
        {
            var chronicle = new Provider("Chronicle", "Test.Thing", ("brave", "勇敢"), ("genius", "天才"));
            var other     = new Provider("Other",     "Test.Thing", ("brave", "Brave (dup)"), ("shy", "Shy"));
            var elsewhere = new Provider("Other",     "Test.Elsewhere", ("x", "X"));

            ConditionDrawerHooks.RegisterProvider(chronicle);
            ConditionDrawerHooks.RegisterProvider(chronicle);   // 幂等
            ConditionDrawerHooks.RegisterProvider(other);
            ConditionDrawerHooks.RegisterProvider(elsewhere);
            Assert.AreEqual(3, ConditionDrawerHooks.Providers.Count, "重复登记被忽略");

            var items = ConditionDrawerHooks.CollectCandidates("Test.Thing");
            Assert.AreEqual(3, items.Count, "同目录合并、同 id 先登记者优先");
            Assert.AreEqual("brave", items[0].id);
            Assert.AreEqual("勇敢", items[0].display, "先登记者的显示名胜出");
            Assert.AreEqual("Chronicle", items[0].system);
            Assert.AreEqual("shy", items[2].id);

            Assert.AreEqual(1, ConditionDrawerHooks.CollectCandidates("Test.Elsewhere").Count, "按目录隔离");
            Assert.AreEqual(0, ConditionDrawerHooks.CollectCandidates("No.Such").Count, "未知目录无候选（绘制器退化为文本框）");
            Assert.AreEqual(0, ConditionDrawerHooks.CollectCandidates(null).Count, "空目录无候选");

            Assert.IsTrue(ConditionDrawerHooks.UnregisterProvider(chronicle));
            Assert.IsFalse(ConditionDrawerHooks.UnregisterProvider(chronicle), "重复注销返回 false");
            Assert.AreEqual("Brave (dup)", ConditionDrawerHooks.CollectCandidates("Test.Thing")[0].display,
                "注销后由剩下的提供者接手");
        }

        // ── 诊断 ──────────────────────────────────────────────────────────────────

        [Test]
        public void BuildRows_FlagsMissingAttribute_KeyMismatch_AndDuplicateKey()
        {
            var rows = ConditionEvaluatorIndex.BuildRows(new[]
            {
                (typeof(GoodEvaluator),      "Test.Good",  true),
                (typeof(MismatchEvaluator),  "Test.Wrong", true),
                (typeof(NoAttrEvaluator),    (string)null, false),
                (typeof(DuplicateEvaluator), "Test.Good",  true),
            });

            Assert.AreEqual(4, rows.Count, "每个候选类型一行");

            var good = Row(rows, typeof(GoodEvaluator));
            var dup  = Row(rows, typeof(DuplicateEvaluator));
            Assert.IsTrue((good.Issues & EEvaluatorIssue.DuplicateKey) != 0, "同键两边都要标 DuplicateKey");
            Assert.IsTrue((dup.Issues  & EEvaluatorIssue.DuplicateKey) != 0, "同键两边都要标 DuplicateKey");
            Assert.IsTrue(good.Discovered, "先出现者胜出（与目录的先到先得一致）");
            Assert.IsFalse(dup.Discovered, "后出现者被目录丢弃");

            var mismatch = Row(rows, typeof(MismatchEvaluator));
            Assert.IsTrue((mismatch.Issues & EEvaluatorIssue.AttributeKeyMismatch) != 0, "特性键与 Key 属性不一致");
            Assert.AreEqual("Test.Mismatch", mismatch.Key, "生效的是 Key 属性而非特性字符串");
            Assert.AreEqual("Test.Wrong", mismatch.AttributeKey, "特性字符串原样保留供提示");
            Assert.IsTrue(mismatch.Discovered, "键不一致不影响被发现");

            var noAttr = Row(rows, typeof(NoAttrEvaluator));
            Assert.IsTrue((noAttr.Issues & EEvaluatorIssue.MissingAttribute) != 0, "漏打特性");
            Assert.IsFalse(noAttr.Discovered, "漏打特性 → 两条通道都发现不了");
        }

        [Test]
        public void BuildRows_SkipsNoCtorEmptyKeyAndThrowing_MarksNotDiscovered()
        {
            var rows = ConditionEvaluatorIndex.BuildRows(new[]
            {
                (typeof(NoCtorEvaluator),   "Test.NoCtor",   true),
                (typeof(EmptyKeyEvaluator), "Test.Empty",    true),
                (typeof(ThrowingEvaluator), "Test.Throwing", true),
            });

            var noCtor = Row(rows, typeof(NoCtorEvaluator));
            Assert.IsTrue((noCtor.Issues & EEvaluatorIssue.NoDefaultCtor) != 0, "缺公开无参构造");

            var empty = Row(rows, typeof(EmptyKeyEvaluator));
            Assert.IsTrue((empty.Issues & EEvaluatorIssue.EmptyKey) != 0, "Key 属性为空");
            Assert.AreEqual("Test.Empty", empty.Key, "键回退到特性字符串");

            var throwing = Row(rows, typeof(ThrowingEvaluator));
            Assert.IsTrue((throwing.Issues & EEvaluatorIssue.ConstructionFailed) != 0, "实例化抛异常");

            Assert.IsTrue(noCtor.HasProblem && empty.HasProblem && throwing.HasProblem, "这三种都是真问题");
            Assert.IsFalse(noCtor.Discovered || empty.Discovered || throwing.Discovered, "三种都发现不了");
        }

        [Test]
        public void BuildRows_AbstractBaseWithoutAttribute_IsInformationalNotProblem()
        {
            // 判定器基类本就不该打 [ConditionEvaluator]——特性由派生类携带。
            var rows = ConditionEvaluatorIndex.BuildRows(new[]
            {
                (typeof(AbstractEvaluator), (string)null, false),
                (typeof(NoAttrEvaluator),   (string)null, false),
            });

            var abs = Row(rows, typeof(AbstractEvaluator));
            Assert.AreEqual(EEvaluatorIssue.AbstractType, abs.Issues, "抽象基类只留说明性标记，不报「漏打特性」");
            Assert.IsFalse(abs.HasProblem, "说明性标记不算需要修的问题");
            Assert.IsFalse(abs.Discovered, "但它确实不会被发现");
            Assert.IsNull(abs.ParamSchema, "无法实例化 → 拿不到 schema");

            var concrete = Row(rows, typeof(NoAttrEvaluator));
            Assert.IsTrue(concrete.HasProblem, "非抽象类漏打特性仍是真问题");

            Assert.AreEqual(1, ConditionEvaluatorIndex.Filter(rows, null, null, true).Count,
                "「只看有问题」应筛掉抽象基类、只留漏打特性的那个");
        }

        // ── 键收集与过滤 ──────────────────────────────────────────────────────────

        [Test]
        public void CollectKeys_WalksAllGroupsAndItems_SkipsEmptyKeys_ReportsIndices()
        {
            var expr = new ConditionExpression();
            var g0 = new ConditionGroup();
            g0.items.Add(new ConditionItem("Test.Good"));
            g0.items.Add(new ConditionItem(string.Empty));   // 空键跳过
            g0.items.Add(null);                               // null 项跳过
            g0.items.Add(new ConditionItem("Test.Other"));
            var g1 = new ConditionGroup();
            g1.items.Add(new ConditionItem("Test.Good"));
            expr.groups.Add(g0);
            expr.groups.Add(g1);

            var hits = new List<string>();
            ConditionEvaluatorIndex.CollectKeys(expr, (key, gi, ii) => hits.Add($"{key}@{gi}.{ii}"));

            Assert.AreEqual(3, hits.Count, "空键与 null 项跳过，其余逐条上报");
            Assert.AreEqual("Test.Good@0.0",  hits[0]);
            Assert.AreEqual("Test.Other@0.3", hits[1], "下标是项在组内的原始位置");
            Assert.AreEqual("Test.Good@1.0",  hits[2]);

            ConditionEvaluatorIndex.CollectKeys(null, (key, gi, ii) => Assert.Fail("表达式为空不应回调"));
        }

        [Test]
        public void Filter_MatchesKeyDisplayNameTypeAndCategory_IgnoresCase_AndOnlyIssues()
        {
            var rows = ConditionEvaluatorIndex.BuildRows(new[]
            {
                (typeof(GoodEvaluator),     "Test.Good",  true),
                (typeof(MismatchEvaluator), "Test.Wrong", true),
                (typeof(NoAttrEvaluator),   (string)null, false),
            });

            Assert.AreEqual(3, ConditionEvaluatorIndex.Filter(rows, null, null, false).Count, "无条件 → 全部");
            Assert.AreEqual(2, ConditionEvaluatorIndex.Filter(rows, null, "测试", false).Count, "按分类过滤");
            Assert.AreEqual(1, ConditionEvaluatorIndex.Filter(rows, "mismatch", null, false).Count, "类型全名忽略大小写");
            Assert.AreEqual(1, ConditionEvaluatorIndex.Filter(rows, "好用的", null, false).Count, "按显示名");
            Assert.AreEqual(3, ConditionEvaluatorIndex.Filter(rows, "TEST.", null, false).Count, "键前缀忽略大小写");

            Assert.AreEqual(2, ConditionEvaluatorIndex.Filter(rows, null, null, true).Count,
                "只看有问题 → 键不一致 + 漏打特性");
            Assert.AreEqual(1, ConditionEvaluatorIndex.Filter(rows, null, "测试", true).Count,
                "分类与「只看有问题」叠加");
        }

        // ── 用法收集助手 ──────────────────────────────────────────────────────────

        [Test]
        public void UsageCollector_FormatsLocationUniformly_AndBuildsRecords()
        {
            // 位置文案是各宿主提供者共用的格式，锁死它正是把这个助手下沉到 toolkit 的目的。
            Assert.AreEqual("组1 第1项", ConditionUsageCollector.Where(0, 0), "下标 0 起、显示 1 起");
            Assert.AreEqual("组2 第3项", ConditionUsageCollector.Where(1, 2));
            Assert.AreEqual("获得条件 · 组1 第1项", ConditionUsageCollector.Where("获得条件", 0, 0), "带前缀时以 · 相连");
            Assert.AreEqual("组2 第3项", ConditionUsageCollector.Where(null, 1, 2), "前缀为空时只留后半");

            var expr = new ConditionExpression();
            var group = new ConditionGroup();
            group.items.Add(new ConditionItem("Test.Good"));
            group.items.Add(new ConditionItem(string.Empty));   // 空键跳过
            group.items.Add(new ConditionItem("Test.Other"));
            expr.groups.Add(group);

            var db = NewDb("Host");
            bool jumped = false;
            var into = new List<ConditionKeyUsage>();
            ConditionUsageCollector.Collect(into, expr, db, "trait_brave", "获得条件", () => jumped = true);

            Assert.AreEqual(2, into.Count, "空键不入表");
            Assert.AreEqual("Test.Good", into[0].Key);
            Assert.AreSame(db, into[0].Asset);
            Assert.AreEqual("trait_brave", into[0].OwnerId);
            Assert.AreEqual("获得条件 · 组1 第1项", into[0].Location);
            Assert.AreEqual("获得条件 · 组1 第3项", into[1].Location, "下标取项在组内的原始位置");
            into[0].Jump();
            Assert.IsTrue(jumped, "跳转回调原样带上");

            ConditionUsageCollector.Collect(null, expr, db, "x", "y", null);   // 目标为 null 不应抛
            var empty = new List<ConditionKeyUsage>();
            ConditionUsageCollector.Collect(empty, null, db, "x", "y", null);
            Assert.AreEqual(0, empty.Count, "表达式为空时不加任何记录");
        }

        // ── 条件目录 ──────────────────────────────────────────────────────────────

        [Test]
        public void Catalog_BuildIndex_FirstDatabaseWins_SkipsEmptyIds()
        {
            var a = NewDb("A", "unlock", "promote");
            var b = NewDb("B", "unlock", "");
            var all  = new List<ConditionEditorCatalog.Hit>();
            var byId = new Dictionary<string, ConditionEditorCatalog.Hit>();

            ConditionEditorCatalog.BuildIndex(new[] { a, b }, all, byId);

            Assert.AreEqual(3, all.Count, "空 id 跳过");
            Assert.AreSame(a, byId["unlock"].Database, "同 id 先到者优先");
            Assert.AreSame(a.GetEntry("promote"), byId["promote"].Entry);
            Assert.IsFalse(byId.ContainsKey(""));
            Assert.AreEqual("A:unlock (unlock)", ConditionEditorCatalog.LabelOf(byId["unlock"].Entry));
            Assert.AreEqual("plain", ConditionEditorCatalog.LabelOf(new ConditionEntry("plain")), "无显示名只显示 id");
        }
    }
}
