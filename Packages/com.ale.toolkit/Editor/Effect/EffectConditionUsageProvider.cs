using System.Collections.Generic;
using Ale.Condition;
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

            foreach (var guid in AssetDatabase.FindAssets("t:EffectDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<EffectDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (!db) continue;

                foreach (var entry in db.Effects)
                {
                    if (entry?.definition == null) continue;
                    var owner   = entry;
                    var current = db;
                    Collect(result, entry.definition, db, entry.id, () => EffectEditorWindow.Open(current, owner.id));
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:EffectDefinitionAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EffectDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!asset || asset.Definition == null) continue;
                var target = asset;
                Collect(result, asset.Definition, asset, asset.Definition.id,
                    () => { Selection.activeObject = target; EditorGUIUtility.PingObject(target); });
            }

            return result;
        }

        private static void Collect(List<ConditionKeyUsage> into, EffectDefinition def,
            UnityEngine.Object asset, string ownerId, System.Action jump)
        {
            ConditionEvaluatorIndex.CollectKeys(def.applicationCondition, (key, gi, ii) => into.Add(new ConditionKeyUsage
            {
                Key      = key,
                Asset    = asset,
                OwnerId  = ownerId,
                Location = $"施加条件 · 组{gi + 1} 第{ii + 1}项",
                Jump     = jump,
            }));

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
                    ConditionEvaluatorIndex.CollectKeys(item.gate, (key, gi, ii) => into.Add(new ConditionKeyUsage
                    {
                        Key      = key,
                        Asset    = asset,
                        OwnerId  = ownerId,
                        Location = $"{phase} / {execKey} 门控 · 组{gi + 1} 第{ii + 1}项",
                        Jump     = jump,
                    }));
                }
            }
        }
    }
}
