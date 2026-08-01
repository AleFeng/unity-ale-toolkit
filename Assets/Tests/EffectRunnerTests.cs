using System.Collections.Generic;
using NUnit.Framework;
using Ale.Condition;
using Ale.Effect;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// Effect System 引擎无关核心的门槛：按序执行、phase 过滤（匹配 + 空 phase 通配）、
    /// 每项可选 gate 条件门控（满足施加 / 不满足跳过，走 ConditionEngine）、空键 / 未注册键失败 + 告警、
    /// 内置执行器（NoOp/SetFlag/AdjustNumber）改写侧 Sink、AutoRegister 反射发现、JSON 往返（含内嵌 gate）。
    /// </summary>
    public class EffectRunnerTests
    {
        // ── 测试用执行器（无 [EffectExecutor]，仅手动注册，不被 AutoRegister 捞到）──
        private sealed class RecordEffect : IEffectExecutor
        {
            public string Key => "Test.Record";
            public string DisplayName => "记录";
            public string Category => "Test";
            public IReadOnlyList<EffectParamDef> ParamSchema { get; } =
                new[] { new EffectParamDef("tag", EffectParamType.String) };

            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
            {
                var sink = ctx?.GetService<IRecordSink>();
                if (sink == null) return EffectResult.Failed("no sink");
                sink.Record(parameters.Find("tag")?.GetString());
                return EffectResult.Applied;
            }
        }

        // ── 测试服务 ──
        private interface IRecordSink { void Record(string tag); }
        private sealed class RecordSink : IRecordSink
        {
            public readonly List<string> Log = new List<string>();
            public void Record(string tag) => Log.Add(tag);
        }
        private sealed class FlagSource : IConditionFlagSource
        {
            private readonly HashSet<string> _flags;
            public FlagSource(params string[] flags) { _flags = new HashSet<string>(flags); }
            public bool HasFlag(string flag) => _flags.Contains(flag);
        }
        private sealed class FlagSink : IEffectFlagSink
        {
            public readonly Dictionary<string, bool> Flags = new Dictionary<string, bool>();
            public void SetFlag(string flag, bool value) => Flags[flag] = value;
        }
        private sealed class NumberSink : IEffectNumberSink
        {
            public readonly Dictionary<string, double> N = new Dictionary<string, double>();
            public void AddNumber(string id, double delta)
            {
                N.TryGetValue(id, out var v);
                N[id] = v + delta;
            }
        }
        private sealed class TestEffectContext : IEffectContext
        {
            private readonly object[] _services;
            public TestEffectContext(params object[] services) { _services = services ?? new object[0]; }
            public object Subject => null;
            public T GetService<T>() where T : class
            {
                foreach (var s in _services) if (s is T t) return t;
                return null;
            }
        }

        // ── 构造助手 ──
        private static EffectParam Str(string id, string v) { var p = new EffectParam(id, EffectParamType.String); p.SetString(v); return p; }

        private static EffectItem RecordItem(string tag, ConditionExpression gate = null)
        {
            var it = new EffectItem("Test.Record");
            it.parameters.Add(Str("tag", tag));
            if (gate != null) it.gate = gate;
            return it;
        }

        private static EffectGroup Group(string phase, params EffectItem[] items)
        {
            var g = new EffectGroup(phase);
            g.items.AddRange(items);
            return g;
        }

        private static EffectExpression Expr(params EffectGroup[] groups)
        {
            var e = new EffectExpression();
            e.groups.AddRange(groups);
            return e;
        }

        private static ConditionExpression HasFlagGate(string flag)
        {
            var e  = new ConditionExpression();
            var g  = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Condition.HasFlag");
            var p  = new ConditionParam("flag", ConditionParamType.String); p.SetString(flag);
            it.parameters.Add(p);
            g.items.Add(it);
            e.groups.Add(g);
            return e;
        }

        private static EffectRegistry RecordReg()
        {
            var r = new EffectRegistry();
            r.Register(new RecordEffect());
            return r;
        }

        private static ConditionRegistry FlagReg()
        {
            var r = new ConditionRegistry();
            r.Register(new HasFlagEvaluator());
            return r;
        }

        // ── 空表达式 ──
        [Test]
        public void Empty_Expression_EmptyReport()
        {
            var report = EffectRunner.Run(new EffectExpression(), new TestEffectContext(), null, RecordReg());
            Assert.AreEqual(0, report.Total);
        }

        // ── 按序执行 ──
        [Test]
        public void Ordered_Execution_PreservesOrder()
        {
            var sink = new RecordSink();
            var expr = Expr(Group(null, RecordItem("a"), RecordItem("b"), RecordItem("c")));
            var report = EffectRunner.Run(expr, new TestEffectContext(sink), null, RecordReg());

            Assert.AreEqual(3, report.Applied);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, sink.Log);
        }

        // ── phase 过滤：匹配组 + 空 phase 通配组 ──
        [Test]
        public void PhaseFilter_RunsMatchingAndWildcard()
        {
            var sink = new RecordSink();
            var expr = Expr(
                Group("onGained", RecordItem("g")),
                Group("onLost",   RecordItem("l")),
                Group(null,       RecordItem("w")));   // 空 phase = 通配

            EffectRunner.Run(expr, new TestEffectContext(sink), "onGained", RecordReg());
            CollectionAssert.AreEqual(new[] { "g", "w" }, sink.Log);
        }

        [Test]
        public void PhaseFilter_Null_RunsAllGroups()
        {
            var sink = new RecordSink();
            var expr = Expr(
                Group("onGained", RecordItem("g")),
                Group("onLost",   RecordItem("l")),
                Group(null,       RecordItem("w")));

            EffectRunner.Run(expr, new TestEffectContext(sink), null, RecordReg());
            CollectionAssert.AreEqual(new[] { "g", "l", "w" }, sink.Log);
        }

        // ── 条件门控 ──
        [Test]
        public void Gate_Applies_When_Condition_Passes()
        {
            var sink = new RecordSink();
            var expr = Expr(Group(null, RecordItem("x", HasFlagGate("brave"))));
            var ctx  = new TestEffectContext(sink, new FlagSource("brave"));

            var report = EffectRunner.Run(expr, ctx, null, RecordReg(), FlagReg());
            Assert.AreEqual(1, report.Applied);
            CollectionAssert.AreEqual(new[] { "x" }, sink.Log);
        }

        [Test]
        public void Gate_Skips_When_Condition_Fails()
        {
            var sink = new RecordSink();
            var expr = Expr(Group(null, RecordItem("x", HasFlagGate("brave"))));
            var ctx  = new TestEffectContext(sink, new FlagSource()); // 无 brave

            var report = EffectRunner.Run(expr, ctx, null, RecordReg(), FlagReg());
            Assert.AreEqual(0, report.Applied);
            Assert.AreEqual(1, report.Skipped);
            Assert.AreEqual(0, sink.Log.Count);
        }

        // ── 空键 / 未注册键 ──
        [Test]
        public void EmptyKey_Fails()
        {
            var expr = Expr(Group(null, new EffectItem("")));
            var report = EffectRunner.Run(expr, new TestEffectContext(), null, RecordReg());
            Assert.AreEqual(1, report.Failed);
        }

        [Test]
        public void MissingKey_FailsAndWarns()
        {
            var reg = RecordReg();
            bool warned = false;
            reg.MissingKeyWarning = _ => warned = true;

            var expr = Expr(Group(null, new EffectItem("Nope.Missing")));
            var report = EffectRunner.Run(expr, new TestEffectContext(), null, reg);

            Assert.AreEqual(1, report.Failed);
            Assert.IsTrue(warned);
        }

        // ── 内置执行器 ──
        [Test]
        public void BuiltIn_SetFlag_WritesSink()
        {
            var reg  = new EffectRegistry(); reg.Register(new SetFlagEffect());
            var sink = new FlagSink();

            var it = new EffectItem("Effect.SetFlag");
            var pf = new EffectParam("flag",  EffectParamType.String); pf.SetString("fire");
            var pv = new EffectParam("value", EffectParamType.Bool);   pv.SetBool(true);
            it.parameters.Add(pf); it.parameters.Add(pv);

            var report = EffectRunner.Run(Expr(Group(null, it)), new TestEffectContext(sink), null, reg);
            Assert.AreEqual(1, report.Applied);
            Assert.IsTrue(sink.Flags.TryGetValue("fire", out var v) && v);
        }

        [Test]
        public void BuiltIn_SetFlag_NoSink_Fails()
        {
            var reg = new EffectRegistry(); reg.Register(new SetFlagEffect());
            var it  = new EffectItem("Effect.SetFlag");
            var pf  = new EffectParam("flag", EffectParamType.String); pf.SetString("fire");
            it.parameters.Add(pf);

            var report = EffectRunner.Run(Expr(Group(null, it)), new TestEffectContext(), null, reg);
            Assert.AreEqual(1, report.Failed);   // 无 IEffectFlagSink → 失败
        }

        [Test]
        public void BuiltIn_AdjustNumber_Accumulates()
        {
            var reg  = new EffectRegistry(); reg.Register(new AdjustNumberEffect());
            var sink = new NumberSink();

            var it = new EffectItem("Effect.AdjustNumber");
            var pid = new EffectParam("id",    EffectParamType.String); pid.SetString("gold");
            var pd  = new EffectParam("delta", EffectParamType.Float);  pd.SetFloat(5d);
            it.parameters.Add(pid); it.parameters.Add(pd);
            var expr = Expr(Group(null, it));
            var ctx  = new TestEffectContext(sink);

            EffectRunner.Run(expr, ctx, null, reg);
            EffectRunner.Run(expr, ctx, null, reg);   // 再来一次证明累加
            Assert.AreEqual(10d, sink.N["gold"], 1e-9);
        }

        // ── AutoRegister 反射发现内置执行器 ──
        [Test]
        public void AutoRegister_FindsBuiltIns()
        {
            var r = new EffectRegistry();
            r.AutoRegisterFromAssemblies();
            Assert.IsTrue(r.TryGet("Effect.NoOp", out _));
            Assert.IsTrue(r.TryGet("Effect.SetFlag", out _));
            Assert.IsTrue(r.TryGet("Effect.AdjustNumber", out _));
        }

        // ── JSON 往返（含内嵌 gate 条件）──
        [Test]
        public void Json_RoundTrip_PreservesStructureGateAndParams()
        {
            var expr = Expr(Group("onGained", RecordItem("x", HasFlagGate("brave"))));
            var back = EffectJson.FromJson(EffectJson.ToJson(expr));

            Assert.AreEqual(1, back.TotalItemCount());
            var g = back.groups[0];
            Assert.AreEqual("onGained", g.phase);
            var it = g.items[0];
            Assert.AreEqual("Test.Record", it.key);
            Assert.AreEqual("x", it.parameters.Find("tag")?.GetString());
            Assert.AreEqual(1, it.gate.TotalItemCount());
            Assert.AreEqual("Condition.HasFlag", it.gate.groups[0].items[0].key);
        }
    }
}
