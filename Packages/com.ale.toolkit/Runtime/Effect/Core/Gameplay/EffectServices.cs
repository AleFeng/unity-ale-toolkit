using System.Collections.Generic;
using Ale.GameplayTags;
using Ale.Modifier;

namespace Ale.Effect
{
    /// <summary>效果定义来源：按 id 取定义（宿主数据库实现；多来源经 <see cref="EffectDefinitionRegistry"/> 聚合）。</summary>
    public interface IEffectDefinitionSource
    {
        /// <summary>按 id 取效果定义；未找到返回 null。</summary>
        EffectDefinition GetEffect(string id);
    }

    /// <summary>属性读侧：AttributeBased 幅度取主体属性的<b>当前值</b>（含修饰器）。</summary>
    public interface IEffectAttributeSource
    {
        /// <summary>读主体的属性当前值；主体未知 / 无该属性 → false。</summary>
        bool TryGetAttribute(object subject, string attributeId, out float value);
    }

    /// <summary>
    /// 属性写侧：瞬时效果与周期结算把修饰器<b>永久落地</b>（GAS：改基础值）。宿主自行决定落地方式
    /// （直接改基础值、或记为可追溯的永久修饰器）。语义：Add → base += m；PercentAdd / Multiply → base *= (1 + m)；Override → base = m。
    /// </summary>
    public interface IEffectAttributeSink
    {
        /// <summary>对目标的属性永久施加一次修饰。<paramref name="sourceTag"/> 供日志 / 追溯。</summary>
        void ApplyPermanent(object target, string attributeId, EModifierOperation operation, float magnitude, string sourceTag);
    }

    /// <summary>随机源：施加概率判定用；缺服务时容器用 <c>EffectContainer.DefaultRandom</c>。</summary>
    public interface IEffectRandomSource
    {
        /// <summary>返回 [0, 1) 的随机数。</summary>
        float NextUnit();
    }

    /// <summary>线索接收侧（表现层）：随效果 Applied / Executed / Removed 收到 <see cref="EffectDefinition.cueTags"/> 的通知；缺服务即无操作。</summary>
    public interface IEffectCueSink
    {
        void OnCue(GameplayTag cue, EEffectCueEvent cueEvent, IEffectExecutionInfo info);
    }

    /// <summary>
    /// 执行信息：执行器 / 条件在容器驱动下运行时，经 <c>ctx.GetService&lt;IEffectExecutionInfo&gt;()</c> 取得当前效果的定义、实例、来源 / 目标、
    /// 等级、阶段与 SetByCaller（对应 GAS 执行拿到 Spec）。
    /// </summary>
    public interface IEffectExecutionInfo
    {
        /// <summary>效果定义。</summary>
        EffectDefinition Definition { get; }

        /// <summary>活动实例（瞬时效果 → null）。</summary>
        ActiveEffect Effect { get; }

        /// <summary>施加者（可空）。</summary>
        object Source { get; }

        /// <summary>目标（容器 Owner）。</summary>
        object Target { get; }

        /// <summary>等级。</summary>
        int Level { get; }

        /// <summary>当前阶段（<see cref="EffectPhases"/> 或宿主自定义）。</summary>
        string Phase { get; }

        /// <summary>SetByCaller 键值（可空）。</summary>
        IReadOnlyDictionary<string, float> SetByCaller { get; }
    }
}
