using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;
#if ATK_LOCALIZATION
using System.Collections.Generic;
#endif

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// <see cref="TextValue"/> 的属性绘制器：一行纯文本 fallback + （启用 ATK_LOCALIZATION 时）原生本地化选择器
    /// （表 / 条目可搜索下拉）。可在任意组件 / 配置字段上直接声明 <c>TextValue</c> 即得干净的 Inspector。
    ///
    /// <para>本地化选择器复用 <see cref="AttributeFieldDrawer"/> 同款「<see cref="LocalizedStringHolder"/> 桥接」技巧
    /// （把扁平的 tableRef / entryKey 同步进一个持有 <c>LocalizedString</c> 的临时 SO，借 Unity 原生绘制器编辑后回写），
    /// 但<b>自包含</b>在本类内，不改动 <see cref="AttributeFieldDrawer"/>，零回归风险。</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(TextValue))]
    public class TextValueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;   // fallback 行
#if ATK_LOCALIZATION
            h += EditorGUIUtility.standardVerticalSpacing + GetLocHeight(property);
#endif
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float lh = EditorGUIUtility.singleLineHeight;
            float sp = EditorGUIUtility.standardVerticalSpacing;

            var fallbackProp = property.FindPropertyRelative("fallback");

            EditorGUI.BeginProperty(position, label, property);

            // 行 1：字段标签 + 纯文本 fallback
            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, lh), fallbackProp, label);

#if ATK_LOCALIZATION
            DrawLocalized(position, property, position.y + lh + sp);
#endif

            EditorGUI.EndProperty();
        }

#if ATK_LOCALIZATION
        private static readonly GUIContent LocLabel   = new GUIContent("本地化");
        private static readonly GUIContent TableLabel = new GUIContent("Table");
        private static readonly GUIContent EntryLabel = new GUIContent("Entry Key");

        // 每个 (目标对象, 属性路径) 各一套桥接 SO，避免多字段共享时状态互染。会话内稳定，域重载时随 SO 一并销毁。
        private readonly Dictionary<string, LocalizedStringHolder> _holders = new Dictionary<string, LocalizedStringHolder>();
        private readonly Dictionary<string, SerializedObject>      _sos     = new Dictionary<string, SerializedObject>();

        private (LocalizedStringHolder holder, SerializedObject so) EnsureHolder(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            string key = (target ? target.GetInstanceID() : 0) + ":" + property.propertyPath;

            if (!_holders.TryGetValue(key, out var holder) || !holder)
            {
                holder = ScriptableObject.CreateInstance<LocalizedStringHolder>();
                holder.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                _holders[key] = holder;
                _sos[key]     = new SerializedObject(holder);
            }
            var so = _sos.TryGetValue(key, out var cached) && cached != null && cached.targetObject != null
                ? cached
                : (_sos[key] = new SerializedObject(holder));
            return (holder, so);
        }

        /// <summary>本地化控件高度：原生 LocalizedString 属性高度（随展开变化）；路径不匹配时降级为两行文本框。</summary>
        private float GetLocHeight(SerializedProperty property)
        {
            var (_, so) = EnsureHolder(property);
            var valueProp = so.FindProperty("value");
            var vTable = valueProp?.FindPropertyRelative("m_TableReference")?.FindPropertyRelative("m_TableCollectionName");
            var vKey   = valueProp?.FindPropertyRelative("m_TableEntryReference")?.FindPropertyRelative("m_Key");
            if (valueProp != null && vTable != null && vKey != null)
                return EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        private void DrawLocalized(Rect position, SerializedProperty property, float y)
        {
            float lh = EditorGUIUtility.singleLineHeight;
            float sp = EditorGUIUtility.standardVerticalSpacing;

            var tableProp = property.FindPropertyRelative("tableRef");
            var entryProp = property.FindPropertyRelative("entryKey");

            var (_, so) = EnsureHolder(property);
            var valueProp = so.FindProperty("value");
            var vTable = valueProp?.FindPropertyRelative("m_TableReference")?.FindPropertyRelative("m_TableCollectionName");
            var vKey   = valueProp?.FindPropertyRelative("m_TableEntryReference")?.FindPropertyRelative("m_Key");

            using (new EditorGUI.IndentLevelScope())
            {
                if (valueProp != null && vTable != null && vKey != null)
                {
                    // (tableRef, entryKey) → holder（仅值不同才写，避免打断进行中的编辑）
                    so.Update();
                    if (vTable.stringValue != tableProp.stringValue || vKey.stringValue != entryProp.stringValue)
                    {
                        vTable.stringValue = tableProp.stringValue;
                        vKey.stringValue   = entryProp.stringValue;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        so.Update();
                    }

                    float ph = EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, ph), valueProp, LocLabel, true);
                    bool changed = so.ApplyModifiedProperties();
                    if (EditorGUI.EndChangeCheck() || changed)
                    {
                        // 用户经原生控件改动后回写到 TextValue 的扁平字段
                        tableProp.stringValue = vTable.stringValue ?? string.Empty;
                        entryProp.stringValue = vKey.stringValue ?? string.Empty;
                    }
                }
                else
                {
                    // 降级：包版本属性路径不匹配时，退回两个普通文本框
                    EditorGUI.PropertyField(new Rect(position.x, y,             position.width, lh), tableProp, TableLabel);
                    EditorGUI.PropertyField(new Rect(position.x, y + lh + sp,    position.width, lh), entryProp, EntryLabel);
                }
            }
        }
#endif
    }
}
