using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 作业通道：决定 <see cref="TweenJob.From"/> / <see cref="TweenJob.To"/> 载荷的语义，
    /// 以及 <see cref="TweenJob.Apply"/> 把插值结果写回目标的方式。
    ///
    /// <para><b>Delay 刻意排在最后（值非 0）：</b>这样「零初始化 / 池复用后未填字段」的脏作业会落在
    /// <see cref="CanvasGroupAlpha"/> 且 <c>Target</c> 为 null，被 runner 判为「无目标」剔除；
    /// 若 Delay 占 0，这类脏作业会被当成合法的「无目标纯延时」而永远存活。</para>
    /// </summary>
    internal enum ETweenChannel
    {
        /// <summary><see cref="CanvasGroup.alpha"/>。载荷取 x 分量。</summary>
        CanvasGroupAlpha = 0,

        /// <summary><see cref="Graphic.color"/> 的 alpha。载荷取 x 分量。</summary>
        GraphicAlpha,

        /// <summary><see cref="Graphic.color"/> 整色（RGBA）。载荷取全部四个分量。</summary>
        GraphicColor,

        /// <summary><see cref="SpriteRenderer.color"/> 的 alpha。载荷取 x 分量。</summary>
        SpriteRendererAlpha,

        /// <summary><see cref="Transform.position"/>（世界坐标）。载荷取 xyz 分量。</summary>
        TransformPosition,

        /// <summary>
        /// <see cref="Transform.eulerAngles"/>（世界欧拉角）。载荷取 xyz 分量；
        /// <c>To</c> 在起始时已折算为「逐轴最短弧」的等价终点，故此处直接线性插值即可。
        /// </summary>
        TransformEulerAngles,

        /// <summary><see cref="Transform.localScale"/>。载荷取 xyz 分量。</summary>
        TransformLocalScale,

        /// <summary>纯延时：无插值目标，仅到期触发回调。</summary>
        Delay,
    }

    // 单个补间作业。经 ToolkitClassPool 池化复用；字段由 ToolkitTween 填充、ToolkitTweenRunner 推进。
    // 目标是「单一 UnityEngine.Object + 通道」的联合体：通道决定 Target 的实际类型与 From/To 载荷的语义。
    internal sealed class TweenJob
    {
        public long               Id;
        public bool               Alive;
        public ETweenChannel      Channel;
        public UnityEngine.Object Target;   // 通道对应的目标；Delay 通道下为可选 owner，可为 null
        public Vector4            From;
        public Vector4            To;
        public float              Duration;
        public float              Elapsed;
        public EToolkitEase       Ease;
        public bool               Unscaled;
        public Action             OnComplete;

        /// <summary>
        /// 作业是否仍有存活目标。<see cref="ETweenChannel.Delay"/> 未传 owner 时无目标可失效、恒为 true；
        /// 其余通道（以及传了 owner 的延时）随目标存亡——目标被 Destroy 后由 runner 剔除，且不触发完成回调。
        /// </summary>
        public bool HasTarget
            // ReferenceEquals 判「真 null」（从未设过目标）——只有纯延时允许这种情况；
            // 确有引用时再走 Unity 的 operator bool，以识别「已被 Destroy」的假 null。
            => ReferenceEquals(Target, null) ? Channel == ETweenChannel.Delay : (bool)Target;

        /// <summary>按插值系数 <paramref name="k"/>（已过缓动，非线性进度 t）在 From→To 之间取值并写入目标。</summary>
        public void Apply(float k) => Write(Vector4.LerpUnclamped(From, To, k));

        /// <summary>
        /// 把终值 <see cref="To"/> <b>精确</b>写入目标（收尾与「立即完成」用）。
        /// 不走 <see cref="Apply"/>：<c>LerpUnclamped</c> 是 <c>a + (b - a) * t</c>，
        /// <c>t = 1</c> 时未必逐位等于 <c>b</c>，直接写 To 可避免这一末位误差。
        /// </summary>
        public void ApplyEnd() => Write(To);

        /// <summary>
        /// 把 <paramref name="v"/> 按通道语义写回目标（alpha 类通道只取 x 分量）。
        ///
        /// <para>各通道一律用 <c>as</c> 转型 + Unity 空守卫：这样「通道与目标类型不匹配」会退化为静默空操作，
        /// 而不是在 runner 的作业循环里抛 <see cref="InvalidCastException"/> 把本帧其余作业一并打断。
        /// 空守卫同时覆盖了「目标已被 Destroy」。</para>
        /// </summary>
        private void Write(Vector4 v)
        {
            switch (Channel)
            {
                case ETweenChannel.CanvasGroupAlpha:
                {
                    var cg = Target as CanvasGroup;
                    if (cg) cg.alpha = v.x;
                    break;
                }
                case ETweenChannel.GraphicAlpha:
                {
                    var gr = Target as Graphic;
                    if (gr) { var c = gr.color; c.a = v.x; gr.color = c; }
                    break;
                }
                case ETweenChannel.GraphicColor:
                {
                    var gr = Target as Graphic;
                    if (gr) gr.color = v;               // Vector4 → Color 隐式转换
                    break;
                }
                case ETweenChannel.SpriteRendererAlpha:
                {
                    var sr = Target as SpriteRenderer;
                    if (sr) { var c = sr.color; c.a = v.x; sr.color = c; }
                    break;
                }
                case ETweenChannel.TransformPosition:
                {
                    var tr = Target as Transform;
                    if (tr) tr.position = v;            // Vector4 → Vector3 隐式转换
                    break;
                }
                case ETweenChannel.TransformEulerAngles:
                {
                    var tr = Target as Transform;
                    if (tr) tr.eulerAngles = v;
                    break;
                }
                case ETweenChannel.TransformLocalScale:
                {
                    var tr = Target as Transform;
                    if (tr) tr.localScale = v;
                    break;
                }
                case ETweenChannel.Delay:
                    break;  // 纯延时：无插值目标
            }
        }

        /// <summary>
        /// 打断 / 完成作业：<paramref name="complete"/>=true 时瞬置终值（<see cref="ApplyEnd"/>，Delay 通道为空操作）
        /// 并触发回调；否则打断且不回调。仅置标志，实际从 runner 移除与归还池在下一帧 tick 完成。
        /// <b>幂等</b>：重复调用（含回调内重入）不会二次触发。
        /// </summary>
        public void Kill(bool complete)
        {
            if (!Alive) return;
            Alive = false;

            var cb = OnComplete;
            OnComplete = null; // 无论是否 complete 都清空，避免 runner 回收时重复触发
            if (complete)
            {
                ApplyEnd();
                cb?.Invoke();
            }
        }

        /// <summary>
        /// 归还池前清理<b>引用型</b>字段（避免保活 Target / 回调）。
        /// 值型字段由 <see cref="ToolkitTween"/> 下次取用时全量覆盖，无需在此清零。
        /// </summary>
        public void Reset()
        {
            Alive      = false;
            Target     = null;
            OnComplete = null;
        }
    }
}
