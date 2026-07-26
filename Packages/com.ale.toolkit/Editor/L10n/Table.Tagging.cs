namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// 标签面板（<c>Editor/Tagging/EditorTagPanel.cs</c>）渲染的通用文案的英 / 日译表。
    /// 「给配置对象打标签」是与具体业务无关的基础能力，故其面板文案登记于 toolkit，
    /// 保证纯 toolkit 环境也具备三语。以中文原文为键。
    /// </summary>
    public static partial class ToolkitEditorL10n
    {
        static partial void RegisterTagging()
        {
            Add("新标签", "New Tag", "新規タグ");
            Add("请选择或新建一个功能标签。",
                "Select or create a function tag.",
                "機能タグを選択または新規作成してください。");
            Add("标签ID",       "Tag ID",                  "タグ ID");
            Add("功能标签属性", "Function Tag Attributes", "機能タグの属性");
            Add("背景图",   "Background Sprite", "背景画像");
            Add("背景颜色", "Background Color",  "背景色");
            Add("UI中隐藏", "Hide in UI",        "UI で非表示");
            Add("附加后会自动添加至目标的「属性字段」列表中",
                "Once attached, they are automatically added to the target's \"attribute fields\" list.",
                "付加すると、対象の「属性フィールド」一覧に自動追加されます。");
        }
    }
}
