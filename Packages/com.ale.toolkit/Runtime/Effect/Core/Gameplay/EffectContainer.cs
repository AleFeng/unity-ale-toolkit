using System;
using System.Collections.Generic;
using Ale.Condition;
using Ale.GameplayTags;
using Ale.Modifier;

namespace Ale.Effect
{
    /// <summary>
    /// 效果容器（GAS <c>AbilitySystemComponent</c> 的效果部分）：一个拥有者身上全部活动效果的运行时——施加管线（免疫 → 标签要求 → 条件 → 概率 →
    /// 瞬时落地 / 叠加 / 新实例）、<see cref="Tick"/>（周期 / 到期）、抑制（持续标签要求）、按标签 / 来源 / id 移除、修饰器汇流、存档。纯 C#。
    ///
    /// <para><b>值 / 事分工</b>：持续 / 无限效果的修饰器经 <see cref="CollectModifiers"/> 交给宿主的属性汇流（临时、可撤）；
    /// 瞬时效果与周期结算的修饰器经 <see cref="IEffectAttributeSink"/> 永久落地——<b>周期效果的修饰器不参与 <see cref="CollectModifiers"/></b>，
    /// 否则「每周期 +10 且持续 +10」会被双算。各阶段执行（<see cref="EffectPhases"/>）经 <see cref="EffectRunner"/> 派发给执行器。</para>
    ///
    /// <para><b>标签</b>：<see cref="OwnedTags"/> = 效果授予标签 + 宿主松散标签（<see cref="AddLooseTag"/>）+ 宿主直接写入的标签。
    /// 任何标签变化都会重评各效果的持续要求；抑制中的效果：修饰器不汇流、周期冻结、授予标签撤回，<b>时长照走</b>。</para>
    ///
    /// <para><b>时间单位</b>由宿主决定：<see cref="Tick"/> 的 delta 与定义里的时长 / 周期同单位（世界日 / 秒…）。主线程使用。</para>
    /// </summary>
    public sealed class EffectContainer
    {
        /// <summary>单次 Tick 内一个效果最多结算的周期数（防周期过密死循环）。</summary>
        public const int MaxPeriodFiresPerTick = 1000;

        private static readonly Random Rng = new Random();

        private readonly List<ActiveEffect> _effects = new List<ActiveEffect>();
        private readonly List<ActiveEffect> _scratch = new List<ActiveEffect>();
        private readonly Dictionary<GameplayTag, int> _loose = new Dictionary<GameplayTag, int>();
        private int  _nextHandle = 1;
        private int  _suspendTagEvents;
        private bool _reevaluating;
        private bool _ticking;

        public EffectContainer(object owner)
        {
            Owner     = owner;
            OwnedTags = new GameplayTagCountContainer();
            OwnedTags.OnTagCountChanged += OnOwnedTagChanged;
        }

        /// <summary>拥有者（施加时的目标；宿主自定义对象或 id）。</summary>
        public object Owner { get; }

        /// <summary>当前持有的标签（授予 + 松散 + 宿主直写）。宿主直接改动它也会触发抑制重评。</summary>
        public GameplayTagCountContainer OwnedTags { get; }

        /// <summary>活动效果（施加顺序）。</summary>
        public IReadOnlyList<ActiveEffect> ActiveEffects => _effects;

        /// <summary>活动效果数。</summary>
        public int Count => _effects.Count;

        /// <summary>执行器注册表；null → <see cref="EffectRegistry.Default"/>（并兜底自动注册）。</summary>
        public EffectRegistry EffectRegistry { get; set; }

        /// <summary>判定器注册表；null → <see cref="ConditionRegistry.Default"/>（并兜底自动注册）。</summary>
        public ConditionRegistry ConditionRegistry { get; set; }

        /// <summary>诊断钩子（缺 Sink、幅度求值失败、周期过密、抑制振荡、存档定义缺失…）。</summary>
        public Action<string> Warning;

        /// <summary>无 <see cref="IEffectRandomSource"/> 服务时的随机回退（[0, 1)）；测试可置为常量。</summary>
        public static Func<float> DefaultRandom { get; set; } = () => (float)Rng.NextDouble();

        // ── 事件 ──────────────────────────────────────────────────────────────────

        /// <summary>新实例建立后。</summary>
        public event Action<ActiveEffect> OnEffectAdded;

        /// <summary>实例移除后（到期 / 显式 / 被标签移除）。</summary>
        public event Action<ActiveEffect> OnEffectRemoved;

