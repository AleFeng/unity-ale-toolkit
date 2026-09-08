using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 判定器实现的诊断标记：多数是「静默失效」——两条发现通道都不会报错，只是悄悄不生效。
    /// 其中 <see cref="AbstractType"/> 属于**说明性**标记（见 <see cref="ConditionEvaluatorIndex.InformationalIssues"/>），
    /// 不是需要修的问题。与效果侧的 <c>EExecutorIssue</c> 同构。
    /// </summary>
    [Flags]
    public enum EEvaluatorIssue
    {
        None = 0,

        /// <summary>
        /// **非抽象**类实现了 <see cref="IConditionEvaluator"/> 却没打 <see cref="ConditionEvaluatorAttribute"/>：两条通道都发现不了。
        /// 抽象基类不算——特性本就该由派生类携带，基类打了也没用。
        /// </summary>
        MissingAttribute = 1 << 0,

        /// <summary>特性里的键与 <see cref="IConditionEvaluator.Key"/> 不一致：特性字符串<b>从不被读取</b>，实际生效的是 Key 属性。</summary>
        AttributeKeyMismatch = 1 << 1,

        /// <summary>与别的实现同键：编辑器目录先到先得、运行时注册表后者覆盖，两边行为相反。</summary>
        DuplicateKey = 1 << 2,

        /// <summary><see cref="IConditionEvaluator.Key"/> 为空：会被目录与注册表一并跳过。</summary>
        EmptyKey = 1 << 3,

        /// <summary>抽象基类：自身不参与发现，由派生类携带特性被注册。**说明性标记，不是问题**。</summary>
        AbstractType = 1 << 4,

        /// <summary>缺公开无参构造：两条通道都会跳过。</summary>
        NoDefaultCtor = 1 << 5,

        /// <summary>实例化抛异常。</summary>
        ConstructionFailed = 1 << 6,
    }

    /// <summary>一条判定器实现（含诊断与引用计数）。</summary>
    public sealed class ConditionEvaluatorRow
    {
        /// <summary>实现类型。</summary>
        public Type Type;

        /// <summary>实际生效的键（取自实例的 <see cref="IConditionEvaluator.Key"/>；无法实例化时回退特性里的键）。</summary>
        public string Key;

        /// <summary>特性 <see cref="ConditionEvaluatorAttribute"/> 上写的键（无特性为 null）。</summary>
        public string AttributeKey;

        /// <summary>显示名（为空时回退 <see cref="Key"/>）。</summary>
        public string DisplayName;

        /// <summary>分类（为空时归入 <see cref="ConditionEvaluatorIndex.OtherCategory"/>）。</summary>
        public string Category;

        /// <summary>所在程序集名。</summary>
        public string AssemblyName;

        /// <summary>参数 schema（无法实例化时为 null）。</summary>
        public IReadOnlyList<ConditionParamDef> ParamSchema;

        /// <summary>诊断标记。</summary>
        public EEvaluatorIssue Issues;

        /// <summary>
        /// 是否存在**真正需要修**的问题。抽象基类这类说明性标记
        /// （<see cref="ConditionEvaluatorIndex.InformationalIssues"/>）不算——面板据此决定是否标红 / 计入「只看有问题」。
        /// </summary>
        public bool HasProblem => (Issues & ~ConditionEvaluatorIndex.InformationalIssues) != 0;

        /// <summary>是否会被 <see cref="ConditionEvaluatorCatalog"/> / <see cref="ConditionRegistry"/> 实际拾取。</summary>
        public bool Discovered;

        /// <summary>工程配置里引用本键的处数（由 <see cref="ConditionEvaluatorIndex"/> 回填）。</summary>
        public int UsageCount;
    }

    /// <summary>一处「配置引用了某判定器键」的记录。</summary>
    public sealed class ConditionKeyUsage
    {
        /// <summary>被引用的判定器键。</summary>
        public string Key;

        /// <summary>承载配置的资产。</summary>
        public UnityEngine.Object Asset;

        /// <summary>条目 id（无则为空，如 <see cref="ConditionAsset"/>）。</summary>
        public string OwnerId;

        /// <summary>人类可读的位置描述，如「组1 第2项」「施加条件」。</summary>
        public string Location;

        /// <summary>
        /// 「跳转」按钮的动作，由提供者给出。
        /// <para>之所以让提供者带上跳转回调而不是由本索引统一处理：效果系统里的条件用法要跳回 Effect Editor，
        /// 若由条件侧直接处理就会让 <c>Ale.Condition.Editor</c> 反向依赖效果系统。</para>
        /// </summary>
        public Action Jump;
    }

    /// <summary>
    /// 编辑期「条件实现」索引：用 <see cref="TypeCache"/> 收集全部 <see cref="IConditionEvaluator"/> 实现（含被
    /// <see cref="ConditionEvaluatorCatalog"/> <b>静默丢弃</b>的那些），逐条给出诊断；并统计每个键被配置引用的处数与
    /// 「引用了但无实现」的悬空键。
    ///
    /// <para>用法来源分两类：本索引<b>直接扫</b>工程里的 <see cref="ConditionDatabase"/> 与 <see cref="ConditionAsset"/>；
    /// 其它系统（如效果系统的施加条件与执行项门控）经 <see cref="RegisterUsageProvider"/> 贡献，从而保持依赖方向不反转。</para>
    ///
    /// <para><see cref="BuildRows"/> / <see cref="CollectKeys"/> / <see cref="Filter"/> 是不碰 AssetDatabase / TypeCache / GUI
    /// 的纯函数，供单测直接调用。与效果侧的 <c>EffectExecutorIndex</c> 同构。</para>
    /// </summary>
    public static class ConditionEvaluatorIndex
    {
        /// <summary>无分类时归入的分类名（与 <see cref="ConditionEvaluatorCatalog"/> 的下拉分组一致）。</summary>
        public const string OtherCategory = "其它";

        /// <summary>只作说明、并非问题的标记：抽象基类不参与发现是设计如此，不该标红、也不该被「只看有问题」筛出来。</summary>
        public const EEvaluatorIssue InformationalIssues = EEvaluatorIssue.AbstractType;

        private static List<ConditionEvaluatorRow> _rows;
        private static List<ConditionKeyUsage>     _usages;
        private static List<string>                _dangling;
        private static List<string>                _categories;

        private static readonly List<Func<IEnumerable<ConditionKeyUsage>>> _usageProviders =
            new List<Func<IEnumerable<ConditionKeyUsage>>>();

        /// <summary>全部实现（按 分类 → 键 排序），含无法被发现的。</summary>
        public static IReadOnlyList<ConditionEvaluatorRow> Rows { get { EnsureBuilt(); return _rows; } }

        /// <summary>工程配置里对判定器键的全部引用。</summary>
        public static IReadOnlyList<ConditionKeyUsage> Usages { get { EnsureBuilt(); return _usages; } }

        /// <summary>配置里引用了、但没有对应实现的键（运行时会报「未注册的判定器键」）。</summary>
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

        /// <summary>登记一个用法提供者（去重）。供其它系统把自己配置里的条件用法贡献进来。</summary>
        public static void RegisterUsageProvider(Func<IEnumerable<ConditionKeyUsage>> provider)
        {
            if (provider == null || _usageProviders.Contains(provider)) return;
            _usageProviders.Add(provider);
            Rebuild();
        }

        /// <summary>注销一个用法提供者。</summary>
        public static bool UnregisterUsageProvider(Func<IEnumerable<ConditionKeyUsage>> provider)
        {
            bool removed = provider != null && _usageProviders.Remove(provider);
            if (removed) Rebuild();
            return removed;
        }

        /// <summary>清空全部用法提供者（测试用）。</summary>
        public static void ClearUsageProviders()
        {
            _usageProviders.Clear();
            Rebuild();
        }

        /// <summary>已登记的用法提供者数。</summary>
        public static int UsageProviderCount => _usageProviders.Count;

        /// <summary>刷新缓存（脚本重编译、资产增删改后调用）。</summary>
        public static void Rebuild()
        {
            _rows = null; _usages = null; _dangling = null; _categories = null;
        }

        /// <summary>某个键的全部引用处。</summary>
        public static List<ConditionKeyUsage> UsagesOf(string key)
        {
            EnsureBuilt();
            var result = new List<ConditionKeyUsage>();
            if (string.IsNullOrEmpty(key)) return result;
            foreach (var u in _usages)
                if (string.Equals(u.Key, key, StringComparison.Ordinal)) result.Add(u);
            return result;
        }

        // ── 纯函数（可单测）──────────────────────────────────────────────────────

        /// <summary>
        /// 由「类型 + 特性键 + 是否带特性」三元组构建行并打诊断标记。
        /// <para>之所以把特性键当参数传进来而不是就地反射，是为了让单测能造出「特性键与 Key 属性不一致」的样本，
        /// 而不必真的给测试类打上 <see cref="ConditionEvaluatorAttribute"/>——那会污染工程的真实判定器目录与下拉。</para>
        /// </summary>
        public static List<ConditionEvaluatorRow> BuildRows(
            IEnumerable<(Type type, string attributeKey, bool hasAttribute)> candidates)
        {
            var rows = new List<ConditionEvaluatorRow>();
            if (candidates == null) return rows;

            foreach (var c in candidates)
            {
                if (c.type == null) continue;
                rows.Add(BuildRow(c.type, c.attributeKey, c.hasAttribute));
            }

            // 同键：所有行都打 DuplicateKey；按候选顺序第一个「其它方面都合格」的胜出（与目录的先到先得一致）。
            var byKey = new Dictionary<string, List<ConditionEvaluatorRow>>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (string.IsNullOrEmpty(r.Key)) continue;
                if (!byKey.TryGetValue(r.Key, out var list)) byKey[r.Key] = list = new List<ConditionEvaluatorRow>();
                list.Add(r);
            }

            foreach (var kv in byKey)
            {
                var list = kv.Value;
                if (list.Count > 1)
                    foreach (var r in list) r.Issues |= EEvaluatorIssue.DuplicateKey;

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

        /// <summary>遍历表达式的全部组 / 项，对每个非空判定器键回调 <c>(key, groupIndex, itemIndex)</c>（下标从 0 起）。</summary>
        public static void CollectKeys(ConditionExpression expr, Action<string, int, int> onKey)
        {
            var groups = expr?.groups;
            if (groups == null || onKey == null) return;

            for (int gi = 0; gi < groups.Count; gi++)
            {
                var items = groups[gi]?.items;
                if (items == null) continue;
                for (int ii = 0; ii < items.Count; ii++)
                {
                    var item = items[ii];
                    if (item == null || string.IsNullOrEmpty(item.key)) continue;
                    onKey(item.key, gi, ii);
                }
            }
        }

        /// <summary>按搜索词（键 / 显示名 / 分类 / 类型全名 / 程序集，忽略大小写）、分类、「只看有问题」过滤。</summary>
        public static List<ConditionEvaluatorRow> Filter(IReadOnlyList<ConditionEvaluatorRow> rows,
            string term, string category, bool onlyIssues)
        {
            var result = new List<ConditionEvaluatorRow>();
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
        private const EEvaluatorIssue BlockingIssues =
            EEvaluatorIssue.MissingAttribute | EEvaluatorIssue.EmptyKey | EEvaluatorIssue.AbstractType |
            EEvaluatorIssue.NoDefaultCtor | EEvaluatorIssue.ConstructionFailed;

        private static ConditionEvaluatorRow BuildRow(Type type, string attributeKey, bool hasAttribute)
        {
            var row = new ConditionEvaluatorRow
            {
                Type         = type,
                AttributeKey = attributeKey,
                AssemblyName = type.Assembly.GetName().Name,
                Issues       = EEvaluatorIssue.None,
            };

            // 抽象基类不打特性是正常的（特性由派生类携带），不算漏打。
            if (!hasAttribute && !type.IsAbstract) row.Issues |= EEvaluatorIssue.MissingAttribute;

            bool constructed = false;
            if (type.IsAbstract) row.Issues |= EEvaluatorIssue.AbstractType;
            else if (type.GetConstructor(Type.EmptyTypes) == null) row.Issues |= EEvaluatorIssue.NoDefaultCtor;
            else
            {
                try
                {
                    if (Activator.CreateInstance(type) is IConditionEvaluator inst)
                    {
                        row.Key         = inst.Key;
                        row.DisplayName = inst.DisplayName;
                        row.Category    = inst.Category;
                        row.ParamSchema = inst.ParamSchema;
                        constructed     = true;
                    }
                    else row.Issues |= EEvaluatorIssue.ConstructionFailed;
                }
                catch
                {
                    row.Issues |= EEvaluatorIssue.ConstructionFailed;
                }
            }

            if (constructed)
            {
                if (string.IsNullOrEmpty(row.Key))
                    row.Issues |= EEvaluatorIssue.EmptyKey;
                else if (hasAttribute && !string.IsNullOrEmpty(attributeKey)
                         && !string.Equals(attributeKey, row.Key, StringComparison.Ordinal))
                    row.Issues |= EEvaluatorIssue.AttributeKeyMismatch;
            }

            if (string.IsNullOrEmpty(row.Key))         row.Key         = attributeKey ?? string.Empty;
            if (string.IsNullOrEmpty(row.DisplayName)) row.DisplayName = string.IsNullOrEmpty(row.Key) ? type.Name : row.Key;
            if (string.IsNullOrEmpty(row.Category))    row.Category    = OtherCategory;

            row.Discovered = (row.Issues & BlockingIssues) == 0;   // 同键胜负在 BuildRows 里定夺
            return row;
        }

        private static bool Matches(ConditionEvaluatorRow r, string term)
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

            foreach (var t in TypeCache.GetTypesDerivedFrom<IConditionEvaluator>()) AddCandidate(t, seen, candidates);
            foreach (var t in TypeCache.GetTypesWithAttribute<ConditionEvaluatorAttribute>()) AddCandidate(t, seen, candidates);

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

            // ② 引用：本索引直接扫条件库与条件资产，其余由提供者贡献
            _usages = new List<ConditionKeyUsage>();
            var countByKey = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var guid in AssetDatabase.FindAssets("t:ConditionDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<ConditionDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (!db) continue;

                foreach (var entry in db.Conditions)
                {
                    if (entry == null) continue;
                    var owner = entry;
                    CollectKeys(entry.expression, (key, gi, ii) => AddUsage(countByKey, new ConditionKeyUsage
                    {
                        Key      = key,
                        Asset    = db,
                        OwnerId  = owner.id,
                        Location = ConditionUsageCollector.Where(gi, ii),
                        Jump     = () => ConditionEditorWindow.Open(db, owner.id),
                    }));
                }

                foreach (var tmpl in db.ConditionTemplates)
                {
                    if (tmpl == null) continue;
                    string tmplName = tmpl.name;
                    CollectKeys(tmpl.defaultExpression, (key, gi, ii) => AddUsage(countByKey, new ConditionKeyUsage
                    {
                        Key      = key,
                        Asset    = db,
                        OwnerId  = tmplName,
                        Location = ConditionUsageCollector.Where("模板默认表达式", gi, ii),
                        Jump     = () => ConditionEditorWindow.Open(db),
                    }));
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:ConditionAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ConditionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!asset) continue;
                var target = asset;
                CollectKeys(asset.Expression, (key, gi, ii) => AddUsage(countByKey, new ConditionKeyUsage
                {
                    Key      = key,
                    Asset    = target,
                    Location = ConditionUsageCollector.Where(gi, ii),
                    Jump     = () => { Selection.activeObject = target; EditorGUIUtility.PingObject(target); },
                }));
            }

            foreach (var provider in _usageProviders)
            {
                IEnumerable<ConditionKeyUsage> supplied;
                try { supplied = provider(); }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ConditionEvaluatorIndex] 用法提供者异常：" + ex.Message);
                    continue;
                }
                if (supplied == null) continue;
                foreach (var u in supplied)
                    if (u != null && !string.IsNullOrEmpty(u.Key)) AddUsage(countByKey, u);
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
            if (!typeof(IConditionEvaluator).IsAssignableFrom(t)) return;
            if (!seen.Add(t)) return;

            var attr = (ConditionEvaluatorAttribute)Attribute.GetCustomAttribute(t, typeof(ConditionEvaluatorAttribute), false);
            into.Add((t, attr?.Key, attr != null));
        }

        private static void AddUsage(Dictionary<string, int> countByKey, ConditionKeyUsage usage)
        {
            _usages.Add(usage);
            countByKey.TryGetValue(usage.Key, out int n);
            countByKey[usage.Key] = n + 1;
        }
    }

    /// <summary>资产增删改（.asset）后失效判定器索引的引用统计。</summary>
    internal sealed class ConditionEvaluatorIndexPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (Touches(imported) || Touches(deleted) || Touches(movedTo)) ConditionEvaluatorIndex.Rebuild();
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
