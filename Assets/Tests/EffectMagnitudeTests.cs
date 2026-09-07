using System.Collections.Generic;
using NUnit.Framework;
using Ale.Effect;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// <see cref="EffectMagnitude"/> 的门槛：Scalable 等级缩放与下限、SetByCaller 取值与回退、AttributeBased 公式与来源 / 目标捕获、
    /// 读不到属性的失败语义、校验、工厂与克隆。
    /// </summary>
    public class EffectMagnitudeTests
    {
        private sealed class AttrSource : IEffectAttributeSource
        {
            public readonly Dictionary<(object, string), float> Values = new Dictionary<(object, string), float>();
            public bool TryGetAttribute(object subject, string attributeId, out float value)
            {
                if (subject != null && Values.TryGetValue((subject, attributeId), out value)) return true;
                value = 0f;
                return false;
            }
        }

        private static EffectContext Ctx(IEffectAttributeSource src = null)
        {
            var ctx = new EffectContext();
            if (src != null) ctx.RegisterService<IEffectAttributeSource>(src);
            return ctx;
        }

        [Test]
        public void Scalable_LevelScaling_LevelBelowOneClampsToOne()
        {
            var m = EffectMagnitude.Scalable(10f, 2f);
            Assert.AreEqual(10f, m.Evaluate(new EffectApplyRequest("x", 1), Ctx()), 1e-6f);
            Assert.AreEqual(14f, m.Evaluate(new EffectApplyRequest("x", 3), Ctx()), 1e-6f);
            Assert.AreEqual(10f, m.Evaluate(new EffectApplyRequest("x", 0), Ctx()), 1e-6f);
            Assert.AreEqual(10f, m.Evaluate(new EffectApplyRequest("x", -5), Ctx()), 1e-6f);
            Assert.AreEqual(10f, m.Evaluate(null, null), 1e-6f);   // 无请求 / 无上下文亦可求值
            Assert.IsTrue(m.TryEvaluate(null, null, out _));
            Assert.IsFalse(m.IsZeroScalable);
            Assert.IsTrue(EffectMagnitude.Scalable(0f).IsZeroScalable);
            Assert.IsFalse(EffectMagnitude.Scalable(0f, 1f).IsZeroScalable);
        }

        [Test]
        public void SetByCaller_UsesRequestValue_FallsBackToBase()
        {
            var m = EffectMagnitude.SetByCaller("dmg", 3f);
            var withKey = new EffectApplyRequest("x").WithSetByCaller("dmg", 7f);
            Assert.AreEqual(7f, m.Evaluate(withKey, Ctx()), 1e-6f);
            Assert.AreEqual(3f, m.Evaluate(new EffectApplyRequest("x"), Ctx()), 1e-6f);   // 缺键回退
            Assert.AreEqual(3f, m.Evaluate(null, null), 1e-6f);
            Assert.IsTrue(m.TryEvaluate(new EffectApplyRequest("x"), Ctx(), out float v) && v == 3f);
            Assert.AreEqual(1, withKey.SetByCaller.Count);
        }

        [Test]
        public void AttributeBased_Formula_And_CaptureFromSourceVsTarget()
        {
            var src = new AttrSource();
            src.Values[("src", "might")] = 20f;
            src.Values[("tgt", "might")] = 5f;
            var req = new EffectApplyRequest("x") { Source = "src", Target = "tgt" };

            var fromTarget = EffectMagnitude.AttributeBased("might", 2f, 1f, 3f);
            Assert.AreEqual(2f * (5f + 1f) + 3f, fromTarget.Evaluate(req, Ctx(src)), 1e-6f);

            var fromSource = EffectMagnitude.AttributeBased("might", 2f, 1f, 3f, EMagnitudeCaptureFrom.Source);
            Assert.AreEqual(2f * (20f + 1f) + 3f, fromSource.Evaluate(req, Ctx(src)), 1e-6f);
        }

        [Test]
        public void AttributeBased_MissingAttributeOrService_TryEvaluateFalse()
        {
            var m = EffectMagnitude.AttributeBased("might");
            var req = new EffectApplyRequest("x") { Target = "tgt" };
            Assert.IsFalse(m.TryEvaluate(req, Ctx(), out _));                 // 无服务
            Assert.IsFalse(m.TryEvaluate(req, null, out _));                  // 无上下文
            var src = new AttrSource();
            Assert.IsFalse(m.TryEvaluate(req, Ctx(src), out _));              // 有服务但无该属性
            Assert.AreEqual(-1f, m.Evaluate(req, Ctx(src), -1f), 1e-6f);      // 失败回退
            src.Values[("tgt", "might")] = 4f;
            Assert.IsTrue(m.TryEvaluate(req, Ctx(src), out float v) && v == 4f);
            Assert.IsFalse(EffectMagnitude.AttributeBased("").TryEvaluate(req, Ctx(src), out _));   // 未配 attributeId
        }

        [Test]
        public void Validate_Errors_ForMissingAttributeIdOrKey()
        {
            var errors = new List<string>();
            Assert.IsFalse(EffectMagnitude.AttributeBased(" ").Validate(errors, "m"));
            Assert.IsFalse(EffectMagnitude.SetByCaller(null).Validate(errors, "m"));
            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("attributeId", errors[0]);
            StringAssert.Contains("setByCallerKey", errors[1]);
            errors.Clear();
            Assert.IsTrue(EffectMagnitude.Scalable(1f).Validate(errors, "m"));
            Assert.IsTrue(EffectMagnitude.AttributeBased("a").Validate(errors, "m"));
            Assert.IsTrue(EffectMagnitude.SetByCaller("k").Validate(errors, "m"));
            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Factories_And_Clone_Independent()
        {
            var a = EffectMagnitude.AttributeBased("hp", 0.5f, 1f, 2f, EMagnitudeCaptureFrom.Source);
            Assert.AreEqual(EMagnitudeKind.AttributeBased, a.kind);
            Assert.AreEqual("hp", a.attributeId);
            Assert.AreEqual(0.5f, a.coefficient);
            Assert.AreEqual(1f, a.preAdd);
            Assert.AreEqual(2f, a.postAdd);
            Assert.AreEqual(EMagnitudeCaptureFrom.Source, a.captureFrom);

            var c = a.Clone();
            c.attributeId = "mp";
            c.coefficient = 9f;
            Assert.AreEqual("hp", a.attributeId);
            Assert.AreEqual(0.5f, a.coefficient);

            var s = EffectMagnitude.SetByCaller("k", 2f);
            Assert.AreEqual(EMagnitudeKind.SetByCaller, s.kind);
            Assert.AreEqual("k", s.setByCallerKey);
            Assert.AreEqual(2f, s.baseValue);
        }
    }
}
