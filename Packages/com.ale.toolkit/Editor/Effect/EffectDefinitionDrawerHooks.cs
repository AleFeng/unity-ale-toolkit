using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>属性 id 目录提供者：宿主系统登记自己的属性候选（如角色系统的核心属性），供效果定义绘制器的「属性 id」字段做下拉。</summary>
    public interface IEffectAttributeCatalogProvider
    {
        /// <summary>系统名（多个提供者并存时作为下拉分组）。</summary>
        string SystemName { get; }

        /// <summary>候选属性（id, 显示名）。</summary>
        IEnumerable<(string id, string display)> GetAttributes();
    }

    /// <summary>
    /// 宿主注入点：效果定义绘制器里「属性 id」字段的绘制方式与「基本」节显隐。
    /// <list type="bullet">
    ///   <item><see cref="AttributeIdField"/>：整字段绘制委托（宿主全权接管；优先级最高，兼容旧接入）。</item>
    ///   <item><see cref="RegisterAttributeProvider"/>：登记属性目录提供者——绘制器把各系统候选并集做分组下拉（无候选退化为文本框）。
    ///   共用效果库的场景下推荐此方式：多个上层系统可同时贡献候选。</item>
    ///   <item><see cref="ShowIdentityFields"/>：宿主把定义包在自带 id / 名称的实体里时置 false（绘制前设、绘制后还原）。</item>
    /// </list>
    /// </summary>
    public static class EffectDefinitionDrawerHooks
    {
        /// <summary>属性 id 字段绘制回调；null = 走提供者下拉 / 文本框。</summary>
        public static Func<Rect, string, string> AttributeIdField;

        /// <summary>
        /// 是否绘制「基本」节（id / 显示名两行）。宿主把定义包在自带 id / 名称的实体里、由实体同步这两个字段时，
        /// 在绘制前置 false、绘制后还原，避免出现两处可编辑却被同步覆盖的字段。默认 true。
        /// </summary>
        public static bool ShowIdentityFields = true;

        private static readonly List<IEffectAttributeCatalogProvider> _providers = new List<IEffectAttributeCatalogProvider>();

        /// <summary>已登记的属性目录提供者。</summary>
        public static IReadOnlyList<IEffectAttributeCatalogProvider> AttributeProviders => _providers;

        /// <summary>登记提供者（去重）。</summary>
        public static void RegisterAttributeProvider(IEffectAttributeCatalogProvider provider)
        {
            if (provider != null && !_providers.Contains(provider)) _providers.Add(provider);
        }

        /// <summary>移除提供者。</summary>
        public static bool UnregisterAttributeProvider(IEffectAttributeCatalogProvider provider) => _providers.Remove(provider);

        /// <summary>清空提供者（测试 / 重载用）。</summary>
        public static void ClearAttributeProviders() => _providers.Clear();

        /// <summary>汇总各提供者的候选（按登记顺序，同 id 先到者优先）。</summary>
        public static List<(string id, string display, string system)> CollectAttributeCandidates()
        {
            var result = new List<(string id, string display, string system)>();
            var seen   = new HashSet<string>();
            foreach (var p in _providers)
            {
                if (p == null) continue;
                IEnumerable<(string id, string display)> attrs;
                try { attrs = p.GetAttributes(); }
                catch (Exception ex) { Debug.LogWarning("[EffectDefinitionDrawerHooks] 属性提供者异常：" + ex.Message); continue; }
                if (attrs == null) continue;
                foreach (var (id, display) in attrs)
                {
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    result.Add((id, string.IsNullOrEmpty(display) ? id : display, p.SystemName ?? string.Empty));
                }
            }
            return result;
        }

        /// <summary>按当前钩子绘制属性 id 字段：委托 → 提供者下拉（多系统按系统名分组；悬空 / 未选择项保留在首位）→ 文本框。</summary>
        public static string DrawAttributeId(Rect rect, string current)
        {
            var hook = AttributeIdField;
            if (hook != null) return hook(rect, current);

            var candidates = CollectAttributeCandidates();
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
                string label = (c.display == c.id ? c.id : c.display + " (" + c.id + ")").Replace('/', '／');
                labels.Add(grouped && !string.IsNullOrEmpty(c.system) ? c.system + "/" + label : label);
            }
            if (current.Length == 0)          { ids.Insert(0, string.Empty); labels.Insert(0, "（未选择）"); }
            else if (!ids.Contains(current))  { ids.Insert(0, current);      labels.Insert(0, current + "（未知）"); }

            int idx    = Mathf.Max(0, ids.IndexOf(current));
            int newIdx = EditorGUI.Popup(rect, idx, labels.ToArray());
            return ids[newIdx];
        }
    }
}