        /// <summary>周期结算后。</summary>
        public event Action<ActiveEffect> OnPeriodicExecuted;

        /// <summary>层数变化（effect, 旧层数）。</summary>
        public event Action<ActiveEffect, int> OnEffectStackChanged;

        /// <summary>抑制状态变化（effect, 是否抑制）。</summary>
        public event Action<ActiveEffect, bool> OnEffectInhibitedChanged;

        /// <summary>任何影响 <see cref="CollectModifiers"/> 结果的变化（宿主据此失效属性缓存）。</summary>
        public event Action OnModifiersChanged;

        // ── 施加 ──────────────────────────────────────────────────────────────────

        /// <summary>便捷施加：以定义 + 等级 / 来源 / 来源标记构造请求。</summary>
        public EffectApplyResult ApplyEffect(EffectDefinition definition, IEffectContext ctx, int level = 1, object source = null, string sourceTag = null)
            => ApplyEffect(new EffectApplyRequest(definition, level) { Source = source, SourceTag = sourceTag }, ctx);

        /// <summary>
        /// 施加管线：免疫 → 施加标签要求 → 施加条件 → 概率 → 时长求值 → 瞬时落地 / 叠加 / 新实例 → 按标签移除他者。
        /// 请求的 <see cref="EffectApplyRequest.Definition"/> 必须非空（按 id 施加请走 <see cref="EffectApplier"/>）。
        /// </summary>
        public EffectApplyResult ApplyEffect(EffectApplyRequest request, IEffectContext ctx)
        {
            if (request?.Definition == null) return EffectApplyResult.Blocked(EEffectApplyOutcome.Invalid, "效果定义为空");
            var def = request.Definition;
            if (request.Level < 1) request.Level = 1;
            request.Target = Owner;
            string sourceTag = request.ResolveSourceTag();

            // ① 免疫：已激活且未抑制效果的免疫标签命中来者的 assetTags
            if (def.assetTags != null && !def.assetTags.IsEmpty)
            {
                for (int i = 0; i < _effects.Count; i++)
                {
                    var e = _effects[i];
                    if (!e.IsActive || e.IsInhibited) continue;
                    var immunity = e.Definition.grantedApplicationImmunityTags;
                    if (immunity != null && !immunity.IsEmpty && def.assetTags.HasAny(immunity))
                        return EffectApplyResult.Blocked(EEffectApplyOutcome.BlockedByImmunity, "被效果 " + e.Definition.id + " 免疫");
                }
            }

            // ② 施加标签要求
            if (def.applicationTagRequirements != null && !def.applicationTagRequirements.IsEmpty
                && !def.applicationTagRequirements.IsMet(OwnedTags))
                return EffectApplyResult.Blocked(EEffectApplyOutcome.BlockedByTagRequirements, "不满足施加标签要求");

            // ③ 施加条件（主体 = 目标）
            if (def.applicationCondition != null && !def.applicationCondition.IsEmpty)
            {
                var condCtx = new EffectExecutionContext(ctx, def, null, request.Source, Owner, request.Level, null, request.SetByCaller);
                if (!ConditionEngine.Evaluate(def.applicationCondition, condCtx, ResolveConditionRegistry()).Passed)
                    return EffectApplyResult.Blocked(EEffectApplyOutcome.BlockedByCondition, "不满足施加条件");
            }

            // ④ 概率
            if (def.chanceToApply < 1f)
            {
                if (def.chanceToApply <= 0f) return EffectApplyResult.Blocked(EEffectApplyOutcome.BlockedByChance, "施加概率为 0");
                float roll = ctx?.GetService<IEffectRandomSource>()?.NextUnit() ?? DefaultRandom();
                if (roll >= def.chanceToApply) return EffectApplyResult.Blocked(EEffectApplyOutcome.BlockedByChance, "概率未命中");
            }

            // ⑤ 时长 / 周期求值
            float duration = -1f;
            if (def.durationPolicy == EDurationPolicy.HasDuration)
            {
                if (def.duration == null || !def.duration.TryEvaluate(request, ctx, out duration) || duration <= 0f)
                    return EffectApplyResult.Blocked(EEffectApplyOutcome.Invalid, "有限时长效果的时长不可求值或 ≤ 0");
            }
            float period = 0f;
            if (!def.IsInstant && def.period != null && def.period.TryEvaluate(request, ctx, out float p) && p > 0f)
                period = p;

            // ⑥ 瞬时：永久落地 + onApply，不入容器
            if (def.IsInstant)
            {
                var mags = EvaluateMagnitudes(def, request, ctx);
                ApplyPermanentInstant(def, mags, sourceTag, ctx);
                var execCtx = new EffectExecutionContext(ctx, def, null, request.Source, Owner, request.Level, EffectPhases.OnApply, request.SetByCaller);
                RunPhase(def, execCtx, EffectPhases.OnApply);
                Cue(def, EEffectCueEvent.Executed, execCtx, ctx);
                RemoveEffectsWithTags(def.removeEffectsWithTags, ctx, null);
                return new EffectApplyResult(EEffectApplyOutcome.Applied);
            }

            // ⑦ 叠加到既有实例
            var existing = FindStackTarget(def, sourceTag);
            if (existing != null)
            {
                bool atLimit = def.stackLimit > 0 && existing.Stacks >= def.stackLimit;
                int  old     = existing.Stacks;
                if (!atLimit) existing.Stacks++;
                existing.Level  = request.Level;
                existing.Source = request.Source ?? existing.Source;
                existing.MergeSetByCaller(request.SetByCaller);
                existing.SetBaseMagnitudes(EvaluateMagnitudes(def, request, ctx));

                if (def.durationPolicy == EDurationPolicy.HasDuration)
                {
                    existing.Duration = duration;
                    if (def.stackDurationRefreshPolicy == EStackDurationRefreshPolicy.RefreshOnSuccessfulApplication)
                        existing.Remaining = duration;
                }
                existing.Period = period;
                bool periodReset = def.stackPeriodResetPolicy == EStackPeriodResetPolicy.ResetOnSuccessfulApplication;
                if (periodReset) existing.PeriodTimer = period;

                OnEffectStackChanged?.Invoke(existing, old);
                OnModifiersChanged?.Invoke();

                var execCtx = Exec(existing, EffectPhases.OnStack, ctx);
                RunPhase(def, execCtx, EffectPhases.OnStack);
                Cue(def, EEffectCueEvent.Applied, execCtx, ctx);
                if (existing.IsActive && periodReset && period > 0f && def.executePeriodicOnApplication && !existing.IsInhibited)
                    ExecutePeriod(existing, ctx);
                RemoveEffectsWithTags(def.removeEffectsWithTags, ctx, existing);
                return new EffectApplyResult(atLimit ? EEffectApplyOutcome.Refreshed : EEffectApplyOutcome.Stacked, existing.Handle, existing.Stacks);
            }

            // ⑧ 新实例
            var effect = new ActiveEffect(_nextHandle++, def, Owner, request.Source, sourceTag, request.Level, request.SetByCaller)
            {
                Duration    = duration,
                Remaining   = duration,   // Infinite 时为 −1
                Period      = period,
                PeriodTimer = period,
            };
            effect.SetBaseMagnitudes(EvaluateMagnitudes(def, request, ctx));
            _effects.Add(effect);

            GrantTags(def.grantedTags);
            effect.IsInhibited = !RequirementsMet(def);
            if (effect.IsInhibited) RevokeTags(def.grantedTags);

            OnEffectAdded?.Invoke(effect);
            OnModifiersChanged?.Invoke();

            var applyCtx = Exec(effect, EffectPhases.OnApply, ctx);
            RunPhase(def, applyCtx, EffectPhases.OnApply);
            Cue(def, EEffectCueEvent.Applied, applyCtx, ctx);
            if (effect.IsActive && period > 0f && def.executePeriodicOnApplication && !effect.IsInhibited)
                ExecutePeriod(effect, ctx);

            ReevaluateInhibition();
            RemoveEffectsWithTags(def.removeEffectsWithTags, ctx, effect);
            return new EffectApplyResult(EEffectApplyOutcome.Applied, effect.Handle, effect.Stacks);
        }

