using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 轻量中央 Tween 门面（DOTween 式「单 Update 轮询作业表」）。当前提供 <see cref="CanvasGroup"/> 与
    /// <see cref="Graphic"/>（<see cref="Image"/> / 文本）的 alpha 淡入淡出。
    ///
    /// <para>内部以「<see cref="ETweenChannel"/> 通道 + 单一 <see cref="UnityEngine.Object"/> 目标 +
    /// <see cref="Vector4"/> 载荷」的联合体承载各类作业（见 <c>ToolkitTweenJob.cs</c>），
    /// 作业经 <see cref="ToolkitClassPool{T}"/> 池化、由 <c>ToolkitTweenRunner</c> 统一推进，近零 GC。</para>
    ///
    /// <para><b>作用域为轻量：</b>不复刻 DOTween 的 Sequence / 泛型链式 / 全套 Ease，按需增量扩展。</para>
    /// </summary>
    public static class ToolkitTween
    {
        // 全局自增的作业 ID（单调唯一，用于句柄校验，不复用）。
        private static long _nextId = 1;

        /// <summary>
        /// 对 <paramref name="target"/> 的 <see cref="CanvasGroup.alpha"/> 做淡入 / 淡出。
        /// 返回可用于打断的句柄；<paramref name="duration"/> ≤ 0 或目标为空时立即到位并返回空句柄。
        /// </summary>
        /// <param name="target">目标 CanvasGroup。</param>
        /// <param name="endAlpha">目标 alpha。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle FadeCanvasGroup(
            CanvasGroup target, float endAlpha, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            if (!target) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            if (duration <= 0f)
            {
                target.alpha = endAlpha;
                onComplete?.Invoke();
                return default;
            }

            return Start(ETweenChannel.CanvasGroupAlpha, target,
                new Vector4(target.alpha, 0f, 0f, 0f), new Vector4(endAlpha, 0f, 0f, 0f),
                duration, ease, unscaled, onComplete);
        }

        /// <summary>
        /// 对 <paramref name="target"/> 的 <see cref="Graphic.color"/> 的 alpha 做淡入 / 淡出
        /// （<see cref="Image"/> / 文本等均为 <see cref="Graphic"/>）。返回可用于打断的句柄；
        /// <paramref name="duration"/> ≤ 0 或目标为空时立即到位并返回空句柄。语义同 <see cref="FadeCanvasGroup"/>。
        /// </summary>
        /// <param name="target">目标 Graphic（Image / 文本等）。</param>
        /// <param name="endAlpha">目标 alpha。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle FadeGraphic(
            Graphic target, float endAlpha, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            if (!target) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            if (duration <= 0f)
            {
                var c = target.color; c.a = endAlpha; target.color = c;
                onComplete?.Invoke();
                return default;
            }

            return Start(ETweenChannel.GraphicAlpha, target,
                new Vector4(target.color.a, 0f, 0f, 0f), new Vector4(endAlpha, 0f, 0f, 0f),
                duration, ease, unscaled, onComplete);
        }

        // 统一的作业启动：取池 / 填字段 / 入 runner / 出句柄。
        // 各公开方法先做完自己的空守卫与「时长 ≤ 0」快路径，再汇到这里。
        // runner 不可用（退出播放等极端情形）时丢弃回调并返回空句柄。
        private static ToolkitTweenHandle Start(
            ETweenChannel channel, UnityEngine.Object target,
            Vector4 from, Vector4 to, float duration,
            EToolkitEase ease, bool unscaled, Action onComplete)
        {
            var runner = EnsureRunner();
            if (!runner) return default;

            var job = ToolkitClassPool<TweenJob>.Spawn() ?? new TweenJob();
            job.Id         = _nextId++;
            job.Alive      = true;
            job.Channel    = channel;
            job.Target     = target;
            job.From       = from;
            job.To         = to;
            job.Duration   = duration;
            job.Elapsed    = 0f;
            job.Ease       = ease;
            job.Unscaled   = unscaled;
            job.OnComplete = onComplete;

            runner.Add(job);
            return new ToolkitTweenHandle(job, job.Id);
        }

        // 惰性获取 / 创建常驻 runner。ToolkitMonoSingleton.Instance 不自建，故此处显式创建。
        private static ToolkitTweenRunner EnsureRunner()
        {
            if (ToolkitTweenRunner.IsQuitting) return null;

            var inst = ToolkitTweenRunner.Instance;
            if (inst) return inst;

            var go = new GameObject("[ToolkitTween]");
            return go.AddComponent<ToolkitTweenRunner>(); // Awake: 设 Instance + DontDestroyOnLoad
        }
    }

    /// <summary>
    /// <see cref="ToolkitTween"/> 返回的作业句柄（值类型，零分配）。默认值（<c>default</c>）为无效句柄，
    /// 其 <see cref="Kill"/> 为安全空操作。通过作业 ID 校验，避免误杀已被池复用的作业。
    /// </summary>
    public readonly struct ToolkitTweenHandle
    {
        internal readonly TweenJob Job;
        internal readonly long     Id;

        internal ToolkitTweenHandle(TweenJob job, long id)
        {
            Job = job;
            Id  = id;
        }

        /// <summary>该作业是否仍在进行中（未完成、未被打断、未被池复用为其它作业）。</summary>
        public bool IsActive => Job != null && Job.Alive && Job.Id == Id;

        /// <summary>停止该作业。<paramref name="complete"/> 为 true 时先把目标瞬置到终值并触发完成回调。</summary>
        public void Kill(bool complete = false)
        {
            if (IsActive) Job.Kill(complete);
        }
    }
}
