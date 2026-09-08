using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ale.Condition;
using Ale.Condition.Serialization;
using Ale.Toolkit.Runtime;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 条件库数据层的门槛：条目 / 模板的归一与深拷贝、按模板 schema 对账自定义属性、校验各分支、
    /// JSON 与二进制往返、数据管理器的登记·跨库索引·幂等·清空，以及 <see cref="ConditionResolver"/> 的三级解析与 fail-closed。
    /// </summary>
    public class ConditionDatabaseTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _warnedIds = new List<string>();

        private ConditionDatabase NewDb(string name)
        {
            var db = ScriptableObject.CreateInstance<ConditionDatabase>();
            db.name = name;
            _created.Add(db);
            return db;
        }

        /// <summary>造一条「持有标记 flag」的表达式。</summary>
        private static ConditionExpression FlagExpr(string flag)
        {
            var expr = new ConditionExpression();
            var group = new ConditionGroup();
            var item = new ConditionItem("Condition.HasFlag");
            var p = new ConditionParam("flag", ConditionParamType.String);
            p.SetString(flag);
            item.parameters.Add(p);
            group.items.Add(item);
            expr.groups.Add(group);
            return expr;
        }

        private static ConditionEntry NewEntry(string id, string flag, string templateRef = null)
        {
            var e = new ConditionEntry(id, templateRef) { expression = FlagExpr(flag) };
            e.displayText.SetTextValue(0, "名-" + id);
            return e;
        }

        /// <summary>最简上下文：可挂一个具名条件源与一个标记源。</summary>
        private sealed class Ctx : IConditionContext
        {
            public object Subject { get; set; }
            public IConditionDefinitionSource Definitions;
            public IConditionFlagSource Flags;

            public T GetService<T>() where T : class
            {
                if (typeof(T) == typeof(IConditionDefinitionSource)) return Definitions as T;
                if (typeof(T) == typeof(IConditionFlagSource))       return Flags as T;
                return null;
            }
        }

        private sealed class FlagSource : IConditionFlagSource
        {
            private readonly HashSet<string> _flags;
            public FlagSource(params string[] flags) => _flags = new HashSet<string>(flags);
            public bool HasFlag(string flag) => _flags.Contains(flag);
        }

        /// <summary>固定返回一条表达式的具名条件源。</summary>
        private sealed class StubSource : IConditionDefinitionSource
        {
            private readonly string _id;
            private readonly ConditionExpression _expr;
            public StubSource(string id, ConditionExpression expr) { _id = id; _expr = expr; }
            public ConditionExpression GetCondition(string id) => id == _id ? _expr : null;
        }

        [SetUp]
        public void Setup()
        {
            ConditionDefinitionRegistry.Default.Clear();
            ConditionDataManager.Instance.ClearDatabases();
            ConditionResolver.MissingIdWarning = id => _warnedIds.Add(id);
            ConditionRegistry.Default.EnsureAutoRegistered();
        }

        [TearDown]
        public void Cleanup()
        {
            ConditionResolver.MissingIdWarning = null;
            _warnedIds.Clear();
            ConditionDataManager.Instance.ClearDatabases();
            ConditionDefinitionRegistry.Default.Clear();
            foreach (var o in _created) if (o) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ── 归一 / 拷贝 ───────────────────────────────────────────────────────────

        [Test]
        public void Normalize_FixesFieldTypes_AndDropsNullGroupsAndItems()
        {
            var e = new ConditionEntry("c1")
            {
                displayText     = new AttributeValue(EFieldType.Int),   // 类型不对
                descriptionText = null,                                  // 缺失
                iconValue       = null,
                expression      = null,
            };
            e.Normalize();

            Assert.AreEqual(EFieldType.Text,   e.displayText.Type,     "显示名归一为 Text");
            Assert.AreEqual(EFieldType.Text,   e.descriptionText.Type, "描述补齐为 Text");
            Assert.AreEqual(EFieldType.Sprite, e.iconValue.Type,       "图标补齐为 Sprite");
            Assert.IsNotNull(e.expression, "表达式补齐");

            var e2 = new ConditionEntry("c2") { expression = FlagExpr("f") };
            e2.expression.groups.Add(null);
            e2.expression.groups[0].items.Add(null);
            e2.Normalize();
            Assert.AreEqual(1, e2.expression.groups.Count, "null 组被剔除");
            Assert.AreEqual(1, e2.expression.groups[0].items.Count, "null 项被剔除");
        }

        [Test]
        public void Clone_IsDeep_ForEntryAndTemplate()
        {
            var e = NewEntry("c1", "flagA");
            var ec = e.Clone();
            ec.expression.groups[0].items[0].parameters[0].SetString("flagB");
            ec.displayText.SetTextValue(0, "改了");
            Assert.AreEqual("flagA", e.expression.groups[0].items[0].parameters[0].GetString(), "条目表达式是深拷贝");
            Assert.AreEqual("名-c1", e.displayText.GetTextValue(), "条目显示名是深拷贝");

            var t = new ConditionTemplate("模板") { defaultExpression = FlagExpr("flagA") };
            t.attributes.Add(new AttributeDefinition("power", EFieldType.Int));
            var tc = t.Clone();
            tc.defaultExpression.groups[0].items[0].parameters[0].SetString("flagB");
            tc.attributes[0].id = "changed";
            Assert.AreEqual("flagA", t.defaultExpression.groups[0].items[0].parameters[0].GetString(), "模板表达式是深拷贝");
            Assert.AreEqual("power", t.attributes[0].id, "模板 schema 是深拷贝");
        }

        [Test]
        public void RebuildAttributes_SyncsWithTemplateSchema_AndClearsWhenTemplateMissing()
        {
            var db = NewDb("A");
            var tmpl = new ConditionTemplate("通用");
            tmpl.attributes.Add(new AttributeDefinition("power", EFieldType.Int));
            tmpl.attributes.Add(new AttributeDefinition("hint",  EFieldType.String));
            db.ConditionTemplates.Add(tmpl);

            var e = NewEntry("c1", "f", "通用");
            db.Conditions.Add(e);
            db.RebuildAllAttributes();

            Assert.AreEqual(2, e.values.Count, "按 schema 建立两个属性值");
            Assert.IsNotNull(e.GetAttributeValue("power"));
            Assert.IsNotNull(e.GetAttributeValue("hint"));

            tmpl.attributes.RemoveAt(1);
            db.RebuildAllAttributes();
            Assert.AreEqual(1, e.values.Count, "schema 去掉的字段被移除");

            e.templateRef = "不存在";
            db.RebuildAllAttributes();
            Assert.AreEqual(0, e.values.Count, "模板缺失时清空");
        }

        // ── 校验 ──────────────────────────────────────────────────────────────────

        [Test]
        public void Validate_ReportsDuplicates_DanglingTemplate_NullExpression_AndEmptyItemKey()
        {
            var db = NewDb("A");
            db.ConditionTemplates.Add(new ConditionTemplate("dup"));
            db.ConditionTemplates.Add(new ConditionTemplate("dup"));
            db.AddEnumType("e1");
            db.EnumTypesList.Add(new EnumType("e1"));

            db.Conditions.Add(NewEntry("same", "f"));
            db.Conditions.Add(NewEntry("same", "f"));
            db.Conditions.Add(NewEntry("dangling", "f", "查无此模板"));
            db.Conditions.Add(new ConditionEntry("nullExpr") { expression = null });

            var halfConfigured = NewEntry("half", "f");
            halfConfigured.expression.groups[0].items.Add(new ConditionItem(string.Empty));
            db.Conditions.Add(halfConfigured);

            Assert.IsFalse(db.Validate(out var errors));
            string all = string.Join("\n", errors);
            StringAssert.Contains("条件模板 name", all, "模板名重复");
            StringAssert.Contains("枚举类型 name", all, "枚举名重复");
            StringAssert.Contains("条件 id", all, "条件 id 重复");
            StringAssert.Contains("查无此模板", all, "悬空模板引用");
            StringAssert.Contains("nullExpr", all, "表达式为空");
            StringAssert.Contains("未选择判定器", all, "空键条件项");

            var clean = NewDb("B");
            clean.Conditions.Add(NewEntry("ok", "f"));
            Assert.IsTrue(clean.Validate(out var none), "干净的库无错误");
            Assert.AreEqual(0, none.Count);
        }

        // ── 序列化 ────────────────────────────────────────────────────────────────

        private ConditionDatabase BuildSample()
        {
            var db = NewDb("Sample");
            db.AddEnumType("阵营", "中立", "友方");
            var tmpl = new ConditionTemplate("通用") { defaultExpression = FlagExpr("默认") };
            tmpl.attributes.Add(new AttributeDefinition("power", EFieldType.Int));
            db.ConditionTemplates.Add(tmpl);
            var e = NewEntry("c1", "flagA", "通用");
            e.descriptionText.SetTextValue(0, "需要持有 flagA");
            db.Conditions.Add(e);
            db.RebuildAllAttributes();
            return db;
        }

        private void AssertSample(ConditionDatabase db, string what)
        {
            Assert.AreEqual(1, db.EnumTypesList.Count, what + "：枚举类型");
            Assert.AreEqual(2, db.GetEnumType("阵营").items.Count, what + "：枚举项");
            Assert.AreEqual(1, db.ConditionTemplates.Count, what + "：模板");
            Assert.AreEqual("默认", db.GetTemplate("通用").defaultExpression.groups[0].items[0].parameters[0].GetString(),
                what + "：模板默认表达式");
            Assert.AreEqual(1, db.ConditionTemplates[0].attributes.Count, what + "：模板 schema");

            var e = db.GetEntry("c1");
            Assert.IsNotNull(e, what + "：条目");
            Assert.AreEqual("通用", e.templateRef, what + "：模板引用");
            Assert.AreEqual("名-c1", e.PlainName(), what + "：显示名");
            Assert.AreEqual("需要持有 flagA", e.descriptionText.GetTextValue(), what + "：描述");
            Assert.AreEqual(1, e.values.Count, what + "：自定义属性值");
            Assert.AreEqual("Condition.HasFlag", e.expression.groups[0].items[0].key, what + "：判定器键");
            Assert.AreEqual("flagA", e.expression.groups[0].items[0].parameters[0].GetString(), what + "：参数值");
        }

        [Test]
        public void Json_RoundTrip_PreservesEnumsTemplatesAndEntries()
        {
            string json = ConditionConfigSerializer.ExportJson(BuildSample());
            var back = ConditionConfigSerializer.ImportJson(json);
            _created.Add(back);
            AssertSample(back, "JSON 往返");
        }

        [Test]
        public void Binary_RoundTrip_PreservesEnumsTemplatesAndEntries()
        {
            byte[] bytes = ConditionConfigSerializer.Export(BuildSample());
            var back = ConditionConfigSerializer.Import(bytes);
            _created.Add(back);
            AssertSample(back, "二进制往返");

            var untouched = NewDb("Untouched");
            untouched.Conditions.Add(NewEntry("keep", "f"));
            // 期望必须先于日志发生时登记，否则会被记成「未预期的错误日志」而使测试变红。
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("魔数不匹配"));
            ConditionConfigSerializer.ImportInto(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, untouched);
            Assert.AreEqual(1, untouched.Conditions.Count, "魔数不匹配时目标保持不变");
        }

        // ── 数据管理器 ────────────────────────────────────────────────────────────

        [Test]
        public void DataManager_Register_IsIdempotent_AndIndexesFirstWins()
        {
            var a = NewDb("A"); a.Conditions.Add(NewEntry("shared", "fromA")); a.Conditions.Add(NewEntry("onlyA", "f"));
            var b = NewDb("B"); b.Conditions.Add(NewEntry("shared", "fromB")); b.Conditions.Add(NewEntry("onlyB", "f"));

            var m = ConditionDataManager.Instance;
            m.Register(a);
            m.Register(a);   // 幂等
            m.Register(b);

            Assert.AreEqual(2, m.Databases.Count, "重复注册被忽略");
            Assert.AreEqual("fromA", m.GetEntry("shared").expression.groups[0].items[0].parameters[0].GetString(),
                "同 id 先注册者优先");
            Assert.IsNotNull(m.GetEntry("onlyB"), "跨库索引");
            Assert.AreEqual(3, m.GetAllConditions().Count, "全部条目按 id 去重");

            m.Unregister(b);
            Assert.IsNull(m.GetEntry("onlyB"), "注销后索引重建");
        }

        [Test]
        public void DataManager_RegistersItselfAsSource_AndClearRemovesIt()
        {
            var db = NewDb("A"); db.Conditions.Add(NewEntry("c1", "f"));
            var m = ConditionDataManager.Instance;

            Assert.AreEqual(0, ConditionDefinitionRegistry.Default.SourceCount, "注册前无来源");
            m.Register(db);
            Assert.AreEqual(1, ConditionDefinitionRegistry.Default.SourceCount, "注册后自动成为全局条件源");
            Assert.IsNotNull(ConditionDefinitionRegistry.Default.GetCondition("c1"), "可经全局注册表按 id 取到");

            m.ClearDatabases();
            Assert.AreEqual(0, ConditionDefinitionRegistry.Default.SourceCount, "清空后从全局条件源移除");
            Assert.IsNull(ConditionDefinitionRegistry.Default.GetCondition("c1"));
        }

        [Test]
        public void Registry_LocalRegistrationWinsOverSources_AndClearResetsBoth()
        {
            var reg = ConditionDefinitionRegistry.Default;
            reg.AddSource(new StubSource("c1", FlagExpr("fromSource")));
            reg.AddSource(null);                                    // 忽略
            reg.Register("c1", FlagExpr("fromLocal"));

            Assert.AreEqual(1, reg.SourceCount);
            Assert.AreEqual(1, reg.LocalCount);
            Assert.AreEqual("fromLocal", reg.GetCondition("c1").groups[0].items[0].parameters[0].GetString(),
                "直接登记优先于来源");

            Assert.IsTrue(reg.Unregister("c1"));
            Assert.AreEqual("fromSource", reg.GetCondition("c1").groups[0].items[0].parameters[0].GetString(),
                "注销后回落到来源");

            reg.Clear();
            Assert.AreEqual(0, reg.SourceCount);
            Assert.IsNull(reg.GetCondition("c1"));
        }

        // ── 解析与求值 ────────────────────────────────────────────────────────────

        [Test]
        public void Resolver_PrefersContextSource_ThenFallback_ThenGlobalRegistry()
        {
            ConditionDefinitionRegistry.Default.Register("c1", FlagExpr("fromGlobal"));
            var fallback = new StubSource("c1", FlagExpr("fromFallback"));
            var ctx = new Ctx { Definitions = new StubSource("c1", FlagExpr("fromCtx")) };

            Assert.AreEqual("fromCtx", ConditionResolver.Resolve("c1", ctx, fallback)
                .groups[0].items[0].parameters[0].GetString(), "上下文源最优先");

            ctx.Definitions = null;
            Assert.AreEqual("fromFallback", ConditionResolver.Resolve("c1", ctx, fallback)
                .groups[0].items[0].parameters[0].GetString(), "其次是显式回落源");

            Assert.AreEqual("fromGlobal", ConditionResolver.Resolve("c1", ctx)
                .groups[0].items[0].parameters[0].GetString(), "最后是全局注册表");

            Assert.IsNull(ConditionResolver.Resolve(null, ctx), "空 id 直接返回 null");
            Assert.IsNull(ConditionResolver.Resolve("查无此条件", ctx), "未找到返回 null 且不告警");
            Assert.AreEqual(0, _warnedIds.Count, "Resolve 本身不告警");
        }

        [Test]
        public void Resolver_EvaluatesThroughEngine_AndFailsClosedOnMissingId()
        {
            var db = NewDb("A");
            db.Conditions.Add(NewEntry("needs_flag", "hero"));
            ConditionDataManager.Instance.Register(db);

            var ctx = new Ctx { Flags = new FlagSource("hero") };
            Assert.IsTrue(ConditionResolver.IsSatisfied("needs_flag", ctx), "持有标记 → 通过");

            ctx.Flags = new FlagSource();
            Assert.IsFalse(ConditionResolver.IsSatisfied("needs_flag", ctx), "不持有标记 → 不通过");

            var missing = ConditionResolver.Evaluate("查无此条件", ctx);
            Assert.IsFalse(missing.Passed, "解析不到时 fail-closed");
            CollectionAssert.Contains(missing.FailedKeys, "查无此条件", "失败键里带上该 id");
            CollectionAssert.Contains(_warnedIds, "查无此条件", "经 MissingIdWarning 告警");
        }
    }
}
