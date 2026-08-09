using System.Collections.Generic;
using NUnit.Framework;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// Tween 模块中<b>纯函数与守卫路径</b>的门槛：<see cref="ToolkitEase.Evaluate"/> 的端点 / 钳制 / 值域、
    /// <see cref="ToolkitTween.ShortestEuler"/> 的逐轴最短弧折算，以及空句柄与「时长 ≤ 0 / 目标失效」快路径。
    ///
    /// <para><b>覆盖边界</b>：作业推进由 <c>ToolkitTweenRunner</c> 在 <c>LateUpdate</c> 完成，需 PlayMode；
    /// 而本测试程序集是 <c>includePlatforms: ["Editor"]</c> 的 EditMode 套件，故不覆盖。
    /// <c>ToolkitTweenHandle</c> 的构造器为 <c>internal</c> 且未开 <c>InternalsVisibleTo</c>（也不应为测试而开），
    /// 故只覆盖 <c>default</c> 空句柄的语义。运行中作业的行为由开发工程 Demo 场景人工核验兜底。</para>
    ///
    /// <para>本测试仅存在于 toolkit 开发工程的 <c>Assets/</c> 下，不随 <c>com.ale.toolkit</c> 包分发。</para>
    /// </summary>
    public class ToolkitTweenTests
    {
        private const float Eps = 1e-4f;

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, "x");
            Assert.AreEqual(expected.y, actual.y, Eps, "y");
            Assert.AreEqual(expected.z, actual.z, Eps, "z");
        }

        #region ToolkitEase

        [Test]
        public void Evaluate_AllEases_MapEndpointsToEndpoints()
        {
            foreach (EToolkitEase ease in System.Enum.GetValues(typeof(EToolkitEase)))
            {
                Assert.AreEqual(0f, ToolkitEase.Evaluate(ease, 0f), Eps, $"{ease} t=0");
                Assert.AreEqual(1f, ToolkitEase.Evaluate(ease, 1f), Eps, $"{ease} t=1");
            }
        }

        [Test]
        public void Evaluate_AllEases_ClampInputToUnitRange()
        {
            foreach (EToolkitEase ease in System.Enum.GetValues(typeof(EToolkitEase)))
            {
                Assert.AreEqual(0f, ToolkitEase.Evaluate(ease, -5f), Eps, $"{ease} t<0");
                Assert.AreEqual(1f, ToolkitEase.Evaluate(ease, 5f), Eps, $"{ease} t>1");
            }
        }

        /// <summary>
        /// 四种缓动的输出恒落在 [0,1]。这是「作业推进用 <c>LerpUnclamped</c> 与用 <c>Lerp</c> 结果逐位相同」
        /// 的前提——若将来加入 Back / Elastic 等过冲缓动，本用例会先失败以提醒该等价性不再成立。
        /// </summary>
        [Test]
        public void Evaluate_AllEases_StayWithinUnitRange()
        {
            foreach (EToolkitEase ease in System.Enum.GetValues(typeof(EToolkitEase)))
            {
                for (int i = 0; i <= 100; i++)
                {
                    float k = ToolkitEase.Evaluate(ease, i / 100f);
                    Assert.GreaterOrEqual(k, -Eps, $"{ease} 下溢 @t={i / 100f}");
                    Assert.LessOrEqual(k, 1f + Eps, $"{ease} 上溢 @t={i / 100f}");
                }
            }
        }

        [Test]
        public void Evaluate_Linear_IsIdentity()
        {
            Assert.AreEqual(0.25f, ToolkitEase.Evaluate(EToolkitEase.Linear, 0.25f), Eps);
            Assert.AreEqual(0.50f, ToolkitEase.Evaluate(EToolkitEase.Linear, 0.50f), Eps);
            Assert.AreEqual(0.75f, ToolkitEase.Evaluate(EToolkitEase.Linear, 0.75f), Eps);
        }

        [Test]
        public void Evaluate_InQuad_IsSquare_AndStartsSlow()
        {
            Assert.AreEqual(0.25f, ToolkitEase.Evaluate(EToolkitEase.InQuad, 0.5f), Eps);
            // 缓入：前半段进度落后于线性
            Assert.Less(ToolkitEase.Evaluate(EToolkitEase.InQuad, 0.3f), 0.3f);
        }

        [Test]
        public void Evaluate_OutQuad_MirrorsInQuad_AndStartsFast()
        {
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Assert.AreEqual(1f - ToolkitEase.Evaluate(EToolkitEase.InQuad, 1f - t),
                                ToolkitEase.Evaluate(EToolkitEase.OutQuad, t), Eps, $"t={t}");
            }
            // 缓出：前半段进度领先于线性
            Assert.Greater(ToolkitEase.Evaluate(EToolkitEase.OutQuad, 0.3f), 0.3f);
        }

        [Test]
        public void Evaluate_InOutQuad_IsSymmetricAboutMidpoint()
        {
            Assert.AreEqual(0.5f, ToolkitEase.Evaluate(EToolkitEase.InOutQuad, 0.5f), Eps);
            // 点对称：f(t) + f(1-t) == 1
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                Assert.AreEqual(1f, ToolkitEase.Evaluate(EToolkitEase.InOutQuad, t)
                                  + ToolkitEase.Evaluate(EToolkitEase.InOutQuad, 1f - t), Eps, $"t={t}");
            }
        }

        [Test]
        public void Evaluate_AllEases_AreMonotonicallyIncreasing()
        {
            foreach (EToolkitEase ease in System.Enum.GetValues(typeof(EToolkitEase)))
            {
                float prev = ToolkitEase.Evaluate(ease, 0f);
                for (int i = 1; i <= 100; i++)
                {
                    float cur = ToolkitEase.Evaluate(ease, i / 100f);
                    Assert.GreaterOrEqual(cur, prev - Eps, $"{ease} 在 t={i / 100f} 处回退");
                    prev = cur;
                }
            }
        }

        #endregion

        #region ShortestEuler

        [Test]
        public void ShortestEuler_AcrossZero_TakesShortArcForward()
        {
            // 350° → 10°：折算终点 370°，扫过 +20°，而非朴素插值的 -340°
            var baked = ToolkitTween.ShortestEuler(new Vector3(0f, 350f, 0f), new Vector3(0f, 10f, 0f));
            AssertVector3(new Vector3(0f, 370f, 0f), baked);
        }

        [Test]
        public void ShortestEuler_AcrossZero_TakesShortArcBackward()
        {
            // 10° → 350°：折算终点 -10°，扫过 -20°
            var baked = ToolkitTween.ShortestEuler(new Vector3(0f, 10f, 0f), new Vector3(0f, 350f, 0f));
            AssertVector3(new Vector3(0f, -10f, 0f), baked);
        }

        [Test]
        public void ShortestEuler_HalfTurn_KeepsPositiveDirection()
        {
            var baked = ToolkitTween.ShortestEuler(Vector3.zero, new Vector3(0f, 180f, 0f));
            AssertVector3(new Vector3(0f, 180f, 0f), baked);
        }

        [Test]
        public void ShortestEuler_JustOverHalfTurn_FlipsToBackward()
        {
            // 181° 的短边是反向 179°
            var baked = ToolkitTween.ShortestEuler(Vector3.zero, new Vector3(0f, 181f, 0f));
            AssertVector3(new Vector3(0f, -179f, 0f), baked);
        }

        [Test]
        public void ShortestEuler_NoChange_ReturnsSameAngles()
        {
            var from = new Vector3(30f, 200f, 359f);
            AssertVector3(from, ToolkitTween.ShortestEuler(from, from));
        }

        [Test]
        public void ShortestEuler_AxesAreIndependent()
        {
            var baked = ToolkitTween.ShortestEuler(new Vector3(350f, 10f, 90f), new Vector3(10f, 350f, 270f));
            AssertVector3(new Vector3(370f, -10f, 270f), baked);
        }

        /// <summary>折算后的终点归一化到 [0,360) 后，必须仍等价于调用方请求的目标角。</summary>
        [Test]
        public void ShortestEuler_BakedEnd_NormalizesBackToRequestedTarget()
        {
            var from = new Vector3(350f, 10f, 90f);
            var to   = new Vector3(10f, 350f, 270f);
            var baked = ToolkitTween.ShortestEuler(from, to);
            AssertVector3(
                new Vector3(Mathf.Repeat(to.x, 360f), Mathf.Repeat(to.y, 360f), Mathf.Repeat(to.z, 360f)),
                new Vector3(Mathf.Repeat(baked.x, 360f), Mathf.Repeat(baked.y, 360f), Mathf.Repeat(baked.z, 360f)));
        }

        /// <summary>逐轴扫过角不得超过 180°——这正是「时长按 Quaternion.Angle 计算」不会超速的依据。</summary>
        [Test]
        public void ShortestEuler_PerAxisSweep_NeverExceedsHalfTurn()
        {
            for (int a = 0; a < 360; a += 17)
            {
                for (int b = 0; b < 360; b += 23)
                {
                    var from  = new Vector3(a, a, a);
                    var to    = new Vector3(b, b, b);
                    var sweep = ToolkitTween.ShortestEuler(from, to) - from;
                    Assert.LessOrEqual(Mathf.Abs(sweep.x), 180f + Eps, $"{a}→{b}");
                }
            }
        }

        #endregion

        #region 空句柄与快路径

        [Test]
        public void DefaultHandle_IsInactive_AndKillCompleteAreNoOps()
        {
            var h = default(ToolkitTweenHandle);
            Assert.IsFalse(h.IsActive);
            Assert.DoesNotThrow(() => h.Kill());
            Assert.DoesNotThrow(() => h.Kill(true));
            Assert.DoesNotThrow(() => h.Complete());
        }

        [Test]
        public void DefaultHandles_CompareEqual_AndAreRemovableFromList()
        {
            Assert.IsTrue(default(ToolkitTweenHandle) == default(ToolkitTweenHandle));
            Assert.IsFalse(default(ToolkitTweenHandle) != default(ToolkitTweenHandle));
            Assert.AreEqual(default(ToolkitTweenHandle).GetHashCode(), default(ToolkitTweenHandle).GetHashCode());

            var list = new List<ToolkitTweenHandle> { default };
            Assert.IsTrue(list.Remove(default));
            Assert.IsEmpty(list);
        }

        [Test]
        public void DelayedCall_NonPositiveDelay_InvokesSynchronously_AndReturnsEmptyHandle()
        {
            int hits = 0;
            var h = ToolkitTween.DelayedCall(0f, () => hits++);
            // 与 DOTween 的差异：此处同步立刻触发，而非推迟一帧
            Assert.AreEqual(1, hits);
            Assert.IsFalse(h.IsActive);
            Assert.IsTrue(h == default(ToolkitTweenHandle));

            h = ToolkitTween.DelayedCall(-1f, () => hits++);
            Assert.AreEqual(2, hits);
            Assert.IsFalse(h.IsActive);
        }

        [Test]
        public void DelayedCall_DestroyedOwner_IsDropped_WithoutInvoking()
        {
            var owner = ScriptableObject.CreateInstance<ScriptableObject>();
            Object.DestroyImmediate(owner);

            int hits = 0;
            var h = ToolkitTween.DelayedCall(10f, () => hits++, owner: owner);
            Assert.AreEqual(0, hits, "owner 已销毁时不应触发回调");
            Assert.IsFalse(h.IsActive, "owner 已销毁时应返回空句柄（且不入 runner）");
        }

        [Test]
        public void Kill_NullTarget_ReturnsZero()
        {
            Assert.AreEqual(0, ToolkitTween.Kill(null));
            Assert.AreEqual(0, ToolkitTween.Kill(null, complete: true));
        }

        #endregion
    }
}
