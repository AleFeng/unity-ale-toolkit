using System;
using System.Collections.Generic;
using Ale.Condition.Editor;
using UnityEditor;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 把<b>效果系统里的条件用法</b>贡献给条件判定器索引：每条效果的施加条件 <see cref="EffectDefinition.applicationCondition"/>、
    /// 各执行项的门控 <see cref="EffectItem.gate"/>，来源覆盖工程内的 <see cref="EffectDatabase"/> 与 <see cref="EffectDefinitionAsset"/>。
    ///
    /// <para><b>为什么由效果侧提供而不是条件侧直接扫</b>：条件系统不该认识效果系统。走
    /// <see cref="ConditionEvaluatorIndex.RegisterUsageProvider"/> 让依赖方向保持 <c>Ale.Effect.Editor → Ale.Condition.Editor</c>，
    /// 「跳转」回调也由本类给出（跳回 Effect Editor 并定位到那条效果）。</para>
    ///
    /// <para>记录构造与位置文案统一走 <see cref="ConditionUsageCollector"/>，与其它宿主的提供者保持一致格式。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class EffectConditionUsageProvider
    {
        static EffectConditionUsageProvider()
        {
            ConditionEvaluatorIndex.RegisterUsageProvider(Provide);
        }

        private static IEnumerable<ConditionKeyUsage> Provide()
        {
            var result = new List<ConditionKeyUsage>();

            ConditionUsageCollector.ForEachAsset<EffectDatabase>(db =>
            {
                foreach (var entry in db.Effects)
                {
                    if (entry?.definition == null) continue;
                    var owner   = entry;
                    var current = db;
                    Collect(result, entry.definition, db, entry.id, () => EffectEditorWindow.Open(current, owner.id));
                }
            });

            ConditionUsageCollector.ForEachAsset<EffectDefinitionAsset>(asset =>
            {
                if (asset.Definition == null) return;
                var target = asset;
                Collect(result, asset.Definition, asset, asset.Definition.id,
                    () => { Selection.activeObject = target; EditorGUIUtility.PingObject(target); });
            });

            return result;
        }

        private static void Collect(List<ConditionKeyUsage> into, EffectDefinition def,
            UnityEngine.Object asset, string ownerId, Action jump)
        {
            ConditionUsageCollector.Collect(into, def.applicationCondition, asset, ownerId, "施加条件", jump);

            var groups = def.executions?.groups;
            if (groups == null) return;
            foreach (var group in groups)
            {
                if (group?.items == null) continue;
                string phase = string.IsNullOrEmpty(group.phase) ? "*" : group.phase;
                foreach (var item in group.items)
                {
                    if (item?.gate == null) continue;
                    string execKey = string.IsNullOrEmpty(item.key) ? "?" : item.key;
                    ConditionUsageCollector.Collect(into, item.gate, asset, ownerId, $"{phase} / {execKey} 门控", jump);
                }
            }
        }
    }
}