        // ── 移除 ──────────────────────────────────────────────────────────────────

        /// <summary>按句柄移除：<paramref name="stacksToRemove"/> &lt; 0 或 ≥ 层数 → 整个实例；否则减层。返回是否有改动。</summary>
        public bool RemoveEffect(int handle, IEffectContext ctx, int stacksToRemove = -1)
        {
            if (!TryGet(handle, out var e)) return false;
            if (stacksToRemove == 0) return false;
            if (stacksToRemove < 0 || stacksToRemove >= e.Stacks)
            {
                RemoveInternal(e, ctx);
                return true;
            }
            int old = e.Stacks;
            e.Stacks -= stacksToRemove;
            e.RebuildScaledModifiers();
            OnEffectStackChanged?.Invoke(e, old);
            OnModifiersChanged?.Invoke();
            return true;
        }

        /// <summary>移除 assetTags 或 grantedTags 命中 <paramref name="tags"/>（层级）的全部效果，返回移除数。</summary>
        public int RemoveEffectsWithTags(GameplayTagContainer tags, IEffectContext ctx) => RemoveEffectsWithTags(tags, ctx, null);

        /// <summary>移除来源标记等于 <paramref name="sourceTag"/> 的全部效果，返回移除数。</summary>
        public int RemoveEffectsBySourceTag(string sourceTag, IEffectContext ctx)
        {
            if (string.IsNullOrEmpty(sourceTag)) return 0;
            return RemoveWhere(e => e.SourceTag == sourceTag, ctx);
        }

