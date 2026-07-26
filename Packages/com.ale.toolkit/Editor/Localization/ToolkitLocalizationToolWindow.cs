#if ATK_LOCALIZATION
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 通用本地化工具窗口（仅 ATK_LOCALIZATION 编译）。对<b>任意</b>数据库资产（<see cref="ScriptableObject"/>）：
    /// 生成 / 关联一个 String Table 集合，并遍历其内全部 Text 型 <c>AttributeValue</c>（经 <see cref="TextFieldWalker"/>）
    /// 自动生成唯一 Key、写回引用、建表条目。建表 / Key 生成 / 进度条 / 日志全部继承自
    /// <see cref="EditorLocalizationToolWindow{TDb}"/>；本类只提供菜单入口、Text 字段来源与表集合 GUID 存取。
    ///
    /// <para>与库存等专用子类的两点差别（通用工具固有）：① Key 为结构化反射路径（无业务语义中文 Key）；
    /// ② 通用 SO 上无存放表集合 GUID 的字段，改用 <see cref="EditorPrefs"/> 按资产 GUID 记录
    /// （本机本地、不进资产 / 版本库）。需要语义 Key 或表绑定入资产的宿主，请用其自建的专用窗口。</para>
    /// </summary>
    public class ToolkitLocalizationToolWindow : EditorLocalizationToolWindow<ScriptableObject>
    {
        private const string TableGuidPrefKeyPrefix = "AleToolkit.Localization.TableGuid.";

        [MenuItem("Tools/Ale Toolkit/Localization/本地化工具窗口", priority = 100)]
        public static void Open()
        {
            var win = GetWindow<ToolkitLocalizationToolWindow>(true, "本地化建表 / Key 生成（通用）", true);
            win.minSize = new Vector2(500f, 460f);
            win.Show();
        }

        /// <summary>表集合 GUID 按当前数据库资产 GUID 存 / 取 <see cref="EditorPrefs"/>。</summary>
        protected override string TableCollectionGuid
        {
            get
            {
                string k = PrefKey();
                return string.IsNullOrEmpty(k) ? null : EditorPrefs.GetString(k, null);
            }
            set
            {
                string k = PrefKey();
                if (string.IsNullOrEmpty(k)) return;
                if (string.IsNullOrEmpty(value)) EditorPrefs.DeleteKey(k);
                else                             EditorPrefs.SetString(k, value);
            }
        }

        /// <summary>本库全部 Text 型属性值（反射遍历，结构化 Key 路径）。</summary>
        protected override IReadOnlyList<TextFieldRef> CollectTextFields(ScriptableObject db)
            => TextFieldWalker.Collect(db);

        /// <summary>按当前数据库资产 GUID 生成 EditorPrefs 键；无资产 / 未落盘返回 null。</summary>
        private string PrefKey()
        {
            if (!database) return null;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(database));
            return string.IsNullOrEmpty(guid) ? null : TableGuidPrefKeyPrefix + guid;
        }
    }
}
#endif
