namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// <see cref="ToolkitWelcomeWindow"/>（Ale Toolkit 欢迎 / 全局设置窗口）的英 / 日译表：
    /// 分区标题、通用工具按钮、去领域化的可选依赖宏描述与文档提示。中文为源语言，只登记英、日两栏。
    /// 通用面板文案（警告 / 确定 / 取消 / 已安装 / 未安装 / 等待重编译 / 枚举值）已在 <c>Table.Defines</c>。
    /// </summary>
    public static partial class ToolkitEditorL10n
    {
        static partial void RegisterWelcome()
        {
            // ── 页眉 / 分区标题 ──────────────────────────────────────────────────
            Add("通用底层库 · 全局设置",
                "Foundation library · Global settings",
                "汎用基盤ライブラリ · グローバル設定");
            Add("界面语言", "Editor Language", "エディター言語");
            Add("通用工具", "General Tools",   "汎用ツール");
            Add("插件支持（编译宏）", "Plugin Support (Defines)", "プラグインサポート（マクロ）");

            // ── 通用工具区 ────────────────────────────────────────────────────────
            Add("打开 本地化工具窗口",     "Open Localization Tool", "ローカライズツールを開く");
            Add("打开 Addressable工具窗口", "Open Addressable Tool",  "Addressable ツールを開く");
            Add("查看文档",                "View Docs",              "ドキュメントを見る");
            Add("通用工具对任意数据文件（ScriptableObject）生效，遍历其全部属性值批量处理。",
                "General tools work on any data asset (ScriptableObject), walking all of its attribute values for batch processing.",
                "汎用ツールは任意のデータアセット（ScriptableObject）に対して動作し、その全属性値を走査して一括処理します。");

            // ── 宏开关区 ──────────────────────────────────────────────────────────
            Add("项目级全局开关：一处启用即全项目生效，无需各插件分别设置。",
                "Project-level global switches: enable once and it applies to the whole project — no need to set them per plugin.",
                "プロジェクト単位のグローバルスイッチ：一度有効にするとプロジェクト全体に適用され、プラグインごとに設定する必要はありません。");

            // TextMeshPro（去领域化）
            Add("启用后，UI 脚本的文本组件使用 TMP_Text；未启用时使用 UnityEngine.UI.Text。Unity 2021+ 已内置 TextMeshPro，通常可直接启用。",
                "When enabled, text components of UI scripts use TMP_Text; otherwise UnityEngine.UI.Text is used. TextMeshPro is built into Unity 2021+, so it can usually be enabled directly.",
                "有効にすると、UI スクリプトのテキストコンポーネントが TMP_Text を使用します。無効時は UnityEngine.UI.Text を使用します。TextMeshPro は Unity 2021+ に内蔵されているため、通常はそのまま有効にできます。");
            Add("TMPro 命名空间未检测到。\n请确认 TextMeshPro 已通过 Package Manager 安装。\n\n确定要继续启用吗？",
                "The TMPro namespace was not detected.\nPlease make sure TextMeshPro is installed via Package Manager.\n\nEnable anyway?",
                "TMPro 名前空間が検出されませんでした。\nTextMeshPro が Package Manager 経由でインストールされているか確認してください。\n\nこのまま有効にしますか？");

            // Unity Localization（与库存同措辞，共享同一键）
            Add("启用后，属性字段类型可选择 LocalizedString，支持 Unity Localization 多语言配置。",
                "When enabled, attribute field types can use LocalizedString for Unity Localization multi-language configuration.",
                "有効にすると、属性フィールドの型で LocalizedString を選択でき、Unity Localization による多言語設定に対応します。");
            Add("com.unity.localization 包尚未安装。\n启用宏后，LocalizedString 字段将出现在编辑器中，但运行时无法解析。\n\n确定要继续启用吗？",
                "The com.unity.localization package is not installed.\nAfter enabling the define, LocalizedString fields will appear in the editor but cannot be resolved at runtime.\n\nEnable anyway?",
                "com.unity.localization パッケージがインストールされていません。\nマクロを有効にすると LocalizedString フィールドがエディターに表示されますが、実行時には解決できません。\n\nこのまま有効にしますか？");

            // Unity Addressable（去领域化：去掉导出登记句，菜单指向 Ale Toolkit）
            Add("启用后，属性系统的资源字段（Sprite/Prefab 等）在编辑器改用原生 AssetReference 选择器授权（仅存 GUID，" +
                "不硬引用、加载数据库不再一并载入资源）；运行时经 Addressable 按需异步加载、引用计数随宿主销毁自动卸载。" +
                "可用菜单 Tools/Ale Toolkit/Addressable/Addressable工具窗口在「Object 引用 ↔ AssetReference(GUID)」间批量互转。",
                "When enabled, asset fields of the attribute system (Sprite/Prefab, etc.) switch to the native AssetReference selector in the editor " +
                "(storing only the GUID, no hard references, so loading the database no longer loads the assets too); at runtime they are loaded " +
                "asynchronously on demand via Addressable and unloaded automatically by reference counting when the host is destroyed. " +
                "Use the menu Tools/Ale Toolkit/Addressable/Addressable Tool to batch-convert between \"Object reference ↔ AssetReference(GUID)\".",
                "有効にすると、属性システムのアセットフィールド（Sprite/Prefab など）がエディターでネイティブの AssetReference セレクターに切り替わります" +
                "（GUID のみを保存し、ハード参照しないため、データベースを読み込んでもアセットは同時に読み込まれません）。実行時は Addressable で必要に応じて" +
                "非同期読み込みし、参照カウントによりホストの破棄時に自動でアンロードされます。" +
                "メニュー Tools/Ale Toolkit/Addressable/Addressable ツールで「Object 参照 ↔ AssetReference(GUID)」を一括変換できます。");
            Add("com.unity.addressables 包尚未安装。\n启用宏后，运行时无法通过 Addressable 加载资源。\n\n确定要继续启用吗？",
                "The com.unity.addressables package is not installed.\nAfter enabling the define, assets cannot be loaded via Addressable at runtime.\n\nEnable anyway?",
                "com.unity.addressables パッケージがインストールされていません。\nマクロを有効にすると、実行時に Addressable でアセットを読み込めません。\n\nこのまま有効にしますか？");

            // ── 文档 ──────────────────────────────────────────────────────────────
            Add("文档未找到", "Documentation Not Found", "ドキュメントが見つかりません");
            Add("未能找到文档文件：\nPackages/com.ale.toolkit/README.md",
                "Could not find the documentation file:\nPackages/com.ale.toolkit/README.md",
                "ドキュメントファイルが見つかりませんでした：\nPackages/com.ale.toolkit/README.md");
        }
    }
}
