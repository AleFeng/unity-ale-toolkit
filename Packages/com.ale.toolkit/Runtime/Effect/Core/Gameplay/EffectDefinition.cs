using System;
using System.Collections.Generic;
using Ale.Condition;
using Ale.GameplayTags;

namespace Ale.Effect
{
    /// <summary>
    /// 效果定义（GAS <c>GameplayEffect</c>）：一份可复用的「效果长什么样」的配置——时长策略 / 周期 / 叠加 / 标签 / 施加条件与概率 /
    /// 修饰器 / 各阶段执行 / 线索。运行时由 <c>EffectContainer</c> 施加为实例。纯 POCO（公开字段 + List，无字典、无多态），
    /// Unity 原生序列化与 Newtonsoft 皆可往返。
    ///
    /// <para><b>放置</b>：宿主把定义放在数据库<b>顶层列表</b>、以 <see cref="id"/> 引用——本类内含 <see cref="EffectExpression"/> → 组 → 项 →
    /// <c>ConditionExpression</c> → 组 → 项 → 参数，嵌套已达 8 层，再多包两层会触及 Unity 序列化深度上限（10）。</para>
    ///
    /// <para><b>时间单位</b>由宿主决定（世界日 / 秒…）：<see cref="duration"/> / <see cref="period"/> 与容器 <c>Tick(delta)</c> 使用同一单位。</para>
    /// </summary>
    [Serializable]
    public class EffectDefinition
    {
        /// <summary>来源标记前缀（<c>effect:</c>）。</summary>
        public const string SourceTagPrefix = "effect:";

        /// <summary>校验消息里「警告」的前缀（警告不影响 <see cref="Validate"/> 的返回值）。</summary>
        public const string WarningPrefix = "警告:";

        /// <summary>稳定引用键。</summary>
        public string id;

        /// <summary>显示名（纯文本；宿主如需本地化，在自己的包装实体里另存）。</summary>
        public string displayName;

        // ── 时长 / 周期 ─────────────────────────────────────────────────────────────

        /// <summary>时长策略。</summary>
        public EDurationPolicy durationPolicy = EDurationPolicy.Instant;

        /// <summary>有限时长的总时长（宿主时间单位）；仅 <see cref="EDurationPolicy.HasDuration"/> 生效。</summary>
        public EffectMagnitude duration = new EffectMagnitude();

        /// <summary>周期（宿主时间单位）；求值 ≤ 0 = 非周期。周期结算把修饰器经 Sink 永久落地并执行 <see cref="EffectPhases.OnPeriod"/>。</summary>
        public EffectMagnitude period = new EffectMagnitude();

        /// <summary>周期效果施加时是否立即结算一次（GAS 默认 true）。</summary>
        public bool executePeriodicOnApplication = true;

        // ── 叠加 ───────────────────────────────────────────────────────────────────

        /// <summary>叠加类型。</summary>
        public EEffectStackingType stackingType = EEffectStackingType.None;

        /// <summary>最大层数（≤ 0 = 不限）。</summary>
        public int stackLimit = 1;

        public EStackDurationRefreshPolicy stackDurationRefreshPolicy = EStackDurationRefreshPolicy.RefreshOnSuccessfulApplication;
        public EStackPeriodResetPolicy     stackPeriodResetPolicy     = EStackPeriodResetPolicy.ResetOnSuccessfulApplication;
        public EStackExpirationPolicy      stackExpirationPolicy      = EStackExpirationPolicy.ClearEntireStack;

        // ── 标签 ───────────────────────────────────────────────────────────────────

        /// <summary>描述本效果的标签（免疫 / 按标签移除的匹配对象）。</summary>
        public GameplayTagContainer assetTags = new GameplayTagContainer();

        /// <summary>激活期间授予拥有者的标签（每实例一次，不按层数）。抑制期间撤回。</summary>
        public GameplayTagContainer grantedTags = new GameplayTagContainer();

