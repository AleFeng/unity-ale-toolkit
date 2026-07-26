using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 通用 Addressable 资源引用迁移窗口（仅 ATK_ADDRESSABLE 编译）。对<b>任意</b>数据库资产
    /// （<see cref="ScriptableObject"/>）批量互转其属性系统内全部对象类 <c>AttributeValue</c>
    /// （Sprite / Prefab / Texture / Material …）的「Object 引用 ↔ AssetReference(GUID)」存储。
    ///
    /// <para>遍历 / 迁移 / 进度条 / 日志 / 取消 全部继承自 <see cref="EditorAddressableToolWindow{TDb}"/>
    /// （经 <c>AttributeValueWalker</c> 反射遍历全库）。本类只提供菜单入口；因通用（无领域知识），
    /// 不提供属性系统之外的具名固定资源字段——那类字段（如某插件的图标字段）由该插件自建的专用子类
    /// 经 <see cref="EditorAddressableToolWindow{TDb}.FixedFields"/> 覆盖。</para>
    /// </summary>
    [InitializeOnLoad]
    public class ToolkitAddressableToolWindow : EditorAddressableToolWindow<ScriptableObject>
    {
        // 注入钩子：让 core 欢迎窗口（对 Addressables 零依赖、看不见本类型）得以打开本窗口。
        // 仅在本程序集参与编译（ATK_ADDRESSABLE 启用）时执行，故宏关闭时欢迎窗口对应按钮自动隐藏。
        static ToolkitAddressableToolWindow()
        {
            ToolkitWelcomeWindow.OpenAddressableMigration = Open;
        }

        [MenuItem("Tools/Ale Toolkit/Addressable/Addressable工具窗口", priority = 100)]
        public static void Open()
        {
            var win = GetWindow<ToolkitAddressableToolWindow>(true, "Addressable 资源迁移（通用）", true);
            win.minSize = new Vector2(460f, 420f);
            win.Show();
        }

        /// <summary>通用工具无领域知识，不提供属性系统之外的固定资源字段。</summary>
        protected override IEnumerable<AddressableFixedField> FixedFields(ScriptableObject db)
            => Enumerable.Empty<AddressableFixedField>();
    }
}
