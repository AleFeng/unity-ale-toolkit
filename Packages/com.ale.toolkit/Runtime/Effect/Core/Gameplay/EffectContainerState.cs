using System;
using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>
    /// 一条活动效果的存档形态（<c>[Serializable]</c> POCO，宿主随自身存档 DTO 序列化）。
    /// 快照幅度一并保存——AttributeBased 幅度在读档时无法重现施加当时的来源属性。抑制状态不存，导入后重算。
    /// </summary>
    [Serializable]
    public class ActiveEffectState
    {
        public string effectId;
        public int    handle;
        public int    level = 1;
        public int    stacks = 1;

        /// <summary>总时长；−1 = 无限。</summary>
        public float duration = -1f;

        /// <summary>剩余时长；−1 = 无限。</summary>
        public float remaining = -1f;

        public float period;
        public float periodTimer;
        public float elapsed;
        public string sourceTag;

        public List<string> setByCallerKeys   = new List<string>();
        public List<float>  setByCallerValues = new List<float>();

        /// <summary>每条修饰器的单层快照幅度（与定义的 modifiers 同序；被跳过的为 NaN）。</summary>
        public List<float> baseMagnitudes = new List<float>();
    }

    /// <summary>一个效果容器的存档形态：句柄计数、全部活动效果、宿主经容器添加的松散标签。</summary>
    [Serializable]
    public class EffectContainerState
    {
        public int nextHandle = 1;
        public List<ActiveEffectState> effects = new List<ActiveEffectState>();
        public List<string> looseTags      = new List<string>();
        public List<int>    looseTagCounts = new List<int>();
    }
}
