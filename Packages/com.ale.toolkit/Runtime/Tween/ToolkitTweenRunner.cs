using System.Collections.Generic;
using UnityEngine;

namespace Ale.Toolkit.Runtime
{
    // 常驻 Tween 运行器：单 LateUpdate 轮询全部作业。跨场景持久、关闭 Domain Reload 自动复位（均继承自基类）。
    // 由 ToolkitTween.EnsureRunner() 惰性创建（GameObject "[ToolkitTween]"），不需要手动挂载。
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

                // 已被 Kill 或目标被销毁：移除并回收（不触发完成回调）。
                if (!job.Alive || !job.HasTarget)
                {
                    _jobs.RemoveAt(i);
                    Recycle(job);
                    continue;
                }

                job.Elapsed += job.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = job.Duration > 0f ? job.Elapsed / job.Duration : 1f;
                job.Apply(ToolkitEase.Evaluate(job.Ease, t));

                if (t >= 1f)
                {
                    job.ApplyEnd();     // 收尾精确写入 To，不经插值
                    var cb = job.OnComplete;
                    _jobs.RemoveAt(i);
                    Recycle(job);       // 先回收再回调：回调内若再发起 tween 不受本作业回收影响
                    cb?.Invoke();
                }
            }
        }

        /// <summary>
        /// 打断该目标上的全部在途作业，返回打断数。O(n) 线性扫描——量级为数十个并发作业时，
        /// 目标登记表（Dictionary）反而多出一份需与池回收同步的状态，得不偿失。
        ///
        /// <para><b>只置标志、不移除</b>：移除与回池仍由下一次 <see cref="LateUpdate"/> 完成，
        /// 以保持「<c>_jobs</c> 仅在 LateUpdate 内收缩」这一不变量——这样即便回调里重入
        /// （再起 tween / 再 Kill），本次扫描的索引也不会失效。</para>
        /// </summary>
        public int KillByTarget(UnityEngine.Object target, bool complete)
        {
            // 先快照数量：complete=true 会同步触发回调，回调里若再发起 tween 会追加到末尾——
            // 这些新作业不属于本次「按目标打断」的范围（与 LateUpdate「本帧新增、下帧再推进」一致）。
            int count  = _jobs.Count;
            int killed = 0;
            for (int i = 0; i < count; i++)
            {
                var job = _jobs[i];
                // 必须用 ReferenceEquals 而非 ==：Unity 的 operator== 把已销毁对象判为 null，
                // 于是两个互不相干的已销毁对象会互相「相等」，用 == 会误杀并触发错误回调。
                if (!job.Alive || !ReferenceEquals(job.Target, target)) continue;
                job.Kill(complete);   // 幂等；complete=true 时其完成回调在此同步触发
                killed++;
            }
            return killed;
        }

        private static void Recycle(TweenJob job)
            => ToolkitClassPool<TweenJob>.Despawn(job, j => j.Reset());
    }
}
