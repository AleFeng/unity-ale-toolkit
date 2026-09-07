using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// <see cref="EffectDefinitionAsset"/> 的 Inspector：默认绘制（落到 <see cref="EffectDefinitionDrawer"/>）+ 「校验」「归一」按钮，
    /// 校验结果按错误 / 警告分色显示。
    /// </summary>
    [CustomEditor(typeof(EffectDefinitionAsset))]
    public sealed class EffectDefinitionAssetEditor : UnityEditor.Editor
    {
        private readonly List<string> _messages = new List<string>();
        private bool _validated;
        private bool _valid;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("definition"), new GUIContent("效果定义"), true);
            if (EditorGUI.EndChangeCheck()) _validated = false;
            serializedObject.ApplyModifiedProperties();

            var asset = (EffectDefinitionAsset)target;
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("校验"))
                {
                    _messages.Clear();
                    _valid = asset.Validate(_messages);
                    _validated = true;
                }
                if (GUILayout.Button(new GUIContent("归一", "补 null、归一标签、空阶段改写为 onApply、夹取层数上限与概率")))
                {
                    Undo.RecordObject(asset, "归一效果定义");
                    asset.Normalize();
                    EditorUtility.SetDirty(asset);
                    serializedObject.Update();
                    _validated = false;
                }
            }

            if (!_validated) return;
            if (_messages.Count == 0)
            {
                EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
                return;
            }
            var errors = new List<string>();
            var warnings = new List<string>();
            foreach (var m in _messages)
                (EffectDefinition.IsWarning(m) ? warnings : errors).Add(m);
            if (errors.Count > 0)   EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            if (warnings.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
            if (_valid && errors.Count == 0) EditorGUILayout.LabelField("无错误（仅警告）。", EditorStyles.miniLabel);
        }
    }
}
