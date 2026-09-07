using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ale.GameplayTags
{
    /// <summary>
    /// 标签表资产：项目级「声明本项目有哪些 Gameplay 标签」的载体（可多份，按模块拆分）。
    /// 放在任意 <c>Resources</c> 目录下即被 <see cref="GameplayTagRuntime"/> 在启动时自动登记；放在别处则由宿主显式
    /// <see cref="GameplayTagRuntime.Register(GameplayTagTable)"/>。编辑器目录（下拉树 / 校验）会扫描工程内全部本类资产，与放置位置无关。
    /// </summary>
    /// <remarks>层级约定：SO 字段遵循仓库常规「private + <c>[SerializeField]</c>」；条目类型 <see cref="GameplayTagDefinition"/> 为引擎无关 POCO。</remarks>
    [CreateAssetMenu(menuName = "Ale/GameplayTag/Gameplay Tag Table", fileName = "GameplayTagTable")]
    public class GameplayTagTable : ScriptableObject
    {
        [SerializeField] private List<GameplayTagDefinition> entries = new List<GameplayTagDefinition>();

        /// <summary>条目列表（编辑器直接改动；运行时只读取）。</summary>
        public List<GameplayTagDefinition> Entries => entries;

        /// <summary>把全部合法条目登记进注册表（null → <see cref="GameplayTagRegistry.Default"/>），返回新增数。</summary>
        public int RegisterInto(GameplayTagRegistry registry) => (registry ?? GameplayTagRegistry.Default).Register(entries);

        /// <summary>
        /// 校验：非法名（错误）、重复（错误）、仅大小写不同（警告，前缀「警告:」）。消息写入 <paramref name="messages"/>，返回是否无错误。
        /// </summary>
        public bool Validate(List<string> messages)
        {
            bool ok = true;
            var seen   = new Dictionary<string, int>(StringComparer.Ordinal);
            var seenCi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                string raw = entries[i]?.name;
                string n   = GameplayTag.Normalize(raw);
                if (n == null)
                {
                    messages?.Add($"[{i}]：标签 '{raw}' 不合法");
                    ok = false;
                    continue;
                }
                if (seen.TryGetValue(n, out int first))
                {
                    messages?.Add($"[{i}]：标签 '{n}' 与 [{first}] 重复");
                    ok = false;
                    continue;
                }
                seen[n] = i;
                if (seenCi.TryGetValue(n, out var other))
                    messages?.Add($"警告:[{i}]：标签 '{n}' 与 '{other}' 仅大小写不同");
                else
                    seenCi[n] = n;
            }
            return ok;
        }
    }
}
