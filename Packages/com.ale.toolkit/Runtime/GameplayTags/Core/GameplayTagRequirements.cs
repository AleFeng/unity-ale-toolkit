using System;
using System.Collections.Generic;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签要求（GAS <c>FGameplayTagRequirements</c>）：「必须持有全部 <see cref="requireTags"/>」且「不得持有任一 <see cref="ignoreTags"/>」，
    /// 均为层级匹配。用于效果的施加 / 持续要求等轻量门槛——不需要注册表、无分配；
    /// 需要与或非组合时改用 Condition System 的 <c>Condition.GameplayTags</c> 判定器。
    /// </summary>
    [Serializable]
    public class GameplayTagRequirements
    {
        /// <summary>必须全部持有（层级）。</summary>
        public GameplayTagContainer requireTags = new GameplayTagContainer();

        /// <summary>持有任一即不满足（层级）。</summary>
        public GameplayTagContainer ignoreTags = new GameplayTagContainer();

        /// <summary>两组均为空（恒满足）。</summary>
        public bool IsEmpty => (requireTags == null || requireTags.IsEmpty) && (ignoreTags == null || ignoreTags.IsEmpty);

        /// <summary>
        /// 拥有者是否满足要求。<paramref name="owned"/> 为 null 视为空集：有 <see cref="requireTags"/> 则不满足，仅有 <see cref="ignoreTags"/> 则满足。
        /// </summary>
        public bool IsMet(GameplayTagCountContainer owned)
        {
            if (owned == null) return requireTags == null || requireTags.IsEmpty;
            return owned.HasAll(requireTags) && !owned.HasAny(ignoreTags);
        }

        /// <summary>同上，拥有者以配置容器表达。</summary>
        public bool IsMet(GameplayTagContainer owned)
        {
            if (owned == null) return requireTags == null || requireTags.IsEmpty;
            return owned.HasAll(requireTags) && !owned.HasAny(ignoreTags);
        }

        /// <summary>深拷贝。</summary>
        public GameplayTagRequirements Clone() => new GameplayTagRequirements
        {
            requireTags = requireTags != null ? requireTags.Clone() : new GameplayTagContainer(),
            ignoreTags  = ignoreTags  != null ? ignoreTags.Clone()  : new GameplayTagContainer(),
        };

        /// <summary>归一两组容器（补 null、去无效与重复）。</summary>
        public void Normalize()
        {
            (requireTags ??= new GameplayTagContainer()).Normalize();
            (ignoreTags  ??= new GameplayTagContainer()).Normalize();
        }

        /// <summary>
        /// 校验：非法标签名、以及 require 与 ignore 同时含同一标签（精确）——这样的要求永远无法满足。
        /// 消息写入 <paramref name="errors"/>（前缀 <paramref name="path"/>），返回是否无错误。
        /// </summary>
        public bool Validate(List<string> errors, string path)
        {
            bool ok = true;
            ok &= ValidateNames(requireTags, errors, path + ".requireTags");
            ok &= ValidateNames(ignoreTags,  errors, path + ".ignoreTags");
            if (requireTags != null && ignoreTags != null && requireTags.HasAnyExact(ignoreTags))
            {
                errors?.Add($"{path}：requireTags 与 ignoreTags 含相同标签，要求永远无法满足");
                ok = false;
            }
            return ok;
        }

        private static bool ValidateNames(GameplayTagContainer c, List<string> errors, string path)
        {
            if (c?.tags == null) return true;
            bool ok = true;
            for (int i = 0; i < c.tags.Count; i++)
            {
                if (GameplayTag.IsValidName(c.tags[i])) continue;
                errors?.Add($"{path}[{i}]：标签 '{c.tags[i]}' 不合法");
                ok = false;
            }
            return ok;
        }
    }
}
