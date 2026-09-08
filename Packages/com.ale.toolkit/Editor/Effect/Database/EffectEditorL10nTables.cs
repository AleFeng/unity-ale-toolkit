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

            // ── 执行器目录页签（Effect Executors）─────────────────────────────────
            Add("只看有问题", "Issues only", "問題のみ");
            Add("刷新", "Refresh", "更新");
            Add("已发现 {0} / 共 {1}", "{0} discovered / {1} total", "検出 {0} / 全 {1}");
            Add("⚠ 配置里有 {0} 个执行器键没有对应实现（运行时会报「未注册的执行器键」）",
                "⚠ {0} executor key(s) referenced by configuration have no implementation (the runtime logs an unregistered-executor-key warning)",
                "⚠ 設定から参照されている実装のない実行器キーが {0} 件あります（ランタイムで未登録の実行器キーとして警告されます）");
            Add("{0} 处引用", "{0} reference(s)", "参照 {0} 件");
            Add("没有匹配的执行器", "No matching executor", "一致する実行器がありません");
            Add("从左侧选择一个执行器", "Select an executor on the left", "左から実行器を選択してください");
            Add("分类 {0} · 被引用 {1} 处", "Category {0} · {1} reference(s)", "カテゴリ {0} · 参照 {1} 件");
            Add("分类 {0} · 未被任何配置引用", "Category {0} · not referenced by any configuration",
                "カテゴリ {0} · どの設定からも参照されていません");
            Add("实现", "Type", "実装");
            Add("程序集", "Assembly", "アセンブリ");
            Add("脚本", "Script", "スクリプト");
            Add("无源码（来自程序集 {0}）", "No source (from assembly {0})", "ソースなし（アセンブリ {0}）");
            Add("打开脚本", "Open Script", "スクリプトを開く");
            Add("在 Project 中定位", "Show in Project", "Project で表示");
            Add("复制 Key", "Copy Key", "Key をコピー");
            Add("参数（{0}）", "Parameters ({0})", "パラメータ（{0}）");
            Add("无参数", "No parameters", "パラメータなし");
            Add("被引用（{0}）", "References ({0})", "参照（{0}）");
            Add("尚未被任何效果配置引用", "Not referenced by any effect configuration yet",
                "まだどのエフェクト設定からも参照されていません");
            Add("跳转", "Go", "移動");
            Add("诊断", "Diagnostics", "診断");
            Add("找不到 {0} 的源码脚本", "Source script for {0} not found", "{0} のソーススクリプトが見つかりません");

            // 诊断文案
            Add("实现了 IEffectExecutor 却没打 [EffectExecutor] 特性：编辑器目录与运行时注册表都发现不了它。",
                "Implements IEffectExecutor but is not marked with [EffectExecutor]: neither the editor catalog nor the runtime registry can discover it.",
                "IEffectExecutor を実装していますが [EffectExecutor] 属性がありません。エディタのカタログにもランタイムのレジストリにも検出されません。");
            Add("特性里写的是 '{0}'，实际生效的是 Key 属性的 '{1}'——特性字符串从不被读取，请改成一致。",
                "The attribute says '{0}' but the effective key is the Key property's '{1}' — the attribute string is never read; please make them match.",
                "属性には '{0}'、実際に有効なのは Key プロパティの '{1}' です。属性の文字列は読まれません。一致させてください。");
            Add("键 '{0}' 有多个实现：编辑器目录取先发现的、运行时注册表取后注册的，两边可能不是同一个。",
                "Key '{0}' has multiple implementations: the editor catalog keeps the first one found while the runtime registry keeps the last one registered — they may not be the same.",
                "キー '{0}' に複数の実装があります。エディタのカタログは最初に見つかったもの、ランタイムのレジストリは最後に登録されたものを採用するため、一致しない可能性があります。");
            Add("Key 属性为空：目录与注册表都会跳过它。",
                "The Key property is empty: both the catalog and the registry skip it.",
                "Key プロパティが空です。カタログもレジストリもスキップします。");
            Add("抽象基类：自身不参与发现，由派生类打上 [EffectExecutor] 后被注册。这是执行器基类的正常形态，无需处理。",
                "Abstract base class: it is not discovered itself; derived classes carry [EffectExecutor] and get registered. This is the normal shape of an executor base class — nothing to fix.",
                "抽象基底クラス：それ自体は検出対象にならず、派生クラスが [EffectExecutor] を付けて登録されます。実行器の基底クラスとして正常な形であり、対応は不要です。");
            Add("说明", "Note", "説明");
            Add("缺少公开无参构造：目录与注册表都会跳过它。",
                "No public parameterless constructor: both the catalog and the registry skip it.",
                "public な引数なしコンストラクタがありません。カタログもレジストリもスキップします。");
            Add("实例化失败：请检查构造函数与静态初始化。",
                "Instantiation failed: check the constructor and static initialization.",
                "インスタンス化に失敗しました。コンストラクタと静的初期化を確認してください。");

            // 速查区
            Add("新增执行器速查", "New Executor Cheat Sheet", "実行器の追加ガイド");
            Add("内置阶段：{0}（空阶段组视为通配，Normalize() 会改写为 onApply；宿主自定义阶段经 EffectContainer.RunPhase 触发）",
                "Built-in phases: {0} (an empty phase group is a wildcard and Normalize() rewrites it to onApply; host-defined phases are triggered via EffectContainer.RunPhase)",
                "組み込みフェーズ：{0}（空のフェーズグループはワイルドカードで、Normalize() が onApply に書き換えます。ホスト独自のフェーズは EffectContainer.RunPhase で発火します）");
            Add("最小实现模板", "Minimal implementation template", "最小実装テンプレート");
            Add("复制模板", "Copy Template", "テンプレートをコピー");
        }
    }
}
