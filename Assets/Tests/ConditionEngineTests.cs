using System.Collections.Generic;
using NUnit.Framework;
using Ale.Condition;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// Condition System 引擎无关核心的门槛：求值真值表（组内/顶层 And·Or × 每项/每组 negate × 短路）、
    /// 空表达式、未注册键、collectAll 一致性、JSON 往返、AutoRegister 与内置判定器。
    /// </summary>
    public class ConditionEngineTests
    {
        // 测试判定器：读 Bool 参数 "v"。无 [ConditionEvaluator] 标注，故只手动注册、不被 AutoRegister 捞到。
        private sealed class ConstEvaluator : IConditionEvaluator
        {
            public string Key => "Test.Const";
            public string DisplayName => "常量";
            public string Category => "Test";
            public IReadOnlyList<ConditionParamDef> ParamSchema { get; } =
                new[] { new ConditionParamDef("v", ConditionParamType.Bool) };

            public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
                => parameters.Find("v")?.GetBool() ?? false;
        }

        private sealed class TestContext : IConditionContext
        {
            private readonly object[] _services;
            public TestContext(params object[] services) { _services = services ?? new object[0]; }
            public object Subject => null;
            public T GetService<T>() where T : class
            {
                foreach (var s in _services) if (s is T t) return t;
                return null;
            }
        }

        private readonly ConditionRegistry _reg = MakeRegistry();
        private readonly TestContext _ctx = new TestContext();

        private static ConditionRegistry MakeRegistry()
        {
            var r = new ConditionRegistry();
            r.Register(new ConstEvaluator());
            return r;
        }

        private static ConditionItem Item(bool value, bool negate = false)
        {
            var it = new ConditionItem("Test.Const") { negate = negate };
            var p = new ConditionParam("v", ConditionParamType.Bool);
            p.SetBool(value);
            it.parameters.Add(p);
            return it;
        }

        private static ConditionGroup Group(ConditionLogicOp op, bool negate, params ConditionItem[] items)
        {
            var g = new ConditionGroup { itemOperator = op, negate = negate };
            g.items.AddRange(items);
            return g;
        }

        private static ConditionExpression Expr(ConditionLogicOp groupOp, params ConditionGroup[] groups)
        {
            var e = new ConditionExpression { groupOperator = groupOp };
            e.groups.AddRange(groups);
            return e;
        }

        private bool Eval(ConditionExpression e) => ConditionEngine.Evaluate(e, _ctx, _reg).Passed;

        // ── 空表达式 / 空组 ──
        [Test] public void Empty_Expression_Passes()
            => Assert.IsTrue(ConditionEngine.Evaluate(new ConditionExpression(), _ctx, _reg).Passed);

        [Test] public void AllEmptyGroups_Passes()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false))));

        // ── 组内 And / Or ──
        [Test] public void GroupAnd_AllTrue_Pass()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, Item(true), Item(true)))));

        [Test] public void GroupAnd_OneFalse_Fail()
            => Assert.IsFalse(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, Item(true), Item(false)))));

        [Test] public void GroupOr_OneTrue_Pass()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.Or, false, Item(false), Item(true)))));

        [Test] public void GroupOr_AllFalse_Fail()
            => Assert.IsFalse(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.Or, false, Item(false), Item(false)))));

        // ── negate（项 / 组）──
        [Test] public void ItemNegate_Inverts()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, Item(false, negate: true)))));

        [Test] public void GroupNegate_Inverts()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, true, Item(false)))));

        // ── 顶层 And / Or ──
        [Test] public void TopAnd_BothGroupsPass()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.And,
                Group(ConditionLogicOp.And, false, Item(true)),
                Group(ConditionLogicOp.And, false, Item(true)))));

        [Test] public void TopAnd_OneGroupFails()
            => Assert.IsFalse(Eval(Expr(ConditionLogicOp.And,
                Group(ConditionLogicOp.And, false, Item(true)),
                Group(ConditionLogicOp.And, false, Item(false)))));

        [Test] public void TopOr_OneGroupPasses()
            => Assert.IsTrue(Eval(Expr(ConditionLogicOp.Or,
                Group(ConditionLogicOp.And, false, Item(false)),
                Group(ConditionLogicOp.And, false, Item(true)))));

        // ── 未注册 / 空键 ──
        [Test] public void MissingKey_FailsAndWarns()
        {
            var e = Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, new ConditionItem("Nope.Missing")));
            bool warned = false;
            _reg.MissingKeyWarning = _ => warned = true;
            try
            {
                Assert.IsFalse(ConditionEngine.Evaluate(e, _ctx, _reg).Passed);
                Assert.IsTrue(warned);
            }
            finally { _reg.MissingKeyWarning = null; }
        }

        // ── collectAll 与短路结论一致 ──
        [Test] public void CollectAll_SamePassedAsShortCircuit()
        {
            var e = Expr(ConditionLogicOp.And,
                Group(ConditionLogicOp.And, false, Item(true), Item(false), Item(true)));
            bool sc = ConditionEngine.Evaluate(e, _ctx, _reg, collectAll: false).Passed;
            bool ca = ConditionEngine.Evaluate(e, _ctx, _reg, collectAll: true).Passed;
            Assert.AreEqual(sc, ca);
        }

        // ── JSON 往返 ──
        [Test] public void Json_RoundTrip_PreservesStructureAndEvaluation()
        {
            var e = Expr(ConditionLogicOp.Or,
                Group(ConditionLogicOp.And, false, Item(true), Item(false, negate: true)),
                Group(ConditionLogicOp.Or, true, Item(false)));

            var back = ConditionJson.FromJson(ConditionJson.ToJson(e));

            Assert.AreEqual(e.TotalItemCount(), back.TotalItemCount());
            Assert.AreEqual(ConditionEngine.Evaluate(e, _ctx, _reg).Passed,
                            ConditionEngine.Evaluate(back, _ctx, _reg).Passed);
        }

        // ── AutoRegister 反射发现内置判定器 ──
        [Test] public void AutoRegister_FindsBuiltIns()
        {
            var r = new ConditionRegistry();
            r.AutoRegisterFromAssemblies();
            Assert.IsTrue(r.TryGet("Condition.AlwaysTrue", out _));
            Assert.IsTrue(r.TryGet("Condition.HasFlag", out _));
            Assert.IsTrue(r.TryGet("Condition.NumberCompare", out _));
        }

        // ── 内置判定器：HasFlag / NumberAtLeast ──
        private sealed class FlagSource : IConditionFlagSource
        {
            private readonly HashSet<string> _flags;
            public FlagSource(params string[] flags) { _flags = new HashSet<string>(flags); }
            public bool HasFlag(string flag) => _flags.Contains(flag);
        }

        private sealed class NumberSource : IConditionNumberSource
        {
            private readonly Dictionary<string, double> _n;
            public NumberSource(Dictionary<string, double> n) { _n = n; }
            public double GetNumber(string id) => _n.TryGetValue(id, out var v) ? v : 0d;
        }

        [Test] public void BuiltIn_HasFlag()
        {
            var r = new ConditionRegistry(); r.Register(new HasFlagEvaluator());
            var it = new ConditionItem("Condition.HasFlag");
            var p = new ConditionParam("flag", ConditionParamType.String); p.SetString("brave");
            it.parameters.Add(p);
            var e = Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, it));

            Assert.IsTrue(ConditionEngine.Evaluate(e, new TestContext(new FlagSource("brave")), r).Passed);
            Assert.IsFalse(ConditionEngine.Evaluate(e, new TestContext(new FlagSource("meek")), r).Passed);
            Assert.IsFalse(ConditionEngine.Evaluate(e, new TestContext(), r).Passed); // 无服务 → 不通过
        }

        [Test] public void BuiltIn_NumberCompare()
        {
            var r = new ConditionRegistry(); r.Register(new NumberCompareEvaluator());
            var ctx = new TestContext(new NumberSource(new Dictionary<string, double> { { "gold", 100d } }));

            ConditionExpression Make(int op, double amount)
            {
                var it = new ConditionItem("Condition.NumberCompare");
                var pid  = new ConditionParam("id",     ConditionParamType.String); pid.SetString("gold");
                var pop  = new ConditionParam("op",     ConditionParamType.Int);    pop.SetInt(op);
                var pamt = new ConditionParam("amount", ConditionParamType.Float);  pamt.SetFloat(amount);
                it.parameters.Add(pid); it.parameters.Add(pop); it.parameters.Add(pamt);
                return Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, it));
            }

            // gold = 100
            Assert.IsTrue (ConditionEngine.Evaluate(Make(NumberCompareEvaluator.GreaterOrEqual, 50d),  ctx, r).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Make(NumberCompareEvaluator.Greater,        50d),  ctx, r).Passed);
            Assert.IsFalse(ConditionEngine.Evaluate(Make(NumberCompareEvaluator.Greater,        100d), ctx, r).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Make(NumberCompareEvaluator.Equal,          100d), ctx, r).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Make(NumberCompareEvaluator.LessOrEqual,    100d), ctx, r).Passed);
            Assert.IsTrue (ConditionEngine.Evaluate(Make(NumberCompareEvaluator.Less,           200d), ctx, r).Passed);
            Assert.IsFalse(ConditionEngine.Evaluate(Make(NumberCompareEvaluator.Less,           100d), ctx, r).Passed);
        }
    }
}