        /// <summary>成功施加后，移除目标身上 assetTags 或 grantedTags 命中这些标签的其它效果。</summary>
        public GameplayTagContainer removeEffectsWithTags = new GameplayTagContainer();

        /// <summary>激活期间：新效果的 assetTags 命中任一 → 免疫（阻断施加）。</summary>
        public GameplayTagContainer grantedApplicationImmunityTags = new GameplayTagContainer();

        /// <summary>施加时对目标标签的要求。</summary>
        public GameplayTagRequirements applicationTagRequirements = new GameplayTagRequirements();

        /// <summary>激活期间对目标标签的持续要求；不满足 → 抑制（修饰器不汇流、周期冻结、撤回授予标签），时长照走。</summary>
        public GameplayTagRequirements ongoingTagRequirements = new GameplayTagRequirements();

        // ── 施加条件 / 概率 ─────────────────────────────────────────────────────────

        /// <summary>施加条件（Condition System；空 = 恒满足）。求值时 Subject = 目标。</summary>
        public ConditionExpression applicationCondition = new ConditionExpression();

        /// <summary>施加概率 [0, 1]；1 = 必中。</summary>
        public float chanceToApply = 1f;

        // ── 值 / 事 / 表现 ──────────────────────────────────────────────────────────

        /// <summary>修饰器（值侧）。</summary>
        public List<EffectModifier> modifiers = new List<EffectModifier>();

        /// <summary>各阶段执行（事侧）：阶段组的 phase 取 <see cref="EffectPhases"/> 常量或宿主自定义阶段。</summary>
        public EffectExpression executions = new EffectExpression();

        /// <summary>线索标签（表现侧）：随 Applied / Executed / Removed 通知 <see cref="IEffectCueSink"/>。</summary>
        public GameplayTagContainer cueTags = new GameplayTagContainer();

        public EffectDefinition()
        {
        }

        public EffectDefinition(string id, EDurationPolicy durationPolicy = EDurationPolicy.Instant)
        {
            this.id             = id;
            this.durationPolicy = durationPolicy;
        }

        /// <summary>是否瞬时。</summary>
        public bool IsInstant => durationPolicy == EDurationPolicy.Instant;

        /// <summary>默认来源标记（<c>effect:{id}</c>）。</summary>
        public string DefaultSourceTag => SourceTagPrefix + id;

        /// <summary>是否配置了周期（Scalable 需为正；其它来源视为已配置）。</summary>
        public bool IsPeriodConfigured => period != null && !period.IsZeroScalable;

        // ── 维护 ──────────────────────────────────────────────────────────────────

        /// <summary>深拷贝。</summary>
        public EffectDefinition Clone()
        {
            var c = new EffectDefinition
            {
                id                             = id,
                displayName                    = displayName,
                durationPolicy                 = durationPolicy,
                duration                       = duration?.Clone() ?? new EffectMagnitude(),
                period                         = period?.Clone() ?? new EffectMagnitude(),
                executePeriodicOnApplication   = executePeriodicOnApplication,
                stackingType                   = stackingType,
                stackLimit                     = stackLimit,
                stackDurationRefreshPolicy     = stackDurationRefreshPolicy,
                stackPeriodResetPolicy         = stackPeriodResetPolicy,
                stackExpirationPolicy          = stackExpirationPolicy,
                assetTags                      = assetTags?.Clone() ?? new GameplayTagContainer(),
                grantedTags                    = grantedTags?.Clone() ?? new GameplayTagContainer(),
                removeEffectsWithTags          = removeEffectsWithTags?.Clone() ?? new GameplayTagContainer(),
                grantedApplicationImmunityTags = grantedApplicationImmunityTags?.Clone() ?? new GameplayTagContainer(),
                applicationTagRequirements     = applicationTagRequirements?.Clone() ?? new GameplayTagRequirements(),
                ongoingTagRequirements         = ongoingTagRequirements?.Clone() ?? new GameplayTagRequirements(),
                applicationCondition           = applicationCondition?.Clone() ?? new ConditionExpression(),
                chanceToApply                  = chanceToApply,
                executions                     = executions?.Clone() ?? new EffectExpression(),
                cueTags                        = cueTags?.Clone() ?? new GameplayTagContainer(),
            };
            c.modifiers = new List<EffectModifier>(modifiers?.Count ?? 0);
            if (modifiers != null)
                foreach (var m in modifiers)
                    if (m != null) c.modifiers.Add(m.Clone());
            return c;
        }