        /// <summary>移除定义 id 等于 <paramref name="effectId"/> 的全部效果，返回移除数。</summary>
        public int RemoveEffectsById(string effectId, IEffectContext ctx)
        {
            if (string.IsNullOrEmpty(effectId)) return 0;
            return RemoveWhere(e => e.Definition.id == effectId, ctx);
        }

        /// <summary>移除全部效果（跑 onRemove、发事件）。</summary>
        public void RemoveAll(IEffectContext ctx) => RemoveWhere(_ => true, ctx);

        /// <summary>静默清空（重置 / 读档前用）：不跑阶段、不发事件；清空全部持有标签与松散标签，句柄计数归 1。</summary>
        public void Clear()
        {
            for (int i = 0; i < _effects.Count; i++) _effects[i].IsActive = false;
            _effects.Clear();
            _loose.Clear();
            _suspendTagEvents++;
            OwnedTags.Clear();
            _suspendTagEvents--;
            _nextHandle = 1;
        }

        private int RemoveEffectsWithTags(GameplayTagContainer tags, IEffectContext ctx, ActiveEffect except)
        {
            if (tags == null || tags.IsEmpty) return 0;
            return RemoveWhere(e => e != except
                && ((e.Definition.assetTags != null && e.Definition.assetTags.HasAny(tags))
                    || (e.Definition.grantedTags != null && e.Definition.grantedTags.HasAny(tags))), ctx);
        }

        private int RemoveWhere(Predicate<ActiveEffect> predicate, IEffectContext ctx)
        {
            if (_effects.Count == 0) return 0;
            var snapshot = _effects.ToArray();
            int n = 0;
            foreach (var e in snapshot)
            {
                if (!e.IsActive || !predicate(e)) continue;
                RemoveInternal(e, ctx);
                n++;
            }
            return n;
        }

        private void RemoveInternal(ActiveEffect e, IEffectContext ctx)
        {
            if (!e.IsActive) return;
            e.IsActive = false;
            _effects.Remove(e);
            if (!e.IsInhibited) RevokeTags(e.Definition.grantedTags);

            var execCtx = Exec(e, EffectPhases.OnRemove, ctx);
            RunPhase(e.Definition, execCtx, EffectPhases.OnRemove);
            Cue(e.Definition, EEffectCueEvent.Removed, execCtx, ctx);

            OnEffectRemoved?.Invoke(e);
            OnModifiersChanged?.Invoke();
            ReevaluateInhibition();
        }

