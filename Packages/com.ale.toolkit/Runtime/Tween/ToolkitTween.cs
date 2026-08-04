using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 轻量中央 Tween 门面（DOTween 式「单 Update 轮询作业表」）。当前提供 <see cref="CanvasGroup"/> 与
    /// <see cref="Graphic"/>（<see cref="Image"/> / 文本）的 alpha 淡入淡出，作业经 <see cref="ToolkitClassPool{T}"/>
    /// 池化、由 <see cref="ToolkitTweenRunner"/> 统一推进，近零 GC。
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

            var runner = EnsureRunner();
            if (!runner) return default; // 退出播放等极端情形

            var job = ToolkitClassPool<TweenJob>.Spawn() ?? new TweenJob();
            job.Id         = _nextId++;
            job.Alive      = true;
            job.Cg         = target;
            job.Gr         = null;
            job.From       = target.alpha;
            job.To         = endAlpha;
            job.Duration   = duration;
            job.Elapsed    = 0f;
            job.Ease       = ease;
            job.Unscaled   = unscaled;
            job.OnComplete = onComplete;

            runner.Add(job);
            return new ToolkitTweenHandle(job, job.Id);
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

            var runner = EnsureRunner();
            if (!runner) return default; // 退出播放等极端情形

            var job = ToolkitClassPool<TweenJob>.Spawn() ?? new TweenJob();
            job.Id         = _nextId++;
            job.Alive      = true;
            job.Gr         = target;
            job.Cg         = null;
            job.From       = target.color.a;
            job.To         = endAlpha;
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

    // 单个淡入淡出作业。经 ToolkitClassPool 池化复用；字段由 ToolkitTween 填充、ToolkitTweenRunner 推进。
    internal sealed class TweenJob
    {
        public long         Id;
        public bool         Alive;
        public CanvasGroup  Cg;   // 目标二选一：CanvasGroup（与 Gr 互斥，仅一个非空）
        public Graphic      Gr;   // 目标二选一：Graphic（Image / 文本）
        public float        From;
        public float        To;
        public float        Duration;
        public float        Elapsed;
        public EToolkitEase Ease;
        public bool         Unscaled;
        public Action       OnComplete;

        /// <summary>作业是否仍有存活目标（两目标皆空 = 目标已被销毁 / 未设置）。</summary>
        public bool HasTarget => Cg || Gr;

        /// <summary>把 alpha 写入当前激活目标（各自带 Unity null 守卫）。</summary>
        public void SetAlpha(float a)
        {
            if (Cg) Cg.alpha = a;
            else if (Gr) { var c = Gr.color; c.a = a; Gr.color = c; }
        }

        /// <summary>
        /// 打断 / 完成作业：<paramref name="complete"/>=true 时瞬置终值并触发回调；否则打断且不回调。
        /// 仅置标志，实际从 runner 移除与归还池在下一帧 tick 完成。
        /// </summary>
        public void Kill(bool complete)
        {
            if (!Alive) return;
            Alive = false;

            var cb = OnComplete;
            OnComplete = null; // 无论是否 complete 都清空，避免 runner 回收时重复触发
            if (complete)
            {
                SetAlpha(To);
                cb?.Invoke();
            }
        }

        /// <summary>归还池前清理引用（避免保活 Target / 回调）。两个目标字段都要清空，防止池复用时串写。</summary>
        public void Reset()
        {
            Alive      = false;
            Cg         = null;
            Gr         = null;
            OnComplete = null;
        }
    }

    // 常驻 Tween 运行器：单 LateUpdate 轮询全部作业。跨场景持久、关闭 Domain Reload 自动复位（均继承自基类）。
    internal sealed class ToolkitTweenRunner : ToolkitMonoSingleton<ToolkitTweenRunner>
    {
        private readonly List<TweenJob> _jobs = new List<TweenJob>();

        public void Add(TweenJob job) => _jobs.Add(job);

        private void LateUpdate()
        {
            // 倒序遍历：便于就地移除；回调中新增的作业追加在末尾，本帧不处理、下一帧再推进。
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var job = _jobs[i];

                // 已被 Kill 或目标被销毁（两目标皆空）：移除并回收（不触发完成回调）。
                if (!job.Alive || !job.HasTarget)
                {
                    _jobs.RemoveAt(i);
                    Recycle(job);
                    continue;
                }

                job.Elapsed += job.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = job.Duration > 0f ? job.Elapsed / job.Duration : 1f;
                float k = ToolkitEase.Evaluate(job.Ease, t);
                job.SetAlpha(Mathf.Lerp(job.From, job.To, k));

                if (t >= 1f)
                {
                    job.SetAlpha(job.To);
                    var cb = job.OnComplete;
                    _jobs.RemoveAt(i);
                    Recycle(job);       // 先回收再回调：回调内若再发起 tween 不受本作业回收影响
                    cb?.Invoke();
                }
            }
        }

        private static void Recycle(TweenJob job)
            => ToolkitClassPool<TweenJob>.Despawn(job, j => j.Reset());
    }
}
