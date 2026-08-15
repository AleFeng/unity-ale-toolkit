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

        /// <summary>
        /// 本判定器的「等于」用严格容差 1e-9，不跟随 <see cref="ConditionCompare.DefaultEpsilon"/>（1e-6）。
        /// <para>用 float 扩宽出来的 10.100000381469727 与 10.1 相差约 3.8e-7 —— 恰好卡在两个容差之间，
        /// 于是这条断言能钉住「容差没有被悄悄放宽」。</para>
        /// </summary>
        [Test] public void BuiltIn_NumberCompare_EqualUsesStrictEpsilon()
        {
            var r = new ConditionRegistry(); r.Register(new NumberCompareEvaluator());
            var ctx = new TestContext(new NumberSource(new Dictionary<string, double> { { "attr", 10.1f } }));

            var it = new ConditionItem("Condition.NumberCompare");
            var pid  = new ConditionParam("id",     ConditionParamType.String); pid.SetString("attr");
            var pop  = new ConditionParam("op",     ConditionParamType.Int);    pop.SetInt(NumberCompareEvaluator.Equal);
            var pamt = new ConditionParam("amount", ConditionParamType.Float);  pamt.SetFloat(10.1d);
            it.parameters.Add(pid); it.parameters.Add(pop); it.parameters.Add(pamt);
            var e = Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, it));

            Assert.IsFalse(ConditionEngine.Evaluate(e, ctx, r).Passed, "1e-9 下不应判为相等");
            // 同一组数值在默认容差下是相等的——反证上面那条不是因为写错了参数才失败。
            Assert.IsTrue(ConditionCompare.Compare(10.1f, 10.1d, ConditionCompare.Equal));
        }

        // ── ConditionCompare 比较符范式 ──

        /// <summary>标签的文本与顺序是通信格式（进对话剧本与配置索引），必须冻结。</summary>
        [Test] public void ConditionCompare_LabelsAreFrozen()
        {
            CollectionAssert.AreEqual(
                new[] { "大于", "大于等于", "等于", "小于等于", "小于" }, ConditionCompare.Labels);
            Assert.AreEqual(0, ConditionCompare.Greater);
            Assert.AreEqual(1, ConditionCompare.GreaterOrEqual);
            Assert.AreEqual(2, ConditionCompare.Equal);
            Assert.AreEqual(3, ConditionCompare.LessOrEqual);
            Assert.AreEqual(4, ConditionCompare.Less);
        }

        /// <summary>内置判定器的常量必须与公共实现一致（它们是转发别名）。</summary>
        [Test] public void ConditionCompare_NumberCompareConstantsForward()
        {
            Assert.AreEqual(ConditionCompare.Greater,        NumberCompareEvaluator.Greater);
            Assert.AreEqual(ConditionCompare.GreaterOrEqual, NumberCompareEvaluator.GreaterOrEqual);
            Assert.AreEqual(ConditionCompare.Equal,          NumberCompareEvaluator.Equal);
            Assert.AreEqual(ConditionCompare.LessOrEqual,    NumberCompareEvaluator.LessOrEqual);
            Assert.AreEqual(ConditionCompare.Less,           NumberCompareEvaluator.Less);
        }

        [Test] public void ConditionCompare_IntegerOverload_IsExact()
        {
            Assert.IsTrue (ConditionCompare.Compare(10L, 10L, ConditionCompare.Equal));
            Assert.IsFalse(ConditionCompare.Compare(11L, 10L, ConditionCompare.Equal));
            Assert.IsTrue (ConditionCompare.Compare(11L, 10L, ConditionCompare.Greater));
            Assert.IsTrue (ConditionCompare.Compare(10L, 10L, ConditionCompare.GreaterOrEqual));
            Assert.IsTrue (ConditionCompare.Compare(9L,  10L, ConditionCompare.Less));
            Assert.IsTrue (ConditionCompare.Compare(10L, 10L, ConditionCompare.LessOrEqual));
        }

        /// <summary>默认容差 1e-6 要能吃下 float 扩宽误差；显式传更严的值则不能。</summary>
        [Test] public void ConditionCompare_FloatOverload_RespectsEpsilon()
        {
            double widened = 10.1f;   // 10.100000381469727，与 10.1 相差约 3.8e-7
            Assert.IsTrue (ConditionCompare.Compare(widened, 10.1d, ConditionCompare.Equal));
            Assert.IsFalse(ConditionCompare.Compare(widened, 10.1d, ConditionCompare.Equal, 1e-9));
        }

        /// <summary>未知比较符一律回落到「大于等于」，与各历史副本的行为一致。</summary>
        [Test] public void ConditionCompare_UnknownOp_FallsBackToGreaterOrEqual()
        {
            Assert.IsTrue (ConditionCompare.Compare(11L, 10L, 99));
            Assert.IsFalse(ConditionCompare.Compare(9L,  10L, 99));
            Assert.IsTrue (ConditionCompare.Compare(11d, 10d, -1));
        }

        [Test] public void ConditionCompare_ReadOp_DefaultsToGreaterOrEqual()
        {
            var withOp = new List<ConditionParam>();
            var p = new ConditionParam("op", ConditionParamType.Int); p.SetInt(ConditionCompare.Less);
            withOp.Add(p);

            Assert.AreEqual(ConditionCompare.Less, ConditionCompare.ReadOp(withOp));
            Assert.AreEqual(ConditionCompare.GreaterOrEqual, ConditionCompare.ReadOp(new List<ConditionParam>()));
        }

        [Test] public void ConditionCompare_CreateOpParam_UsesSharedLabels()
        {
            var def = ConditionCompare.CreateOpParam();
            Assert.AreEqual(ConditionCompare.DefaultParamId, def.id);
            Assert.AreEqual(ConditionParamType.Int, def.type);
            Assert.IsFalse(def.isArray);
            Assert.AreSame(ConditionCompare.Labels, def.choices);
        }

        // ── ConditionContext / SubjectConditionContext ──

        [Test] public void ConditionContext_ResolvesByExactType()
        {
            var src = new FlagSource("brave");
            var ctx = new ConditionContext();
            ctx.RegisterService<IConditionFlagSource>(src);

            Assert.AreSame(src, ctx.GetService<IConditionFlagSource>());
            Assert.IsTrue(ctx.HasService<IConditionFlagSource>());
            // 精确查表：按具体类取不到，按未注册的接口也取不到。
            Assert.IsNull(ctx.GetService<FlagSource>());
            Assert.IsNull(ctx.GetService<IConditionNumberSource>());
        }

        [Test] public void ConditionContext_RegisterNull_Unregisters()
        {
            var ctx = new ConditionContext();
            ctx.RegisterService<IConditionFlagSource>(new FlagSource("brave"));
            ctx.RegisterService<IConditionFlagSource>(null);
            Assert.IsFalse(ctx.HasService<IConditionFlagSource>());
        }

        [Test] public void ConditionContext_UnregisterAndClear()
        {
            var ctx = new ConditionContext { Subject = "hero" };
            ctx.RegisterService<IConditionFlagSource>(new FlagSource("brave"));

            Assert.IsTrue(ctx.UnregisterService<IConditionFlagSource>());
            Assert.IsFalse(ctx.UnregisterService<IConditionFlagSource>());

            ctx.RegisterService<IConditionFlagSource>(new FlagSource("brave"));
            ctx.Clear();
            Assert.IsNull(ctx.GetService<IConditionFlagSource>());
            Assert.IsNull(ctx.Subject);
        }

        [Test] public void ConditionContext_WorksAsEvaluatorContext()
        {
            var r = new ConditionRegistry(); r.Register(new HasFlagEvaluator());
            var it = new ConditionItem("Condition.HasFlag");
            var p = new ConditionParam("flag", ConditionParamType.String); p.SetString("brave");
            it.parameters.Add(p);
            var e = Expr(ConditionLogicOp.And, Group(ConditionLogicOp.And, false, it));

            var ctx = new ConditionContext();
            ctx.RegisterService<IConditionFlagSource>(new FlagSource("brave"));
            Assert.IsTrue(ConditionEngine.Evaluate(e, ctx, r).Passed);

            Assert.IsFalse(ConditionEngine.Evaluate(e, new ConditionContext(), r).Passed); // 无服务 → 不通过
        }

        [Test] public void SubjectConditionContext_OverridesSubjectAndForwardsServices()
        {
            var src = new FlagSource("brave");
            var inner = new ConditionContext { Subject = "inner" };
            inner.RegisterService<IConditionFlagSource>(src);

            var scoped = new SubjectConditionContext(inner, "outer");

            Assert.AreEqual("outer", scoped.Subject);
            Assert.AreSame(src, scoped.GetService<IConditionFlagSource>());
            Assert.AreEqual("inner", inner.Subject, "包装不应改动内层状态");
        }

        [Test] public void SubjectConditionContext_NullInner_ReturnsNullService()
        {
            var scoped = new SubjectConditionContext(null, "outer");
            Assert.AreEqual("outer", scoped.Subject);
            Assert.IsNull(scoped.GetService<IConditionFlagSource>());
        }

        // ── EnsureAutoRegistered 幂等兜底 ──

        [Test] public void EnsureAutoRegistered_IsIdempotent()
        {
            var r = new ConditionRegistry();
            Assert.IsTrue(r.EnsureAutoRegistered(), "首次应执行扫描");
            Assert.IsTrue(r.Count > 0);
            Assert.IsFalse(r.EnsureAutoRegistered(), "第二次应跳过");
        }

        /// <summary>Clear() 必须复位标志，否则清空后再 Ensure 会静默变成空操作、注册表永久为空。</summary>
        [Test] public void EnsureAutoRegistered_ClearResetsFlag()
        {
            var r = new ConditionRegistry();
            r.EnsureAutoRegistered();
            r.Clear();
            Assert.AreEqual(0, r.Count);

            Assert.IsTrue(r.EnsureAutoRegistered(), "Clear 之后应能重新扫描");
            Assert.IsTrue(r.TryGet("Condition.NumberCompare", out _));
        }

        /// <summary>手动全量重扫之后，Ensure 应认为已扫过。</summary>
        [Test] public void EnsureAutoRegistered_AfterManualScan_Skips()
        {
            var r = new ConditionRegistry();
            r.AutoRegisterFromAssemblies();
            Assert.IsFalse(r.EnsureAutoRegistered());
        }
    }
}
