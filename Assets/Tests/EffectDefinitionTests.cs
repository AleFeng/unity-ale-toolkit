using System.Collections.Generic;
using NUnit.Framework;
using Ale.Condition;
using Ale.Effect;
using Ale.GameplayTags;
using Ale.Modifier;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 效果定义层的门槛：Normalize（空阶段改写、补 null、夹取）、Validate 的错误 / 警告矩阵、深拷贝、JSON 往返、
    /// <see cref="EffectRegistry.EnsureAutoRegistered"/> 幂等、定义注册表的查找顺序、<see cref="ActiveEffect"/> 层数缩放与默认值、
    /// 请求来源标记、效果上下文与主体包装。
    /// </summary>
    public class EffectDefinitionTests
    {
        private sealed class DictSource : IEffectDefinitionSource
        {
            public readonly Dictionary<string, EffectDefinition> Map = new Dictionary<string, EffectDefinition>();
            public EffectDefinition GetEffect(string id) => id != null && Map.TryGetValue(id, out var d) ? d : null;
        }

        private static EffectDefinition Rich()
        {
            var d = new EffectDefinition("battle_focus", EDurationPolicy.HasDuration)
            {
                displayName  = "战意",
                duration     = EffectMagnitude.Scalable(30f, 5f),
                period       = EffectMagnitude.Scalable(0f),
                stackingType = EEffectStackingType.AggregateByTarget,
                stackLimit   = 3,
                chanceToApply = 0.75f,
            };
            d.assetTags.AddTag("Status.Buff.Might");
            d.grantedTags.AddTag("Status.Focused");
            d.removeEffectsWithTags.AddTag("Status.Debuff.Fear");
            d.grantedApplicationImmunityTags.AddTag("Status.Debuff.Fear");
            d.applicationTagRequirements.ignoreTags.AddTag("Immunity.Buff");
            d.ongoingTagRequirements.requireTags.AddTag("State.Alive");
            d.cueTags.AddTag("Cue.Buff.Might");
            d.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
            d.modifiers.Add(new EffectModifier("agility", EModifierOperation.PercentAdd, EffectMagnitude.SetByCaller("agi", 0.1f)));

            var gate = new ConditionExpression();
            var cg = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var ci = new ConditionItem("Condition.HasFlag");
            var cp = new ConditionParam("flag", ConditionParamType.String); cp.SetString("brave");
            ci.parameters.Add(cp);
            cg.items.Add(ci);
            gate.groups.Add(cg);
            d.applicationCondition = gate;

            var g = new EffectGroup(EffectPhases.OnApply);
            var it = new EffectItem("Effect.NoOp");
            g.items.Add(it);
            d.executions.groups.Add(g);
            return d;
        }

        // ── Normalize ──
        [Test]
        public void Normalize_RewritesEmptyPhaseToOnApply_AndFillsNulls()
        {
            var d = new EffectDefinition(" x ")
            {
                assetTags = null, grantedTags = null, removeEffectsWithTags = null, grantedApplicationImmunityTags = null, cueTags = null,
                applicationTagRequirements = null, ongoingTagRequirements = null, applicationCondition = null,
                modifiers = null, duration = null, period = null, executions = null,
                stackLimit = -3, chanceToApply = 1.5f,
            };
            d.Normalize();
            Assert.AreEqual("x", d.id);
            Assert.IsNotNull(d.assetTags);
            Assert.IsNotNull(d.applicationTagRequirements);
            Assert.IsNotNull(d.applicationCondition);
            Assert.IsNotNull(d.modifiers);
            Assert.IsNotNull(d.duration);
            Assert.IsNotNull(d.executions);
            Assert.AreEqual(0, d.stackLimit);
            Assert.AreEqual(1f, d.chanceToApply);

            d.executions.groups.Add(new EffectGroup(""));
            d.executions.groups.Add(new EffectGroup("  onRemove "));
            d.executions.groups.Add(null);
            d.modifiers.Add(null);
            d.modifiers.Add(new EffectModifier { attributeId = " might ", magnitude = null });
            d.assetTags.tags.Add(" A . B ");
            d.assetTags.tags.Add("A.B");
            d.chanceToApply = -1f;
            d.Normalize();
            Assert.AreEqual(2, d.executions.groups.Count);
            Assert.AreEqual(EffectPhases.OnApply, d.executions.groups[0].phase);
            Assert.AreEqual(EffectPhases.OnRemove, d.executions.groups[1].phase);
            Assert.AreEqual(1, d.modifiers.Count);
            Assert.AreEqual("might", d.modifiers[0].attributeId);
            Assert.IsNotNull(d.modifiers[0].magnitude);
            CollectionAssert.AreEqual(new[] { "A.B" }, d.assetTags.tags);
            Assert.AreEqual(0f, d.chanceToApply);
        }

        // ── Validate：错误 ──
        [Test]
        public void Validate_Errors_Matrix()
        {
            var d = new EffectDefinition("", EDurationPolicy.HasDuration);   // id 空 + 时长 0
            d.modifiers.Add(new EffectModifier("", EModifierOperation.Add, 1f));          // attributeId 空
            d.modifiers.Add(new EffectModifier("a", EModifierOperation.Add, EffectMagnitude.AttributeBased(""))); // 幅度缺 attributeId
            d.applicationTagRequirements.requireTags.AddTag("A");
            d.applicationTagRequirements.ignoreTags.AddTag("A");                            // 自相矛盾
            d.chanceToApply = 1.5f;                                                          // 越界
            d.assetTags.tags.Add("bad..tag");                                                // 非法标签
            d.period = EffectMagnitude.SetByCaller(null);                                    // 非 Scalable 周期缺键

            var errors = new List<string>();
            Assert.IsFalse(d.Validate(errors));
            var real = errors.FindAll(e => !EffectDefinition.IsWarning(e));
            Assert.AreEqual(8, real.Count, string.Join("\n", errors));
            StringAssert.Contains("id 为空", real[0]);
            StringAssert.Contains("时长为 0", real[1]);
            StringAssert.Contains("period", real[2]);
            StringAssert.Contains("modifiers[0]", real[3]);
            StringAssert.Contains("modifiers[1].magnitude", real[4]);
            StringAssert.Contains("assetTags[0]", real[5]);
            StringAssert.Contains("applicationTagRequirements", real[6]);
            StringAssert.Contains("chanceToApply", real[7]);
        }

        // ── Validate：警告不影响返回值 ──
        [Test]
        public void Validate_Warnings_DoNotFail()
        {
            var instant = new EffectDefinition("i");
            instant.grantedTags.AddTag("Status.X");
            instant.ongoingTagRequirements.requireTags.AddTag("State.Alive");
            instant.period = EffectMagnitude.Scalable(1f);
            instant.stackingType = EEffectStackingType.AggregateByTarget;
            instant.grantedApplicationImmunityTags.AddTag("Immunity.X");
            instant.chanceToApply = 0f;
            instant.executions.groups.Add(new EffectGroup("custom.phase"));
            instant.executions.groups.Add(new EffectGroup(""));
            var msgs = new List<string>();
            Assert.IsTrue(instant.Validate(msgs));
            Assert.AreEqual(8, msgs.Count, string.Join("\n", msgs));
            Assert.IsTrue(msgs.TrueForAll(EffectDefinition.IsWarning));

            var noop = new EffectDefinition("s", EDurationPolicy.Infinite)
            {
                stackingType = EEffectStackingType.AggregateBySource, stackLimit = 1,
                stackDurationRefreshPolicy = EStackDurationRefreshPolicy.NeverRefresh,
                stackPeriodResetPolicy = EStackPeriodResetPolicy.NeverReset,
            };
            noop.grantedTags.AddTag("Status.Debuff.X");
            noop.ongoingTagRequirements.ignoreTags.AddTag("Status.Debuff");   // 自授标签命中禁止标签 → 自我抑制
            msgs.Clear();
            Assert.IsTrue(noop.Validate(msgs));
            Assert.AreEqual(2, msgs.Count, string.Join("\n", msgs));
            StringAssert.Contains("等于未配置", msgs[0]);
            StringAssert.Contains("自我抑制", msgs[1]);

            msgs.Clear();
            Assert.IsTrue(Rich().Validate(msgs));
            Assert.AreEqual(0, msgs.Count, string.Join("\n", msgs));
        }

        // ── Clone ──
        [Test]
        public void Clone_DeepCopies_AllNested()
        {
            var a = Rich();
            var b = a.Clone();
            b.id = "other";
            b.assetTags.AddTag("Z");
            b.grantedTags.Clear();
            b.applicationTagRequirements.ignoreTags.AddTag("Q");
            b.ongoingTagRequirements.requireTags.Clear();
            b.modifiers[0].attributeId = "changed";
            b.modifiers.Add(new EffectModifier("x", EModifierOperation.Add, 1f));
            b.executions.groups[0].items.Clear();
            b.applicationCondition.groups.Clear();
            b.duration.baseValue = 999f;
            b.cueTags.Clear();

            Assert.AreEqual("battle_focus", a.id);
            Assert.AreEqual(1, a.assetTags.Count);
            Assert.AreEqual(1, a.grantedTags.Count);
            Assert.AreEqual(1, a.applicationTagRequirements.ignoreTags.Count);
            Assert.AreEqual(1, a.ongoingTagRequirements.requireTags.Count);
            Assert.AreEqual("might", a.modifiers[0].attributeId);
            Assert.AreEqual(2, a.modifiers.Count);
            Assert.AreEqual(1, a.executions.groups[0].items.Count);
            Assert.AreEqual(1, a.applicationCondition.groups.Count);
            Assert.AreEqual(30f, a.duration.baseValue);
            Assert.AreEqual(1, a.cueTags.Count);
        }

        // ── JSON ──
        [Test]
        public void Json_RoundTrip_DefinitionWithTagsModifiersConditionExecutions()
        {
            var a = Rich();
            var json = EffectJson.ToJson(a);
            var b = EffectJson.DefinitionFromJson(json);

            Assert.AreEqual(a.id, b.id);
            Assert.AreEqual(a.displayName, b.displayName);
            Assert.AreEqual(EDurationPolicy.HasDuration, b.durationPolicy);
            Assert.AreEqual(30f, b.duration.baseValue);
            Assert.AreEqual(5f, b.duration.perLevel);
            Assert.AreEqual(EEffectStackingType.AggregateByTarget, b.stackingType);
            Assert.AreEqual(3, b.stackLimit);
            Assert.AreEqual(0.75f, b.chanceToApply, 1e-6f);
            CollectionAssert.AreEqual(a.assetTags.tags, b.assetTags.tags);
            CollectionAssert.AreEqual(a.grantedTags.tags, b.grantedTags.tags);
            CollectionAssert.AreEqual(a.applicationTagRequirements.ignoreTags.tags, b.applicationTagRequirements.ignoreTags.tags);
            CollectionAssert.AreEqual(a.ongoingTagRequirements.requireTags.tags, b.ongoingTagRequirements.requireTags.tags);
            CollectionAssert.AreEqual(a.cueTags.tags, b.cueTags.tags);
            Assert.AreEqual(2, b.modifiers.Count);
            Assert.AreEqual(EModifierOperation.PercentAdd, b.modifiers[1].operation);
            Assert.AreEqual(EMagnitudeKind.SetByCaller, b.modifiers[1].magnitude.kind);
            Assert.AreEqual("agi", b.modifiers[1].magnitude.setByCallerKey);
            Assert.AreEqual(1, b.applicationCondition.TotalItemCount());
            Assert.AreEqual("Condition.HasFlag", b.applicationCondition.groups[0].items[0].key);
            Assert.AreEqual(1, b.executions.TotalItemCount());
            Assert.AreEqual(EffectPhases.OnApply, b.executions.groups[0].phase);

            Assert.IsNotNull(EffectJson.DefinitionFromJson(""));
            Assert.IsNotNull(EffectJson.DefinitionFromJson("{}").assetTags);   // 缺字段经 Normalize 兜底
        }

        // ── EffectRegistry.EnsureAutoRegistered ──
        [Test]
        public void EffectRegistry_EnsureAutoRegistered_IsIdempotent_And_ClearResets()
        {
            var r = new EffectRegistry();
            Assert.IsTrue(r.EnsureAutoRegistered(), "首次应执行扫描");
            Assert.IsTrue(r.TryGet("Effect.NoOp", out _));
            Assert.IsFalse(r.EnsureAutoRegistered(), "第二次应跳过");
            r.Clear();
            Assert.AreEqual(0, r.Count);
            Assert.IsTrue(r.EnsureAutoRegistered(), "Clear 之后应能重新扫描");
            Assert.IsTrue(r.TryGet("Effect.SetFlag", out _));

            var manual = new EffectRegistry();
            manual.AutoRegisterFromAssemblies();
            Assert.IsFalse(manual.EnsureAutoRegistered());
        }

        // ── 定义注册表 ──
        [Test]
        public void DefinitionRegistry_LocalThenSources_FirstWins()
        {
            var reg = new EffectDefinitionRegistry();
            var local = new EffectDefinition("a");
            var s1 = new DictSource();
            s1.Map["a"] = new EffectDefinition("a");
            s1.Map["b"] = new EffectDefinition("b");
            var s2 = new DictSource();
            s2.Map["b"] = new EffectDefinition("b");
            s2.Map["c"] = new EffectDefinition("c");

            reg.Register(local);
            reg.AddSource(s1);
            reg.AddSource(s2);
            reg.AddSource(s1);        // 去重
            reg.AddSource(reg);       // 忽略自身
            reg.AddSource(null);
            Assert.AreEqual(2, reg.SourceCount);
            Assert.AreEqual(1, reg.LocalCount);

            Assert.AreSame(local, reg.GetEffect("a"));
            Assert.AreSame(s1.Map["b"], reg.GetEffect("b"));
            Assert.AreSame(s2.Map["c"], reg.GetEffect("c"));
            Assert.IsNull(reg.GetEffect("x"));
            Assert.IsNull(reg.GetEffect(null));
            Assert.IsTrue(reg.TryGet("c", out var c) && c.id == "c");

            Assert.IsTrue(reg.RemoveSource(s1));
            Assert.AreSame(s2.Map["b"], reg.GetEffect("b"));
            Assert.IsTrue(reg.Unregister("a"));
            Assert.IsNull(reg.GetEffect("a"));   // 本地已注销、s1 已移除 → a 不可得
            reg.Register(new EffectDefinition(""));   // 空 id 忽略
            Assert.AreEqual(0, reg.LocalCount);
            reg.Clear();
            Assert.AreEqual(0, reg.SourceCount);
        }

        // ── ActiveEffect ──
        [Test]
        public void ActiveEffect_ScaleMagnitude_StackMath()
        {
            Assert.AreEqual(6f,   ActiveEffect.ScaleMagnitude(EModifierOperation.Add,        2f,   3), 1e-6f);
            Assert.AreEqual(0.3f, ActiveEffect.ScaleMagnitude(EModifierOperation.PercentAdd, 0.1f, 3), 1e-6f);
            Assert.AreEqual(0.331f, ActiveEffect.ScaleMagnitude(EModifierOperation.Multiply, 0.1f, 3), 1e-5f);   // 1.1^3 − 1
            Assert.AreEqual(5f,   ActiveEffect.ScaleMagnitude(EModifierOperation.Override,   5f,   3), 1e-6f);
            Assert.AreEqual(2f,   ActiveEffect.ScaleMagnitude(EModifierOperation.Add,        2f,   0), 1e-6f);   // 层数下限 1
        }

        [Test]
        public void ActiveEffect_Construct_DefaultsAndSourceTag()
        {
            var def = Rich();
            var sbc = new Dictionary<string, float> { { "agi", 0.5f } };
            var e = new ActiveEffect(7, def, "tgt", "src", null, 0, sbc);
            Assert.AreEqual(7, e.Handle);
            Assert.AreSame(def, e.Definition);
            Assert.AreEqual("effect:battle_focus", e.SourceTag);
            Assert.AreEqual("effect:battle_focus#7", e.ModifierSourceTag);
            Assert.AreEqual(1, e.Level);
            Assert.AreEqual(-1f, e.Remaining);
            Assert.AreEqual(1, e.Stacks);
            Assert.IsTrue(e.IsActive);
            Assert.IsFalse(e.IsInhibited);
            Assert.IsFalse(e.IsPeriodic);
            Assert.IsTrue(e.HasDuration);
            Assert.AreEqual(0.5f, e.SetByCaller["agi"]);
            sbc["agi"] = 9f;
            Assert.AreEqual(0.5f, e.SetByCaller["agi"]);   // 已拷贝
            Assert.AreEqual(0, e.ScaledModifiers.Count);
            Assert.IsTrue(ActiveEffect.IsSkipped(float.NaN));

            var custom = new ActiveEffect(1, def, "tgt", null, "item:potion", 2);
            Assert.AreEqual("item:potion", custom.SourceTag);
            Assert.AreEqual("item:potion#1", custom.ModifierSourceTag);
            Assert.AreEqual(2, custom.Level);
        }

        // ── 请求 ──
        [Test]
        public void ApplyRequest_ResolveSourceTag_DefaultsToEffectId()
        {
            var def = new EffectDefinition("heal");
            Assert.AreEqual("effect:heal", new EffectApplyRequest(def).ResolveSourceTag());
            Assert.AreEqual("effect:heal", new EffectApplyRequest("heal").ResolveSourceTag());
            Assert.AreEqual("item:potion", new EffectApplyRequest(def) { SourceTag = "item:potion" }.ResolveSourceTag());
            Assert.AreEqual("heal", new EffectApplyRequest(def).ResolvedEffectId);
            var r = new EffectApplyRequest("x", 3).WithSetByCaller("", 1f).WithSetByCaller("k", 2f);
            Assert.AreEqual(3, r.Level);
            Assert.AreEqual(1, r.SetByCaller.Count);
        }

        // ── 上下文 ──
        [Test]
        public void EffectContext_ServiceBag_And_SubjectWrapper()
        {
            var ctx = new EffectContext { Subject = "hero" };
            var src = new DictSource();
            ctx.RegisterService<IEffectDefinitionSource>(src);
            Assert.AreSame(src, ctx.GetService<IEffectDefinitionSource>());
            Assert.IsNull(ctx.GetService<IEffectAttributeSource>());
            Assert.IsTrue(ctx is IEffectContext);

            var wrapped = new SubjectEffectContext(ctx, "other");
            Assert.AreEqual("other", wrapped.Subject);
            Assert.AreSame(src, wrapped.GetService<IEffectDefinitionSource>());
            Assert.AreEqual("hero", ctx.Subject);
            Assert.IsNull(new SubjectEffectContext(null, "x").GetService<IEffectDefinitionSource>());
        }
    }
}
