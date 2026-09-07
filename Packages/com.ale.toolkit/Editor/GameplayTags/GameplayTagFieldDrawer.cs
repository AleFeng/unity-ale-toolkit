using UnityEditor;
using UnityEngine;

namespace Ale.GameplayTags.Editor
{
    /// <summary>
    /// 「标签字段」的共享绘制：延迟文本框（提交时合法即归一、非法保留原文）+ 目录树下拉按钮；
    /// 状态覆层：非法红、未登记黄。供 <see cref="GameplayTagFieldDrawer"/> 与 <see cref="GameplayTagContainerDrawer"/> 复用。
    /// 下拉回调在本帧 GUI 之后触发，故经 <see cref="SerializedObject"/> + 属性路径回写。
    /// </summary>
    public static class GameplayTagFieldGUI
    {
        /// <summary>下拉按钮宽度。</summary>
        public const float MenuButtonW = 22f;

        private static readonly GUIContent MenuButtonContent = new GUIContent("▾", "从目录选择");

        /// <summary>在 <paramref name="rect"/> 内绘制一个绑定到 string 属性的标签字段。</summary>
        public static void Draw(Rect rect, SerializedProperty stringProp)
        {
            var so = stringProp.serializedObject;
            string path    = stringProp.propertyPath;
            string current = stringProp.stringValue;

            var textRect = new Rect(rect.x, rect.y, Mathf.Max(20f, rect.width - MenuButtonW - 2f), rect.height);
            var btnRect  = new Rect(rect.xMax - MenuButtonW, rect.y, MenuButtonW, rect.height);

            EditorGUI.BeginChangeCheck();
            string typed = EditorGUI.DelayedTextField(textRect, current);
            if (EditorGUI.EndChangeCheck())
                stringProp.stringValue = GameplayTag.Normalize(typed) ?? typed;   // 合法即归一；非法保留原文（红底提示）
            DrawStatusOverlay(textRect, stringProp.stringValue);

            if (GUI.Button(btnRect, MenuButtonContent, EditorStyles.miniButton))
            {
                GameplayTagEditorCatalog.BuildTreeMenu(current, name =>
                {
                    so.Update();
                    var p = so.FindProperty(path);
                    if (p != null) p.stringValue = name;
                    so.ApplyModifiedProperties();
                }).DropDown(btnRect);
            }
        }

        /// <summary>状态覆层（画在字段之上，半透明）：非空且非法 → 红；合法但目录未知 → 黄。</summary>
        public static void DrawStatusOverlay(Rect rect, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var tag = new GameplayTag(value);
            if (!tag.IsValid) EditorGUI.DrawRect(rect, GameplayTagSystemStyles.InvalidBg);
            else if (!GameplayTagEditorCatalog.IsKnown(tag)) EditorGUI.DrawRect(rect, GameplayTagSystemStyles.UnknownBg);
        }

        /// <summary>字段提示文案：非法 / 未登记 / 目录说明。</summary>
        public static string TooltipOf(string value)
        {
            if (string.IsNullOrEmpty(value)) return "未设置标签";
            var tag = new GameplayTag(value);
            if (!tag.IsValid) return "非法标签名：段不能为空，段内不能含空白或斜杠";
            if (!GameplayTagEditorCatalog.IsKnown(tag)) return "未登记（目录中没有此标签；运行时仍可匹配）";
            return GameplayTagEditorCatalog.CommentOf(tag) ?? tag.Name;
        }
    }

    /// <summary><c>[GameplayTagField]</c> 的 PropertyDrawer：仅对 string 字段生效，其它类型回退默认绘制。</summary>
    [CustomPropertyDrawer(typeof(GameplayTagFieldAttribute))]
    public sealed class GameplayTagFieldDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => property.propertyType == SerializedPropertyType.String
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);
            if (string.IsNullOrEmpty(label.tooltip)) label.tooltip = GameplayTagFieldGUI.TooltipOf(property.stringValue);
            var rect = EditorGUI.PrefixLabel(position, label);
            GameplayTagFieldGUI.Draw(rect, property);
            EditorGUI.EndProperty();
        }
    }
}
