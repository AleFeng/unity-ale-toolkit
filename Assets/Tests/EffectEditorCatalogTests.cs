using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Effect;
using Ale.Effect.Editor;

namespace Ale.Toolkit.Tests
{
    /// <summary>效果编辑器目录与属性 id provider 的门槛：索引构建（同 id 先到者优先、空 id 跳过）、显示文本、provider 注册 / 汇总 / 注销。</summary>
    public class EffectEditorCatalogTests
    {
        private sealed class Provider : IEffectAttributeCatalogProvider
        {
            private readonly List<(string id, string display)> _attrs;
            public Provider(string system, params (string id, string display)[] attrs) { SystemName = system; _attrs = new List<(string, string)>(attrs); }
            public string SystemName { get; }
            public IEnumerable<(string id, string display)> GetAttributes() => _attrs;
        }

        private readonly List<Object> _created = new List<Object>();

        private EffectDatabase NewDb(string name, params string[] ids)
        {
            var db = ScriptableObject.CreateInstance<EffectDatabase>();
            db.name = name;
            foreach (var id in ids)
            {
                var e = new EffectEntry(id);
                e.displayText.SetTextValue(0, name + ":" + id);
                db.Effects.Add(e);
            }
            _created.Add(db);
            return db;
        }

        [TearDown]
        public void Cleanup()
        {
            EffectDefinitionDrawerHooks.ClearAttributeProviders();
            foreach (var o in _created) if (o) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void BuildIndex_FirstDatabaseWins_SkipsEmptyIds()
        {
            var a = NewDb("A", "regen", "focus");
            var b = NewDb("B", "regen", "");
            var all  = new List<EffectEditorCatalog.Hit>();
            var byId = new Dictionary<string, EffectEditorCatalog.Hit>();

            EffectEditorCatalog.BuildIndex(new[] { a, b }, all, byId);

            Assert.AreEqual(3, all.Count, "空 id 跳过");
            Assert.AreSame(a, byId["regen"].Database);
            Assert.AreSame(a.GetEffect("focus"), byId["focus"].Entry);
            Assert.IsFalse(byId.ContainsKey(""));
            Assert.AreEqual("A:regen (regen)", EffectEditorCatalog.LabelOf(byId["regen"].Entry));
            Assert.AreEqual("plain", EffectEditorCatalog.LabelOf(new EffectEntry("plain")), "无显示名只显示 id");
        }

        [Test]
        public void AttributeProviders_Register_Aggregate_Unregister()
        {
            var chronicle = new Provider("Chronicle", ("might", "战力"), ("stamina", "耐力"));
            var other     = new Provider("Other", ("might", "Might (dup)"), ("mana", "Mana"));

            EffectDefinitionDrawerHooks.RegisterAttributeProvider(chronicle);
            EffectDefinitionDrawerHooks.RegisterAttributeProvider(chronicle);   // 幂等
            EffectDefinitionDrawerHooks.RegisterAttributeProvider(other);
            Assert.AreEqual(2, EffectDefinitionDrawerHooks.AttributeProviders.Count);

            var cands = EffectDefinitionDrawerHooks.CollectAttributeCandidates();
            Assert.AreEqual(3, cands.Count, "同 id 去重，先注册者优先");
            Assert.AreEqual("might", cands[0].id);
            Assert.AreEqual("Chronicle", cands[0].system);
            Assert.AreEqual("mana", cands[2].id);

            Assert.IsTrue(EffectDefinitionDrawerHooks.UnregisterAttributeProvider(chronicle));
            Assert.IsFalse(EffectDefinitionDrawerHooks.UnregisterAttributeProvider(chronicle), "重复注销返回 false");
            Assert.AreEqual(2, EffectDefinitionDrawerHooks.CollectAttributeCandidates().Count);
            Assert.AreEqual("Other", EffectDefinitionDrawerHooks.CollectAttributeCandidates()[0].system);
        }
    }
}
