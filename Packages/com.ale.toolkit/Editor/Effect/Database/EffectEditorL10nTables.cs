using UnityEditor;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Effect.Editor
{
    /// <summary>效果编辑器（<c>Editor/Effect/Database/*.cs</c>）与引用绘制器的英 / 日译表；编辑器初始化时登记到 toolkit 界面多语言表。</summary>
    [InitializeOnLoad]
    internal static class EffectEditorL10nTables
    {
        static EffectEditorL10nTables()
        {
            // ── 页签 / 名词 ───────────────────────────────────────────────────────
            Add("效果",         "Effect",           "エフェクト");
            Add("效果模板",     "Effect Templates", "エフェクトテンプレート");
            Add("Gameplay 标签", "Gameplay Tags",    "ゲームプレイタグ");
            Add("枚举类型",     "Enum Types",       "列挙型");
            Add("新效果",       "New Effect",       "新規エフェクト");
            Add("通用",         "General",          "汎用");
            Add("效果 id",      "effect id",        "エフェクト id");
            Add("效果模板 name", "effect template name", "エフェクトテンプレート name");
            Add("枚举类型 name", "enum type name",   "列挙型 name");

            // ── 窗口 ─────────────────────────────────────────────────────────────
            Add("请创建或选择一个 EffectDatabase 效果库", "Create or select an EffectDatabase asset", "EffectDatabase アセットを作成または選択してください");
            Add("创建效果库",   "Create Effect Database", "エフェクトデータベースを作成");
            Add("请选择数据文件保存位置", "Choose where to save the data file", "データファイルの保存先を選択してください");
            Add("导出 JSON",    "Export JSON",      "JSON エクスポート");
            Add("导出二进制",   "Export Binary",    "バイナリエクスポート");
            Add("导出为 JSON",  "Export as JSON",   "JSON としてエクスポート");
            Add("导出为二进制", "Export as Binary", "バイナリとしてエクスポート");
            Add("已导出 JSON",  "JSON exported",    "JSON をエクスポートしました");
            Add("已导出二进制", "Binary exported",  "バイナリをエクスポートしました");
            Add("无法导出",     "Cannot export",    "エクスポートできません");
            Add("确定",         "OK",               "OK");
            Add("⚠ {0}重复：{1}（导出已禁用）", "⚠ Duplicate {0}: {1} (export disabled)", "⚠ {0} が重複：{1}（エクスポート無効）");

            // ── 效果列表 / Inspector ─────────────────────────────────────────────
            Add("（无可用效果模板；请先在左侧「效果模板」中创建）",
                "(No effect templates; create one in \"Effect Templates\" on the left first)",
                "（エフェクトテンプレートがありません。まず左の「エフェクトテンプレート」で作成してください）");
            Add("瞬时", "Instant",  "即時");
            Add("持续", "Duration", "持続");
            Add("无限", "Infinite", "無限");
            Add("名称", "Name",     "名前");
            Add("描述", "Description", "説明");
            Add("图标", "Icon",     "アイコン");
            Add("策略", "Policy",   "ポリシー");
            Add("内容", "Content",  "内容");
            Add("(空 ID)", "(empty ID)", "（空 ID）");
            Add("{0} 修饰 · {1} 执行 · {2} 标签", "{0} mods · {1} execs · {2} tags", "{0} 修飾 · {1} 実行 · {2} タグ");
            Add("请选择或新建一个效果。", "Select or create an effect.", "エフェクトを選択または新規作成してください。");
            Add("基础信息", "Basic Info", "基本情報");
            Add("⚠ ID 重复或为空", "⚠ Duplicate or empty ID", "⚠ ID が重複または空です");
            Add("修改{0} ID", "Change {0} ID", "{0} ID を変更");
            Add("来源模板", "Source Template", "元のテンプレート");
            Add("（无）", "(none)", "（なし）");
            Add("自定义属性（来自模板 schema）", "Custom Attributes (from template schema)", "カスタム属性（テンプレート schema 由来）");
            Add("（该效果暂无自定义属性字段；可在左侧「效果模板」的 schema 中添加）",
                "(This effect has no custom attribute fields; add them to the template schema in \"Effect Templates\" on the left)",
                "（このエフェクトにはカスタム属性がありません。左の「エフェクトテンプレート」の schema に追加できます）");
            Add("效果定义", "Effect Definition", "エフェクト定義");
            Add("定义内的 id / 显示名由上方 ID / 名称同步；上层系统以 ID 引用本效果。",
                "The definition's id / display name are synced from the ID / Name above; upper systems reference this effect by ID.",
                "定義内の id / 表示名は上の ID / 名前から同期されます。上位システムは ID でこのエフェクトを参照します。");
            Add("效果定义编辑暂不可用（无序列化对象）。", "Effect definition editing is unavailable (no serialized object).", "エフェクト定義の編集は現在利用できません（シリアライズ対象がありません）。");
            Add("效果定义编辑暂不可用。", "Effect definition editing is unavailable.", "エフェクト定義の編集は現在利用できません。");
            Add("定义为空", "Definition is null", "定義が空です");

            // ── 效果模板面板 ─────────────────────────────────────────────────────
            Add("(未命名)", "(unnamed)", "（未命名）");
            Add("请选择或新建一个效果模板。", "Select or create an effect template.", "エフェクトテンプレートを選択または新規作成してください。");
            Add("模板名称", "Template Name", "テンプレート名");
            Add("标识颜色", "Color", "識別色");
            Add("⚠ 模板名称重复或为空", "⚠ Duplicate or empty template name", "⚠ テンプレート名が重複または空です");
            Add("默认效果定义（从模板创建时复制）", "Default Effect Definition (copied on create)", "既定のエフェクト定義（作成時にコピー）");
            Add("默认效果定义", "Default Effect Definition", "既定のエフェクト定義");
            Add("自定义属性字段 schema", "Custom Attribute Schema", "カスタム属性フィールド schema");

            // ── Gameplay 标签面板 ────────────────────────────────────────────────
            Add("请选择或新建一个 Gameplay 标签。", "Select or create a gameplay tag.", "ゲームプレイタグを選択または新規作成してください。");
            Add("本库声明的层级标签（如 Status.Buff.Might）。注册效果库时并入 toolkit 标签注册表，供效果的标签字段下拉与「未登记」提示；运行时匹配不依赖此表。",
                "Hierarchical tags declared by this database (e.g. Status.Buff.Might). Merged into the toolkit tag registry when the database is registered, feeding the tag dropdowns and \"unregistered\" hints of effect fields; runtime matching does not depend on it.",
                "このデータベースが宣言する階層タグ（例：Status.Buff.Might）。データベース登録時に toolkit のタグレジストリへ統合され、エフェクトのタグ欄のドロップダウンと「未登録」表示に使われます。ランタイムのマッチングはこの表に依存しません。");
            Add("(空名)", "(empty name)", "（空の名前）");
            Add("注释", "Comment", "コメント");
            Add("⚠ 名称不合法：段不能为空，段内不能含空白或 '/'",
                "⚠ Invalid name: segments must not be empty and must not contain whitespace or '/'",
                "⚠ 名前が不正です：セグメントを空にできず、空白や '/' を含められません");
            Add("隐式登记的祖先", "Implicitly registered ancestors", "暗黙的に登録される祖先");

            // ── 引用绘制器 / 资产 Inspector / 欢迎窗口 ───────────────────────────
            Add("（暂无使用效果）", "(No effects)", "（エフェクトはありません）");
            Add("打开", "Open", "開く");
            Add("（未找到）", " (not found)", "（未検出）");
            Add("未在任何效果库中找到；运行时按 id 经全局效果注册表解析",
                "Not found in any effect database; resolved by id at runtime via the global effect registry",
                "どのエフェクトデータベースにも見つかりません。ランタイムにグローバルエフェクトレジストリから id で解決されます");
            Add("添加 id", "Add id", "id を追加");
            Add("（无可添加的效果；请先在 Effect Editor 中创建，或在下方输入 id）",
                "(No effects to add; create them in the Effect Editor first, or enter an id below)",
                "（追加できるエフェクトがありません。先に Effect Editor で作成するか、下に id を入力してください）");
            Add("添加{0}", "Add {0}", "{0} を追加");
            Add("在 Effect Editor 中编辑", "Edit in Effect Editor", "Effect Editor で編集");
            Add("效果 {0} · 模板 {1} · Gameplay 标签 {2} · 枚举 {3}", "Effects {0} · Templates {1} · Gameplay tags {2} · Enums {3}", "エフェクト {0} · テンプレート {1} · ゲームプレイタグ {2} · 列挙 {3}");
            Add("推荐通过上方编辑器窗口进行配置；下方为原始数据视图。",
                "Configure via the editor window above; below is the raw data view.",
                "上のエディタウィンドウでの設定を推奨します。下は生データのビューです。");
            Add("打开效果编辑器", "Open Effect Editor", "Effect Editor を開く");
            Add("新建效果库", "New Effect Database", "エフェクトデータベースを新規作成");
            Add("（未选择）", "(none)", "（未選択）");
            Add("（未知）", " (unknown)", "（不明）");
        }
    }
}
