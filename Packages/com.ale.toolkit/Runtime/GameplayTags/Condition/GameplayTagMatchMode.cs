using System.Collections.Generic;
using Ale.Condition;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签集合匹配模式范式：下拉选项 + 索引常量 + 匹配函数，供 <see cref="GameplayTagsEvaluator"/> 等复用。
    ///
    /// <para><b>⚠️ <see cref="Labels"/> 是通信格式，不是 UI 文案</b>（同 <see cref="ConditionCompare.Labels"/> 的禁令）：
    /// 配置里存的是索引，<b>永远不要本地化、永远不要调整顺序</b>。</para>
    /// </summary>
    public static class GameplayTagMatchMode
    {
        /// <summary>持有任一。</summary>
        public const int Any = 0;

        /// <summary>持有全部。</summary>
        public const int All = 1;

        /// <summary>一个都不持有。</summary>
        public const int None = 2;

        /// <summary>模式下拉标签。下标即存储值。⚠️ 顺序与文案发布后冻结。</summary>
        public static readonly string[] Labels = { "任一", "全部", "皆无" };

        /// <summary>模式参数的默认 id。</summary>
        public const string DefaultParamId = "mode";

        /// <summary>构造统一的「匹配模式」参数定义（Int + <see cref="Labels"/> 下拉，存索引）。</summary>
        public static ConditionParamDef CreateModeParam(string label = "匹配", string id = DefaultParamId) =>
            new ConditionParamDef(id, ConditionParamType.Int, false, label, null, Labels);

        /// <summary>从参数表读模式。缺省 <see cref="Any"/>。</summary>
        public static int ReadMode(IReadOnlyList<ConditionParam> parameters, string id = DefaultParamId) =>
            (int)(parameters.Find(id)?.GetInt() ?? Any);

        /// <summary>
        /// 按模式判断 <paramref name="owned"/> 与 <paramref name="tags"/> 的关系。空集：Any → false，All / None → true。
        /// 未知模式按 <see cref="Any"/> 处理。
        /// </summary>
        public static bool Matches(GameplayTagCountContainer owned, GameplayTagContainer tags, int mode, bool exact)
        {
            if (owned == null) owned = Empty;
            switch (mode)
            {
                case All:  return exact ? owned.HasAllExact(tags) : owned.HasAll(tags);
                case None: return !(exact ? owned.HasAnyExact(tags) : owned.HasAny(tags));
                default:   return exact ? owned.HasAnyExact(tags) : owned.HasAny(tags);
            }
        }

        // 无持有者时的空集（只读用途，永不写入）。
        private static readonly GameplayTagCountContainer Empty = new GameplayTagCountContainer();
    }
}
