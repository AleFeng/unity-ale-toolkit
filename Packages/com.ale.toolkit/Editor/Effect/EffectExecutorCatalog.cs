using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// 编辑期执行器目录：用 <see cref="TypeCache"/> 发现所有带 <see cref="EffectExecutorAttribute"/> 的执行器，
    /// 缓存 Key→实例，提供按 Category 分组的下拉菜单 + 参数区按 schema 同步。镜像 <c>ConditionEvaluatorCatalog</c>。
    /// </summary>
    public static class EffectExecutorCatalog
    {
        private static Dictionary<string, IEffectExecutor> _byKey;
        private static List<IEffectExecutor> _all;

        private static void EnsureBuilt()
        {
            if (_byKey != null) return;
            _byKey = new Dictionary<string, IEffectExecutor>();
            _all   = new List<IEffectExecutor>();

            foreach (var t in TypeCache.GetTypesWithAttribute<EffectExecutorAttribute>())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                if (!typeof(IEffectExecutor).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                var inst = Activator.CreateInstance(t) as IEffectExecutor;
                if (inst == null || string.IsNullOrEmpty(inst.Key) || _byKey.ContainsKey(inst.Key)) continue;
                _byKey[inst.Key] = inst;
                _all.Add(inst);
            }

            _all.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Category ?? "", b.Category ?? "");
                return c != 0 ? c : string.CompareOrdinal(a.Key ?? "", b.Key ?? "");
            });
        }

        /// <summary>刷新缓存（脚本重编译后 TypeCache 变化时调用）。</summary>
        public static void Rebuild() { _byKey = null; _all = null; EnsureBuilt(); }

        /// <summary>全部已发现执行器（按 Category、Key 排序）。</summary>
        public static IReadOnlyList<IEffectExecutor> All { get { EnsureBuilt(); return _all; } }

        /// <summary>按键取执行器，未找到返回 null。</summary>
        public static IEffectExecutor Get(string key)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(key) && _byKey.TryGetValue(key, out var e) ? e : null;
        }

        /// <summary>某键的下拉按钮文案（显示名 / 未配置占位 / 未知键原样）。</summary>
        public static string DisplayNameOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return "(选择执行器)";
            var e = Get(key);
            return e != null ? e.DisplayName : key + " (未知)";
        }

        /// <summary>构建按 Category 分组的下拉菜单；选中某项时回调其 Key。</summary>
        public static GenericMenu BuildKeyMenu(string currentKey, Action<string> onSelect)
        {
            EnsureBuilt();
            var menu = new GenericMenu();
            foreach (var ex in _all)
            {
                string cat   = string.IsNullOrEmpty(ex.Category) ? "其它" : ex.Category;
                string label = $"{cat}/{ex.DisplayName}";
                string key   = ex.Key;
                menu.AddItem(new GUIContent(label), key == currentKey, () => onSelect?.Invoke(key));
            }
            if (_all.Count == 0)
                menu.AddDisabledItem(new GUIContent("（未发现任何执行器）"));
            return menu;
        }

        /// <summary>
        /// 让 <c>itemProp.parameters</c> 与所选执行器 schema 对齐：删除 schema 外的参数、补缺失参数、
        /// 类型 / 数组形态 / 枚举引用不符则重置。直接改 <see cref="SerializedProperty"/>，Undo 由 SerializedObject 自动处理。
        /// </summary>
        public static void SyncParameters(SerializedProperty itemProp, IReadOnlyList<EffectParamDef> schema)
        {
            if (itemProp == null) return;
            var paramsProp = itemProp.FindPropertyRelative("parameters");
            if (paramsProp == null) return;
            schema = schema ?? Array.Empty<EffectParamDef>();

            // 1) 删除 schema 中不存在的参数（按 id）
            for (int i = paramsProp.arraySize - 1; i >= 0; i--)
            {
                string id = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                bool inSchema = false;
                foreach (var def in schema) if (def.id == id) { inSchema = true; break; }
                if (!inSchema) paramsProp.DeleteArrayElementAtIndex(i);
            }

            // 2) 补缺 + 类型/数组/枚举引用不符则重置
            foreach (var def in schema)
            {
                int found = -1;
                for (int i = 0; i < paramsProp.arraySize; i++)
                    if (paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == def.id) { found = i; break; }

                if (found < 0)
                {
                    paramsProp.arraySize++;
                    ResetParam(paramsProp.GetArrayElementAtIndex(paramsProp.arraySize - 1), def);
                }
                else
                {
                    var el   = paramsProp.GetArrayElementAtIndex(found);
                    var type = el.FindPropertyRelative("type");
                    var arr  = el.FindPropertyRelative("isArray");
                    var eref = el.FindPropertyRelative("enumTypeRef");
                    if (type.enumValueIndex != (int)def.type || arr.boolValue != def.isArray
                        || eref.stringValue != (def.enumTypeRef ?? ""))
                        ResetParam(el, def);
                }
            }
        }

        private static void ResetParam(SerializedProperty el, EffectParamDef def)
        {
            el.FindPropertyRelative("id").stringValue          = def.id;
            el.FindPropertyRelative("type").enumValueIndex     = (int)def.type;
            el.FindPropertyRelative("isArray").boolValue       = def.isArray;
            el.FindPropertyRelative("enumTypeRef").stringValue = def.enumTypeRef ?? "";

            var ints    = el.FindPropertyRelative("ints");    ints.ClearArray();
            var floats  = el.FindPropertyRelative("floats");  floats.ClearArray();
            var strings = el.FindPropertyRelative("strings"); strings.ClearArray();

            if (!def.isArray) // 标量：初始化一个默认元素以便直接编辑
            {
                switch (def.type)
                {
                    case EffectParamType.String: strings.arraySize = 1; break;
                    case EffectParamType.Float:  floats.arraySize  = 1; break;
                    default:                     ints.arraySize    = 1; break; // Int / Bool / Enum
                }
            }
        }
    }
}
