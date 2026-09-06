using System.Collections.Generic;
using NUnit.Framework;
using Ale.Condition;
using Ale.GameplayTags;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 标签 × 条件桥（<c>Ale.GameplayTags.Condition</c>）的门槛：主体标签解析顺序（服务 → 拥有者 → false）、
    /// 精确 / 层级开关、集合匹配三模式与空集语义、模式标签冻结、反射自动注册、与 ConditionExpression 的组合。
    /// </summary>
    public class HasGameplayTagEvaluatorTests
    {
        private sealed class Owner : IGameplayTagOwner
        {
            public GameplayTagCountContainer OwnedTags { get; } = new GameplayTagCountContainer();
        }

        private sealed class Source : IGameplayTagSource
        {
            public readonly Dictionary<object, GameplayTagCountContainer> Map = new Dictionary<object, GameplayTagCountContainer>();
            public GameplayTagCountContainer GetOwnedTags(object subject)
                => subject != null && Map.TryGetValue(subject, out var c) ? c : null;
        }

        private sealed class Ctx : IConditionContext
        {
            private readonly object[] _services;
            public Ctx(object subject, params object[] services) { Subject = subject; _services = services ?? new object[0]; }
            public object Subject { get; }
            public T GetService<T>() where T : class
            {
                foreach (var s in _services) if (s is T t) return t;
                return null;
            }
        }

        private static GameplayTag T(string s) => new GameplayTag(s);
        private static ConditionParam PStr(string id, string v)  { var p = new ConditionParam(id, ConditionParamType.String); p.SetString(v); return p; }
        private static ConditionParam PBool(string id, bool v)   { var p = new ConditionParam(id, ConditionParamType.Bool);   p.SetBool(v);   return p; }
        private static ConditionParam PInt(string id, long v)    { var p = new ConditionParam(id, ConditionParamType.Int);    p.SetInt(v);    return p; }
        private static ConditionParam PStrArr(string id, params string[] vs)
        {
            var p = new ConditionParam(id, ConditionParamType.String, true);
            for (int i = 0; i < vs.Length; i++) p.SetString(vs[i], i);
            return p;
        }

        private static bool Has(IConditionContext ctx, string tag, bool exact = false)
            => new HasGameplayTagEvaluator().Evaluate(new[] { PStr("tag", tag), PBool("exact", exact) }, ctx);

        private static bool Tags(IConditionContext ctx, int mode, bool exact, params string[] tags)
            => new GameplayTagsEvaluator().Evaluate(new[] { PStrArr("tags", tags), PInt("mode", mode), PBool("exact", exact) }, ctx);

        [Test]
        public void HasGameplayTag_UsesSourceService_ThenOwnerFallback_ThenFalse()
        {
            var owner = new Owner();
            owner.OwnedTags.AddTag(T("Status.Buff"));
            Assert.IsTrue(Has(new Ctx(owner), "Status"));                 // 仅拥有者回落

            var src = new Source();
            var c1Tags = new GameplayTagCountContainer();
            c1Tags.AddTag(T("Immunity.Mental"));
            src.Map["c1"] = c1Tags;
            Assert.IsTrue(Has(new Ctx("c1", src), "Immunity"));           // 服务优先，按主体查
            Assert.IsFalse(Has(new Ctx("c2", src), "Immunity"));          // 未知主体且无拥有者回落 → false
            Assert.IsTrue(Has(new Ctx(owner, src), "Status.Buff"));       // 服务对该主体返回 null → 回落拥有者

            Assert.IsFalse(Has(new Ctx(null), "Status"));                 // 无服务无主体
            Assert.IsFalse(Has(null, "Status"));                          // ctx 为 null
        }

        [Test]
        public void HasGameplayTag_ExactFlag_RespectsHierarchy()
        {
            var owner = new Owner();
            owner.OwnedTags.AddTag(T("Status.Buff.Might"));
            var ctx = new Ctx(owner);
            Assert.IsTrue(Has(ctx, "Status"));
            Assert.IsFalse(Has(ctx, "Status", exact: true));
            Assert.IsTrue(Has(ctx, "Status.Buff.Might", exact: true));
            Assert.IsFalse(Has(ctx, "Status.Buff.Might.X"));
            Assert.IsFalse(Has(ctx, ""));          // 空标签 → false
            Assert.IsFalse(Has(ctx, "A..B"));      // 非法 → false
        }

        [Test]
        public void GameplayTags_AnyAllNone_Modes()
        {
            var owner = new Owner();
            owner.OwnedTags.AddTag(T("A.1"));
            owner.OwnedTags.AddTag(T("B"));
            var ctx = new Ctx(owner);

            Assert.IsTrue(Tags(ctx, GameplayTagMatchMode.Any, false, "A", "Z"));
            Assert.IsFalse(Tags(ctx, GameplayTagMatchMode.Any, true, "A", "Z"));   // 精确：A 未显式持有
            Assert.IsTrue(Tags(ctx, GameplayTagMatchMode.All, false, "A", "B"));
            Assert.IsFalse(Tags(ctx, GameplayTagMatchMode.All, false, "A", "Z"));
            Assert.IsTrue(Tags(ctx, GameplayTagMatchMode.None, false, "Z", "Y"));
            Assert.IsFalse(Tags(ctx, GameplayTagMatchMode.None, false, "A"));

            // 空集：Any → false，All / None → true
            Assert.IsFalse(Tags(ctx, GameplayTagMatchMode.Any, false));
            Assert.IsTrue(Tags(ctx, GameplayTagMatchMode.All, false));
            Assert.IsTrue(Tags(ctx, GameplayTagMatchMode.None, false));

            // 缺 mode 参数 → Any
            Assert.IsTrue(new GameplayTagsEvaluator().Evaluate(new[] { PStrArr("tags", "B") }, ctx));

            // 无拥有者 → false（即使 None 模式）
            Assert.IsFalse(Tags(new Ctx(null), GameplayTagMatchMode.None, false, "Z"));
        }

        [Test]
        public void MatchMode_Labels_And_Constants_Frozen()
        {
            CollectionAssert.AreEqual(new[] { "任一", "全部", "皆无" }, GameplayTagMatchMode.Labels);
            Assert.AreEqual(0, GameplayTagMatchMode.Any);
            Assert.AreEqual(1, GameplayTagMatchMode.All);
            Assert.AreEqual(2, GameplayTagMatchMode.None);
            var def = GameplayTagMatchMode.CreateModeParam();
            Assert.AreEqual("mode", def.id);
            Assert.AreSame(GameplayTagMatchMode.Labels, def.choices);
        }

        [Test]
        public void AutoRegister_FindsGameplayTagEvaluators()
        {
            var r = new ConditionRegistry();
            r.AutoRegisterFromAssemblies();
            Assert.IsTrue(r.TryGet("Condition.HasGameplayTag", out var e1) && e1 is HasGameplayTagEvaluator);
            Assert.IsTrue(r.TryGet("Condition.GameplayTags", out var e2) && e2 is GameplayTagsEvaluator);
        }

        [Test]
        public void Evaluators_ComposeInsideConditionExpression()
        {
            var owner = new Owner();
            owner.OwnedTags.AddTag(T("Status.Buff"));

            var expr = new ConditionExpression();
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var has = new ConditionItem("Condition.HasGameplayTag");
            has.parameters.Add(PStr("tag", "Status"));
            var not = new ConditionItem("Condition.HasGameplayTag") { negate = true };
            not.parameters.Add(PStr("tag", "Immunity"));
            g.items.Add(has);
            g.items.Add(not);
            expr.groups.Add(g);

            var reg = new ConditionRegistry();
            reg.Register(new HasGameplayTagEvaluator());
            Assert.IsTrue(ConditionEngine.Evaluate(expr, new Ctx(owner), reg).Passed);
            owner.OwnedTags.AddTag(T("Immunity.Mental"));
            Assert.IsFalse(ConditionEngine.Evaluate(expr, new Ctx(owner), reg).Passed);
        }
    }
}
