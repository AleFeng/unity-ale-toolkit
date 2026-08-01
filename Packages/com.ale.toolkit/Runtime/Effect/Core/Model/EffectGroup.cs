using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 效果组：一个「时机 / 阶段批次」。<see cref="phase"/> 是该批次的标签（如 <c>onGained</c> / <c>onLost</c>；
    /// 空 = 通配，任意 phase 请求都会执行）。组内 <see cref="items"/> <b>按声明顺序执行</b>（无 And/Or 布尔语义）。
    /// </summary>
    [Serializable]
    public class EffectGroup
    {
        /// <summary>时机 / 阶段标签（可空 = 通配组）。运行时由 <see cref="EffectRunner.Run"/> 的 phase 过滤取用。</summary>
        public string phase;

        /// <summary>组内效果项（按序执行）。</summary>
        public List<EffectItem> items = new List<EffectItem>();

        public EffectGroup() { }

        public EffectGroup(string phase) { this.phase = phase; }

        public EffectGroup Clone()
        {
            var c = new EffectGroup { phase = phase };
            c.items = new List<EffectItem>(items.Count);
            foreach (var i in items) c.items.Add(i?.Clone());
            return c;
        }
    }
}
