using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ale.Effect;
using Ale.Effect.Editor;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 执行器索引的门槛：诊断标记（漏打特性 / 特性键与 Key 不一致 / 重复键 / 空键 / 抽象 / 缺无参构造 / 实例化失败）、
    /// 「哪一个会被真正发现」的判定、执行器键收集（跨阶段组、跳过空键、带回 phase）、以及搜索 / 分类 / 只看有问题的过滤。
    ///
    /// <para>测试用的假执行器<b>一律不打 <c>[EffectExecutor]</c></b>——打了会混进工程的真实执行器目录与绘制器下拉。
    /// 特性键改由 <see cref="EffectExecutorIndex.BuildRows"/> 的三元组参数显式传入。</para>
    /// </summary>
    public class EffectExecutorIndexTests
    {
        // ── 假执行器 ──────────────────────────────────────────────────────────────

        private static readonly EffectParamDef[] OneParam =
        {
            new EffectParamDef("amount", EffectParamType.Float, false, "数值"),
        };

        private abstract class AbstractExecutor : IEffectExecutor
        {
            public string Key => "Test.Abstract";
            public string DisplayName => "抽象";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => OneParam;
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class GoodExecutor : IEffectExecutor
        {
            public GoodExecutor() { }
            public string Key => "Test.Good";
            public string DisplayName => "好用的";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => OneParam;
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class DuplicateExecutor : IEffectExecutor
        {
            public DuplicateExecutor() { }
            public string Key => "Test.Good";                 // 与 GoodExecutor 同键
            public string DisplayName => "撞键的";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class MismatchExecutor : IEffectExecutor
        {
            public MismatchExecutor() { }
            public string Key => "Test.Mismatch";              // 与传入的特性键不一致
            public string DisplayName => "键不一致";
            public string Category => "别的分类";
            public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class NoAttrExecutor : IEffectExecutor
        {
            public NoAttrExecutor() { }
            public string Key => "Test.NoAttr";
            public string DisplayName => "漏打特性";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class NoCtorExecutor : IEffectExecutor
        {
            public NoCtorExecutor(int unused) { }             // 只有带参构造
            public string Key => "Test.NoCtor";
            public string DisplayName => "缺无参构造";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class EmptyKeyExecutor : IEffectExecutor
        {
            public EmptyKeyExecutor() { }
            public string Key => string.Empty;
            public string DisplayName => "空键";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private sealed class ThrowingExecutor : IEffectExecutor
        {
            public ThrowingExecutor() => throw new InvalidOperationException("测试用：构造失败");
            public string Key => "Test.Throwing";
            public string DisplayName => "构造失败";
            public string Category => "测试";
            public IReadOnlyList<EffectParamDef> ParamSchema => Array.Empty<EffectParamDef>();
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx) => EffectResult.Applied;
        }

        private static EffectExecutorRow Row(List<EffectExecutorRow> rows, Type type)
        {
            foreach (var r in rows) if (r.Type == type) return r;
            Assert.Fail("未找到行：" + type.Name);
            return null;
        }

        // ── 用例 ──────────────────────────────────────────────────────────────────

        [Test]
        public void BuildRows_FlagsMissingAttribute_KeyMismatch_AndDuplicateKey()
        {
            var rows = EffectExecutorIndex.BuildRows(new[]
            {
                (typeof(GoodExecutor),      "Test.Good",     true),
                (typeof(MismatchExecutor),  "Test.Wrong",    true),
                (typeof(NoAttrExecutor),    (string)null,    false),
                (typeof(DuplicateExecutor), "Test.Good",     true),
            });

            Assert.AreEqual(4, rows.Count, "每个候选类型一行");

            var good = Row(rows, typeof(GoodExecutor));
            var dup  = Row(rows, typeof(DuplicateExecutor));
            Assert.IsTrue((good.Issues & EExecutorIssue.DuplicateKey) != 0, "同键两边都要标 DuplicateKey");
            Assert.IsTrue((dup.Issues & EExecutorIssue.DuplicateKey) != 0, "同键两边都要标 DuplicateKey");
            Assert.IsTrue(good.Discovered,  "先出现者胜出（与目录的先到先得一致）");
            Assert.IsFalse(dup.Discovered,  "后出现者被目录丢弃");

            var mismatch = Row(rows, typeof(MismatchExecutor));
            Assert.IsTrue((mismatch.Issues & EExecutorIssue.AttributeKeyMismatch) != 0, "特性键与 Key 属性不一致");
            Assert.AreEqual("Test.Mismatch", mismatch.Key, "生效的是 Key 属性而非特性字符串");
            Assert.AreEqual("Test.Wrong", mismatch.AttributeKey, "特性字符串原样保留供提示");
            Assert.IsTrue(mismatch.Discovered, "键不一致不影响被发现");

            var noAttr = Row(rows, typeof(NoAttrExecutor));
            Assert.IsTrue((noAttr.Issues & EExecutorIssue.MissingAttribute) != 0, "漏打特性");
            Assert.IsFalse(noAttr.Discovered, "漏打特性 → 两条通道都发现不了");
        }

        [Test]
        public void BuildRows_SkipsAbstractNoCtorEmptyKeyAndThrowing_MarksNotDiscovered()
        {
            var rows = EffectExecutorIndex.BuildRows(new[]
            {
                (typeof(AbstractExecutor), "Test.Abstract", true),
                (typeof(NoCtorExecutor),   "Test.NoCtor",   true),
                (typeof(EmptyKeyExecutor), "Test.Empty",    true),
                (typeof(ThrowingExecutor), "Test.Throwing", true),
            });

            var abs = Row(rows, typeof(AbstractExecutor));
            Assert.IsTrue((abs.Issues & EExecutorIssue.AbstractType) != 0, "抽象类");
            Assert.IsFalse(abs.Discovered);
            Assert.IsNull(abs.ParamSchema, "无法实例化 → 拿不到 schema");
            Assert.AreEqual("Test.Abstract", abs.Key, "键回退到特性字符串，至少有个标识");

            var noCtor = Row(rows, typeof(NoCtorExecutor));
            Assert.IsTrue((noCtor.Issues & EExecutorIssue.NoDefaultCtor) != 0, "缺公开无参构造");
            Assert.IsFalse(noCtor.Discovered);

            var empty = Row(rows, typeof(EmptyKeyExecutor));
            Assert.IsTrue((empty.Issues & EExecutorIssue.EmptyKey) != 0, "Key 属性为空");
            Assert.IsFalse(empty.Discovered);

            var throwing = Row(rows, typeof(ThrowingExecutor));
            Assert.IsTrue((throwing.Issues & EExecutorIssue.ConstructionFailed) != 0, "实例化抛异常");
            Assert.IsFalse(throwing.Discovered);
        }

        [Test]
        public void CollectKeys_WalksAllGroupsAndItems_SkipsEmptyKeys_ReportsPhase()
        {
            var def = new EffectDefinition("fx");
            var apply = new EffectGroup(EffectPhases.OnApply);
            apply.items.Add(new EffectItem("Test.Good"));
            apply.items.Add(new EffectItem(string.Empty));   // 空键跳过
            apply.items.Add(null);                            // null 项跳过
            apply.items.Add(new EffectItem("Test.Other"));
            var wildcard = new EffectGroup(null);             // 通配组：phase 为空
            wildcard.items.Add(new EffectItem("Test.Good"));
            def.executions.groups.Add(apply);
            def.executions.groups.Add(wildcard);

            var hits = new List<string>();
            EffectExecutorIndex.CollectKeys(def, (key, phase) => hits.Add(key + "@" + (phase ?? "*")));

            Assert.AreEqual(3, hits.Count, "空键与 null 项跳过，其余逐条上报");
            Assert.AreEqual("Test.Good@onApply",  hits[0]);
            Assert.AreEqual("Test.Other@onApply", hits[1]);
            Assert.AreEqual("Test.Good@*",        hits[2], "通配组的 phase 为空");

            EffectExecutorIndex.CollectKeys(null, (key, phase) => Assert.Fail("定义为空不应回调"));
        }

        [Test]
        public void Filter_MatchesKeyDisplayNameTypeAndCategory_IgnoresCase_AndOnlyIssues()
        {
            var rows = EffectExecutorIndex.BuildRows(new[]
            {
                (typeof(GoodExecutor),     "Test.Good",  true),
                (typeof(MismatchExecutor), "Test.Wrong", true),
                (typeof(NoAttrExecutor),   (string)null, false),
            });

            Assert.AreEqual(3, EffectExecutorIndex.Filter(rows, null, null, false).Count, "无条件 → 全部");
            Assert.AreEqual(2, EffectExecutorIndex.Filter(rows, null, "测试", false).Count, "按分类过滤");
            Assert.AreEqual(1, EffectExecutorIndex.Filter(rows, "mismatch", null, false).Count, "类型全名 / 键忽略大小写");
            Assert.AreEqual(1, EffectExecutorIndex.Filter(rows, "好用的", null, false).Count, "按显示名");
            Assert.AreEqual(3, EffectExecutorIndex.Filter(rows, "TEST.", null, false).Count, "键前缀忽略大小写");

            var issues = EffectExecutorIndex.Filter(rows, null, null, true);
            Assert.AreEqual(2, issues.Count, "只看有问题 → 键不一致 + 漏打特性");
            Assert.AreEqual(1, EffectExecutorIndex.Filter(rows, null, "测试", true).Count,
                "分类与「只看有问题」叠加：「测试」分类里只剩漏打特性的那个");
        }
    }
}
