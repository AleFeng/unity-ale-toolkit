using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 幅度（GAS Magnitude）：修饰器幅度、时长、周期的统一取值方式。三种来源：
    /// <list type="bullet">
    ///   <item><see cref="EMagnitudeKind.Scalable"/>：<c>baseValue + perLevel × (level − 1)</c>（level &lt; 1 按 1）。</item>
    ///   <item><see cref="EMagnitudeKind.AttributeBased"/>：经上下文的 <see cref="IEffectAttributeSource"/> 读目标 / 来源的属性<b>当前值</b>，
    ///         <c>coefficient × (值 + preAdd) + postAdd</c>；读不到 → 求值失败。<b>施加 / 叠层时一次性快照</b>，不做非快照追踪。</item>
    ///   <item><see cref="EMagnitudeKind.SetByCaller"/>：从 <see cref="EffectApplyRequest.SetByCaller"/> 按键取；缺键回退 <see cref="baseValue"/>。</item>
    /// </list>
    /// 曲线表（GAS CurveTable）不做；后续如需可加新 kind 而不破坏数据。
    /// </summary>
    [Serializable]
    public class EffectMagnitude
    {
        /// <summary>来源类型。</summary>
        public EMagnitudeKind kind = EMagnitudeKind.Scalable;

        /// <summary>Scalable：基数；SetByCaller：缺键时的回退值。</summary>
        public float baseValue;

        /// <summary>Scalable：每级增量（level 1 = 基数）。</summary>
        public float perLevel;

        /// <summary>AttributeBased：属性 id（由宿主约定）。</summary>
        public string attributeId;

        /// <summary>AttributeBased：从目标还是来源身上取属性。</summary>
        public EMagnitudeCaptureFrom captureFrom = EMagnitudeCaptureFrom.Target;

        /// <summary>AttributeBased：系数。</summary>
        public float coefficient = 1f;

        /// <summary>AttributeBased：乘系数前的加数。</summary>
        public float preAdd;

        /// <summary>AttributeBased：乘系数后的加数。</summary>
        public float postAdd;

        /// <summary>SetByCaller：键。</summary>
        public string setByCallerKey;

        /// <summary>
        /// 求值。AttributeBased 读不到属性（无服务 / 无主体 / 无该属性 / 未配 attributeId）→ false，由调用方决定跳过或失败；
        /// SetByCaller 缺键 → 用 <see cref="baseValue"/> 并返回 true；Scalable 恒 true。
        /// </summary>
        public bool TryEvaluate(EffectApplyRequest request, IEffectContext ctx, out float value)
        {
            int level = request != null && request.Level > 1 ? request.Level : 1;
            switch (kind)
            {
                case EMagnitudeKind.AttributeBased:
                {
                    value = 0f;
                    if (string.IsNullOrEmpty(attributeId)) return false;
                    var src = ctx?.GetService<IEffectAttributeSource>();
                    if (src == null) return false;
                    object subject = captureFrom == EMagnitudeCaptureFrom.Source ? request?.Source : request?.Target;
                    if (!src.TryGetAttribute(subject, attributeId, out float attr)) return false;
                    value = coefficient * (attr + preAdd) + postAdd;
                    return true;
                }
                case EMagnitudeKind.SetByCaller:
                {
                    if (request?.SetByCaller != null && !string.IsNullOrEmpty(setByCallerKey)
                        && request.SetByCaller.TryGetValue(setByCallerKey, out float v))
                    {
                        value = v;
                        return true;
                    }
                    value = baseValue;
                    return true;
                }
                default:
                    value = baseValue + perLevel * (level - 1);
                    return true;
            }
        }

        /// <summary>求值；失败返回 <paramref name="fallback"/>。</summary>
        public float Evaluate(EffectApplyRequest request, IEffectContext ctx, float fallback = 0f)
            => TryEvaluate(request, ctx, out float v) ? v : fallback;

        /// <summary>Scalable 且基数与每级增量都不为正（用于「有限时长但时长为 0」这类配置错误的静态判断）。</summary>
        public bool IsZeroScalable => kind == EMagnitudeKind.Scalable && baseValue <= 0f && perLevel <= 0f;

        // ── 工厂 ──────────────────────────────────────────────────────────────────

        public static EffectMagnitude Scalable(float baseValue, float perLevel = 0f)
            => new EffectMagnitude { kind = EMagnitudeKind.Scalable, baseValue = baseValue, perLevel = perLevel };

        public static EffectMagnitude SetByCaller(string key, float fallback = 0f)
            => new EffectMagnitude { kind = EMagnitudeKind.SetByCaller, setByCallerKey = key, baseValue = fallback };

        public static EffectMagnitude AttributeBased(string attributeId, float coefficient = 1f, float preAdd = 0f, float postAdd = 0f,
            EMagnitudeCaptureFrom captureFrom = EMagnitudeCaptureFrom.Target)
            => new EffectMagnitude
            {
                kind = EMagnitudeKind.AttributeBased, attributeId = attributeId, coefficient = coefficient,
                preAdd = preAdd, postAdd = postAdd, captureFrom = captureFrom,
            };

        /// <summary>深拷贝。</summary>
        public EffectMagnitude Clone() => new EffectMagnitude
        {
            kind = kind, baseValue = baseValue, perLevel = perLevel, attributeId = attributeId, captureFrom = captureFrom,
            coefficient = coefficient, preAdd = preAdd, postAdd = postAdd, setByCallerKey = setByCallerKey,
        };

        /// <summary>校验：AttributeBased 缺 attributeId、SetByCaller 缺键 → 错误。返回是否无错误。</summary>
        public bool Validate(List<string> errors, string path)
        {
            switch (kind)
            {
                case EMagnitudeKind.AttributeBased when string.IsNullOrWhiteSpace(attributeId):
                    errors?.Add($"{path}：AttributeBased 幅度未配置 attributeId");
                    return false;
                case EMagnitudeKind.SetByCaller when string.IsNullOrWhiteSpace(setByCallerKey):
                    errors?.Add($"{path}：SetByCaller 幅度未配置 setByCallerKey");
                    return false;
                default:
                    return true;
            }
        }
    }
}
