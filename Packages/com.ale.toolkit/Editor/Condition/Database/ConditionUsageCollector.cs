using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 写「条件用法提供者」的样板助手：把一条 <see cref="ConditionExpression"/> 里的判定器键收成
    /// <see cref="ConditionKeyUsage"/> 记录，并<b>统一位置文案的格式</b>。
    ///
    /// <para><b>为什么要有它</b>：条件既可配成条件库里的具名条目，也可继续内联在宿主实体的字段里
    /// （toolkit 自己的 <c>EffectDefinition.applicationCondition</c> / <c>EffectItem.gate</c> 就是内联）。内联的那些要进
    /// Condition Editor 的「被引用 / 悬空键 / 实现体检」，只能由宿主经
    /// <see cref="ConditionEvaluatorIndex.RegisterUsageProvider"/> 主动贡献——因为「这条条件在哪、叫什么、点了往哪跳」
    /// 只有宿主知道。宿主要写的那部分（走自己的字段）无法下沉，但记录构造与<b>位置文案</b>可以：
    /// 各系统若各写各的格式（「组1 第1项」/「group 1 / item 1」/「[0][0]」），用法列表就会花掉。</para>
    ///
    /// <para>典型写法见 <c>Ale.Effect.Editor.EffectConditionUsageProvider</c>（效果库的施加条件与执行项门控）
    /// 与宿主侧的同类提供者。</para>
    /// </summary>
    public static class ConditionUsageCollector
    {
        /// <summary>位置文案里「组 N 第 M 项」的统一格式（下标 0 起，显示 1 起）。</summary>
        public static string Where(int groupIndex, int itemIndex) => $"组{groupIndex + 1} 第{itemIndex + 1}项";

        /// <summary>拼「{前缀} · 组N 第M项」；前缀为空时只留后半。</summary>
        public static string Where(string prefix, int groupIndex, int itemIndex)
            => string.IsNullOrEmpty(prefix) ? Where(groupIndex, itemIndex) : prefix + " · " + Where(groupIndex, itemIndex);

        /// <summary>
        /// 把一条表达式里的全部判定器键收成用法记录追加到 <paramref name="into"/>（空键与 null 项自动跳过）。
        /// </summary>
        /// <param name="into">收集目标（提供者最终返回的列表）。</param>
        /// <param name="expr">被扫描的条件表达式；为空则什么都不加。</param>
        /// <param name="asset">承载这条条件的资产（用法列表按资产名显示）。</param>
        /// <param name="ownerId">条目 id（特质 id / 效果 id / 技能树 id…），可为空。</param>
        /// <param name="where">这条条件在宿主实体里的位置名，如「获得条件」「施加条件」；会与「组N 第M项」拼接。</param>
        /// <param name="jump">「跳转」按钮的动作——由宿主给出（跳回自己的编辑器窗口并定位）；可为 null。</param>
        public static void Collect(List<ConditionKeyUsage> into, ConditionExpression expr,
            UnityEngine.Object asset, string ownerId, string where, Action jump)
        {
            if (into == null) return;
            ConditionEvaluatorIndex.CollectKeys(expr, (key, gi, ii) => into.Add(new ConditionKeyUsage
            {
                Key      = key,
                Asset    = asset,
                OwnerId  = ownerId,
                Location = Where(where, gi, ii),
                Jump     = jump,
            }));
        }

        /// <summary>遍历工程内某类 <see cref="ScriptableObject"/> 资产（`FindAssets` + `Load` 的样板；加载失败的跳过）。</summary>
        public static void ForEachAsset<T>(Action<T> visit) where T : ScriptableObject
        {
            if (visit == null) return;
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset) visit(asset);
            }
        }
    }
}
