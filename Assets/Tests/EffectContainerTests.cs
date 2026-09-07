using System.Collections.Generic;
using NUnit.Framework;
using Ale.Condition;
using Ale.Effect;
using Ale.GameplayTags;
using Ale.Modifier;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// <see cref="EffectContainer"/> 运行时的门槛：瞬时落地 / 缺 Sink、持续汇流与到期、无限、周期（施加即结算 / 跨多周期余数 / 到期边界 /
    /// 不入汇流）、三种叠加与封顶刷新 / 不刷新 / 周期重置、三种到期策略、免疫、阻断顺序、抑制（汇流 / 周期冻结 / 时长照走 / 撤标签 / 初始判定）、
    /// 按标签 / 来源 / id / 句柄移除、执行信息、内置组合执行器、线索、等级重求、存档往返静默、Tick 再入防护、反射发现。
    /// </summary>
    public class EffectContainerTests
    {
        // ── 假件 ──────────────────────────────────────────────────────────────────
        private sealed class Sink : IEffectAttributeSink
        {
            public readonly List<(string attr, EModifierOperation op, float mag, string src)> Log =
                new List<(string, EModifierOperation, float, string)>();
            public void ApplyPermanent(object target, string attributeId, EModifierOperation operation, float magnitude, string sourceTag)
                => Log.Add((attributeId, operation, magnitude, sourceTag));
            public float Sum(string attr) { float s = 0f; foreach (var l in Log) if (l.attr == attr) s += l.mag; return s; }
        }

        private sealed class Rng : IEffectRandomSource
        {
            public float Value;
            public float NextUnit() => Value;
        }

        private sealed class CueSink : IEffectCueSink
        {
            public readonly List<string> Log = new List<string>();
            public void OnCue(GameplayTag cue, EEffectCueEvent cueEvent, IEffectExecutionInfo info) => Log.Add($"{cue}:{cueEvent}");
        }

        private interface IRecordSink { void Record(string s); }
        private sealed class RecordSink : IRecordSink
        {
            public readonly List<string> Log = new List<string>();
            public void Record(string s) => Log.Add(s);
        }

        /// <summary>记录阶段 / 句柄 / 等级 / 来源 / 主体（无 [EffectExecutor]，仅手动注册）。</summary>
        private sealed class RecordExecutor : IEffectExecutor
        {
            public string Key => "Test.Record";
            public string DisplayName => "记录";
            public string Category => "Test";
            public IReadOnlyList<EffectParamDef> ParamSchema { get; } = new EffectParamDef[0];
            public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
            {
                var rec = ctx?.GetService<IRecordSink>();
                if (rec == null) return EffectResult.Failed("no sink");
                var info = ctx.GetService<IEffectExecutionInfo>();
                rec.Record($"{info?.Phase}|h{info?.Effect?.Handle ?? 0}|L{info?.Level}|src={info?.Source}|tgt={ctx.Subject}");
                return EffectResult.Applied;
            }
        }

        private sealed class DictDefs : IEffectDefinitionSource
        {
            public readonly Dictionary<string, EffectDefinition> Map = new Dictionary<string, EffectDefinition>();
            public DictDefs(params EffectDefinition[] defs) { foreach (var d in defs) Map[d.id] = d; }
            public EffectDefinition GetEffect(string id) => id != null && Map.TryGetValue(id, out var d) ? d : null;
        }

        private sealed class ContainerMap : IEffectContainerSource
        {
            public readonly Dictionary<object, EffectContainer> Map = new Dictionary<object, EffectContainer>();
            public EffectContainer GetContainer(object subject) => subject != null && Map.TryGetValue(subject, out var c) ? c : null;
        }

        private sealed class FlagSource : IConditionFlagSource
        {
            private readonly HashSet<string> _flags;
            public FlagSource(params string[] flags) { _flags = new HashSet<string>(flags); }
            public bool HasFlag(string flag) => _flags.Contains(flag);
        }

        private readonly List<string> _warnings = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _warnings.Clear();
            EffectContainer.DefaultRandom = () => 0f;
        }

        // ── 构造助手 ────────────────────────────────────────────────────────────────
        private static EffectContext Ctx(object subject = null, Sink sink = null, Rng rng = null, CueSink cue = null, RecordSink rec = null,
            DictDefs defs = null, ContainerMap map = null, FlagSource flags = null, IEffectAttributeSource attrs = null)
        {
            var ctx = new EffectContext { Subject = subject ?? "hero" };
            if (sink  != null) ctx.RegisterService<IEffectAttributeSink>(sink);
            if (rng   != null) ctx.RegisterService<IEffectRandomSource>(rng);
            if (cue   != null) ctx.RegisterService<IEffectCueSink>(cue);
            if (rec   != null) ctx.RegisterService<IRecordSink>(rec);
            if (defs  != null) ctx.RegisterService<IEffectDefinitionSource>(defs);
            if (map   != null) ctx.RegisterService<IEffectContainerSource>(map);
            if (flags != null) ctx.RegisterService<IConditionFlagSource>(flags);
            if (attrs != null) ctx.RegisterService<IEffectAttributeSource>(attrs);
            return ctx;
        }

        private static EffectRegistry Reg()
        {
            var r = new EffectRegistry();
            r.Register(new RecordExecutor());
            r.Register(new NoOpEffect());
            r.Register(new ApplyEffectExecutor());
            r.Register(new RemoveEffectsWithTagExecutor());
            r.Register(new RemoveEffectByIdExecutor());
            return r;
        }

        private static ConditionRegistry CondReg()
        {
            var r = new ConditionRegistry();
            r.Register(new HasFlagEvaluator());
            return r;
        }

        private EffectContainer NewContainer(object owner = null)
        {
            var c = new EffectContainer(owner ?? "hero") { EffectRegistry = Reg(), ConditionRegistry = CondReg() };
            c.Warning = w => _warnings.Add(w);
            return c;
        }

        private static EffectDefinition Instant(string id) => new EffectDefinition(id, EDurationPolicy.Instant);
        private static EffectDefinition Infinite(string id) => new EffectDefinition(id, EDurationPolicy.Infinite);
        private static EffectDefinition Duration(string id, float duration)
            => new EffectDefinition(id, EDurationPolicy.HasDuration) { duration = EffectMagnitude.Scalable(duration) };

        private static EffectDefinition Mod(EffectDefinition d, string attr, EModifierOperation op, float m)
        {
            d.modifiers.Add(new EffectModifier(attr, op, m));
            return d;
        }

        private static EffectDefinition Periodic(EffectDefinition d, float period, bool executeOnApply)
        {
            d.period = EffectMagnitude.Scalable(period);
            d.executePeriodicOnApplication = executeOnApply;
            return d;
        }

        private static EffectDefinition Stacking(EffectDefinition d, EEffectStackingType type, int limit,
            EStackDurationRefreshPolicy refresh = EStackDurationRefreshPolicy.RefreshOnSuccessfulApplication,
            EStackPeriodResetPolicy reset = EStackPeriodResetPolicy.ResetOnSuccessfulApplication,
            EStackExpirationPolicy expiration = EStackExpirationPolicy.ClearEntireStack)
        {
            d.stackingType = type;
            d.stackLimit = limit;
            d.stackDurationRefreshPolicy = refresh;
            d.stackPeriodResetPolicy = reset;
            d.stackExpirationPolicy = expiration;
            return d;
        }

        private static EffectDefinition Exec(EffectDefinition d, string phase, EffectItem item = null)
        {
            var g = new EffectGroup(phase);
            g.items.Add(item ?? new EffectItem("Test.Record"));
            d.executions.groups.Add(g);
            return d;
        }

        private static EffectItem ApplyItem(string effectId, int level = 1)
        {
            var it = new EffectItem("Effect.ApplyEffect");
            var p1 = new EffectParam("effectId", EffectParamType.String); p1.SetString(effectId);
            var p2 = new EffectParam("level", EffectParamType.Int); p2.SetInt(level);
            it.parameters.Add(p1);
            it.parameters.Add(p2);
            return it;
        }

        private static EffectItem StrItem(string key, string paramId, string value)
        {
            var it = new EffectItem(key);
            var p = new EffectParam(paramId, EffectParamType.String); p.SetString(value);
            it.parameters.Add(p);
            return it;
        }

        private static ConditionExpression HasFlagCondition(string flag)
        {
            var e = new ConditionExpression();
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Condition.HasFlag");
            var p = new ConditionParam("flag", ConditionParamType.String); p.SetString(flag);
            it.parameters.Add(p);
            g.items.Add(it);
            e.groups.Add(g);
            return e;
        }

        private static List<ModifierDefinition> Collect(EffectContainer c, string attr = null)
        {
            var list = new List<ModifierDefinition>();
            c.CollectModifiers(attr, list);
            return list;
        }

        // ── 瞬时 ────────────────────────────────────────────────────────────────────
        [Test]
        public void Instant_AppliesPermanentModifiers_RunsOnApply_NotStored()
        {
            var c = NewContainer();
            var sink = new Sink();
            var rec = new RecordSink();
            var def = Exec(Mod(Instant("x"), "might", EModifierOperation.Add, 10f), EffectPhases.OnApply);

            var r = c.ApplyEffect(def, Ctx(sink: sink, rec: rec));
            Assert.AreEqual(EEffectApplyOutcome.Applied, r.Outcome);
            Assert.AreEqual(0, r.Handle);
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(1, sink.Log.Count);
            Assert.AreEqual(("might", EModifierOperation.Add, 10f, "effect:x"), sink.Log[0]);
            CollectionAssert.AreEqual(new[] { "onApply|h0|L1|src=|tgt=hero" }, rec.Log);
            Assert.AreEqual(0, _warnings.Count);
        }

        [Test]
        public void Instant_MissingSink_WarnsAndSkipsModifiers_ButRunsExecutions()
        {
            var c = NewContainer();
            var rec = new RecordSink();
            var def = Exec(Mod(Instant("x"), "might", EModifierOperation.Add, 10f), EffectPhases.OnApply);
            var r = c.ApplyEffect(def, Ctx(rec: rec));
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(1, rec.Log.Count);
            Assert.AreEqual(1, _warnings.Count);
            StringAssert.Contains("IEffectAttributeSink", _warnings[0]);
        }

        [Test]
        public void Apply_NullDefinition_Invalid_And_ZeroDuration_Invalid()
        {
            var c = NewContainer();
            Assert.AreEqual(EEffectApplyOutcome.Invalid, c.ApplyEffect((EffectDefinition)null, Ctx()).Outcome);
            Assert.AreEqual(EEffectApplyOutcome.Invalid, c.ApplyEffect(Duration("z", 0f), Ctx()).Outcome);
            Assert.AreEqual(0, c.Count);
        }

        // ── 持续 / 无限 ──────────────────────────────────────────────────────────────
        [Test]
        public void Duration_CollectModifiers_IncludesActive_ExcludesAfterExpire()
        {
            var c = NewContainer();
            int removed = 0;
            c.OnEffectRemoved += _ => removed++;
            var ctx = Ctx();
            var r = c.ApplyEffect(Mod(Duration("x", 30f), "might", EModifierOperation.Add, 10f), ctx);
            Assert.AreEqual(EEffectApplyOutcome.Applied, r.Outcome);
            Assert.AreEqual(1, r.Handle);
            Assert.AreEqual(1, r.Stacks);

            var mods = Collect(c, "might");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(10f, mods[0].magnitude);
            Assert.AreEqual("effect:x#1", mods[0].sourceTag);
            Assert.AreEqual(EModifierOperation.Add, mods[0].operation);
            Assert.AreEqual(0, Collect(c, "other").Count);

            c.Tick(29f, ctx);
            Assert.AreEqual(1, Collect(c, "might").Count);
            Assert.IsTrue(c.TryGet(1, out var e) && e.Remaining > 0.99f && e.Remaining < 1.01f);
            c.Tick(1f, ctx);
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(0, Collect(c, "might").Count);
            Assert.AreEqual(1, removed);
            Assert.IsFalse(e.IsActive);
        }

        [Test]
        public void Duration_Expires_RunsOnExpireThenOnRemove_RemovesGrantedTags()
        {
            var c = NewContainer();
            var rec = new RecordSink();
            var def = Duration("x", 30f);
            def.grantedTags.AddTag("Status.Buff");
            Exec(def, EffectPhases.OnApply);
            Exec(def, EffectPhases.OnExpire);
            Exec(def, EffectPhases.OnRemove);
            var ctx = Ctx(rec: rec);

            c.ApplyEffect(def, ctx);
            Assert.IsTrue(c.OwnedTags.HasMatchingTag(new GameplayTag("Status")));
            Assert.IsFalse(c.HasEffectWithTag(new GameplayTag("Status")));   // assetTags 为空，授予标签不算
            c.Tick(30f, ctx);
            CollectionAssert.AreEqual(new[] { "onApply|h1|L1|src=|tgt=hero", "onExpire|h1|L1|src=|tgt=hero", "onRemove|h1|L1|src=|tgt=hero" }, rec.Log);
            Assert.IsFalse(c.OwnedTags.HasMatchingTag(new GameplayTag("Status")));
        }

        [Test]
        public void Infinite_NeverExpires_TicksElapsed()
        {
            var c = NewContainer();
            var ctx = Ctx();
            c.ApplyEffect(Infinite("x"), ctx);
            c.Tick(1000f, ctx);
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.TryGet(1, out var e));
            Assert.AreEqual(1000f, e.Elapsed);
            Assert.AreEqual(-1f, e.Remaining);
            Assert.IsFalse(e.HasDuration);
        }

        // ── 周期 ────────────────────────────────────────────────────────────────────
        [Test]
        public void Periodic_FiresOnApplicationWhenFlagSet_ThenEveryPeriod()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            int periodic = 0;
            c.OnPeriodicExecuted += _ => periodic++;

            c.ApplyEffect(Periodic(Mod(Infinite("x"), "stamina", EModifierOperation.Add, 5f), 1f, true), ctx);
            Assert.AreEqual(1, sink.Log.Count);
            Assert.AreEqual("effect:x#1", sink.Log[0].src);
            c.Tick(1f, ctx);
            Assert.AreEqual(2, sink.Log.Count);
            c.Tick(0.5f, ctx);
            Assert.AreEqual(2, sink.Log.Count);
            c.Tick(0.5f, ctx);
            Assert.AreEqual(3, sink.Log.Count);
            Assert.AreEqual(3, periodic);
            Assert.AreEqual(15f, sink.Sum("stamina"), 1e-5f);

            var c2 = NewContainer();
            var sink2 = new Sink();
            var ctx2 = Ctx(sink: sink2);
            c2.ApplyEffect(Periodic(Mod(Infinite("y"), "stamina", EModifierOperation.Add, 5f), 1f, false), ctx2);
            Assert.AreEqual(0, sink2.Log.Count);
            c2.Tick(1f, ctx2);
            Assert.AreEqual(1, sink2.Log.Count);
        }

        [Test]
        public void Periodic_LargeDelta_FiresMultipleTimes_CarriesRemainder()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            c.ApplyEffect(Periodic(Mod(Infinite("x"), "hp", EModifierOperation.Add, 1f), 7f, false), ctx);
            c.Tick(30f, ctx);
            Assert.AreEqual(4, sink.Log.Count);
            Assert.IsTrue(c.TryGet(1, out var e));
            Assert.AreEqual(5f, e.PeriodTimer, 1e-5f);
        }

        [Test]
        public void Periodic_DoesNotFireBeyondExpiry_AndFiresOnBoundary()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            c.ApplyEffect(Periodic(Mod(Duration("x", 3f), "hp", EModifierOperation.Add, 1f), 1f, false), ctx);
            c.Tick(10f, ctx);
            Assert.AreEqual(3, sink.Log.Count);   // t = 1, 2, 3；到期后不再结算
            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void Periodic_ModifiersGoToSink_NotToCollectModifiers()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            c.ApplyEffect(Periodic(Mod(Duration("x", 10f), "hp", EModifierOperation.Add, 5f), 1f, true), ctx);
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual(0, Collect(c).Count);       // 周期效果不参与汇流
            Assert.AreEqual(1, sink.Log.Count);
            Assert.IsTrue(c.TryGet(1, out var e) && e.IsPeriodic);
        }

        // ── 叠加 ────────────────────────────────────────────────────────────────────
        [Test]
        public void Stacking_None_CreatesSeparateInstances()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var def = Duration("x", 10f);
            Assert.AreEqual(1, c.ApplyEffect(def, ctx).Handle);
            Assert.AreEqual(2, c.ApplyEffect(def, ctx).Handle);
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(2, c.GetStackCount("x"));
        }

        [Test]
        public void Stacking_ByTarget_IncrementsStacks_ScalesModifiers()
        {
            var c = NewContainer();
            var ctx = Ctx();
            int stackEvents = 0;
            c.OnEffectStackChanged += (e, old) => stackEvents++;
            var def = Stacking(Mod(Mod(Duration("x", 30f), "might", EModifierOperation.Add, 10f), "agility", EModifierOperation.Multiply, 0.1f),
                EEffectStackingType.AggregateByTarget, 3);

            var r1 = c.ApplyEffect(def, ctx, source: "a");
            var r2 = c.ApplyEffect(def, ctx, source: "b");   // 按目标聚合：来源不同也叠
            Assert.AreEqual(EEffectApplyOutcome.Applied, r1.Outcome);
            Assert.AreEqual(EEffectApplyOutcome.Stacked, r2.Outcome);
            Assert.AreEqual(r1.Handle, r2.Handle);
            Assert.AreEqual(2, r2.Stacks);
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual(2, c.GetStackCount("x"));
            Assert.AreEqual(1, stackEvents);

            var mods = Collect(c);
            Assert.AreEqual(2, mods.Count);
            Assert.AreEqual(20f, mods[0].magnitude, 1e-5f);
            Assert.AreEqual(0.21f, mods[1].magnitude, 1e-4f);   // (1.1)^2 − 1
        }

        [Test]
        public void Stacking_BySource_SeparatesBySourceTag()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var def = Stacking(Duration("x", 30f), EEffectStackingType.AggregateBySource, 5);
            var a1 = c.ApplyEffect(def, ctx, sourceTag: "a");
            var a2 = c.ApplyEffect(def, ctx, sourceTag: "a");
            var b1 = c.ApplyEffect(def, ctx, sourceTag: "b");
            Assert.AreEqual(a1.Handle, a2.Handle);
            Assert.AreEqual(2, a2.Stacks);
            Assert.AreNotEqual(a1.Handle, b1.Handle);
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(3, c.GetStackCount("x"));
            Assert.AreEqual("a", c.Find("x", "a").SourceTag);
            Assert.AreEqual("b", c.Find("x", "b").SourceTag);
        }

        [Test]
        public void Stacking_AtLimit_ReturnsRefreshed_AppliesRefreshPolicies()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var def = Stacking(Duration("x", 30f), EEffectStackingType.AggregateByTarget, 2);
            c.ApplyEffect(def, ctx);
            Assert.AreEqual(EEffectApplyOutcome.Stacked, c.ApplyEffect(def, ctx).Outcome);
            c.Tick(10f, ctx);
            Assert.IsTrue(c.TryGet(1, out var e));
            Assert.AreEqual(20f, e.Remaining, 1e-5f);

            var r = c.ApplyEffect(def, ctx);
            Assert.AreEqual(EEffectApplyOutcome.Refreshed, r.Outcome);
            Assert.AreEqual(2, r.Stacks);
            Assert.AreEqual(30f, e.Remaining, 1e-5f);   // 封顶仍刷新时长
        }

        [Test]
        public void Stacking_NeverRefreshDuration_KeepsRemaining()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var def = Stacking(Duration("x", 30f), EEffectStackingType.AggregateByTarget, 5, EStackDurationRefreshPolicy.NeverRefresh);
            c.ApplyEffect(def, ctx);
            c.Tick(10f, ctx);
            var r = c.ApplyEffect(def, ctx);
            Assert.AreEqual(EEffectApplyOutcome.Stacked, r.Outcome);
            Assert.IsTrue(c.TryGet(1, out var e));
            Assert.AreEqual(20f, e.Remaining, 1e-5f);
            Assert.AreEqual(30f, e.Duration, 1e-5f);
        }

        [Test]
        public void Stacking_PeriodReset_ExecutesImmediatelyWhenFlagSet()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            var def = Stacking(Periodic(Mod(Infinite("x"), "hp", EModifierOperation.Add, 5f), 1f, true), EEffectStackingType.AggregateByTarget, 5);
            c.ApplyEffect(def, ctx);
            Assert.AreEqual(1, sink.Log.Count);
            c.Tick(0.5f, ctx);
            c.ApplyEffect(def, ctx);
            Assert.AreEqual(2, sink.Log.Count);                 // 重置周期 + 施加即结算
            Assert.AreEqual(10f, sink.Log[1].mag, 1e-5f);        // 已按 2 层缩放
            Assert.IsTrue(c.TryGet(1, out var e));
            Assert.AreEqual(1f, e.PeriodTimer, 1e-5f);

            var c2 = NewContainer();
            var sink2 = new Sink();
            var ctx2 = Ctx(sink: sink2);
            var def2 = Stacking(Periodic(Mod(Infinite("y"), "hp", EModifierOperation.Add, 5f), 1f, true), EEffectStackingType.AggregateByTarget, 5,
                reset: EStackPeriodResetPolicy.NeverReset);
            c2.ApplyEffect(def2, ctx2);
            c2.Tick(0.5f, ctx2);
            c2.ApplyEffect(def2, ctx2);
            Assert.AreEqual(1, sink2.Log.Count);                // 不重置 → 不立即结算
            Assert.IsTrue(c2.TryGet(1, out var e2));
            Assert.AreEqual(0.5f, e2.PeriodTimer, 1e-5f);
        }

        // ── 到期策略 ────────────────────────────────────────────────────────────────
        [Test]
        public void Expiration_RemoveSingleStackAndRefresh_ThenClear()
        {
            var c = NewContainer();
            var ctx = Ctx();
            int stackEvents = 0, removed = 0;
            c.OnEffectStackChanged += (e, old) => { if (old == 2) stackEvents++; };
            c.OnEffectRemoved += _ => removed++;
            var def = Stacking(Mod(Duration("x", 10f), "might", EModifierOperation.Add, 10f), EEffectStackingType.AggregateByTarget, 3,
                expiration: EStackExpirationPolicy.RemoveSingleStackAndRefreshDuration);
            c.ApplyEffect(def, ctx);
            c.ApplyEffect(def, ctx);
            Assert.AreEqual(20f, Collect(c)[0].magnitude, 1e-5f);

            c.Tick(10f, ctx);
            Assert.IsTrue(c.TryGet(1, out var e));
            Assert.AreEqual(1, e.Stacks);
            Assert.AreEqual(10f, e.Remaining, 1e-5f);
            Assert.AreEqual(10f, Collect(c)[0].magnitude, 1e-5f);
            Assert.AreEqual(1, stackEvents);
            Assert.AreEqual(0, removed);

            c.Tick(10f, ctx);
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(1, removed);
        }

        [Test]
        public void Expiration_RefreshDuration_NeverRemoves()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var def = Stacking(Duration("x", 10f), EEffectStackingType.AggregateByTarget, 3, expiration: EStackExpirationPolicy.RefreshDuration);
            c.ApplyEffect(def, ctx);
            for (int i = 0; i < 3; i++)
            {
                c.Tick(10f, ctx);
                Assert.IsTrue(c.TryGet(1, out var e));
                Assert.AreEqual(10f, e.Remaining, 1e-5f);
            }
            Assert.IsTrue(c.TryGet(1, out var e2));
            Assert.AreEqual(30f, e2.Elapsed, 1e-5f);
            Assert.IsTrue(c.RemoveEffect(1, ctx));
            Assert.AreEqual(0, c.Count);
        }

        // ── 免疫 / 阻断顺序 ────────────────────────────────────────────────────────
        [Test]
        public void Immunity_BlocksByAssetTags_OnlyWhileGrantingEffectActiveAndUninhibited()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var ward = Infinite("ward");
            ward.grantedApplicationImmunityTags.AddTag("Status.Mental");
            var mind = Duration("mind", 5f);
            mind.assetTags.AddTag("Status.Mental.Deranged");

            c.ApplyEffect(ward, ctx);
            var blocked = c.ApplyEffect(mind, ctx);
            Assert.AreEqual(EEffectApplyOutcome.BlockedByImmunity, blocked.Outcome);
            StringAssert.Contains("ward", blocked.Note);
            Assert.AreEqual(1, c.RemoveEffectsById("ward", ctx));
            Assert.AreEqual(EEffectApplyOutcome.Applied, c.ApplyEffect(mind, ctx).Outcome);

            // 被抑制的免疫效果不提供免疫
            var c2 = NewContainer();
            var ward2 = Infinite("ward2");
            ward2.grantedApplicationImmunityTags.AddTag("Status.Mental");
            ward2.ongoingTagRequirements.requireTags.AddTag("State.Alive");   // 未持有 → 抑制
            c2.ApplyEffect(ward2, ctx);
            Assert.IsTrue(c2.TryGet(1, out var w) && w.IsInhibited);
            Assert.AreEqual(EEffectApplyOutcome.Applied, c2.ApplyEffect(mind, ctx).Outcome);
        }

        [Test]
        public void ApplicationTagRequirements_Condition_Chance_BlockOrder()
        {
            var c = NewContainer();
            var rng = new Rng { Value = 0.7f };
            var def = Duration("x", 5f);
            def.chanceToApply = 0.5f;
            def.applicationCondition = HasFlagCondition("brave");
            def.applicationTagRequirements.requireTags.AddTag("State.Alive");

            Assert.AreEqual(EEffectApplyOutcome.BlockedByTagRequirements, c.ApplyEffect(def, Ctx(rng: rng)).Outcome);
            c.AddLooseTag(new GameplayTag("State.Alive"));
            Assert.AreEqual(EEffectApplyOutcome.BlockedByCondition, c.ApplyEffect(def, Ctx(rng: rng, flags: new FlagSource())).Outcome);
            Assert.AreEqual(EEffectApplyOutcome.BlockedByChance, c.ApplyEffect(def, Ctx(rng: rng, flags: new FlagSource("brave"))).Outcome);
            rng.Value = 0.2f;
            Assert.AreEqual(EEffectApplyOutcome.Applied, c.ApplyEffect(def, Ctx(rng: rng, flags: new FlagSource("brave"))).Outcome);

            var never = Duration("n", 5f);
            never.chanceToApply = 0f;
            Assert.AreEqual(EEffectApplyOutcome.BlockedByChance, c.ApplyEffect(never, Ctx(rng: new Rng { Value = 0f })).Outcome);
            Assert.AreEqual(1, c.Count);
        }

        // ── 抑制 ────────────────────────────────────────────────────────────────────
        [Test]
        public void Inhibition_OngoingRequirementsFail_ExcludesModifiers_ContinuesDuration_RevokesGrantedTags()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var log = new List<bool>();
            int modChanged = 0;
            c.OnEffectInhibitedChanged += (e, inhibited) => log.Add(inhibited);
            c.OnModifiersChanged += () => modChanged++;
            var alive = new GameplayTag("State.Alive");
            var def = Mod(Duration("x", 10f), "might", EModifierOperation.Add, 10f);
            def.ongoingTagRequirements.requireTags.AddTag("State.Alive");
            def.grantedTags.AddTag("Status.X");

            c.AddLooseTag(alive);
            c.ApplyEffect(def, ctx);
            Assert.AreEqual(1, Collect(c).Count);
            Assert.IsTrue(c.OwnedTags.HasExactTag(new GameplayTag("Status.X")));

            c.RemoveLooseTag(alive);
            Assert.IsTrue(c.TryGet(1, out var e) && e.IsInhibited);
            Assert.AreEqual(0, Collect(c).Count);
            Assert.IsFalse(c.OwnedTags.HasExactTag(new GameplayTag("Status.X")));
            c.Tick(3f, ctx);
            Assert.AreEqual(7f, e.Remaining, 1e-5f);                     // 时长照走

            c.AddLooseTag(alive);
            Assert.IsFalse(e.IsInhibited);
            Assert.AreEqual(1, Collect(c).Count);
            Assert.IsTrue(c.OwnedTags.HasExactTag(new GameplayTag("Status.X")));
            CollectionAssert.AreEqual(new[] { true, false }, log);
            Assert.IsTrue(modChanged >= 3);

            // 宿主直接改 OwnedTags 也会触发重评
            c.OwnedTags.RemoveTag(alive);
            Assert.IsTrue(e.IsInhibited);
            c.OwnedTags.AddTag(alive);
            Assert.IsFalse(e.IsInhibited);
        }

        [Test]
        public void Inhibition_FreezesPeriod_And_InitialCheckOnApply()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            var alive = new GameplayTag("State.Alive");
            var def = Periodic(Mod(Infinite("x"), "hp", EModifierOperation.Add, 1f), 1f, false);
            def.ongoingTagRequirements.requireTags.AddTag("State.Alive");
            def.grantedTags.AddTag("Status.X");

            var r = c.ApplyEffect(def, ctx);                              // 无 Alive → 施加即抑制
            Assert.AreEqual(EEffectApplyOutcome.Applied, r.Outcome);
            Assert.IsTrue(c.TryGet(1, out var e) && e.IsInhibited);
            Assert.IsFalse(c.OwnedTags.HasExactTag(new GameplayTag("Status.X")));
            c.Tick(3f, ctx);
            Assert.AreEqual(0, sink.Log.Count);                           // 周期冻结
            Assert.AreEqual(1f, e.PeriodTimer, 1e-5f);

            c.AddLooseTag(alive);
            Assert.IsFalse(e.IsInhibited);
            c.Tick(1f, ctx);
            Assert.AreEqual(1, sink.Log.Count);                           // 从冻结处继续
        }

        // ── 移除 ────────────────────────────────────────────────────────────────────
        [Test]
        public void RemoveEffectsWithTags_MatchesAssetOrGrantedTags_ExcludesSelf()
        {
            var c = NewContainer();
            var ctx = Ctx();
            var a = Duration("a", 10f); a.assetTags.AddTag("Status.Buff.Might");
            var b = Duration("b", 10f); b.grantedTags.AddTag("Status.Debuff.Fear");
            var dispel = Instant("dispel"); dispel.removeEffectsWithTags.AddTag("Status");
            c.ApplyEffect(a, ctx);
            c.ApplyEffect(b, ctx);
            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c.HasEffectWithTag(new GameplayTag("Status.Buff")));
            c.ApplyEffect(dispel, ctx);
            Assert.AreEqual(0, c.Count);

            var d = Duration("d", 10f); d.assetTags.AddTag("Status.Z"); d.removeEffectsWithTags.AddTag("Status");
            c.ApplyEffect(a, ctx);
            c.ApplyEffect(d, ctx);
            Assert.AreEqual(1, c.Count);                                  // a 被移除，d 不移除自身
            Assert.IsNotNull(c.Find("d"));
            Assert.AreEqual(1, c.RemoveEffectsWithTags(new GameplayTagContainer("Status.Z"), ctx));
            Assert.AreEqual(0, c.RemoveEffectsWithTags(null, ctx));
        }

        [Test]
        public void Remove_ByHandleStacks_ById_BySource_All_And_ClearIsSilent()
        {
            var c = NewContainer();
            var rec = new RecordSink();
            var ctx = Ctx(rec: rec);
            var x = Stacking(Exec(Duration("x", 10f), EffectPhases.OnRemove), EEffectStackingType.AggregateByTarget, 3);
            c.ApplyEffect(x, ctx);
            c.ApplyEffect(x, ctx);
            Assert.IsTrue(c.RemoveEffect(1, ctx, 1));
            Assert.IsTrue(c.TryGet(1, out var e) && e.Stacks == 1);
            Assert.IsFalse(c.RemoveEffect(1, ctx, 0));
            Assert.IsFalse(c.RemoveEffect(99, ctx));
            Assert.AreEqual(1, c.RemoveEffectsById("x", ctx));
            Assert.AreEqual(1, rec.Log.Count);
            StringAssert.StartsWith("onRemove|h1", rec.Log[0]);

            c.ApplyEffect(Duration("p", 10f), ctx, sourceTag: "item:potion");
            c.ApplyEffect(Duration("p", 10f), ctx, sourceTag: "item:other");
            Assert.AreEqual(1, c.RemoveEffectsBySourceTag("item:potion", ctx));
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual(0, c.RemoveEffectsBySourceTag("", ctx));
            Assert.AreEqual(0, c.RemoveEffectsById("", ctx));

            c.ApplyEffect(x, ctx);
            rec.Log.Clear();
            c.RemoveAll(ctx);
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(1, rec.Log.Count);                            // 只有 x 有 onRemove

            c.ApplyEffect(x, ctx);
            c.AddLooseTag(new GameplayTag("Loose"));
            rec.Log.Clear();
            c.Clear();
            Assert.AreEqual(0, c.Count);
            Assert.AreEqual(0, rec.Log.Count);                            // 静默
            Assert.AreEqual(0, c.OwnedTags.Count);
            Assert.AreEqual(1, c.ApplyEffect(x, ctx).Handle);            // 句柄计数归 1
        }

        // ── 执行信息 / 内置执行器 ─────────────────────────────────────────────────
        [Test]
        public void Executions_ReceiveExecutionInfo_SubjectIsOwner_SourceLevelPhaseVisible()
        {
            var c = NewContainer();
            var rec = new RecordSink();
            var ctx = Ctx(subject: "someone-else", rec: rec);
            var def = Exec(Exec(Duration("x", 10f), EffectPhases.OnApply), EffectPhases.OnPeriod);
            Periodic(def, 1f, false);
            c.ApplyEffect(def, ctx, level: 3, source: "src");
            c.Tick(1f, ctx);
            c.RunPhase(1, "custom", ctx);                                  // 未配置的阶段：无组匹配 → 不执行
            CollectionAssert.AreEqual(new[] { "onApply|h1|L3|src=src|tgt=hero", "onPeriod|h1|L3|src=src|tgt=hero" }, rec.Log);
        }

        [Test]
        public void BuiltIn_ApplyEffect_Executor_AppliesViaContainerSource_AndApplierResolves()
        {
            var c = NewContainer();
            var b = Mod(Duration("b", 5f), "might", EModifierOperation.Add, 3f);
            var a = Exec(Instant("a"), EffectPhases.OnApply, ApplyItem("b", 2));
            var defs = new DictDefs(a, b);
            var map = new ContainerMap();
            map.Map["hero"] = c;
            var ctx = Ctx(defs: defs, map: map);

            var r = c.ApplyEffect(a, ctx, source: "caster");
            Assert.IsTrue(r.IsSuccess);
            Assert.AreEqual(1, c.Count);
            var eb = c.Find("b");
            Assert.IsNotNull(eb);
            Assert.AreEqual(2, eb.Level);
            Assert.AreEqual("caster", eb.Source);                         // 来源沿用外层执行信息

            // EffectApplier：按 id 施加（b 不叠加 → 新实例）、找不到定义 / 容器
            Assert.AreEqual(EEffectApplyOutcome.Applied, EffectApplier.Apply("b", ctx).Outcome);
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(EEffectApplyOutcome.DefinitionNotFound, EffectApplier.Apply("nope", ctx).Outcome);
            Assert.AreEqual(EEffectApplyOutcome.NoContainer, EffectApplier.Apply("b", Ctx(subject: "ghost", defs: defs, map: map)).Outcome);
            Assert.AreSame(c, EffectApplier.ResolveContainer(Ctx(subject: c), c));   // 主体本身是容器
        }

        [Test]
        public void BuiltIn_RemoveEffectsWithTag_And_RemoveEffectById_Executors()
        {
            var c = NewContainer();
            var map = new ContainerMap();
            map.Map["hero"] = c;
            var ctx = Ctx(map: map);
            var buff = Duration("buff", 10f); buff.assetTags.AddTag("Status.Buff");
            var other = Duration("other", 10f);
            c.ApplyEffect(buff, ctx);
            c.ApplyEffect(other, ctx);

            c.ApplyEffect(Exec(Instant("dispel"), EffectPhases.OnApply, StrItem("Effect.RemoveEffectsWithTag", "tag", "Status")), ctx);
            Assert.IsNull(c.Find("buff"));
            Assert.IsNotNull(c.Find("other"));
            c.ApplyEffect(Exec(Instant("purge"), EffectPhases.OnApply, StrItem("Effect.RemoveEffectById", "effectId", "other")), ctx);
            Assert.AreEqual(0, c.Count);
        }

        // ── 线索 / 等级 ──────────────────────────────────────────────────────────────
        [Test]
        public void Cues_FireOnAppliedExecutedRemoved()
        {
            var c = NewContainer();
            var cue = new CueSink();
            var ctx = Ctx(cue: cue);
            var def = Periodic(Duration("x", 10f), 1f, true);
            def.cueTags.AddTag("Cue.X");
            c.ApplyEffect(def, ctx);
            c.RemoveEffect(1, ctx);
            var inst = Instant("i");
            inst.cueTags.AddTag("Cue.I");
            c.ApplyEffect(inst, ctx);
            CollectionAssert.AreEqual(new[] { "Cue.X:Applied", "Cue.X:Executed", "Cue.X:Removed", "Cue.I:Executed" }, cue.Log);
        }

        [Test]
        public void SetLevel_ReevaluatesMagnitudes()
        {
            var c = NewContainer();
            var ctx = Ctx();
            int changed = 0;
            c.OnModifiersChanged += () => changed++;
            var def = Duration("x", 10f);
            def.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, EffectMagnitude.Scalable(10f, 5f)));
            c.ApplyEffect(def, ctx);
            Assert.AreEqual(10f, Collect(c)[0].magnitude, 1e-5f);
            Assert.IsTrue(c.SetLevel(1, 3, ctx));
            Assert.AreEqual(20f, Collect(c)[0].magnitude, 1e-5f);
            Assert.IsTrue(c.TryGet(1, out var e) && e.Level == 3);
            Assert.IsFalse(c.SetLevel(99, 3, ctx));
            Assert.AreEqual(2, changed);
        }

        // ── 存档 ────────────────────────────────────────────────────────────────────
        [Test]
        public void ExportImport_RoundTrip_RestoresTimersStacksTagsMagnitudes_Silently()
        {
            var c = NewContainer();
            var sink = new Sink();
            var ctx = Ctx(sink: sink);
            var x = Stacking(Mod(Duration("x", 30f), "might", EModifierOperation.Add, 10f), EEffectStackingType.AggregateByTarget, 3);
            x.grantedTags.AddTag("Status.X");
            var y = Periodic(Mod(Infinite("y"), "hp", EModifierOperation.Add, 1f), 2f, false);
            var defs = new DictDefs(x, y);

            c.ApplyEffect(x, ctx);
            c.ApplyEffect(new EffectApplyRequest(x, 2).WithSetByCaller("k", 4f), ctx);
            c.ApplyEffect(y, ctx);
            c.AddLooseTag(new GameplayTag("Loose.T"), 2);
            c.Tick(4f, ctx);

            var state = c.ExportState();
            Assert.AreEqual(2, state.effects.Count);
            Assert.AreEqual(3, state.nextHandle);
            CollectionAssert.AreEqual(new[] { "Loose.T" }, state.looseTags);
            CollectionAssert.AreEqual(new[] { 2 }, state.looseTagCounts);

            var c2 = NewContainer();
            int events = 0;
            c2.OnEffectAdded += _ => events++;
            c2.OnEffectInhibitedChanged += (e, b) => events++;
            c2.OnModifiersChanged += () => events++;
            c2.ImportState(state, defs, Ctx());
            Assert.AreEqual(0, events);
            Assert.AreEqual(0, _warnings.Count);
            Assert.AreEqual(2, c2.Count);

            Assert.IsTrue(c2.TryGet(1, out var ex));
            Assert.AreEqual("x", ex.Definition.id);
            Assert.AreEqual(2, ex.Stacks);
            Assert.AreEqual(2, ex.Level);
            Assert.AreEqual(30f, ex.Duration, 1e-5f);
            Assert.AreEqual(26f, ex.Remaining, 1e-5f);
            Assert.AreEqual(4f, ex.Elapsed, 1e-5f);
            Assert.AreEqual("effect:x", ex.SourceTag);
            Assert.AreEqual("effect:x#1", ex.ModifierSourceTag);
            Assert.AreEqual(4f, ex.SetByCaller["k"]);
            Assert.AreEqual(20f, ex.ScaledModifiers[0].magnitude, 1e-5f);

            Assert.IsTrue(c2.TryGet(2, out var ey));
            Assert.AreEqual(2f, ey.Period, 1e-5f);
            Assert.AreEqual(2f, ey.PeriodTimer, 1e-5f);
            Assert.AreEqual(-1f, ey.Remaining);

            Assert.AreEqual(1, c2.OwnedTags.GetExplicitTagCount(new GameplayTag("Status.X")));
            Assert.AreEqual(2, c2.OwnedTags.GetExplicitTagCount(new GameplayTag("Loose.T")));
            Assert.AreEqual(3, c2.ApplyEffect(Duration("z", 1f), Ctx()).Handle);   // 句柄计数延续
            Assert.AreEqual(20f, Collect(c2, "might")[0].magnitude, 1e-5f);
        }

        [Test]
        public void ImportState_UnknownEffectId_SkipsAndWarns_HandleCounterContinues()
        {
            var y = Duration("y", 5f);
            var state = new EffectContainerState { nextHandle = 1 };
            state.effects.Add(new ActiveEffectState { effectId = "nope", handle = 5, level = 1, stacks = 1, duration = 5f, remaining = 5f });
            state.effects.Add(new ActiveEffectState { effectId = "y", handle = 7, level = 1, stacks = 1, duration = 5f, remaining = 2f, sourceTag = "effect:y" });
            var c = NewContainer();
            c.ImportState(state, new DictDefs(y), Ctx());
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual(1, _warnings.Count);
            StringAssert.Contains("nope", _warnings[0]);
            Assert.IsTrue(c.TryGet(7, out var e) && e.Remaining == 2f);
            Assert.AreEqual(8, c.ApplyEffect(Duration("z", 1f), Ctx()).Handle);
            c.ImportState(null, null, Ctx());
            Assert.AreEqual(0, c.Count);
        }

        // ── Tick 防护 ───────────────────────────────────────────────────────────────
        [Test]
        public void Tick_NegativeOrZero_NoOp_And_EffectsAddedDuringTickNotTickedThisFrame()
        {
            var c = NewContainer();
            var q = Duration("q", 5f);
            var p = Periodic(Exec(Infinite("p"), EffectPhases.OnPeriod, ApplyItem("q")), 1f, false);
            var map = new ContainerMap();
            map.Map["hero"] = c;
            var ctx = Ctx(defs: new DictDefs(p, q), map: map);

            c.ApplyEffect(p, ctx);
            c.Tick(0f, ctx);
            c.Tick(-1f, ctx);
            Assert.IsTrue(c.TryGet(1, out var ep) && ep.Elapsed == 0f);

            c.Tick(1f, ctx);                                              // p 结算 → 施加 q
            var eq = c.Find("q");
            Assert.IsNotNull(eq);
            Assert.AreEqual(5f, eq.Remaining, 1e-5f);                     // 本 Tick 内新施加者不参与
            Assert.AreEqual(0f, eq.Elapsed, 1e-5f);
            c.Tick(1f, ctx);
            Assert.AreEqual(4f, eq.Remaining, 1e-5f);
            Assert.AreEqual(2, c.GetStackCount("q"));                    // 第二次周期又施加了一个 q
        }

        [Test]
        public void AutoRegister_FindsContainerBuiltIns()
        {
            var r = new EffectRegistry();
            r.AutoRegisterFromAssemblies();
            Assert.IsTrue(r.TryGet("Effect.ApplyEffect", out var a) && a is ApplyEffectExecutor);
            Assert.IsTrue(r.TryGet("Effect.RemoveEffectsWithTag", out var b) && b is RemoveEffectsWithTagExecutor);
            Assert.IsTrue(r.TryGet("Effect.RemoveEffectById", out var d) && d is RemoveEffectByIdExecutor);
        }
    }
}
