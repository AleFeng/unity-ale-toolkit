using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Condition;
using Ale.Effect;
using Ale.Effect.Serialization;
using Ale.GameplayTags;
using Ale.Modifier;
using Ale.Toolkit.Runtime;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 效果库（<see cref="EffectDatabase"/>）门槛：条目归一与深拷贝、模板 schema 对账、模板深拷贝、校验矩阵（重复键 / 模板悬空 /
    /// 定义错误阻断而警告不阻断 / 非法标签名）、JSON 与二进制往返（枚举 / 模板 schema 与默认定义 / 条目显示字段 · 自定义属性 · 定义各节 / 标签）、
    /// 数据管理器（注册幂等、全局定义源、标签登记、注销 / 清空移除来源、先注册者优先）、引导开关默认值。
    /// </summary>
    public class EffectDatabaseTests
    {
        private readonly List<Object> _created = new List<Object>();

        private EffectDatabase NewDb()
        {
            var db = ScriptableObject.CreateInstance<EffectDatabase>();
            _created.Add(db);
            return db;
        }

        [TearDown]
        public void Cleanup()
        {
            EffectDataManager.Instance.ClearDatabases();
            GameplayTagRegistry.Default.Clear();
            foreach (var o in _created) if (o) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ── 构造 ────────────────────────────────────────────────────────────────────

        private static void AddFlagCondition(ConditionExpression expr, string flag)
        {
            var g  = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Condition.HasFlag");
            var p  = new ConditionParam("flag", ConditionParamType.String); p.SetString(flag);
            it.parameters.Add(p);
            g.items.Add(it);
            expr.groups.Add(g);
        }

        /// <summary>枚举「元素」；模板「增益」（schema：power Int / element Enum；默认定义 HasDuration 10 天 + 标签）；
        /// 效果 battle_focus（模板增益、显示名、值 power=3 element=1、定义含修饰器 / 叠加 / 标签 / 施加条件 / 执行）；效果 spark（无模板、瞬时）；两条标签。</summary>
        private EffectDatabase BuildValid()
        {
            var db = NewDb();
            db.AddEnumType("元素", "火", "冰");

            var buff = new EffectTemplate("增益") { color = Color.green };
            buff.attributes.Add(new AttributeDefinition("power", EFieldType.Int));
            buff.attributes.Add(new AttributeDefinition("element", EFieldType.Enum, false, "元素"));
            buff.defaultDefinition.durationPolicy = EDurationPolicy.HasDuration;
            buff.defaultDefinition.duration       = EffectMagnitude.Scalable(10f);
            buff.defaultDefinition.assetTags.AddTag("Status.Buff");
            db.EffectTemplates.Add(buff);

            var focus = new EffectEntry("battle_focus", EDurationPolicy.HasDuration, "增益");
            focus.displayText.SetTextValue(0, "战意");
            focus.descriptionText.SetTextValue(0, "战力 +10，持续 30 天");
            focus.definition.duration     = EffectMagnitude.Scalable(30f);
            focus.definition.stackingType = EEffectStackingType.AggregateByTarget;
            focus.definition.stackLimit   = 1;
            focus.definition.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
            focus.definition.assetTags.AddTag("Status.Buff.Might");
            focus.definition.grantedTags.AddTag("Status.Buff");
            AddFlagCondition(focus.definition.applicationCondition, "awake");
            var grp = new EffectGroup(EffectPhases.OnApply);
            var item = new EffectItem("Effect.NoOp");
            var ip = new EffectParam("note", EffectParamType.String); ip.SetString("hi");
            item.parameters.Add(ip);
            AddFlagCondition(item.gate, "gate");
            grp.items.Add(item);
            focus.definition.executions.groups.Add(grp);
            focus.RebuildAttributes(db);
            focus.SetAttributeValue("power", 3);
            focus.SetAttributeValue("element", 1);
            db.Effects.Add(focus);

            db.Effects.Add(new EffectEntry("spark"));

            db.GameplayTags.Add(new GameplayTagDefinition("Status.Buff.Might", "战意类增益"));
            db.GameplayTags.Add(new GameplayTagDefinition("Immunity.Mental"));
            db.NormalizeAll();
            return db;
        }

        private static bool Any(List<string> errors, string fragment)
        {
            foreach (var e in errors) if (e != null && e.Contains(fragment)) return true;
            return false;
        }

        // ── 条目 / 模板 ────────────────────────────────────────────────────────────

        [Test]
        public void Entry_Normalize_SyncsIdentity_FixesTypes_AndClones()
        {
            var e = new EffectEntry { id = "x", definition = null, displayText = new AttributeValue(EFieldType.Int), values = null };
            e.Normalize();
            Assert.IsNotNull(e.definition);
            Assert.AreEqual("x", e.definition.id);
            Assert.AreEqual(EFieldType.Text, e.displayText.Type);
            Assert.AreEqual("x", e.PlainName(), "无名回退 id");
            Assert.AreEqual("x", e.definition.displayName);
            Assert.IsNotNull(e.values);

            e.displayText.SetTextValue(0, "名");
            e.Normalize();
            Assert.AreEqual("名", e.definition.displayName);

            var c = e.Clone();
            Assert.AreNotSame(e.definition, c.definition);
            Assert.AreEqual("x", c.definition.id);
            Assert.AreEqual("名", c.PlainName());
        }

        [Test]
        public void Entry_RebuildAttributes_FollowsTemplateSchema()
        {
            var db = BuildValid();
            var focus = db.GetEffect("battle_focus");
            Assert.AreEqual(2, focus.values.Count);
            Assert.AreEqual(3, focus.GetAttributeValue<int>("power"));
            Assert.AreEqual(1, focus.GetAttributeValue<int>("element"));

            // schema 变化：删 element、加 note → 对账后保留 power 值
            var tmpl = db.GetTemplate("增益");
            tmpl.attributes.RemoveAt(1);
            tmpl.attributes.Add(new AttributeDefinition("note", EFieldType.String));
            focus.RebuildAttributes(db);
            Assert.AreEqual(2, focus.values.Count);
            Assert.AreEqual(3, focus.GetAttributeValue<int>("power"));
            Assert.IsNull(focus.GetAttributeValue("element"));
            Assert.IsNotNull(focus.GetAttributeValue("note"));

            // 模板缺失 → 清空
            focus.templateRef = "nope";
            focus.RebuildAttributes(db);
            Assert.AreEqual(0, focus.values.Count);
        }

        [Test]
        public void Template_Clone_DeepCopies_SchemaAndDefaultDefinition()
        {
            var db = BuildValid();
            var tmpl = db.GetTemplate("增益");
            var c = tmpl.Clone();
            Assert.AreEqual("增益", c.name);
            Assert.AreEqual(2, c.attributes.Count);
            Assert.AreNotSame(tmpl.attributes[0], c.attributes[0]);
            Assert.AreNotSame(tmpl.defaultDefinition, c.defaultDefinition);
            Assert.AreEqual(EDurationPolicy.HasDuration, c.defaultDefinition.durationPolicy);
            Assert.AreEqual(10f, c.defaultDefinition.duration.baseValue);
            Assert.IsTrue(c.defaultDefinition.assetTags.HasTagExact(GameplayTag.Parse("Status.Buff")));
        }

        // ── 校验 ────────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_Valid_Passes()
        {
            var db = BuildValid();
            Assert.IsTrue(db.Validate(out var errors), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DuplicateKeys_Fail()
        {
            var db = BuildValid();
            db.Effects.Add(new EffectEntry("spark"));
            db.EffectTemplates.Add(new EffectTemplate("增益"));
            db.EnumTypesList.Add(new EnumType("元素"));
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "效果 id"), string.Join("\n", errors));
            Assert.IsTrue(Any(errors, "效果模板 name"), string.Join("\n", errors));
            Assert.IsTrue(Any(errors, "枚举类型 name"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DanglingTemplate_Fails()
        {
            var db = BuildValid();
            db.GetEffect("battle_focus").templateRef = "ghost";
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "templateRef → 效果模板 'ghost'"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DefinitionError_Fails_ButWarningDoesNot()
        {
            var db = BuildValid();
            db.GetEffect("spark").definition.grantedTags.AddTag("Status.X");   // 瞬时效果配 grantedTags → 警告
            Assert.IsTrue(db.Validate(out var errors), string.Join("\n", errors));

            db.GetEffect("battle_focus").definition.duration = EffectMagnitude.Scalable(0f);   // HasDuration 时长 0 → 错误
            Assert.IsFalse(db.Validate(out errors));
            Assert.IsTrue(Any(errors, "battle_focus") && Any(errors, "duration"), string.Join("\n", errors));

            db.GetEffect("battle_focus").definition = null;
            Assert.IsFalse(db.Validate(out errors));
            Assert.IsTrue(Any(errors, "定义为空"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_InvalidTagName_Fails()
        {
            var db = BuildValid();
            db.GameplayTags.Add(new GameplayTagDefinition("Bad..Name"));
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "Gameplay 标签"), string.Join("\n", errors));
        }

        // ── 序列化 ──────────────────────────────────────────────────────────────────

        private void AssertRoundTrip(EffectDatabase dst)
        {
            Assert.AreEqual(1, dst.EnumTypesList.Count);
            Assert.AreEqual("冰", dst.GetEnumType("元素").GetDisplayName(1));

            var tmpl = dst.GetTemplate("增益");
            Assert.IsNotNull(tmpl);
            Assert.AreEqual(2, tmpl.attributes.Count);
            Assert.AreEqual(EFieldType.Enum, tmpl.attributes[1].type);
            Assert.AreEqual("元素", tmpl.attributes[1].enumTypeRef);
            Assert.AreEqual(EDurationPolicy.HasDuration, tmpl.defaultDefinition.durationPolicy);
            Assert.AreEqual(10f, tmpl.defaultDefinition.duration.baseValue);
            Assert.IsTrue(tmpl.defaultDefinition.assetTags.HasTagExact(GameplayTag.Parse("Status.Buff")));
            Assert.AreEqual(Color.green, tmpl.color);

            Assert.AreEqual(2, dst.Effects.Count);
            var focus = dst.GetEffect("battle_focus");
            Assert.IsNotNull(focus);
            Assert.AreEqual("增益", focus.templateRef);
            Assert.AreEqual("战意", focus.displayText.GetTextValue(0));
            Assert.AreEqual("战力 +10，持续 30 天", focus.descriptionText.GetTextValue(0));
            Assert.AreEqual(EFieldType.Sprite, focus.iconValue.Type);
            Assert.AreEqual(3, focus.GetAttributeValue<int>("power"));
            Assert.AreEqual(1, focus.GetAttributeValue<int>("element"));
            var d = focus.definition;
            Assert.AreEqual("battle_focus", d.id);
            Assert.AreEqual("战意", d.displayName);
            Assert.AreEqual(30f, d.duration.baseValue);
            Assert.AreEqual(EEffectStackingType.AggregateByTarget, d.stackingType);
            Assert.AreEqual("might", d.modifiers[0].attributeId);
            Assert.AreEqual(10f, d.modifiers[0].magnitude.baseValue);
            Assert.IsTrue(d.assetTags.HasTagExact(GameplayTag.Parse("Status.Buff.Might")));
            Assert.IsTrue(d.grantedTags.HasTagExact(GameplayTag.Parse("Status.Buff")));
            Assert.AreEqual("Condition.HasFlag", d.applicationCondition.groups[0].items[0].key);
            Assert.AreEqual("awake", d.applicationCondition.groups[0].items[0].parameters[0].GetString());
            Assert.AreEqual(EffectPhases.OnApply, d.executions.groups[0].phase);
            var ei = d.executions.groups[0].items[0];
            Assert.AreEqual("Effect.NoOp", ei.key);
            Assert.AreEqual("hi", ei.parameters[0].GetString());
            Assert.AreEqual("gate", ei.gate.groups[0].items[0].parameters[0].GetString());

            Assert.AreEqual(EDurationPolicy.Instant, dst.GetEffect("spark").definition.durationPolicy);
            Assert.AreEqual(2, dst.GameplayTags.Count);
            Assert.AreEqual("战意类增益", dst.GameplayTags[0].comment);
            Assert.IsTrue(dst.Validate(out var errors), string.Join("\n", errors));
        }

        [Test]
        public void Serializer_Json_RoundTrip()
        {
            var src  = BuildValid();
            string json = EffectConfigSerializer.ExportJson(src);
            Assert.IsTrue(json.Contains("\"battle_focus\""));
            Assert.IsTrue(json.Contains("\"durationPolicy\""), "定义直接内嵌，不是转义串");
            var dst = EffectConfigSerializer.ImportJson(json);
            _created.Add(dst);
            AssertRoundTrip(dst);
        }

        [Test]
        public void Serializer_Binary_RoundTrip()
        {
            var src   = BuildValid();
            var bytes = EffectConfigSerializer.Export(src);
            var dst   = EffectConfigSerializer.Import(bytes);
            _created.Add(dst);
            AssertRoundTrip(dst);
        }

        [Test]
        public void Serializer_ImportInto_ClearsTarget()
        {
            var src = BuildValid();
            var dst = NewDb();
            dst.Effects.Add(new EffectEntry("stale"));
            EffectConfigSerializer.ImportInto(EffectConfigSerializer.Export(src), dst);
            Assert.IsNull(dst.GetEffect("stale"));
            Assert.IsNotNull(dst.GetEffect("battle_focus"));
        }

        // ── 数据管理器 ──────────────────────────────────────────────────────────────

        [Test]
        public void DataManager_Register_IndexesAndActsAsGlobalSource()
        {
            var db  = BuildValid();
            var mgr = EffectDataManager.Instance;
            int sourcesBefore = EffectDefinitionRegistry.Default.SourceCount;

            mgr.Register(db);
            mgr.Register(db);   // 幂等
            Assert.AreEqual(1, mgr.Databases.Count);
            Assert.AreEqual(sourcesBefore + 1, EffectDefinitionRegistry.Default.SourceCount);

            Assert.AreSame(db.GetEffect("battle_focus"), mgr.GetEffect("battle_focus"));
            Assert.AreSame(db.GetTemplate("增益"), mgr.GetTemplate("增益"));
            Assert.AreSame(db.GetEnumType("元素"), mgr.GetEnumType("元素"));
            Assert.AreEqual(1, mgr.EnumTypes.Count);
            Assert.AreEqual(2, mgr.GetAllEffects().Count);
            Assert.IsNull(mgr.GetEffect("nope"));

            IEffectDefinitionSource src = mgr;
            Assert.AreSame(db.GetEffect("spark").definition, src.GetEffect("spark"));
            Assert.AreSame(db.GetEffect("spark").definition, EffectDefinitionRegistry.Default.GetEffect("spark"));
            Assert.AreEqual("spark", EffectDefinitionRegistry.Default.GetEffect("spark").id, "注册时已归一同步 id");

            Assert.IsTrue(GameplayTagRegistry.Default.IsRegistered(GameplayTag.Parse("Status.Buff.Might")));
            Assert.IsTrue(GameplayTagRegistry.Default.IsRegistered(GameplayTag.Parse("Immunity")));

            mgr.Unregister(db);
            Assert.AreEqual(sourcesBefore, EffectDefinitionRegistry.Default.SourceCount, "最后一个注销 → 移除来源");
            Assert.IsNull(EffectDefinitionRegistry.Default.GetEffect("spark"));
            Assert.IsNull(mgr.GetEffect("spark"));
        }

        [Test]
        public void DataManager_FirstRegisteredWins_AndClearRemovesSource()
        {
            var a = NewDb(); a.Effects.Add(new EffectEntry("dup") { displayText = Text("A") });
            var b = NewDb(); b.Effects.Add(new EffectEntry("dup") { displayText = Text("B") });
            var mgr = EffectDataManager.Instance;
            int sourcesBefore = EffectDefinitionRegistry.Default.SourceCount;

            mgr.Register(a);
            mgr.Register(b);
            Assert.AreEqual("A", mgr.GetEffect("dup").PlainName());
            Assert.AreEqual(sourcesBefore + 1, EffectDefinitionRegistry.Default.SourceCount, "多库只登记一次来源");

            mgr.ClearDatabases();
            Assert.AreEqual(0, mgr.Databases.Count);
            Assert.AreEqual(sourcesBefore, EffectDefinitionRegistry.Default.SourceCount);
            Assert.IsNull(mgr.GetEffect("dup"));
        }

        [Test]
        public void DataManager_LoadFromBinary_RegistersImportedDb()
        {
            var src = BuildValid();
            var mgr = EffectDataManager.Instance;
            var db  = mgr.LoadFromBinary(EffectConfigSerializer.Export(src));
            _created.Add(db);
            Assert.AreEqual(1, mgr.Databases.Count);
            Assert.AreEqual("战意", mgr.GetEffect("battle_focus").PlainName());
            Assert.IsNotNull(EffectDefinitionRegistry.Default.GetEffect("battle_focus"));
        }

        [Test]
        public void Runtime_AutoLoadFlags_DefaultTrue()
        {
            Assert.IsTrue(EffectRuntime.AutoLoadFromResources);
            Assert.IsTrue(GameplayTagRuntime.AutoLoadFromResources);
        }

        private static AttributeValue Text(string s)
        {
            var v = new AttributeValue(EFieldType.Text);
            v.SetTextValue(0, s);
            return v;
        }
    }
}
