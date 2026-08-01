using System;
using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Effect
{
    /// <summary>
    /// 效果表达式：<b>一级分组</b>结构（表达式 → 组 → 项 → 参数）。各组是「时机 / 阶段批次」，
    /// 组内效果项按序执行；<b>无 And/Or 布尔层</b>（对「动作」无意义，与 Condition 的两级结构在此分道）。
    /// 无多态、无 Dictionary —— Unity 原生序列化 + Undo 直接可用，Newtonsoft JSON 亦可往返。
    /// <para>空表达式（无组 / 组皆空）执行为无操作。每条效果项可挂可选 <see cref="EffectItem.gate"/> 条件门控。</para>
    /// </summary>
    [Serializable]
    public class EffectExpression
    {
        /// <summary>效果组列表（各组 = 时机 / 阶段批次；按序执行）。</summary>
        public List<EffectGroup> groups = new List<EffectGroup>();

        /// <summary>是否为空（不含任何效果项）。</summary>
        public bool IsEmpty => TotalItemCount() == 0;

        /// <summary>全部效果项总数（用于列表列 / 折叠标题摘要）。</summary>
        public int TotalItemCount()
        {
            int n = 0;
            if (groups != null)
                foreach (var g in groups)
                    if (g?.items != null) n += g.items.Count;
            return n;
        }

        public EffectExpression Clone()
        {
            var c = new EffectExpression();
            c.groups = new List<EffectGroup>(groups.Count);
            foreach (var g in groups) c.groups.Add(g?.Clone());
            return c;
        }

        /// <summary>便捷执行：转调 <see cref="EffectRunner.Run"/>。</summary>
        public EffectRunReport Run(IEffectContext ctx, string phase = null,
            EffectRegistry effectRegistry = null, ConditionRegistry conditionRegistry = null)
            => EffectRunner.Run(this, ctx, phase, effectRegistry, conditionRegistry);
    }
}