        /// <summary>
        /// 归一（幂等）：补 null、标签容器归一、修饰器去 null、<b>空阶段改写为 <see cref="EffectPhases.OnApply"/></b>、
        /// <see cref="stackLimit"/> 负值归 0、<see cref="chanceToApply"/> 夹到 [0, 1]。反序列化 / 编辑后调用。
        /// </summary>
        public void Normalize()
        {
            id          = id?.Trim();
            displayName = displayName?.Trim();

            duration ??= new EffectMagnitude();
            period   ??= new EffectMagnitude();

            (assetTags                      ??= new GameplayTagContainer()).Normalize();
            (grantedTags                    ??= new GameplayTagContainer()).Normalize();
            (removeEffectsWithTags          ??= new GameplayTagContainer()).Normalize();
            (grantedApplicationImmunityTags ??= new GameplayTagContainer()).Normalize();
            (cueTags                        ??= new GameplayTagContainer()).Normalize();
            (applicationTagRequirements     ??= new GameplayTagRequirements()).Normalize();
            (ongoingTagRequirements         ??= new GameplayTagRequirements()).Normalize();

            applicationCondition ??= new ConditionExpression();
            applicationCondition.groups ??= new List<ConditionGroup>();

            modifiers ??= new List<EffectModifier>();
            modifiers.RemoveAll(m => m == null);
            foreach (var m in modifiers)
            {
                m.attributeId = m.attributeId?.Trim();
                m.magnitude ??= new EffectMagnitude();
            }

            executions ??= new EffectExpression();
            executions.groups ??= new List<EffectGroup>();
            executions.groups.RemoveAll(g => g == null);
            foreach (var g in executions.groups)
            {
                g.phase = string.IsNullOrWhiteSpace(g.phase) ? EffectPhases.OnApply : g.phase.Trim();
                g.items ??= new List<EffectItem>();
            }

            if (stackLimit < 0) stackLimit = 0;
            if (chanceToApply < 0f) chanceToApply = 0f;
            else if (chanceToApply > 1f) chanceToApply = 1f;
        }