        // ── Tick ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 推进时间：对施加顺序的快照逐个处理（本次 Tick 内新施加的效果不参与本次）。<b>周期先于到期</b>：未抑制且有周期时，
        /// 周期计时按 <c>min(delta, 剩余)</c> 递减、每到点结算一次（跨多周期则多次，余数保留）；抑制中周期计时冻结；
        /// 有限时长效果随后递减剩余时长，≤ 0 按到期策略处理。<paramref name="delta"/> ≤ 0 忽略。
        /// </summary>
        public void Tick(float delta, IEffectContext ctx)
        {
            if (delta <= 0f || _effects.Count == 0) return;
            if (_ticking)
            {
                Warning?.Invoke("EffectContainer.Tick 重入（执行器内又推进了时间），已忽略");
                return;
            }
            _ticking = true;
            try
            {
                _scratch.Clear();
                _scratch.AddRange(_effects);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    var e = _scratch[i];
                    if (!e.IsActive) continue;
                    e.Elapsed += delta;

                    if (e.Period > 0f && !e.IsInhibited)
                    {
                        float budget = e.HasDuration ? Math.Min(delta, Math.Max(0f, e.Remaining)) : delta;
                        e.PeriodTimer -= budget;
                        int fires = 0;
                        while (e.PeriodTimer <= 0f && e.IsActive)
                        {
                            ExecutePeriod(e, ctx);
                            e.PeriodTimer += e.Period;
                            if (++fires >= MaxPeriodFiresPerTick)
                            {
                                Warning?.Invoke("效果 " + e.Definition.id + " 周期过密，单次 Tick 已截断");
                                e.PeriodTimer = e.Period;
                                break;
                            }
                        }
                    }

                    if (e.IsActive && e.HasDuration)
                    {
                        e.Remaining -= delta;
                        if (e.Remaining <= 0f) Expire(e, ctx);
                    }
                }
                _scratch.Clear();
            }
            finally
            {
                _ticking = false;
            }
        }

        private void ExecutePeriod(ActiveEffect e, IEffectContext ctx)
        {
            var scaled = e.ScaledModifiers;
            if (scaled.Count > 0)
            {
                var sink = ctx?.GetService<IEffectAttributeSink>();
                if (sink == null)
                    Warning?.Invoke("效果 " + e.Definition.id + "：上下文缺少 IEffectAttributeSink，周期修饰器未落地");
                else
                    for (int i = 0; i < scaled.Count; i++)
                    {
                        var m = scaled[i];
                        sink.ApplyPermanent(Owner, m.targetAttributeId, m.operation, m.magnitude, e.ModifierSourceTag);
                    }
            }
            var execCtx = Exec(e, EffectPhases.OnPeriod, ctx);
            RunPhase(e.Definition, execCtx, EffectPhases.OnPeriod);
            Cue(e.Definition, EEffectCueEvent.Executed, execCtx, ctx);
            OnPeriodicExecuted?.Invoke(e);
        }

        private void Expire(ActiveEffect e, IEffectContext ctx)
        {
            var policy = e.Definition.stackExpirationPolicy;
            if (policy == EStackExpirationPolicy.RefreshDuration)
            {
                e.Remaining = e.Duration;
                return;
            }
            if (policy == EStackExpirationPolicy.RemoveSingleStackAndRefreshDuration && e.Stacks > 1)
            {
                int old = e.Stacks;
                e.Stacks--;
                e.Remaining = e.Duration;
                e.RebuildScaledModifiers();
                OnEffectStackChanged?.Invoke(e, old);
                OnModifiersChanged?.Invoke();
                return;
            }
            RunPhase(e.Definition, Exec(e, EffectPhases.OnExpire, ctx), EffectPhases.OnExpire);
            RemoveInternal(e, ctx);
        }

        // ── 阶段 / 等级 ──────────────────────────────────────────────────────────

        /// <summary>对某活动效果执行一个（宿主自定义）阶段。</summary>
        public void RunPhase(int handle, string phase, IEffectContext ctx)
        {
            if (string.IsNullOrEmpty(phase) || !TryGet(handle, out var e)) return;
            RunPhase(e.Definition, Exec(e, phase, ctx), phase);
        }

        /// <summary>改等级并按新等级重求快照幅度（时长 / 周期不变）。</summary>
        public bool SetLevel(int handle, int level, IEffectContext ctx)
        {
            if (!TryGet(handle, out var e)) return false;
            e.Level = level < 1 ? 1 : level;
            var request = new EffectApplyRequest(e.Definition, e.Level)
            {
                Source = e.Source, Target = Owner, SourceTag = e.SourceTag,
                SetByCaller = new Dictionary<string, float>(e.SetByCaller),
            };
            e.SetBaseMagnitudes(EvaluateMagnitudes(e.Definition, request, ctx));
            OnModifiersChanged?.Invoke();
            return true;
        }

        // ── 查询 ──────────────────────────────────────────────────────────────────

