using System.Collections.Generic;

namespace Ale.Condition
{
    /// <summary>判定器读取参数的便捷扩展。</summary>
    public static class ConditionParamExtensions
    {
        /// <summary>按 id 查参数，未找到返回 null。</summary>
        public static ConditionParam Find(this IReadOnlyList<ConditionParam> list, string id)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return list[i];
            return null;
        }
    }
}
