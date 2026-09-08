using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 执行器实现的诊断标记：多数是「静默失效」——两条发现通道都不会报错，只是悄悄不生效。
    /// 其中 <see cref="AbstractType"/> 属于**说明性**标记（见 <see cref="EffectExecutorIndex.InformationalIssues"/>），
    /// 不是需要修的问题。
    /// </summary>
    [Flags]
    public enum EExecutorIssue
    {
        None = 0,

        /// <summary>
        /// **非抽象**类实现了 <see cref="IEffectExecutor"/> 却没打 <see cref="EffectExecutorAttribute"/>：两条通道都发现不了。
        /// 抽象基类不算——特性本就该由派生类携带，基类打了也没用。
        /// </summary>
        MissingAttribute = 1 << 0,

        /// <summary>特性里的键与 <see cref="IEffectExecutor.Key"/> 不一致：特性字符串<b>从不被读取</b>，实际生效的是 Key 属性。</summary>
        AttributeKeyMismatch = 1 << 1,

        /// <summary>与别的实现同键：编辑器目录先到先得、运行时注册表后者覆盖，两边行为相反。</summary>
        DuplicateKey = 1 << 2,

        /// <summary><see cref="IEffectExecutor.Key"/> 为空：会被目录与注册表一并跳过。</summary>
        EmptyKey = 1 << 3,

        /// <summary>
        /// 抽象基类：自身不参与发现，由派生类携带特性被注册。**说明性标记，不是问题**——执行器基类本就长这样。
        /// </summary>
        AbstractType = 1 << 4,

        /// <summary>缺公开无参构造：两条通道都会跳过。</summary>
        NoDefaultCtor = 1 << 5,

        /// <summary>实例化抛异常。</summary>
        ConstructionFailed = 1 << 6,
    }

    /// <summary>一条执行器实现（含诊断与引用计数）。</summary>
    public sealed class EffectExecutorRow
    {
        /// <summary>实现类型。</summary>
        public Type Type;

        /// <summary>实际生效的键（取自实例的 <see cref="IEffectExecutor.Key"/>；无法实例化时回退特性里的键）。</summary>
        public string Key;

        /// <summary>特性 <see cref="EffectExecutorAttribute"/> 上写的键（无特性为 null）。</summary>
        public string AttributeKey;

        /// <summary>显示名（为空时回退 <see cref="Key"/>）。</summary>
        public string DisplayName;

        /// <summary>分类（为空时归入 <see cref="EffectExecutorIndex.OtherCategory"/>）。</summary>
        public string Category;

        /// <summary>所在程序集名。</summary>
        public string AssemblyName;

        /// <summary>参数 schema（无法实例化时为 null）。</summary>
        public IReadOnlyList<EffectParamDef> ParamSchema;

        /// <summary>诊断标记。</summary>
        public EExecutorIssue Issues;

        /// <summary>
        /// 是否存在**真正需要修**的问题。抽象基类这类说明性标记
        /// （<see cref="EffectExecutorIndex.InformationalIssues"/>）不算——面板据此决定是否标红 / 计入「只看有问题」。
        /// </summary>
        public bool HasProblem => (Issues & ~EffectExecutorIndex.InformationalIssues) != 0;

        /// <summary>是否会被 <see cref="EffectExecutorCatalog"/> / <c>EffectRegistry</c> 实际拾取。</summary>
        public bool Discovered;

        /// <summary>工程配置里引用本键的处数（由 <see cref="EffectExecutorIndex"/> 回填）。</summary>
        public int UsageCount;
    }

    /// <summary>一处「配置引用了某执行器键」的记录。</summary>
    public readonly struct EffectKeyUsage
    {
        /// <summary>被引用的执行器键。</summary>
        public readonly string Key;

        /// <summary>承载配置的资产（<see cref="EffectDatabase"/> 或 <see cref="EffectDefinitionAsset"/>）。</summary>
        public readonly UnityEngine.Object Asset;

        /// <summary>效果库里的效果 id（定义资产为 null）。</summary>
        public readonly string EffectId;

        /// <summary>所在阶段组的 phase（可为空 = 通配组）。</summary>
        public readonly string Phase;

        public EffectKeyUsage(string key, UnityEngine.Object asset, string effectId, string phase)
        {
            Key      = key;
            Asset    = asset;
            EffectId = effectId;
            Phase    = phase;
        }
    }

    /// <summary>
    /// 编辑期「效果实现」索引：用 <see cref="TypeCache"/> 收集全部 <see cref="IEffectExecutor"/> 实现（含被
    /// <see cref="EffectExecutorCatalog"/> <b>静默丢弃</b>的那些），逐条给出诊断；并扫描工程里的
    /// <see cref="EffectDatabase"/> / <see cref="EffectDefinitionAsset"/>，统计每个键被引用的处数与「引用了但无实现」的悬空键。
    ///
    /// <para>与 <see cref="EffectExecutorCatalog"/> 分工：后者是绘制器下拉的数据源，只保留「能用的」；本类面向排障与总览，
    /// 刻意保留「不能用的」。两者互不依赖。</para>
    ///
    /// <para><see cref="BuildRows"/> / <see cref="CollectKeys"/> / <see cref="Filter"/> 是不碰 AssetDatabase / TypeCache / GUI
    /// 的纯函数，供单测直接调用。</para>
    /// </summary>
    public static class EffectExecutorIndex
    {
        /// <summary>无分类时归入的分类名（与 <see cref="EffectExecutorCatalog"/> 的下拉分组一致）。</summary>
        public const string OtherCategory = "其它";

        /// <summary>
        /// 只作说明、并非问题的标记：抽象基类不参与发现是设计如此（`ChronicleExecutorBase` 一类），
        /// 不该标红、也不该被「只看有问题」筛出来。
        /// </summary>
        public const EExecutorIssue InformationalIssues = EExecutorIssue.AbstractType;

        private static List<EffectExecutorRow> _rows;
        private static List<EffectKeyUsage>    _usages;
        private static List<string>            _dangling;
        private static List<string>            _categories;

        /// <summary>全部实现（按 分类 → 键 排序），含无法被发现的。</summary>
        public static IReadOnlyList<EffectExecutorRow> Rows { get { EnsureBuilt(); return _rows; } }

        /// <summary>工程配置里对执行器键的全部引用。</summary>
        public static IReadOnlyList<EffectKeyUsage> Usages { get { EnsureBuilt(); return _usages; } }

        /// <summary>配置里引用了、但没有对应实现的键（运行时会报「未注册的执行器键」）。</summary>
        public static IReadOnlyList<string> DanglingKeys { get { EnsureBuilt(); return _dangling; } }

        /// <summary>出现过的分类（升序）。</summary>
        public static IReadOnlyList<string> Categories { get { EnsureBuilt(); return _categories; } }

        /// <summary>实际会被发现的实现数。</summary>
        public static int DiscoveredCount
        {
            get
            {
                EnsureBuilt();
                int n = 0;
                foreach (var r in _rows) if (r.Discovered) n++;
                return n;
            }
        }

        /// <summary>刷新缓存（脚本重编译、资产增删改后调用）。</summary>
        public static void Rebuild()
        {
            _rows = null; _usages = null; _dangling = null; _categories = null;
        }

        /// <summary>某个键的全部引用处。</summary>
        public static List<EffectKeyUsage> UsagesOf(string key)
        {
            EnsureBuilt();
            var result = new List<EffectKeyUsage>();
            if (string.IsNullOrEmpty(key)) return result;
            foreach (var u in _usages)
                if (string.Equals(u.Key, key, StringComparison.Ordinal)) result.Add(u);
            return result;
        }

        // ── 纯函数（可单测）──────────────────────────────────────────────────────

        /// <summary>
        /// 由「类型 + 特性键 + 是否带特性」三元组构建行并打诊断标记。
        /// <para>之所以把特性键当参数传进来而不是就地反射，是为了让单测能造出「特性键与 Key 属性不一致」的样本，
        /// 而不必真的给测试类打上 <see cref="EffectExecutorAttribute"/>——那会污染工程的真实执行器目录与下拉。</para>
        /// </summary>
        public static List<EffectExecutorRow> BuildRows(
            IEnumerable<(Type type, string attributeKey, bool hasAttribute)> candidates)
        {
            var rows = new List<EffectExecutorRow>();
            if (candidates == null) return rows;

            foreach (var c in candidates)
            {
                if (c.type == null) continue;
                rows.Add(BuildRow(c.type, c.attributeKey, c.hasAttribute));
            }

            // 同键：所有行都打 DuplicateKey；按候选顺序第一个「其它方面都合格」的胜出（与目录的先到先得一致）。
            var byKey = new Dictionary<string, List<EffectExecutorRow>>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.Key)) continue;
                if (!byKey.TryGetValue(r.Key, out var list)) byKey[r.Key] = list = new List<EffectExecutorRow>();
                list.Add(r);
            }

            foreach (var kv in byKey)
            {
                var list = kv.Value;
                if (list.Count > 1)
                    foreach (var r in list) r.Issues |= EExecutorIssue.DuplicateKey;

                bool winnerTaken = false;
                foreach (var r in list)
                {
                    bool usable = (r.Issues & BlockingIssues) == 0;
                    r.Discovered = usable && !winnerTaken;
                    if (r.Discovered) winnerTaken = true;
                }
            }
            return rows;
        }

        /// <summary>遍历定义的全部阶段组 / 效果项，对每个非空执行器键回调 <c>(key, phase)</c>。</summary>
        public static void CollectKeys(EffectDefinition def, Action<string, string> onKey)
        {
            var groups = def?.executions?.groups;
            if (groups == null || onKey == null) return;

            foreach (var g in groups)
            {
                if (g?.items == null) continue;
                foreach (var item in g.items)
                {
                    if (item == null || string.IsNullOrEmpty(item.key)) continue;
                    onKey(item.key, g.phase);
                }
            }
        }

        /// <summary>按搜索词（键 / 显示名 / 分类 / 类型全名 / 程序集，忽略大小写）、分类、「只看有问题」过滤。</summary>
        public static List<EffectExecutorRow> Filter(IReadOnlyList<EffectExecutorRow> rows,
            string term, string category, bool onlyIssues)
        {
            var result = new List<EffectExecutorRow>();
            if (rows == null) return result;
            bool hasTerm = !string.IsNullOrEmpty(term);

            foreach (var r in rows)
            {
                if (r == null) continue;
                if (onlyIssues && !r.HasProblem) continue;
                if (!string.IsNullOrEmpty(category) && !string.Equals(r.Category, category, StringComparison.Ordinal)) continue;
                if (hasTerm && !Matches(r, term)) continue;
                result.Add(r);
            }
            return result;
        }

        // ── 内部 ──────────────────────────────────────────────────────────────────

        /// <summary>使实现「无法被发现」的问题（不含 DuplicateKey——重复中的胜者仍会被发现）。</summary>
        private const EExecutorIssue BlockingIssues =
            EExecutorIssue.MissingAttribute | EExecutorIssue.EmptyKey | EExecutorIssue.AbstractType |
            EExecutorIssue.NoDefaultCtor | EExecutorIssue.ConstructionFailed;

        private static EffectExecutorRow BuildRow(Type type, string attributeKey, bool hasAttribute)
        {
            var row = new EffectExecutorRow
            {
                Type         = type,
                AttributeKey = attributeKey,
                AssemblyName = type.Assembly.GetName().Name,
                Issues       = EExecutorIssue.None,
            };

            // 抽象基类不打特性是正常的（特性由派生类携带），不算漏打。
            if (!hasAttribute && !type.IsAbstract) row.Issues |= EExecutorIssue.MissingAttribute;

            bool constructed = false;
            if (type.IsAbstract) row.Issues |= EExecutorIssue.AbstractType;
            else if (type.GetConstructor(Type.EmptyTypes) == null) row.Issues |= EExecutorIssue.NoDefaultCtor;
            else
            {
                try
                {
                    if (Activator.CreateInstance(type) is IEffectExecutor inst)
                    {
                        row.Key         = inst.Key;
                        row.DisplayName = inst.DisplayName;
                        row.Category    = inst.Category;
                        row.ParamSchema = inst.ParamSchema;
                        constructed     = true;
                    }
                    else row.Issues |= EExecutorIssue.ConstructionFailed;
                }
                catch
                {
                    row.Issues |= EExecutorIssue.ConstructionFailed;
                }
            }

            if (constructed)
            {
                if (string.IsNullOrEmpty(row.Key))
                    row.Issues |= EExecutorIssue.EmptyKey;
                else if (hasAttribute && !string.IsNullOrEmpty(attributeKey)
                         && !string.Equals(attributeKey, row.Key, StringComparison.Ordinal))
                    row.Issues |= EExecutorIssue.AttributeKeyMismatch;
            }

            if (string.IsNullOrEmpty(row.Key))         row.Key         = attributeKey ?? string.Empty;
            if (string.IsNullOrEmpty(row.DisplayName)) row.DisplayName = string.IsNullOrEmpty(row.Key) ? type.Name : row.Key;
            if (string.IsNullOrEmpty(row.Category))    row.Category    = OtherCategory;

            row.Discovered = (row.Issues & BlockingIssues) == 0;   // 同键胜负在 BuildRows 里定夺
            return row;
        }

        private static bool Matches(EffectExecutorRow r, string term)
            => Contains(r.Key, term) || Contains(r.DisplayName, term) || Contains(r.Category, term)
               || Contains(r.Type != null ? r.Type.FullName : null, term) || Contains(r.AssemblyName, term);

        private static bool Contains(string s, string term)
            => !string.IsNullOrEmpty(s) && s.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void EnsureBuilt()
        {
            if (_rows != null) return;

            // ① 类型：两个 TypeCache 视角求并集。按接口找 → 能看见「漏打特性」的实现（目录看不见它们，正是要报的问题）；
            //    按特性找 → 与目录 / 运行时注册表的发现口径对齐。取并集才能两头都不漏。
            var seen       = new HashSet<Type>();
            var candidates = new List<(Type, string, bool)>();

            foreach (var t in TypeCache.GetTypesDerivedFrom<IEffectExecutor>()) AddCandidate(t, seen, candidates);
            foreach (var t in TypeCache.GetTypesWithAttribute<EffectExecutorAttribute>()) AddCandidate(t, seen, candidates);

            _rows = BuildRows(candidates);
            _rows.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Category ?? "", b.Category ?? "");
                if (c != 0) return c;
                c = string.CompareOrdinal(a.Key ?? "", b.Key ?? "");
                return c != 0 ? c : string.CompareOrdinal(a.Type?.FullName ?? "", b.Type?.FullName ?? "");
            });

            var categories = new List<string>();
            foreach (var r in _rows) if (!categories.Contains(r.Category)) categories.Add(r.Category);
            categories.Sort(StringComparer.Ordinal);
            _categories = categories;

            // ② 引用：扫工程里的效果库与定义资产
            _usages = new List<EffectKeyUsage>();
            var countByKey = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var guid in AssetDatabase.FindAssets("t:EffectDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<EffectDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (!db) continue;
                foreach (var entry in db.Effects)
                {
                    if (entry == null) continue;
                    string effectId = entry.id;
                    CollectKeys(entry.definition, (key, phase) => AddUsage(key, db, effectId, phase, countByKey));
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:EffectDefinitionAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EffectDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!asset) continue;
                CollectKeys(asset.Definition, (key, phase) => AddUsage(key, asset, null, phase, countByKey));
            }

            // ③ 回填计数 + 悬空键
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in _rows)
            {
                r.UsageCount = !string.IsNullOrEmpty(r.Key) && countByKey.TryGetValue(r.Key, out int n) ? n : 0;
                if (r.Discovered && !string.IsNullOrEmpty(r.Key)) known.Add(r.Key);
            }

            _dangling = new List<string>();
            foreach (var kv in countByKey) if (!known.Contains(kv.Key)) _dangling.Add(kv.Key);
            _dangling.Sort(StringComparer.Ordinal);
        }

        private static void AddCandidate(Type t, HashSet<Type> seen, List<(Type, string, bool)> into)
        {
            if (t == null || t.IsInterface) return;
            if (!typeof(IEffectExecutor).IsAssignableFrom(t)) return;
            if (!seen.Add(t)) return;

            var attr = (EffectExecutorAttribute)Attribute.GetCustomAttribute(t, typeof(EffectExecutorAttribute), false);
            into.Add((t, attr?.Key, attr != null));
        }

        private static void AddUsage(string key, UnityEngine.Object asset, string effectId, string phase,
            Dictionary<string, int> countByKey)
        {
            _usages.Add(new EffectKeyUsage(key, asset, effectId, phase));
            countByKey.TryGetValue(key, out int n);
            countByKey[key] = n + 1;
        }
    }

    /// <summary>资产增删改（.asset）后失效执行器索引的引用统计。</summary>
    internal sealed class EffectExecutorIndexPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(movedTo)) EffectExecutorIndex.Rebuild();
        }

        private static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            foreach (var p in paths)
                if (p != null && p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