        /// <summary>
        /// 校验。错误（返回 false）：id 空；HasDuration 但时长为 0 或不可求值；修饰器 attributeId 空 / 幅度配置缺项；标签名非法；
        /// 标签要求自相矛盾；概率越界。警告（前缀 <see cref="WarningPrefix"/>，不影响返回值）：瞬时效果配了不生效的授予标签 / 持续要求 /
        /// 周期 / 叠加 / 免疫；叠加配置等于未配；授予标签命中持续要求的禁止标签（自我抑制）；非内置阶段；空阶段；概率为 0。
        /// </summary>
        public bool Validate(List<string> errors)
        {
            bool ok = true;
            string p = "效果[" + (id ?? string.Empty) + "]";

            if (string.IsNullOrWhiteSpace(id)) { Error(errors, "效果：id 为空"); ok = false; }

            // 时长 / 周期
            if (durationPolicy == EDurationPolicy.HasDuration)
            {
                if (duration == null || duration.IsZeroScalable) { Error(errors, p + ".duration：HasDuration 但时长为 0"); ok = false; }
                else ok &= duration.Validate(errors, p + ".duration");
            }
            if (!IsInstant && period != null && period.kind != EMagnitudeKind.Scalable)
                ok &= period.Validate(errors, p + ".period");

            // 修饰器
            if (modifiers != null)
                for (int i = 0; i < modifiers.Count; i++)
                    if (modifiers[i] != null) ok &= modifiers[i].Validate(errors, p + ".modifiers[" + i + "]");

            // 标签
            ok &= ValidateTags(assetTags,                      errors, p + ".assetTags");
            ok &= ValidateTags(grantedTags,                    errors, p + ".grantedTags");
            ok &= ValidateTags(removeEffectsWithTags,          errors, p + ".removeEffectsWithTags");
            ok &= ValidateTags(grantedApplicationImmunityTags, errors, p + ".grantedApplicationImmunityTags");
            ok &= ValidateTags(cueTags,                        errors, p + ".cueTags");
            if (applicationTagRequirements != null) ok &= applicationTagRequirements.Validate(errors, p + ".applicationTagRequirements");
            if (ongoingTagRequirements     != null) ok &= ongoingTagRequirements.Validate(errors, p + ".ongoingTagRequirements");

            // 概率
            if (chanceToApply < 0f || chanceToApply > 1f) { Error(errors, p + ".chanceToApply：须在 [0, 1] 内"); ok = false; }
            else if (chanceToApply == 0f) Warn(errors, p + ".chanceToApply 为 0：永远不会施加");

            // 瞬时效果的无效配置
            if (IsInstant)
            {
                if (grantedTags != null && !grantedTags.IsEmpty)                       Warn(errors, p + "：瞬时效果的 grantedTags 不生效（无激活期）");
                if (ongoingTagRequirements != null && !ongoingTagRequirements.IsEmpty) Warn(errors, p + "：瞬时效果的 ongoingTagRequirements 不生效");
                if (IsPeriodConfigured)                                                Warn(errors, p + "：瞬时效果的 period 不生效");
                if (stackingType != EEffectStackingType.None)                          Warn(errors, p + "：瞬时效果的叠加配置不生效");
                if (grantedApplicationImmunityTags != null && !grantedApplicationImmunityTags.IsEmpty)
                    Warn(errors, p + "：瞬时效果的 grantedApplicationImmunityTags 不生效");
            }
            else
            {
                if (stackingType != EEffectStackingType.None && stackLimit == 1
                    && stackDurationRefreshPolicy == EStackDurationRefreshPolicy.NeverRefresh
                    && stackPeriodResetPolicy == EStackPeriodResetPolicy.NeverReset)
                    Warn(errors, p + "：叠加配置等于未配置（上限 1 且不刷新时长、不重置周期）");
                if (grantedTags != null && ongoingTagRequirements?.ignoreTags != null && grantedTags.HasAny(ongoingTagRequirements.ignoreTags))
                    Warn(errors, p + "：grantedTags 命中 ongoingTagRequirements.ignoreTags，效果将自我抑制");
            }

            // 执行阶段
            if (executions?.groups != null)
            {
                for (int i = 0; i < executions.groups.Count; i++)
                {
                    var g = executions.groups[i];
                    if (g == null) continue;
                    if (string.IsNullOrEmpty(g.phase))
                        Warn(errors, p + ".executions[" + i + "]：空阶段组会在每个阶段执行，Normalize() 会改写为 onApply");
                    else if (!EffectPhases.IsBuiltIn(g.phase))
                        Warn(errors, p + ".executions[" + i + "]：阶段 '" + g.phase + "' 非内置，需由宿主经 RunPhase 触发");
                }
            }

            return ok;
        }

        /// <summary>消息是否为警告。</summary>
        public static bool IsWarning(string message) => message != null && message.StartsWith(WarningPrefix, StringComparison.Ordinal);

        private static bool ValidateTags(GameplayTagContainer c, List<string> errors, string path)
        {
            if (c?.tags == null) return true;
            bool ok = true;
            for (int i = 0; i < c.tags.Count; i++)
            {
                if (GameplayTag.IsValidName(c.tags[i])) continue;
                Error(errors, $"{path}[{i}]：标签 '{c.tags[i]}' 不合法");
                ok = false;
            }
            return ok;
        }

        private static void Error(List<string> errors, string message) => errors?.Add(message);

        private static void Warn(List<string> errors, string message) => errors?.Add(WarningPrefix + message);
    }
}
