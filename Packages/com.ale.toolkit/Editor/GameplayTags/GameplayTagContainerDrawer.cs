using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.GameplayTags.Editor
{
    /// <summary>
    /// <see cref="GameplayTagContainer"/> 的 PropertyDrawer：折叠标题「标签 · N」；展开后一行控制行（「+ 从目录」树菜单，已有项禁用；
    /// 「+ 手动输入」追加空行）+ 每标签一行（标签字段 + 删除）。结构性增删延迟到绘制后应用（镜像 EffectExpressionDrawer）。
    /// 全程 <see cref="SerializedProperty"/> 读写，Undo / 脏标记由 <see cref="SerializedObject"/> 处理。
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public sealed class GameplayTagContainerDrawer : PropertyDrawer
    {
        private const float Indent = 14f;
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        private static readonly GUIContent RemoveContent = new GUIContent("×", "移除");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return RowH;
            var tags = property.FindPropertyRelative("tags");
            return RowH * (2 + tags.arraySize);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var tags = property.FindPropertyRelative("tags");

            float y = position.y, left = position.x, right = position.xMax;
            Func<float, Rect> row = ix =>
            {
                var r = new Rect(ix, y, right - ix, LH);
                y += RowH;
                return r;
            };

            property.isExpanded = EditorGUI.Foldout(row(left), property.isExpanded, $"{label.text}   ·  {tags.arraySize} 标签", true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            bool addEmpty = false;
            int  del      = -1;

            // 控制行
            var ctrl = row(left + Indent);
            EditorGUI.DrawRect(new Rect(ctrl.x, ctrl.y - 1, ctrl.width, LH + 2), GameplayTagSystemStyles.GroupBg);
            EditorGUI.LabelField(new Rect(ctrl.x + 4, ctrl.y, 140f, LH), "标签（层级匹配）", EditorStyles.miniLabel);
            var addMenuRect = new Rect(ctrl.xMax - 176f, ctrl.y, 86f, LH);
            if (GUI.Button(addMenuRect, "+ 从目录", EditorStyles.miniButton))
            {
                var so = property.serializedObject;
                string tagsPath = tags.propertyPath;
                GameplayTagEditorCatalog.BuildTreeMenu(null, name =>
                {
                    if (string.IsNullOrEmpty(name)) return;
                    so.Update();
                    var arr = so.FindProperty(tagsPath);
                    if (arr == null || Contains(arr, name)) return;
                    arr.arraySize++;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).stringValue = name;
                    so.ApplyModifiedProperties();
                }, includeClear: false, disabled: Existing(tags)).DropDown(addMenuRect);
            }
            if (GUI.Button(new Rect(ctrl.xMax - 86f, ctrl.y, 86f, LH), "+ 手动输入", EditorStyles.miniButton)) addEmpty = true;

            // 标签行
            for (int i = 0; i < tags.arraySize; i++)
            {
                var r = row(left + Indent * 2);
                GameplayTagFieldGUI.Draw(new Rect(r.x, r.y, r.width - 30f, LH), tags.GetArrayElementAtIndex(i));
                if (GUI.Button(new Rect(r.xMax - 26f, r.y, 26f, LH), RemoveContent, EditorStyles.miniButton)) del = i;
            }

            // 延迟应用结构操作
            if (addEmpty)
            {
                tags.arraySize++;
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = string.Empty;
            }
            else if (del >= 0)
            {
                tags.DeleteArrayElementAtIndex(del);
            }

            EditorGUI.EndProperty();
        }

        private static HashSet<string> Existing(SerializedProperty arr)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < arr.arraySize; i++)
            {
                string n = GameplayTag.Normalize(arr.GetArrayElementAtIndex(i).stringValue);
                if (n != null) set.Add(n);
            }
            return set;
        }

        private static bool Contains(SerializedProperty arr, string name)
        {
            for (int i = 0; i < arr.arraySize; i++)
                if (GameplayTag.Normalize(arr.GetArrayElementAtIndex(i).stringValue) == name) return true;
            return false;
        }
    }

    /// <summary><see cref="GameplayTagRequirements"/> 的 PropertyDrawer：折叠标题「需 N / 忌 M」，展开为两个嵌套容器。</summary>
    [CustomPropertyDrawer(typeof(GameplayTagRequirements))]
    public sealed class GameplayTagRequirementsDrawer : PropertyDrawer
    {
        private const float Indent = 14f;
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        private static readonly GUIContent RequireLabel = new GUIContent("需持有（全部）");
        private static readonly GUIContent IgnoreLabel  = new GUIContent("不得持有（任一）");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return RowH;
            var req = property.FindPropertyRelative("requireTags");
            var ign = property.FindPropertyRelative("ignoreTags");
            return RowH + EditorGUI.GetPropertyHeight(req, RequireLabel, true) + V
                        + EditorGUI.GetPropertyHeight(ign, IgnoreLabel, true) + V;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var req  = property.FindPropertyRelative("requireTags");
            var ign  = property.FindPropertyRelative("ignoreTags");
            int nReq = req.FindPropertyRelative("tags").arraySize;
            int nIgn = ign.FindPropertyRelative("tags").arraySize;

            var head = new Rect(position.x, position.y, position.width, LH);
            property.isExpanded = EditorGUI.Foldout(head, property.isExpanded, $"{label.text}   ·  需 {nReq} / 忌 {nIgn}", true);
            if (property.isExpanded)
            {
                float y = position.y + RowH;
                float x = position.x + Indent, w = position.width - Indent;

                float hReq = EditorGUI.GetPropertyHeight(req, RequireLabel, true);
                EditorGUI.PropertyField(new Rect(x, y, w, hReq), req, RequireLabel, true);
                y += hReq + V;

                float hIgn = EditorGUI.GetPropertyHeight(ign, IgnoreLabel, true);
                EditorGUI.PropertyField(new Rect(x, y, w, hIgn), ign, IgnoreLabel, true);
            }
            EditorGUI.EndProperty();
        }
    }
}
