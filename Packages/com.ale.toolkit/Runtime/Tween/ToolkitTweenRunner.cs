using System.Collections.Generic;
using UnityEngine;

namespace Ale.Toolkit.Runtime
{
    // 常驻 Tween 运行器：单次轮询推进全部作业。跨场景持久、关闭 Domain Reload 自动复位（均继承自基类）。
    // 由 ToolkitTween.EnsureRunner() 惰性创建（GameObject "[ToolkitTween]"），不需要手动挂载。
    //
    // [ExecuteAlways] 是必需的：没有它，编辑模式下 Unity 不调用 Awake，基类的单例登记便不会发生，
    // 于是每次 ToolkitTween 调用都会新建一个 "[ToolkitTween]" 对象（而且落进用户正在编辑的场景），
    // 同时因为 LateUpdate 也不跑，补间永远不推进、完成回调永远不触发。
    [ExecuteAlways]
    internal sealed class ToolkitTweenRunner : ToolkitMonoSingleton<ToolkitTweenRunner>
    {
        private readonly List<TweenJob> _jobs = new List<TweenJob>();

        public void Add(TweenJob job) => _jobs.Add(job);

        private void LateUpdate()
        {
            // 编辑模式由 EditorTick 驱动（见下），此处让开，避免同一次编辑器重绘里双重推进。
            if (!Application.isPlaying) return;
            Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        // 推进一次全部作业。scaledDelta / unscaledDelta 分开传：作业按各自的 Unscaled 取用。
        private void Tick(float scaledDelta, float unscaledDelta)
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

                job.Elapsed += job.Unscaled ? unscaledDelta : scaledDelta;
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

#if UNITY_EDITOR
        // ── 编辑模式的时钟 ────────────────────────────────────────────────────────────
        // 编辑模式下没有稳定的帧循环：LateUpdate 的调用时机取决于编辑器是否重绘，
        // Time.deltaTime / unscaledDeltaTime 也不反映真实流逝。改挂 EditorApplication.update，
        // 用 timeSinceStartup 自行算增量——它是编辑器里唯一稳定推进的时钟。
        private double _editorTimeLast;

        protected override void Init()
        {
            base.Init();
            if (Application.isPlaying) return;

            _editorTimeLast = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= EditorTick;   // 先减后加，避免重复订阅
            UnityEditor.EditorApplication.update += EditorTick;
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        protected override void OnDestroy()
        {
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            base.OnDestroy();
        }

        // 离开编辑模式时自毁，把运行期干净地交还给播放模式新建的那一个。
        //
        // 必须显式做这件事：编辑模式建的运行器带 HideAndDontSave，进入播放模式时不会被销毁；
        // 而静态单例引用会随域重载复位（见基类的 ResetStatics），于是 EnsureRunner 又会新造一个 ——
        // 两个运行器并存，都在推进各自的作业表。
        private void HandlePlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingEditMode) return;
            if (this) DestroyImmediate(gameObject);
        }

        private void EditorTick()
        {
            // 进入播放模式后交还给 LateUpdate；此时该实例通常已随域重载销毁，这里只是兜底。
            if (Application.isPlaying) return;

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float delta = (float)(now - _editorTimeLast);
            _editorTimeLast = now;

            // 编辑器可能因导入 / 编译长时间停摆，导致增量畸大。钳到 0.1 秒：
            // 宁可让补间在停摆后走慢一点，也不要一次跳完而看不到过程。
            if (delta <= 0f) return;
            if (delta > 0.1f) delta = 0.1f;

            // 编辑模式无 timeScale 概念，两个增量同源。
            Tick(delta, delta);
        }
#endif

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
