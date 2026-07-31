using System.Collections.Generic;
using NUnit.Framework;
using Ale.Toolkit.Runtime;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// <see cref="ModifierStackEvaluator"/> 求值门槛：结算顺序（Add → PercentAdd → Multiply → Override）、
    /// clamp、来源明细、空集合 / null 元素、Override 语义。
    /// <para>本测试仅存在于 toolkit 开发工程的 <c>Assets/</c> 下，不随 <c>com.ale.toolkit</c> 包分发。</para>
    /// </summary>
    public class ModifierStackEvaluatorTests
    {
        private const float Inf  = float.PositiveInfinity;
        private const float NInf = float.NegativeInfinity;

        private static ModifierDefinition Mod(EModifierOperation op, float mag, string src = null)
            => new ModifierDefinition("attr", op, mag, src);

        [Test]
        public void EmptyList_ReturnsBase()
        {
            var e = ModifierStackEvaluator.Evaluate(10f, NInf, Inf, null);
            Assert.AreEqual(10f, e.Value, 1e-4f);
            Assert.AreEqual(10f, e.RawValue, 1e-4f);
            Assert.IsEmpty(e.Breakdown);
        }

        [Test]
        public void Add_Sums()
        {
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Add, 2f),
                Mod(EModifierOperation.Add, 3f),
            };
            Assert.AreEqual(15f, ModifierStackEvaluator.Evaluate(10f, NInf, Inf, mods).Value, 1e-4f);
        }

        [Test]
        public void Order_AddThenPercentThenMultiply_RegardlessOfInputOrder()
        {
            // base 10 → +5(Add)=15 → +100%(PercentAdd)=30 → ×(1+0.5)(Multiply)=45
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Multiply, 0.5f),
                Mod(EModifierOperation.Add, 5f),
                Mod(EModifierOperation.PercentAdd, 1.0f),
            };
            Assert.AreEqual(45f, ModifierStackEvaluator.Evaluate(10f, NInf, Inf, mods).Value, 1e-4f);
        }

        [Test]
        public void PercentAdd_IsSummed_NotSequential()
        {
            // base 100, +50% +50% → 求和 100% → 200（不是 100×1.5×1.5=225）
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.PercentAdd, 0.5f),
                Mod(EModifierOperation.PercentAdd, 0.5f),
            };
            Assert.AreEqual(200f, ModifierStackEvaluator.Evaluate(100f, NInf, Inf, mods).Value, 1e-4f);
        }

        [Test]
        public void Multiply_IsSequentialProduct()
        {
            // base 100, ×(1+0.5) ×(1+0.5) → 100×1.5×1.5 = 225
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Multiply, 0.5f),
                Mod(EModifierOperation.Multiply, 0.5f),
            };
            Assert.AreEqual(225f, ModifierStackEvaluator.Evaluate(100f, NInf, Inf, mods).Value, 1e-4f);
        }

        [Test]
        public void Override_TakesLastValue_IgnoringPriorMath()
        {
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Add, 999f),
                Mod(EModifierOperation.Override, 7f),
                Mod(EModifierOperation.Override, 42f),
            };
            Assert.AreEqual(42f, ModifierStackEvaluator.Evaluate(10f, NInf, Inf, mods).Value, 1e-4f);
        }

        [Test]
        public void Clamp_AppliesMinAndMax()
        {
            var over  = new List<ModifierDefinition> { Mod(EModifierOperation.Add, 1000f) };
            Assert.AreEqual(100f, ModifierStackEvaluator.Evaluate(10f, 0f, 100f, over).Value, 1e-4f);

            var under = new List<ModifierDefinition> { Mod(EModifierOperation.Add, -1000f) };
            Assert.AreEqual(0f, ModifierStackEvaluator.Evaluate(10f, 0f, 100f, under).Value, 1e-4f);
        }

        [Test]
        public void Breakdown_DeltasSumToRawMinusBase()
        {
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Add, 5f, "race"),
                Mod(EModifierOperation.PercentAdd, 0.2f, "trait"),
                Mod(EModifierOperation.Multiply, 0.1f, "buff"),
            };
            var e = ModifierStackEvaluator.Evaluate(10f, NInf, Inf, mods);

            float sum = 0f;
            foreach (var c in e.Breakdown) sum += c.Delta;

            Assert.AreEqual(3, e.Breakdown.Count);
            Assert.AreEqual(e.RawValue - e.BaseValue, sum, 1e-3f);
        }

        [Test]
        public void Breakdown_CarriesSourceTags()
        {
            var mods = new List<ModifierDefinition> { Mod(EModifierOperation.Add, 2f, "race:elf") };
            var e = ModifierStackEvaluator.Evaluate(10f, NInf, Inf, mods);
            Assert.AreEqual("race:elf", e.Breakdown[0].SourceTag);
        }

        [Test]
        public void NullEntries_AreSkipped()
        {
            var mods = new List<ModifierDefinition> { null, Mod(EModifierOperation.Add, 4f), null };
            Assert.AreEqual(5f, ModifierStackEvaluator.Evaluate(1f, NInf, Inf, mods).Value, 1e-4f);
        }

        [Test]
        public void EvaluateValue_MatchesFullEvaluate()
        {
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Add, 3f),
                Mod(EModifierOperation.PercentAdd, 0.5f),
            };
            float full  = ModifierStackEvaluator.Evaluate(10f, 0f, 100f, mods).Value;
            float quick = ModifierStackEvaluator.EvaluateValue(10f, 0f, 100f, mods);
            Assert.AreEqual(full, quick, 1e-4f);
        }
    }
}
