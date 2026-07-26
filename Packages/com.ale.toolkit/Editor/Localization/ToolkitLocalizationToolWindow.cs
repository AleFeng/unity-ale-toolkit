#if ATK_LOCALIZATION
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 通用本地化工具窗口（仅 ATK_LOCALIZATION 编译）。对<b>任意</b>数据库资产（<see cref="ScriptableObject"/>）：
    /// 生成 / 关联一个 String Table 集合，并遍历其内全部 Text 型 <c>AttributeValue</c>（经 <see cref="TextFieldWalker"/>）
    /// 自动生成唯一 Key、写回引用、建表条目。建表 / Key 生成 / 进度条 / 日志全部继承自
    /// <see cref="EditorLocalizationToolWindow{TDb}"/>；本类只提供菜单入口、Text 字段来源与表集合绑定。
    ///
    /// <para><b>表集合绑定不落任何独立存储</b>：从已生成字段的 <c>tableRef</c> <b>反推</b>当前库关联的集合
    /// （生成过 Key 后即随数据本身保留、可提交、团队共享）；尚未生成 Key 时由用户在窗口内经「关联多语言表」
    /// ObjectField 选择，仅在会话内暂存到首次生成为止。与专用子类相比：Key 为结构化路径而非业务语义 Key。</para>
    /// </summary>
    public class ToolkitLocalizationToolWindow : EditorLocalizationToolWindow<ScriptableObject>
    {
        // 表集合绑定：以字段 tableRef 为真相源。数据库变化时反推一次并缓存；
        // _cachedGuid 同时充当「首次生成前用户选择」的会话内暂存值。
        private ScriptableObject _cachedDb;
        private string           _cachedGuid;

        [MenuItem("Tools/Ale Toolkit/Localization/本地化工具窗口", priority = 100)]
        public static void Open()
        {
            var win = GetWindow<ToolkitLocalizationToolWindow>(true, "本地化建表 / Key 生成（通用）", true);
            win.minSize = new Vector2(500f, 460f);
            win.Show();
        }

        /// <summary>本库全部 Text 型属性值（反射遍历，结构化 Key 路径）。</summary>
        protected override IReadOnlyList<TextFieldRef> CollectTextFields(ScriptableObject db)
            => TextFieldWalker.Collect(db);

        /// <summary>
        /// 表集合的 SharedTableData GUID。<b>get</b>：数据库切换时从字段 <c>tableRef</c> 反推一次并缓存
        /// （反推不到则回退到会话暂存值）；<b>set</b>：写入会话暂存（供首次生成前记住用户选择，生成后由字段承载）。
        /// </summary>
        protected override string TableCollectionGuid
        {
            get
            {
                if (database != _cachedDb)
                {
                    _cachedDb   = database;
                    _cachedGuid = DeriveGuidFromFields();
                }
                return _cachedGuid;
            }
            set
            {
                _cachedDb   = database;
                _cachedGuid = value;
            }
        }

        /// <summary>扫描本库 Text 字段，取首个非空 <c>tableRef</c>（集合名）对应集合的 SharedTableData GUID；无则 null。</summary>
        private string DeriveGuidFromFields()
        {
            if (!database) return null;
            foreach (var r in CollectTextFields(database))
            {
                var (tableRef, _) = r.Value.GetLocalizedStringRef(r.Element);
                if (string.IsNullOrEmpty(tableRef)) continue;
                foreach (var c in LocalizationEditorSettings.GetStringTableCollections())
                    if (c && c.SharedData && c.TableCollectionName == tableRef)
                        return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c.SharedData));
            }
            return null;
        }
    }
}
#endif
