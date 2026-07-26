namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 三列编辑器框架基类（<c>Editor/Framework/Editor*.cs</c>）与通用控件
    /// （过滤页签栏、可搜索列表等）渲染的领域无关文案的英 / 日译表。
    /// 这些串由 toolkit 自身渲染，登记于此以保证纯 toolkit 环境也具备三语；
    /// <c>{0}</c> 占位符在运行时由宿主插件传入的实体名词（如「道具」「仓库」）填充。
    /// 以中文原文为键。
    /// </summary>
    public static partial class ToolkitEditorL10n
    {
        static partial void RegisterFramework()
        {
            // ── 实体列表 / 三列框架（EditorEntityListPanel / EditorThreeColumnTab）───
            Add("从模板添加", "Add from Template", "テンプレートから追加");
            Add("快速添加",   "Quick Add",         "クイック追加");
            Add("{0} Inspector", "{0} Inspector", "{0} インスペクター");
            Add("删除{0}",    "Delete {0}",        "{0}を削除");
            Add("（无可用{0}模板）",
                "(No {0} templates available)",
                "（利用可能な{0}テンプレートがありません）");

            // ── 通用控件（EditorFilterTabs / EditorSearchableList）─────────────────
            Add("全部",   "All",      "すべて");
            Add("搜索",   "Search",   "検索");
            Add("无匹配", "No match", "一致なし");

            // ── 通用标签（多处绘制器共用）─────────────────────────────────────────
            Add("名称", "Name",        "名称");
            Add("描述", "Description", "説明");
        }
    }
}
