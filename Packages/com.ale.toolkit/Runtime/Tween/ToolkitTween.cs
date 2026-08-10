using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.Toolkit.Runtime
{
    /// <summary>
    /// 轻量中央 Tween 门面（DOTween 式「单 Update 轮询作业表」）。提供
    /// <see cref="CanvasGroup"/> / <see cref="Graphic"/>（<see cref="Image"/> / 文本）/ <see cref="SpriteRenderer"/>
    /// 的 alpha 淡入淡出、<see cref="Graphic"/> 的整色过渡、<see cref="Transform"/> 的位移 / 旋转 / 缩放、
    /// 纯延时回调，以及写回路径自定义的通用浮点补间（<see cref="To"/>）。
    ///
    /// <para>内部以「<see cref="ETweenChannel"/> 通道 + 单一 <see cref="UnityEngine.Object"/> 目标 +
    /// <see cref="Vector4"/> 载荷」的联合体承载各类作业（见 <c>ToolkitTweenJob.cs</c>），
    /// 作业经 <see cref="ToolkitClassPool{T}"/> 池化、由 <c>ToolkitTweenRunner</c> 统一推进，近零 GC。</para>
    ///
    /// <para><b>不做覆盖管理</b>（与 DOTween 一致）：对同一目标同一通道再起一个 tween <b>不会</b>自动打断前一个，
    /// 两者会在同一帧互相争写（runner 倒序推进，先起的那个后写、结果为准）。需要「重来一次」时，
    /// 请先按目标打断——见 <see cref="Kill(UnityEngine.Object, bool)"/>。</para>
    ///
    /// <para><b>作用域为轻量：</b>不复刻 DOTween 的 Sequence / 泛型链式 / 全套 Ease，按需增量扩展。</para>
    /// </summary>
    public static class ToolkitTween
    {
        // 全局自增的作业 ID（单调唯一，用于句柄校验，不复用）。
        private static long _nextId = 1;

        #region 淡入淡出 / 颜色

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

        /// <summary>
        /// 对 <paramref name="target"/> 的 <see cref="SpriteRenderer.color"/> 的 alpha 做淡入 / 淡出
        /// （2D 精灵；<see cref="SpriteRenderer"/> 并非 <see cref="Graphic"/>，故与 <see cref="FadeGraphic"/> 分列）。
        /// 返回可用于打断的句柄；<paramref name="duration"/> ≤ 0 或目标为空时立即到位并返回空句柄。
        /// 对应 DOTween 的 <c>spriteRenderer.DOFade</c>。
        /// </summary>
        /// <param name="target">目标 SpriteRenderer。</param>
        /// <param name="endAlpha">目标 alpha。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle FadeSpriteRenderer(
            SpriteRenderer target, float endAlpha, float duration,
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

            return Start(ETweenChannel.SpriteRendererAlpha, target,
                new Vector4(target.color.a, 0f, 0f, 0f), new Vector4(endAlpha, 0f, 0f, 0f),
                duration, ease, unscaled, onComplete);
        }

        /// <summary>
        /// 对 <paramref name="target"/> 的 <see cref="Graphic.color"/> 做<b>整色（RGBA）</b>过渡
        /// （<see cref="Image"/> / 文本等均为 <see cref="Graphic"/>）——<see cref="FadeGraphic"/> 的全色版本。
        /// 返回可用于打断的句柄；<paramref name="duration"/> ≤ 0 或目标为空时立即到位并返回空句柄。
        /// 对应 DOTween 的 <c>graphic.DOColor</c>。
        /// </summary>
        /// <param name="target">目标 Graphic（Image / 文本等）。</param>
        /// <param name="endColor">目标颜色（含 alpha）。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle TintGraphic(
            Graphic target, Color endColor, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            if (!target) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            if (duration <= 0f)
            {
                target.color = endColor;
                onComplete?.Invoke();
                return default;
            }

            // Color → Vector4 隐式转换，四个分量原样承载。
            return Start(ETweenChannel.GraphicColor, target,
                target.color, endColor,
                duration, ease, unscaled, onComplete);
        }

        #endregion

        #region Transform

        /// <summary>
        /// 把 <paramref name="target"/> 的<b>世界坐标</b> <see cref="Transform.position"/> 平滑移动到
        /// <paramref name="endPosition"/>。返回可用于打断的句柄；<paramref name="duration"/> ≤ 0 或目标为空时
        /// 立即到位并返回空句柄。对应 DOTween 的 <c>transform.DOMove</c>（局部坐标版按需另加）。
        /// </summary>
        /// <param name="target">目标 Transform。</param>
        /// <param name="endPosition">目标世界坐标。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle MoveTransform(
            Transform target, Vector3 endPosition, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            if (!target) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            if (duration <= 0f)
            {
                target.position = endPosition;
                onComplete?.Invoke();
                return default;
            }

            return Start(ETweenChannel.TransformPosition, target,
                target.position, endPosition,
                duration, ease, unscaled, onComplete);
        }

        /// <summary>
        /// 把 <paramref name="target"/> 的<b>世界欧拉角</b> <see cref="Transform.eulerAngles"/> 平滑旋转到
        /// <paramref name="endEulerAngles"/>。<b>各轴独立走最短弧</b>——起始时经 <see cref="ShortestEuler"/>
        /// 折算终值，350° → 10° 只转 20° 而非 340°；等价于 DOTween 的 <c>RotateMode.Fast</c>，
        /// <b>不</b>支持 <c>FastBeyond360</c> 那样超过 360° 的多圈旋转。
        /// 返回可用于打断的句柄；<paramref name="duration"/> ≤ 0 或目标为空时立即到位并返回空句柄。
        /// 对应 DOTween 的 <c>transform.DORotate</c>。
        /// </summary>
        /// <param name="target">目标 Transform。</param>
        /// <param name="endEulerAngles">目标世界欧拉角（度）。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle RotateTransform(
            Transform target, Vector3 endEulerAngles, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            if (!target) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            // 此处写「字面终值」而非折算值：零时长无路径可言，且与 DOTween 表现一致。
            if (duration <= 0f)
            {
                target.eulerAngles = endEulerAngles;
                onComplete?.Invoke();
                return default;
            }

            // 起始角只在此处读一次：每帧重读 eulerAngles 会经四元数往返丢精度、产生漂移。
            var fromEuler = target.eulerAngles;
            return Start(ETweenChannel.TransformEulerAngles, target,
                fromEuler, ShortestEuler(fromEuler, endEulerAngles),
                duration, ease, unscaled, onComplete);
        }

        /// <summary>
        /// 把 <paramref name="target"/> 的 <see cref="Transform.localScale"/> 平滑缩放到 <paramref name="endScale"/>。
        /// 返回可用于打断的句柄；<paramref name="duration"/> ≤ 0 或目标为空时立即到位并返回空句柄。
        /// 对应 DOTween 的 <c>transform.DOScale</c>。
        /// </summary>
        /// <param name="target">目标 Transform。</param>
        /// <param name="endScale">目标局部缩放。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="ease">缓动类型（默认 OutQuad）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        public static ToolkitTweenHandle ScaleTransform(
            Transform target, Vector3 endScale, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            if (!target) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            if (duration <= 0f)
            {
                target.localScale = endScale;
                onComplete?.Invoke();
                return default;
            }

            return Start(ETweenChannel.TransformLocalScale, target,
                target.localScale, endScale,
                duration, ease, unscaled, onComplete);
        }

        #endregion

        #region 延时

        /// <summary>
        /// 纯延时回调：<paramref name="delay"/> 秒后触发 <paramref name="onComplete"/>，不插值任何目标。
        /// 对应 DOTween 的 <c>DOVirtual.DelayedCall</c>。
        ///
        /// <para><b>与 DOTween 的两处差异：</b>
        /// ① <paramref name="unscaled"/> 默认 <c>true</c>（DOTween 默认受 <see cref="Time.timeScale"/> 影响，
        /// 需 <c>SetUpdate(true)</c> 才不受）；
        /// ② <paramref name="delay"/> ≤ 0 时<b>同步立刻</b>触发并返回空句柄（DOTween 会推迟到下一帧）——
        /// 若调用方拿到句柄后才记进列表，请用 <c>if (h.IsActive) list.Add(h);</c> 守卫，
        /// 否则回调里的 <c>list.Remove(h)</c> 会先于 <c>Add</c> 执行、留下永不移除的僵尸条目。</para>
        ///
        /// <para>可选 <paramref name="owner"/> 用于绑定生命周期：owner 被 <c>Destroy</c> 后回调被丢弃，
        /// 且可经 <c>Kill(owner)</c> 按 owner 取消。不传则为「独立于任何对象存亡」的纯计时器（DOTween 的默认语义）。</para>
        /// </summary>
        /// <param name="delay">延时（秒）。</param>
        /// <param name="onComplete">到期回调。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="owner">可选生命周期宿主；传入且已被销毁时直接返回空句柄、不触发回调。</param>
        public static ToolkitTweenHandle DelayedCall(
            float delay, Action onComplete,
            bool unscaled = true, UnityEngine.Object owner = null)
        {
            // 传了 owner 但已被销毁：视同目标失效，直接丢弃（与其余通道「目标为空则不触发」一致）。
            if (!ReferenceEquals(owner, null) && !owner) return default;

            // 延时非正：同步立刻触发，不进入 runner。
            if (delay <= 0f)
            {
                onComplete?.Invoke();
                return default;
            }

            // Delay 通道不插值，载荷与缓动均无意义，取默认值。
            return Start(ETweenChannel.Delay, owner,
                default, default,
                delay, EToolkitEase.Linear, unscaled, onComplete);
        }

        #endregion

        #region 通用浮点

        /// <summary>
        /// 通用浮点补间：把一个值从 <paramref name="from"/> 补到 <paramref name="to"/>，每帧把插值结果交给
        /// <paramref name="onUpdate"/> 自行写回。对应 DOTween 的 <c>DOTween.To(getter, setter, to, duration)</c>。
        ///
        /// <para>用于<b>其余通道覆盖不到的目标</b>：写回对象不是 <see cref="UnityEngine.Object"/>
        /// （如第三方运行时的裸结构体 / 托管对象上的属性），或写回路径无法用固定通道表达。
        /// 内置通道（<see cref="FadeCanvasGroup"/> / <see cref="FadeGraphic"/> / <see cref="MoveTransform"/> 等）
        /// 能表达的场合请优先用它们——它们直接写字段，不经委托。</para>
        ///
        /// <para><b>不读取起始值：</b><paramref name="from"/> 由调用方给定并在起始时固定，本方法不回读目标当前值
        /// （无从回读——写回路径是个只写委托）。需要「从当前值出发」时请自行把当前值传进来。</para>
        ///
        /// <para>可选 <paramref name="owner"/> 用于绑定生命周期：owner 被 <c>Destroy</c> 后停止推进且不再回调，
        /// 且可经 <see cref="Kill(UnityEngine.Object, bool)"/> 按 owner 取消。不传则只能经返回的句柄取消
        /// （与 <see cref="DelayedCall"/> 一致）。<b>写回目标随宿主销毁时务必传 owner</b>，否则委托捕获的引用
        /// 会让作业在宿主消失后继续写一个已失效的对象。</para>
        ///
        /// <para><paramref name="onUpdate"/> 抛出的异常会被就地捕获并记录，该作业随即失效、完成回调不触发——
        /// 单个作业的故障不会打断同帧其余作业。</para>
        /// </summary>
        /// <param name="from">起始值。</param>
        /// <param name="to">目标值。</param>
        /// <param name="duration">时长（秒）。</param>
        /// <param name="onUpdate">每帧写回：参数为当前插值结果。为 null 时不启动作业。</param>
        /// <param name="ease">缓动类型（默认 OutQuad，与本类其余方法一致）。</param>
        /// <param name="unscaled">是否用 <see cref="Time.unscaledDeltaTime"/>（默认 true，暂停时仍推进）。</param>
        /// <param name="onComplete">正常完成（非打断）时回调。</param>
        /// <param name="owner">可选生命周期宿主；传入且已被销毁时直接返回空句柄、不写回也不回调。</param>
        public static ToolkitTweenHandle To(
            float from, float to, float duration, Action<float> onUpdate,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null, UnityEngine.Object owner = null)
        {
            // 无写回路径：作业没有任何可观察效果，视同目标为空，直接丢弃（与其余通道一致）。
            if (onUpdate == null) return default;

            // 传了 owner 但已被销毁：视同目标失效，直接丢弃。
            if (!ReferenceEquals(owner, null) && !owner) return default;

            // 时长非正：瞬置到终值并直接完成，不进入 runner。
            if (duration <= 0f)
            {
                onUpdate(to);
                onComplete?.Invoke();
                return default;
            }

            return Start(ETweenChannel.Custom, owner,
                new Vector4(from, 0f, 0f, 0f), new Vector4(to, 0f, 0f, 0f),
                duration, ease, unscaled, onComplete, onUpdate);
        }

        #endregion

        #region 打断

        /// <summary>
        /// 打断该目标上<b>全部</b>在途作业，返回被打断的作业数。DOTween 目标登记表的等价物，
        /// 对应 <c>target.DOKill()</c>（<paramref name="complete"/>=false）与
        /// <c>target.DOComplete()</c>（<paramref name="complete"/>=true）。
        ///
        /// <para>目标按<b>引用相等</b>匹配，而非 Unity 的 <c>==</c>——后者会把两个已销毁对象都判为 null
        /// 从而互相「相等」，用它会误杀无关作业。因此<b>已被 Destroy 的目标依然能用本方法清理自己的作业</b>。
        /// 匹配是精确的对象身份：<c>Kill(gameObject)</c> 找不到挂在该 GameObject 上的
        /// <see cref="SpriteRenderer"/> 的作业，需传组件本身（DOTween 同理）。</para>
        ///
        /// <para><paramref name="complete"/>=true 时，每个被打断的作业先瞬置终值再触发其完成回调，
        /// <b>同步、在本调用栈上</b>完成。本方法不会为此创建 runner——没有 runner 就没有作业可打断。</para>
        /// </summary>
        /// <param name="target">目标对象：CanvasGroup / Graphic / SpriteRenderer / Transform，或延时 / 通用浮点的 owner。</param>
        /// <param name="complete">true 时瞬置终值并触发完成回调；false 时静默打断（不回调）。</param>
        public static int Kill(UnityEngine.Object target, bool complete = false)
        {
            if (ReferenceEquals(target, null)) return 0;

            // 用 Instance 而非 EnsureRunner：不该因为一次 Kill 就凭空造出 [ToolkitTween] 对象。
            var runner = ToolkitTweenRunner.Instance;
            return runner ? runner.KillByTarget(target, complete) : 0;
        }

        #endregion

        #region 工具

        /// <summary>
        /// 把目标欧拉角折算为「自起始角出发、各轴独立走最短弧」的等价终点：逐轴
        /// <c>from + Mathf.DeltaAngle(from, to)</c>（结果落在 <c>(from − 180, from + 180]</c>）。
        /// 对折算后的终点直接线性插值即可让每个轴走短边；Unity 在写入 <see cref="Transform.eulerAngles"/> 时会自行归一化。
        ///
        /// <para><see cref="RotateTransform"/> 在起始时用它烘焙终值；也可供调用方自行计算最短路径终点。
        /// 注意「逐轴最短」不等于「四元数最短弧」：多轴组合旋转在欧拉空间的插值轨迹与
        /// <c>Quaternion.Slerp</c> 不同，总扫过角可能大于 <c>Quaternion.Angle</c>——DOTween 亦然。</para>
        /// </summary>
        /// <param name="fromEuler">起始欧拉角（度）。</param>
        /// <param name="toEuler">目标欧拉角（度）。</param>
        public static Vector3 ShortestEuler(Vector3 fromEuler, Vector3 toEuler) => new Vector3(
            fromEuler.x + Mathf.DeltaAngle(fromEuler.x, toEuler.x),
            fromEuler.y + Mathf.DeltaAngle(fromEuler.y, toEuler.y),
            fromEuler.z + Mathf.DeltaAngle(fromEuler.z, toEuler.z));

        #endregion

        #region 内部

        // 统一的作业启动：取池 / 填字段 / 入 runner / 出句柄。
        // 各公开方法先做完自己的空守卫与「时长 ≤ 0」快路径，再汇到这里。
        // runner 不可用（退出播放等极端情形）时丢弃回调并返回空句柄。
        private static ToolkitTweenHandle Start(
            ETweenChannel channel, UnityEngine.Object target,
            Vector4 from, Vector4 to, float duration,
            EToolkitEase ease, bool unscaled, Action onComplete,
            Action<float> onUpdate = null)
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
            job.OnUpdate   = onUpdate;   // 仅 Custom 通道使用；其余通道恒为 null（池复用时由 Reset 清空）

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

        #endregion
    }

    /// <summary>
    /// <see cref="ToolkitTween"/> 返回的作业句柄（值类型，零分配）。默认值（<c>default</c>）为无效句柄，
    /// 其 <see cref="Kill"/> / <see cref="Complete"/> 均为安全空操作。通过作业 ID 校验，避免误杀已被池复用的作业。
    ///
    /// <para>实现 <see cref="IEquatable{T}"/>，可直接放进 <c>List&lt;ToolkitTweenHandle&gt;</c> 并用
    /// <c>List.Remove(handle)</c> 移除（无装箱、无反射比较）。<b>注意</b>：所有 <c>default</c> 句柄彼此相等，
    /// 而「时长 ≤ 0」的快路径正是返回 <c>default</c>——列表登记时请用 <c>if (h.IsActive) list.Add(h);</c> 守卫。</para>
    /// </summary>
    public readonly struct ToolkitTweenHandle : IEquatable<ToolkitTweenHandle>
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

        /// <summary>
        /// 立即完成该作业：瞬置到终值并触发完成回调（<b>同步、在本调用栈上</b>）。
        /// 等价于 <c>Kill(true)</c>，对应 DOTween 的 <c>Tween.Complete()</c>。
        /// </summary>
        public void Complete() => Kill(true);

        /// <summary>与另一句柄是否指向<b>同一个作业代次</b>（作业引用相同且 ID 相同）。</summary>
        public bool Equals(ToolkitTweenHandle other) => Id == other.Id && ReferenceEquals(Job, other.Job);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ToolkitTweenHandle other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>是否指向同一个作业代次。</summary>
        public static bool operator ==(ToolkitTweenHandle a, ToolkitTweenHandle b) => a.Equals(b);

        /// <summary>是否指向不同的作业代次。</summary>
        public static bool operator !=(ToolkitTweenHandle a, ToolkitTweenHandle b) => !a.Equals(b);
    }
}
