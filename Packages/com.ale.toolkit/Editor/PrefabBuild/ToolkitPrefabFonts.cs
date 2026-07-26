#if ATK_TMP
using TMPro;
#endif
#if ATK_TMP && ATK_LOCALIZATION
using Ale.Toolkit.Runtime.UI;
#endif

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 预制体生成向导使用的字体配置门面。实际数据存于项目级 <see cref="ToolkitProjectSettings"/>
    /// （随仓库入库、按 GUID 存资源引用，详见其说明）；本类仅提供简洁的静态读写入口，
    /// 供欢迎窗口配置、各插件向导读取。宏未启用时对应属性不参与编译。
    /// </summary>
    public static class ToolkitPrefabFonts
    {
#if ATK_TMP
        /// <summary>向导生成 Prefab 时套用于所有 TMP 文本节点的默认字体（留空则用 TMP 内置默认字体）。</summary>
        public static TMP_FontAsset DefaultTmpFont
        {
            get => ToolkitProjectSettings.instance.defaultTmpFont;
            set
            {
                var s = ToolkitProjectSettings.instance;
                if (s.defaultTmpFont == value) return;
                s.defaultTmpFont = value;
                s.SaveSettings();
            }
        }
#endif

#if ATK_TMP && ATK_LOCALIZATION
        /// <summary>
        /// 向导生成 Prefab 时赋给 <c>LocalizedFontEvent</c> 的本地化字体引用（表 + 条目）。
        /// 返回项目级设置里的实例；其编辑与持久化由欢迎窗口直接绘制在
        /// <see cref="ToolkitProjectSettings"/> 的 <c>SerializedObject</c> 上完成（原生序列化正确处理内嵌引用）。
        /// </summary>
        public static LocalizedTmpFont LocalizedFont => ToolkitProjectSettings.instance.localizedFont;
#endif
    }
}