        /// <summary>按句柄取活动效果。</summary>
        public bool TryGet(int handle, out ActiveEffect effect)
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Handle == handle) { effect = _effects[i]; return true; }
            effect = null;
            return false;
        }

        /// <summary>按定义 id（及可选来源标记）找首个活动效果。</summary>
        public ActiveEffect Find(string effectId, string sourceTag = null)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];
                if (e.Definition.id != effectId) continue;
                if (sourceTag != null && e.SourceTag != sourceTag) continue;
                return e;
            }
            return null;
        }

        /// <summary>某定义 id 的总层数（跨实例求和）。</summary>
        public int GetStackCount(string effectId)
        {
            int n = 0;
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i].Definition.id == effectId) n += _effects[i].Stacks;
            return n;
        }

        /// <summary>是否有活动效果的 assetTags 命中该标签（层级）。</summary>
        public bool HasEffectWithTag(GameplayTag tag)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                var at = _effects[i].Definition.assetTags;
                if (at != null && at.HasTag(tag)) return true;
            }
            return false;
        }

        /// <summary>
        /// 收集参与汇流的修饰器（激活、未抑制、<b>非周期</b>的持续 / 无限效果的缩放修饰器）；<paramref name="attributeId"/> 为 null 收集全部。
        /// 追加的是容器持有的实例，勿改动。返回追加数。
        /// </summary>
        public int CollectModifiers(string attributeId, List<ModifierDefinition> into)
        {
            if (into == null) return 0;
            int n = 0;
            for (int i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];
                if (!e.IsActive || e.IsInhibited || e.IsPeriodic) continue;
                var scaled = e.ScaledModifiers;
                for (int j = 0; j < scaled.Count; j++)
                {
                    if (attributeId != null && scaled[j].targetAttributeId != attributeId) continue;
                    into.Add(scaled[j]);
                    n++;
                }
            }
            return n;
        }

        // ── 松散标签 / 抑制 ──────────────────────────────────────────────────────

        /// <summary>宿主经容器添加标签（可存档；如特质派生标签），随后重评抑制。</summary>
        public void AddLooseTag(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count <= 0) return;
            _loose.TryGetValue(tag, out int c);
            _loose[tag] = c + count;
            _suspendTagEvents++;
            OwnedTags.AddTag(tag, count);
            _suspendTagEvents--;
            ReevaluateInhibition();
        }

        /// <summary>移除经 <see cref="AddLooseTag"/> 添加的标签，随后重评抑制。</summary>
        public void RemoveLooseTag(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count <= 0 || !_loose.TryGetValue(tag, out int c)) return;
            int removed = count < c ? count : c;
            c -= removed;
            if (c == 0) _loose.Remove(tag); else _loose[tag] = c;
            _suspendTagEvents++;
            OwnedTags.RemoveTag(tag, removed);
            _suspendTagEvents--;
            ReevaluateInhibition();
        }

        /// <summary>
        /// 重评全部活动效果的持续标签要求（GAS 抑制语义）：不满足 → 抑制（撤回授予标签）、满足 → 解除（重新授予）。
        /// 循环至收敛；标签要求互相矛盾导致振荡时告警并停止。标签变化后容器会自动调用；宿主也可显式调用。
        /// </summary>
        public void ReevaluateInhibition() => ReevaluateInhibition(true);

        private void ReevaluateInhibition(bool raiseEvents)
        {
            if (_reevaluating) return;
            _reevaluating = true;
            try
            {
                int passes = 0;
                bool changed;
                do
                {
                    changed = false;
                    for (int i = 0; i < _effects.Count; i++)
                    {
                        var e = _effects[i];
                        if (!e.IsActive) continue;
                        bool should = !RequirementsMet(e.Definition);
                        if (should == e.IsInhibited) continue;
                        e.IsInhibited = should;
                        if (should) RevokeTags(e.Definition.grantedTags);
                        else        GrantTags(e.Definition.grantedTags);
                        if (raiseEvents)
                        {
                            OnEffectInhibitedChanged?.Invoke(e, should);
                            OnModifiersChanged?.Invoke();
                        }
                        changed = true;
                    }
                    if (changed && ++passes > _effects.Count + 2)
                    {
                        Warning?.Invoke("效果抑制振荡（持续标签要求互相矛盾），已停止重评");
                        break;
                    }
                } while (changed);
            }
            finally
            {
                _reevaluating = false;
            }
        }

        private bool RequirementsMet(EffectDefinition def)
            => def.ongoingTagRequirements == null || def.ongoingTagRequirements.IsEmpty || def.ongoingTagRequirements.IsMet(OwnedTags);

        private void OnOwnedTagChanged(GameplayTag tag, int count)
        {
            if (_suspendTagEvents > 0 || _reevaluating) return;
            ReevaluateInhibition();
        }

        private void GrantTags(GameplayTagContainer tags)
        {
            if (tags == null || tags.IsEmpty) return;
            _suspendTagEvents++;
            OwnedTags.AddTags(tags);
            _suspendTagEvents--;
        }

        private void RevokeTags(GameplayTagContainer tags)
        {
            if (tags == null || tags.IsEmpty) return;
            _suspendTagEvents++;
            OwnedTags.RemoveTags(tags);
            _suspendTagEvents--;
        }

        // ── 存档 ──────────────────────────────────────────────────────────────────

        /// <summary>导出全部活动效果与松散标签（深拷贝）。</summary>
        public EffectContainerState ExportState()
        {
            var st = new EffectContainerState { nextHandle = _nextHandle };
            for (int i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];
                if (!e.IsActive) continue;
                var s = new ActiveEffectState
                {
                    effectId = e.Definition.id, handle = e.Handle, level = e.Level, stacks = e.Stacks,
                    duration = e.Duration, remaining = e.Remaining, period = e.Period, periodTimer = e.PeriodTimer,
                    elapsed = e.Elapsed, sourceTag = e.SourceTag,
                };
                foreach (var kv in e.SetByCaller)
                {
                    s.setByCallerKeys.Add(kv.Key);
                    s.setByCallerValues.Add(kv.Value);
                }
                s.baseMagnitudes.AddRange(e.BaseMagnitudes);
                st.effects.Add(s);
            }
            foreach (var kv in _loose)
            {
                st.looseTags.Add(kv.Key.Name);
                st.looseTagCounts.Add(kv.Value);
            }
            return st;
        }

        /// <summary>
        /// 从存档恢复（覆盖语义：先 <see cref="Clear"/>）。定义按 <paramref name="definitions"/> → <see cref="EffectDefinitionRegistry.Default"/> 解析，
        /// 找不到的条目跳过并告警；快照幅度数与定义不符时按当前上下文重求并告警。重新授予标签、末尾静默重算抑制；
        /// <b>全程不跑阶段、不发事件</b>（与 ISaveable 契约一致）。
        /// </summary>
        public void ImportState(EffectContainerState state, IEffectDefinitionSource definitions, IEffectContext ctx)
        {
            Clear();
            if (state == null) return;

            _suspendTagEvents++;
            try
            {
                if (state.looseTags != null)
                {
                    for (int i = 0; i < state.looseTags.Count; i++)
                    {
                        var tag = new GameplayTag(state.looseTags[i]);
                        int count = state.looseTagCounts != null && i < state.looseTagCounts.Count ? state.looseTagCounts[i] : 1;
                        if (!tag.IsValid || count <= 0) continue;
                        _loose.TryGetValue(tag, out int c);
                        _loose[tag] = c + count;
                        OwnedTags.AddTag(tag, count);
                    }
                }

                int maxHandle = 0;
                if (state.effects != null)
                {
                    foreach (var s in state.effects)
                    {
                        if (s == null || string.IsNullOrEmpty(s.effectId)) continue;
                        var def = definitions?.GetEffect(s.effectId) ?? EffectDefinitionRegistry.Default.GetEffect(s.effectId);
                        if (def == null)
                        {
                            Warning?.Invoke("存档效果 '" + s.effectId + "' 找不到定义，已跳过");
                            continue;
                        }
                        if (def.IsInstant)
                        {
                            Warning?.Invoke("存档效果 '" + s.effectId + "' 的定义已改为瞬时，已跳过");
                            continue;
                        }

                        Dictionary<string, float> sbc = null;
                        if (s.setByCallerKeys != null && s.setByCallerValues != null)
                        {
                            sbc = new Dictionary<string, float>();
                            for (int i = 0; i < s.setByCallerKeys.Count && i < s.setByCallerValues.Count; i++)
                                if (!string.IsNullOrEmpty(s.setByCallerKeys[i])) sbc[s.setByCallerKeys[i]] = s.setByCallerValues[i];
                        }

                        int handle = s.handle > 0 ? s.handle : ++maxHandle;
                        var e = new ActiveEffect(handle, def, Owner, null, s.sourceTag, s.level, sbc)
                        {
                            Duration = s.duration, Remaining = s.remaining, Period = s.period, PeriodTimer = s.periodTimer,
                            Elapsed = s.elapsed, Stacks = s.stacks < 1 ? 1 : s.stacks,
                        };
                        int modCount = def.modifiers?.Count ?? 0;
                        if (s.baseMagnitudes != null && s.baseMagnitudes.Count == modCount)
                        {
                            e.SetBaseMagnitudes(s.baseMagnitudes);
                        }
                        else
                        {
                            Warning?.Invoke("存档效果 '" + s.effectId + "' 的幅度数与定义不符，已按当前上下文重求");
                            var req = new EffectApplyRequest(def, e.Level) { Target = Owner, SourceTag = s.sourceTag, SetByCaller = sbc };
                            e.SetBaseMagnitudes(EvaluateMagnitudes(def, req, ctx));
                        }
                        _effects.Add(e);
                        if (def.grantedTags != null) OwnedTags.AddTags(def.grantedTags);
                        if (handle > maxHandle) maxHandle = handle;
                    }
                }
                _nextHandle = Math.Max(state.nextHandle, maxHandle + 1);
                if (_nextHandle < 1) _nextHandle = 1;
            }
            finally
            {
                _suspendTagEvents--;
            }
            ReevaluateInhibition(false);
        }

        // ── 内部工具 ──────────────────────────────────────────────────────────────

        private ActiveEffect FindStackTarget(EffectDefinition def, string sourceTag)
        {
            if (def.stackingType == EEffectStackingType.None) return null;
            for (int i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];
                if (!e.IsActive || e.Definition.id != def.id) continue;
                if (def.stackingType == EEffectStackingType.AggregateBySource && e.SourceTag != sourceTag) continue;
                return e;
            }
            return null;
        }

        private List<float> EvaluateMagnitudes(EffectDefinition def, EffectApplyRequest request, IEffectContext ctx)
        {
            var mods = def.modifiers;
            var list = new List<float>(mods?.Count ?? 0);
            if (mods == null) return list;
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m?.magnitude != null && m.magnitude.TryEvaluate(request, ctx, out float v))
                {
                    list.Add(v);
                }
                else
                {
                    list.Add(float.NaN);
                    Warning?.Invoke("效果 " + def.id + " 修饰器[" + i + "]（" + m?.attributeId + "）幅度求值失败，已跳过");
                }
            }
            return list;
        }

        private void ApplyPermanentInstant(EffectDefinition def, List<float> mags, string sourceTag, IEffectContext ctx)
        {
            var mods = def.modifiers;
            if (mods == null || mods.Count == 0) return;
            var sink = ctx?.GetService<IEffectAttributeSink>();
            if (sink == null)
            {
                Warning?.Invoke("效果 " + def.id + "：上下文缺少 IEffectAttributeSink，瞬时修饰器未落地");
                return;
            }
            for (int i = 0; i < mods.Count && i < mags.Count; i++)
            {
                var m = mods[i];
                if (m == null || ActiveEffect.IsSkipped(mags[i])) continue;
                sink.ApplyPermanent(Owner, m.attributeId, m.operation, mags[i], sourceTag);
            }
        }

        private EffectExecutionContext Exec(ActiveEffect e, string phase, IEffectContext ctx)
            => new EffectExecutionContext(ctx, e.Definition, e, e.Source, Owner, e.Level, phase, e.SetByCaller);

        private void RunPhase(EffectDefinition def, EffectExecutionContext execCtx, string phase)
        {
            if (def.executions == null || def.executions.IsEmpty) return;
            EffectRunner.Run(def.executions, execCtx, phase, ResolveEffectRegistry(), ResolveConditionRegistry());
        }

        private void Cue(EffectDefinition def, EEffectCueEvent cueEvent, EffectExecutionContext info, IEffectContext ctx)
        {
            var cues = def.cueTags;
            if (cues?.tags == null || cues.IsEmpty) return;
            var sink = ctx?.GetService<IEffectCueSink>();
            if (sink == null) return;
            for (int i = 0; i < cues.tags.Count; i++)
            {
                var tag = new GameplayTag(cues.tags[i]);
                if (tag.IsValid) sink.OnCue(tag, cueEvent, info);
            }
        }

        private EffectRegistry ResolveEffectRegistry()
        {
            if (EffectRegistry != null) return EffectRegistry;
            var reg = EffectRegistry.Default;
            reg.EnsureAutoRegistered();
            return reg;
        }

        private ConditionRegistry ResolveConditionRegistry()
        {
            if (ConditionRegistry != null) return ConditionRegistry;
            var reg = ConditionRegistry.Default;
            reg.EnsureAutoRegistered();
            return reg;
        }
    }
}
