using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ale.Effect;
using Ale.Effect.Editor;
using Ale.Modifier;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 效果编辑器的门槛（EditMode）：幅度 / 修饰器 / 定义绘制器已绑定且高度随类型、策略、叠加、修饰器数变化（量算与绘制共用布局）、
    /// 定义资产的归一与校验、宿主属性下拉钩子。
    /// </summary>
    public class EffectEditorTests
    {
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        private static EffectDefinitionAsset NewAsset(string id = "x")
        {
            var a = ScriptableObject.CreateInstance<EffectDefinitionAsset>();
            a.Definition.id = id;
            return a;
        }

        [Test]
        public void MagnitudeDrawer_HeightsByKind()
        {
            var asset = NewAsset();
            try
            {
                var so = new SerializedObject(asset);
                var dur = so.FindProperty("definition.duration");
                var kind = dur.FindPropertyRelative("kind");
                kind.enumValueIndex = (int)EMagnitudeKind.Scalable;
                Assert.AreEqual(LH, EditorGUI.GetPropertyHeight(dur, true), 0.01f);
                kind.enumValueIndex = (int)EMagnitudeKind.SetByCaller;
                Assert.AreEqual(LH, EditorGUI.GetPropertyHeight(dur, true), 0.01f);
                kind.enumValueIndex = (int)EMagnitudeKind.AttributeBased;
                Assert.AreEqual(2f * LH + V, EditorGUI.GetPropertyHeight(dur, true), 0.01f);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ModifierDrawer_Height_IsRowPlusMagnitude()
        {
            var asset = NewAsset();
            try
            {
                asset.Definition.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 1f));
                asset.Definition.modifiers.Add(new EffectModifier("agility", EModifierOperation.Multiply, EffectMagnitude.AttributeBased("might")));
                var so = new SerializedObject(asset);
                var mods = so.FindProperty("definition.modifiers");
                Assert.AreEqual(RowH + LH, EditorGUI.GetPropertyHeight(mods.GetArrayElementAtIndex(0), true), 0.01f);
                Assert.AreEqual(RowH + 2f * LH + V, EditorGUI.GetPropertyHeight(mods.GetArrayElementAtIndex(1), true), 0.01f);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void DefinitionDrawer_CollapsedOneRow_ExpandedGrowsWithPolicyStackingAndModifiers()
        {
            var asset = NewAsset();
            try
            {
                var so = new SerializedObject(asset);
                var def = so.FindProperty("definition");
                def.isExpanded = false;
                Assert.AreEqual(LH, EditorGUI.GetPropertyHeight(def, true), 0.01f);

                def.isExpanded = true;
                float instant = EditorGUI.GetPropertyHeight(def, true);
                Assert.Greater(instant, RowH * 8f);

                def.FindPropertyRelative("durationPolicy").enumValueIndex = (int)EDurationPolicy.HasDuration;
                float withDuration = EditorGUI.GetPropertyHeight(def, true);
                Assert.Greater(withDuration, instant);      // 时长 / 周期 / 叠加节 / 授予与免疫标签 / 持续要求

                def.FindPropertyRelative("stackingType").enumValueIndex = (int)EEffectStackingType.AggregateByTarget;
                float withStacking = EditorGUI.GetPropertyHeight(def, true);
                Assert.AreEqual(withDuration + RowH * 4f, withStacking, 0.01f);   // 上限 + 三策略

                so.ApplyModifiedProperties();
                asset.Definition.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 1f));
                so.Update();
                float withModifier = EditorGUI.GetPropertyHeight(so.FindProperty("definition"), true);
                Assert.AreEqual(withStacking + (RowH + LH) + V, withModifier, 0.01f);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void Asset_Normalize_RewritesEmptyPhase_And_ValidateReportsErrors()
        {
            var asset = NewAsset();
            try
            {
                asset.Definition.executions.groups.Add(new EffectGroup(""));
                asset.Definition.assetTags.tags.Add(" A . B ");
                asset.Normalize();
                Assert.AreEqual(EffectPhases.OnApply, asset.Definition.executions.groups[0].phase);
                CollectionAssert.AreEqual(new[] { "A.B" }, asset.Definition.assetTags.tags);

                var msgs = new List<string>();
                Assert.IsTrue(asset.Validate(msgs));
                Assert.AreEqual(0, msgs.Count);
                asset.Definition.id = "";
                Assert.IsFalse(asset.Validate(msgs));
                Assert.AreEqual(1, msgs.Count);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void Hooks_AttributeIdField_Override()
        {
            var prev = EffectDefinitionDrawerHooks.AttributeIdField;
            try
            {
                EffectDefinitionDrawerHooks.AttributeIdField = (r, cur) => cur + "!";
                Assert.AreEqual("might!", EffectDefinitionDrawerHooks.DrawAttributeId(new Rect(), "might"));
            }
            finally { EffectDefinitionDrawerHooks.AttributeIdField = prev; }
        }
    }
}
