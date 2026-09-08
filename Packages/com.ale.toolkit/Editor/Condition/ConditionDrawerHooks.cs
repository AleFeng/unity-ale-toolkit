using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 条件参数的候选目录提供者：宿主为某个 <see cref="CatalogRef"/>（与判定器 schema 里的
    /// <see cref="ConditionParamDef.catalogRef"/> 对应）供给候选 id，供条件表达式绘制器把裸文本框换成分组下拉。
    /// </summary>
    public interface IConditionParamCatalogProvider
    {
        /// <summary>系统名（同一目录有多个提供者时作为下拉分组）。</summary>
        string SystemName { get; }

        /// <summary>本提供者服务的目录引用，如 <c>"Chronicle.Trait"</c>。</summary>
        string CatalogRef { get; }

        /// <summary>候选项（id, 显示名）。</summary>
        IEnumerable<(string id, string display)> GetItems();
    }

    /// <summary>
    /// 宿主注入点：条件表达式绘制器里<b>字符串参数</b>的绘制方式。
    /// <list type="bullet">
    ///   <item><see cref="ParamIdField"/>：整字段绘制委托（宿主全权接管；优先级最高）。</item>
    ///   <item><see cref="RegisterProvider"/>：按目录登记候选提供者——绘制器把同目录各系统的候选并集做分组下拉
    ///   （无候选退化为文本框，行为与 1.11.0 一致）。</item>
    /// </list>
    ///
    /// <para>动机：<c>Chronicle.AttributeCompare</c> 的 <c>attrId</c>、<c>HasTrait</c> 的 <c>traitId</c> 等以前一律是裸文本框，
    /// 打错一个字静默返回 false——<see cref="ConditionEngine"/> 只对<b>未注册的判定器键</b>告警，对写错的<b>参数值</b>完全无声；
    /// 而同一个属性 id 在效果编辑器里却是按系统名分组的下拉。本类是效果侧 <c>EffectDefinitionDrawerHooks</c> 的对偶。</para>
    /// </summary>
    public static class ConditionDrawerHooks
    {
        /// <summary>字符串参数的整字段绘制回调 <c>(rect, catalogRef, current) → new</c>；null = 走提供者下拉 / 文本框。</summary>
        public static Func<Rect, string, string, string> ParamIdField;

        private static readonly List<IConditionParamCatalogProvider> _providers = new List<IConditionParamCatalogProvider>();

        /// <summary>已登记的候选提供者。</summary>
        public static IReadOnlyList<IConditionParamCatalogProvider> Providers => _providers;

        /// <summary>登记提供者（去重）。</summary>
        public static void RegisterProvider(IConditionParamCatalogProvider provider)
        {
            if (provider != null && !_providers.Contains(provider)) _providers.Add(provider);
        }

        /// <summary>移除提供者。</summary>
        public static bool UnregisterProvider(IConditionParamCatalogProvider provider) => _providers.Remove(provider);

        /// <summary>清空提供者（测试 / 重载用）。</summary>
        public static void ClearProviders() => _providers.Clear();

        /// <summary>汇总某目录下各提供者的候选（按登记顺序，同 id 先到者优先）。目录为空返回空表。</summary>
        public static List<(string id, string display, string system)> CollectCandidates(string catalogRef)
        {
            var result = new List<(string id, string display, string system)>();
            if (string.IsNullOrEmpty(catalogRef)) return result;

            var seen = new HashSet<string>();
            foreach (var p in _providers)
            {
                if (p == null || !string.Equals(p.CatalogRef, catalogRef, StringComparison.Ordinal)) continue;
                IEnumerable<(string id, string display)> items;
                try { items = p.GetItems(); }
                catch (Exception ex) { Debug.LogWarning("[ConditionDrawerHooks] 候选提供者异常：" + ex.Message); continue; }
                if (items == null) continue;
                foreach (var (id, display) in items)
                {
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    result.Add((id, string.IsNullOrEmpty(display) ? id : display, p.SystemName ?? string.Empty));
                }
            }
            return result;
        }

        /// <summary>
        /// 按当前钩子绘制一个字符串参数：委托 → 该目录的候选下拉（多系统按系统名分组；未选择 / 悬空值保留在首位）→ 文本框。
        /// <paramref name="catalogRef"/> 为空或该目录无候选时行为与普通文本框完全一致。
        /// </summary>
        public static string DrawParamId(Rect rect, string catalogRef, string current)
        {
            var hook = ParamIdField;
            if (hook != null) return hook(rect, catalogRef, current);

            var candidates = CollectCandidates(catalogRef);
            if (candidates.Count == 0) return EditorGUI.TextField(rect, current ?? string.Empty);

            current ??= string.Empty;
            var systems = new HashSet<string>();
            foreach (var c in candidates) systems.Add(c.system);
            bool grouped = systems.Count > 1;

            var ids    = new List<string>(candidates.Count + 1);
            var labels = new List<string>(candidates.Count + 1);
            foreach (var c in candidates)
            {
                ids.Add(c.id);
                // label 里的 '/' 会被 Popup 当作子菜单分隔符，换成全角。
                string label = (c.display == c.id ? c.id : c.display + " (" + c.id + ")").Replace('/', '／');
                labels.Add(grouped && !string.IsNullOrEmpty(c.system) ? c.system + "/" + label : label);
            }
            if (current.Length == 0)         { ids.Insert(0, string.Empty); labels.Insert(0, "（未选择）"); }
            else if (!ids.Contains(current)) { ids.Insert(0, current);      labels.Insert(0, current + "（未知）"); }

            int idx    = Mathf.Max(0, ids.IndexOf(current));
            int newIdx = EditorGUI.Popup(rect, idx, labels.ToArray());
            return ids[newIdx];
        }
    }
}
