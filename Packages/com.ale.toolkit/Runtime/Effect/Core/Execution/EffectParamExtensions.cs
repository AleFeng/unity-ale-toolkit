using System.Collections.Generic;

namespace Ale.Effect
{
    /// <summary>执行器读取参数的便捷扩展。</summary>
    public static class EffectParamExtensions
    {
        /// <summary>按 id 查参数，未找到返回 null。</summary>
        public static EffectParam Find(this IReadOnlyList<EffectParam> list, string id)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return list[i];
            return null;
        }
    }
}
