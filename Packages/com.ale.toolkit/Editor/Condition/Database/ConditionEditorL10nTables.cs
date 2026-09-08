using UnityEditor;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.Condition.Editor
{
    /// <summary>
    /// 条件编辑器（Editor/Condition/Database/*.cs）与条件引用绘制器的英 / 日译表；编辑器初始化时登记到 toolkit 界面多语言表。
    /// 只登记条件系统<b>特有</b>的键——与效果编辑器共用的通用词（搜索 / 刷新 / 只看有问题 / 打开脚本 / 诊断…）已由
    /// <c>EffectEditorL10nTables</c> 与 toolkit 框架译表登记，重复登记同一中文键只会互相覆盖，徒增不确定性。
    ///
    /// <para>⚠️ <c>ConditionCompare.Labels</c> 与 <c>GameplayTagMatchMode.Labels</c> <b>绝不能进本表</b>——
    /// 它们是通信格式（配置存索引、外部桥按标签反查），不是 UI 文案，有冻结断言测试守着。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class ConditionEditorL10nTables
    {
        static ConditionEditorL10nTables()
        {
            // ── 名词 ─────────────────────────────────────────────────────────────
            Add("条件", "Condition", "条件");
            Add("条件模板", "Condition Template", "条件テンプレート");
            Add("条件 id", "condition id", "条件 id");
            Add("条件模板 name", "condition template name", "条件テンプレート name");
            Add("条件表达式", "Condition Expression", "条件式");
            Add("新条件", "New Condition", "新しい条件");

            // ── 窗口 ─────────────────────────────────────────────────────────────
            Add("创建条件库", "Create Condition Database", "条件データベースを作成");
            Add("请创建或选择一个 ConditionDatabase 条件库",
                "Create or select a ConditionDatabase",
                "ConditionDatabase を作成または選択してください");
            Add("在 Condition Editor 中编辑", "Edit in Condition Editor", "Condition Editor で編集");
            Add("条件 {0} · 模板 {1} · 枚举 {2}", "Conditions {0} · Templates {1} · Enums {2}",
                "条件 {0} · テンプレート {1} · 列挙 {2}");

            // ── 条件库页（三列）───────────────────────────────────────────────────
            Add("请选择或新建一个条件。", "Select or create a condition.", "条件を選択または新規作成してください。");
            Add("请选择或新建一个条件模板。", "Select or create a condition template.",
                "条件テンプレートを選択または新規作成してください。");
            Add("（无可用条件模板；请先在左侧「条件模板」中创建）",
                "(No condition template available; create one under \"Condition Template\" on the left)",
                "（利用可能な条件テンプレートがありません。左の「条件テンプレート」で作成してください）");
            Add("（该条件暂无自定义属性字段；可在左侧「条件模板」的 schema 中添加）",
                "(This condition has no custom attribute fields yet; add them to the template schema on the left)",
                "（この条件にはカスタム属性フィールドがありません。左の「条件テンプレート」の schema で追加できます）");
            Add("默认条件表达式", "Default Condition Expression", "既定の条件式");
            Add("默认条件表达式（从模板创建时复制）", "Default Condition Expression (copied on create)",
                "既定の条件式（作成時にコピー）");
            Add("上层系统以 ID 引用本条件；求值经 ConditionResolver 按 id 解析。",
                "Upper systems reference this condition by ID; evaluation resolves it by id through ConditionResolver.",
                "上位システムは ID でこの条件を参照し、評価時に ConditionResolver が id で解決します。");
            Add("{0} 组 · {1} 条 · {2}", "{0} groups · {1} items · {2}", "{0} グループ · {1} 件 · {2}");

            // ── 校验 ─────────────────────────────────────────────────────────────
            Add("表达式为空", "The expression is empty", "式が空です");
            Add("表达式没有任何条件项，求值恒为通过。",
                "The expression has no condition items, so it always passes.",
                "条件項目が一つも無いため、評価は常に通過します。");
            Add("组{0} 第{1}项：未选择判定器（空键恒判不通过）",
                "Group {0} item {1}: no evaluator selected (an empty key always fails)",
                "グループ {0} の項目 {1}：判定器が未選択です（空のキーは常に不成立）");
            Add("组{0} 第{1}项：判定器 '{2}' 没有对应实现",
                "Group {0} item {1}: evaluator '{2}' has no implementation",
                "グループ {0} の項目 {1}：判定器 '{2}' の実装がありません");
            Add("条件表达式编辑暂不可用。", "Condition expression editing is unavailable.", "条件式の編集は現在利用できません。");
            Add("条件表达式编辑暂不可用（无序列化对象）。",
                "Condition expression editing is unavailable (no serialized object).",
                "条件式の編集は現在利用できません（シリアライズオブジェクトがありません）。");

            // ── 判定器目录页 ─────────────────────────────────────────────────────
            Add("没有匹配的判定器", "No matching evaluator", "一致する判定器がありません");
            Add("从左侧选择一个判定器", "Select an evaluator on the left", "左から判定器を選択してください");
            Add("尚未被任何条件配置引用", "Not referenced by any condition configuration yet",
                "まだどの条件設定からも参照されていません");
            Add("⚠ 配置里有 {0} 个判定器键没有对应实现（运行时会报「未注册的判定器键」）",
                "⚠ {0} evaluator key(s) referenced by configuration have no implementation (the runtime logs an unregistered-evaluator-key warning)",
                "⚠ 設定から参照されている実装のない判定器キーが {0} 件あります（ランタイムで未登録の判定器キーとして警告されます）");
            Add("实现了 IConditionEvaluator 却没打 [ConditionEvaluator] 特性：编辑器目录与运行时注册表都发现不了它。",
                "Implements IConditionEvaluator but is not marked with [ConditionEvaluator]: neither the editor catalog nor the runtime registry can discover it.",
                "IConditionEvaluator を実装していますが [ConditionEvaluator] 属性がありません。エディタのカタログにもランタイムのレジストリにも検出されません。");
            Add("抽象基类：自身不参与发现，由派生类打上 [ConditionEvaluator] 后被注册。这是判定器基类的正常形态，无需处理。",
                "Abstract base class: it is not discovered itself; derived classes carry [ConditionEvaluator] and get registered. This is the normal shape of an evaluator base class — nothing to fix.",
                "抽象基底クラス：それ自体は検出対象にならず、派生クラスが [ConditionEvaluator] を付けて登録されます。判定器の基底クラスとして正常な形であり、対応は不要です。");
            Add("新增判定器速查", "New Evaluator Cheat Sheet", "判定器の追加ガイド");
            Add("判定器只读不写：从上下文取读侧服务，返回 true / false。写侧的对偶是效果系统的 [EffectExecutor] 执行器。",
                "Evaluators only read, never write: pull a read-side service from the context and return true / false. Their write-side dual is the effect system's [EffectExecutor].",
                "判定器は読み取り専用です：コンテキストから読み取り側サービスを取得し、true / false を返します。書き込み側の対偶はエフェクトシステムの [EffectExecutor] です。");

            // ── 引用绘制器 ───────────────────────────────────────────────────────
            Add("（暂无引用条件）", "(No referenced conditions)", "（参照条件はありません）");
            Add("未在任何条件库中找到；运行时按 id 经全局条件注册表解析",
                "Not found in any condition database; resolved by id at runtime via the global condition registry",
                "どの条件データベースにも見つかりません。ランタイムにグローバル条件レジストリから id で解決されます");
            Add("（无可添加的条件；请先在 Condition Editor 中创建，或在下方输入 id）",
                "(No conditions to add; create them in the Condition Editor first, or enter an id below)",
                "（追加できる条件がありません。先に Condition Editor で作成するか、下に id を入力してください）");
        }
    }
}
