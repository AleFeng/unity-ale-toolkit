using Ale.Toolkit.Runtime;
using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// <see cref="TextValue"/> 的属性绘制器：一行纯文本 fallback + （启用 ATK_LOCALIZATION 时）内嵌
    /// <c>LocalizedString</c> 的 Unity 原生表/条目选择器。由原生绘制器负责编辑与序列化，选择即正确保存。
    ///
    /// <para>不含 <c>#if</c>：靠 <c>FindPropertyRelative("localized")</c> 是否存在来判断本地化字段是否被编译进
    /// （未启用 ATK_LOCALIZATION 时该子属性为 null，仅画 fallback）。</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(TextValue))]
    public class TextValueDrawer : PropertyDrawer
    {
        private static readonly GUIContent LocLabel = new GUIContent("本地化");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;   // fallback 行
            var loc = property.FindPropertyRelative("localized");
            if (loc != null)
                h += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(loc, LocLabel, true);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float lh = EditorGUIUtility.singleLineHeight;
            float sp = EditorGUIUtility.standardVerticalSpacing;

            var fallbackProp = property.FindPropertyRelative("fallback");
            var locProp      = property.FindPropertyRelative("localized");

            EditorGUI.BeginProperty(position, label, property);

            // 行 1：字段标签 + 纯文本 fallback
            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, lh), fallbackProp, label);

            // 行 2+：原生 LocalizedString 选择器（缩进）；未启用本地化时该子属性为 null，跳过
            if (locProp != null)
            {
                float y  = position.y + lh + sp;
                float ph = EditorGUI.GetPropertyHeight(locProp, LocLabel, true);
                using (new EditorGUI.IndentLevelScope())
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, ph), locProp, LocLabel, true);
            }

            EditorGUI.EndProperty();
        }
    }
}
