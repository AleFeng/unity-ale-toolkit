using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ale.GameplayTags;
using Ale.GameplayTags.Editor;

namespace Ale.Toolkit.Tests
{
    /// <summary>
    /// 标签系统 Unity 桥 + 编辑器的门槛（EditMode）：标签表校验 / 登记、编辑器目录并入表资产与注册表、树菜单项数、
    /// 容器 / 要求绘制器已绑定（按高度证明）、SerializedProperty 改动可 Undo。
    /// </summary>
    public class GameplayTagEditorTests
    {
        /// <summary>绘制器绑定用的临时 SO（测试程序集内定义，Unity 可正常序列化）。</summary>
        public class GameplayTagSmokeAsset : ScriptableObject
        {
            public GameplayTagContainer    container    = new GameplayTagContainer();
            public GameplayTagRequirements requirements = new GameplayTagRequirements();
            [GameplayTagField] public string tag;
        }

        private const string TempFolder = "Assets/Tests/__GameplayTagSmoke";
        private const string TempTable  = TempFolder + "/SmokeTable.asset";

        private static GameplayTag T(string s) => new GameplayTag(s);
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float RowH => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
                AssetDatabase.Refresh();
            }
            GameplayTagRegistry.Default.Clear();
            GameplayTagEditorCatalog.Rebuild();
        }

        [Test]
        public void Table_Validate_FlagsInvalidDuplicateAndCaseVariants()
        {
            var table = ScriptableObject.CreateInstance<GameplayTagTable>();
            try
            {
                table.Entries.Add(new GameplayTagDefinition("Status.Buff", "增益"));
                table.Entries.Add(new GameplayTagDefinition("status.buff"));   // 仅大小写不同 → 警告
                table.Entries.Add(new GameplayTagDefinition("Status.Buff"));   // 重复 → 错误
                table.Entries.Add(new GameplayTagDefinition("A..B"));          // 非法 → 错误
                var msgs = new List<string>();
                Assert.IsFalse(table.Validate(msgs));
                Assert.AreEqual(3, msgs.Count);
                StringAssert.StartsWith("警告:", msgs[0]);
                StringAssert.Contains("重复", msgs[1]);
                StringAssert.Contains("不合法", msgs[2]);

                table.Entries.Clear();
                table.Entries.Add(new GameplayTagDefinition("X.Y"));
                msgs.Clear();
                Assert.IsTrue(table.Validate(msgs));
                Assert.AreEqual(0, msgs.Count);
            }
            finally { Object.DestroyImmediate(table); }
        }

        [Test]
        public void Runtime_Register_Table_AddsAncestorsIntoDefaultRegistry()
        {
            GameplayTagRegistry.Default.Clear();
            var table = ScriptableObject.CreateInstance<GameplayTagTable>();
            try
            {
                table.Entries.Add(new GameplayTagDefinition("Status.Debuff.Mental", "精神异常"));
                table.Entries.Add(new GameplayTagDefinition("bad..name"));
                Assert.AreEqual(1, GameplayTagRuntime.Register(table));          // 一条合法定义（自动补 2 个祖先）
                Assert.AreEqual(3, GameplayTagRegistry.Default.Count);
                Assert.IsTrue(GameplayTagRegistry.Default.IsRegistered(T("Status")));
                Assert.AreEqual("精神异常", GameplayTagRegistry.Default.GetComment(T("Status.Debuff.Mental")));
                Assert.AreEqual(0, GameplayTagRuntime.Register((GameplayTagTable)null));
                Assert.IsTrue(GameplayTagRuntime.Register("Immunity.Mental", "免疫"));
                Assert.AreEqual(2, GameplayTagRuntime.Register(new[] { new GameplayTagDefinition("A.B"), new GameplayTagDefinition("C") }));
            }
            finally { Object.DestroyImmediate(table); }
        }

        [Test]
        public void Catalog_MergesTableAssetsAndRegistry_TreeMenuCountsMatch()
        {
            GameplayTagRegistry.Default.Clear();
            GameplayTagRuntime.Register("Reg.Only", "仅注册表");

            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets/Tests", Path.GetFileName(TempFolder));
            var table = ScriptableObject.CreateInstance<GameplayTagTable>();
            table.Entries.Add(new GameplayTagDefinition("Asset.Table.Leaf", "来自资产"));
            AssetDatabase.CreateAsset(table, TempTable);
            AssetDatabase.SaveAssets();
            GameplayTagEditorCatalog.Rebuild();

            var all = GameplayTagEditorCatalog.All;
            // Reg, Reg.Only, Asset, Asset.Table, Asset.Table.Leaf
            CollectionAssert.AreEqual(
                new[] { T("Asset"), T("Asset.Table"), T("Asset.Table.Leaf"), T("Reg"), T("Reg.Only") }, all);
            Assert.IsTrue(GameplayTagEditorCatalog.IsKnown(T("Asset.Table")));
            Assert.IsFalse(GameplayTagEditorCatalog.IsKnown(T("Nope")));
            Assert.AreEqual("来自资产", GameplayTagEditorCatalog.CommentOf(T("Asset.Table.Leaf")));
            Assert.AreEqual("仅注册表", GameplayTagEditorCatalog.CommentOf(T("Reg.Only")));
            Assert.IsTrue(GameplayTagEditorCatalog.HasChildren(T("Asset")));
            Assert.IsFalse(GameplayTagEditorCatalog.HasChildren(T("Asset.Table.Leaf")));

            // 树菜单：清空项 + 分隔 + 5 个标签项
            var menu = GameplayTagEditorCatalog.BuildTreeMenu("Reg.Only", _ => { });
            Assert.AreEqual(2 + all.Count, menu.GetItemCount());
            var menuNoClear = GameplayTagEditorCatalog.BuildTreeMenu(null, _ => { }, includeClear: false);
            Assert.AreEqual(all.Count, menuNoClear.GetItemCount());

            // 注册表变化自动失效目录
            GameplayTagRuntime.Register("Reg.Later");
            Assert.IsTrue(GameplayTagEditorCatalog.IsKnown(T("Reg.Later")));
        }

        [Test]
        public void Drawers_AreBound_ContainerAndRequirementsHeights()
        {
            var asset = ScriptableObject.CreateInstance<GameplayTagSmokeAsset>();
            try
            {
                asset.container.AddTag("A.1");
                asset.container.AddTag("B");
                var so = new SerializedObject(asset);

                var container = so.FindProperty("container");
                container.isExpanded = false;
                Assert.AreEqual(RowH, EditorGUI.GetPropertyHeight(container, true), 0.01f);      // 折叠：1 行（含间距）
                container.isExpanded = true;
                Assert.AreEqual(RowH * 4, EditorGUI.GetPropertyHeight(container, true), 0.01f);  // 标题 + 控制行 + 2 标签

                var req = so.FindProperty("requirements");
                req.isExpanded = false;
                Assert.AreEqual(RowH, EditorGUI.GetPropertyHeight(req, true), 0.01f);
                req.isExpanded = true;
                float expected = RowH + (RowH + EditorGUIUtility.standardVerticalSpacing) * 2;    // 两个折叠容器各 1 行
                Assert.AreEqual(expected, EditorGUI.GetPropertyHeight(req, true), 0.01f);

                var tag = so.FindProperty("tag");
                Assert.AreEqual(LH, EditorGUI.GetPropertyHeight(tag, true), 0.01f);
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void SerializedProperty_Edits_AreUndoable()
        {
            var asset = ScriptableObject.CreateInstance<GameplayTagSmokeAsset>();
            try
            {
                Undo.IncrementCurrentGroup();
                var so = new SerializedObject(asset);
                var tags = so.FindProperty("container").FindPropertyRelative("tags");
                tags.arraySize++;
                tags.GetArrayElementAtIndex(0).stringValue = "Status.Buff";
                so.ApplyModifiedProperties();
                Assert.AreEqual(1, asset.container.Count);

                Undo.PerformUndo();
                Assert.AreEqual(0, asset.container.Count);
            }
            finally { Object.DestroyImmediate(asset); }
        }
    }
}
