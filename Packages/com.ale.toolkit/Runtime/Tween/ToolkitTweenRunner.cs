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

        private static void Recycle(TweenJob job)
            => ToolkitClassPool<TweenJob>.Despawn(job, j => j.Reset());
    }
}
