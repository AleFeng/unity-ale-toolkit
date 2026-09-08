# 更新日志（Changelog）

本文件记录 Ale Toolkit（`com.ale.toolkit`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

> 由来：本包自 `com.ale.inventory` 1.8.0 拆分而来。原先埋在库存系统里的通用能力被抽出，使其可被更多插件复用（例如后续的角色系统）。拆分过程中**导出格式与序列化结构不变**，类型的命名空间由 `Ale.Inventory.*` 改为 `Ale.Toolkit.*`。

## [1.12.0] - 2026-09-08

**条件系统补齐到与效果系统对称：新增共用条件库 `ConditionDatabase` 与两页签 Condition Editor。** 条件系统自 1.8.0 起就停在「声明一个 `ConditionExpression` 字段、在 Inspector 内联配」的形态——条件没有 id、不能跨系统复用，同一句「力量 ≥ 10 且拥有勇敢特质」在特质 / 职业 / 头衔 / 技能树里各配一遍；判定器目录也比执行器目录弱一整代（只按特性扫、没有引用统计、没有静默失效诊断、不能跳源码）；参数里的 `attrId` / `traitId` / `titleId` 更是一律裸文本框，打错一个字静默返回 false——`ConditionEngine` 只对**未注册的判定器键**告警，对写错的**参数值**完全无声，而同一个属性 id 在效果编辑器里却是按系统名分组的下拉。本版把 1.10.0～1.11.0 给效果系统铺的那条路原样铺给条件系统：条件成为可按 id 引用的具名条目、Condition Editor 两页签（先看实现、再配数据）、参数候选可由宿主注入。本版新增 18 个测试（共 221）。

### 破坏性变更

- ⚠️ **`Ale.Condition.Runtime` 新增引用 `Ale.Toolkit.Runtime`**（条件库需要属性系统：`AttributeOwner` / `ConfigTemplateBase` / `AttributeValue` / `EnumType` / `ToolkitSingleton` / 序列化编解码）。性质与 1.10.0 给 `Ale.Effect.Runtime` 加同一依赖完全相同：只引用 `Ale.Condition.Core` 的服务端 / 纯 C# 消费方**不受影响**（Core 仍零依赖、`noEngineReferences`）；引用 `Ale.Condition.Runtime` 的宿主从此连带 toolkit 全家桶。`Ale.Condition.Editor` 另新增引用 `Ale.Toolkit.Runtime` / `Ale.Toolkit.Editor`（三列框架与界面多语言），`Ale.Effect.Editor` 新增引用 `Ale.Condition.Editor`（见下文「用法提供者」）——方向仍单向无环。
- **运行时引导拆分**：`ConditionRuntime` 拆成 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 清空（具名条件注册表 + 告警去重集）与 `[BeforeSceneLoad]` 只做加法（判定器自动注册 + 两条告警接线 + `Resources` 加载）。与 1.10.0 给 `EffectRuntime` 做的拆分同因：同一 LoadType 内跨程序集的回调顺序无保证，「清空」与「加法」混在一起会互相抹除。

### 新增

- **条件库 `ConditionDatabase`**（`Ale.Condition.Runtime`，`Runtime/Condition/Unity/Database/`；`Create > Ale > Condition > Condition Database`）：三个顶层列表——`enumTypes`（自定义属性的枚举字段用）、`conditionTemplates`、`conditions`；实现 `IEnumTypeSource` / `IConditionSchemaSource` / `IConditionDefinitionSource`；`GetEntry(id)` / `GetTemplate(name)` / `GetEnumType(name)`、`NormalizeAll` / `RebuildAllAttributes` / `AddEnumType` / `CloneFrom`；`Validate(out errors)`：枚举名 / 模板名 / 条件 id 重复、`templateRef` 悬空、表达式为空、**条件项未选择判定器**（空键在 `ConditionEngine` 恒判不通过，属半配置状态，以前完全无声）。本库**不持有 Gameplay 标签**——标签归 `EffectDatabase` 统一声明，条件侧只经两个标签判定器读取。
  - **`ConditionTemplate : ConfigTemplateBase`**：`name` / `color` / `attributes`（自定义属性 schema）+ `defaultExpression`（从模板创建条件时深拷贝作预设）。
  - **`ConditionEntry : AttributeOwner`**：`id` / `templateRef` / `displayText` / `descriptionText` / `iconValue`（`AttributeValue` Text / Text / Sprite）/ `values` / `expression`；`Normalize()` / `RebuildAttributes(IConditionSchemaSource)` / `ResolveDisplayName()` / `PlainName()` / `Clone()`。显示字段的用处是直接喂给「未满足时」的玩家提示 UI。
  - 为什么要包这一层：`ConditionExpression` 是已发布的纯 POCO、有 JSON 往返格式与 34 个测试守着，**不动它**；具名与显示信息由外层条目承担。序列化深度只有 5 层（库 → 条目 → 表达式 → 组 → 项 → 参数），余量充足。
- **按 id 引用的运行时链路**（`Ale.Condition.Core`）：`IConditionDefinitionSource` + `ConditionDefinitionRegistry`（`Default` / `AddSource` 幂等 / `RemoveSource` / `Register` / `Unregister` / `GetCondition` / `TryGet` / `Clear`）+ `ConditionResolver`（`Resolve` / `Evaluate` / `IsSatisfied`）。解析顺序：上下文的 `IConditionDefinitionSource` → 显式回落源 → 全局注册表。⚠️ **解析不到时 fail-closed**——返回不通过、把该 id 放进 `FailedKeys`、经 `MissingIdWarning` 告警；门控场景下 id 写错应当锁住内容而不是放行，与 `ConditionEngine` 对未注册判定器键的处理一致。
- **`ConditionDataManager : ToolkitSingleton`**（`IConditionDefinitionSource` / `IEnumTypeSource` / `IConditionSchemaSource`）：`Register(db)`（幂等；`NormalizeAll`；首次登记时 `ConditionDefinitionRegistry.Default.AddSource(this)`）/ `Unregister` / `ClearDatabases` / `LoadFromBinary` / `LoadFromJson`；惰性索引 `GetEntry` / `GetTemplate` / `GetEnumType` / `GetAllConditions`。`ConditionRuntime.AutoLoadFromResources`（静态开关，默认 true）为真时启动自动 `Resources.LoadAll<ConditionDatabase>` 逐个登记。
- **`ConditionConfigSerializer`**（命名空间 `Ale.Condition.Serialization`）：魔数 `CNDB`、`Version = 1`；JSON（`JsonUtility`，表达式直接内嵌）与二进制（表达式以 `ConditionJson` 串承载，属性值经 `ToolkitBinaryCodec` / `ToolkitDtoMapper`）的 `Export` / `Import` / `ImportInto` 往返。
- **Condition Editor**（`Ale.Condition.Editor`，`Editor/Condition/Database/`；菜单 `Tools > Ale Toolkit > Condition System > Condition Editor`，priority 2002）：`EditorDatabaseWindowBase<ConditionDatabase>` 外壳 + 两个页签，与 Effect Editor 逐文件对称。
  - **Condition Evaluators**（第一页；内容来自**代码**，没有条件库也能用）：工程内全部 `IConditionEvaluator` 实现的目录，按分类分组、可按 键 / 显示名 / 类型全名 / 程序集 搜索、可「只看有问题」；右列给出实现类型 / 程序集 / 源码路径与「打开脚本」（定位到类声明行）/「在 Project 中定位」/「复制 Key」（双击行等同打开脚本）、参数 schema、被哪些配置引用（可跳转）、诊断；顶部横幅列出「配置引用了但没有实现」的悬空键；底部速查给最小实现模板。
  - **Condition Database**（第二页）：左列 条件模板 / 枚举类型，中列条件列表（模板过滤 / 搜索 / 从模板添加 = 克隆 `defaultExpression` + 按 schema 建属性 / 快速添加），右列 ID 查重 + 名称 / 描述 / 图标 + 自定义属性 + 内联表达式 + 校验摘要（**当场报「未选择判定器」与「判定器 X 没有对应实现」**——后者查编辑期目录，条件系统以前完全没有的保障）；查重阻断导出，导出 JSON / 二进制。`ConditionEditorWindow.Open(db, conditionId)` 定位到指定条件，`OpenEvaluators()` 切到第一页。
- **`ConditionEvaluatorIndex`**：候选类型取 `TypeCache.GetTypesDerivedFrom<IConditionEvaluator>()` 与 `GetTypesWithAttribute<ConditionEvaluatorAttribute>()` 的并集，七种诊断标记 `EEvaluatorIssue` + `InformationalIssues`（抽象基类只作说明，不标红、不计入「只看有问题」）+ `HasProblem`；`Rows` / `Usages` / `DanglingKeys` / `Categories` / `DiscoveredCount` / `UsagesOf(key)`；纯函数 `BuildRows` / `CollectKeys` / `Filter` 可直接单测。
  - **用法提供者**：索引直接扫 `ConditionDatabase`（条目 + 模板默认表达式）与 `ConditionAsset`；**效果系统里的条件用法**（每条效果的 `applicationCondition`、各执行项的 `gate`）由新增的 `Editor/Effect/EffectConditionUsageProvider` 经 `RegisterUsageProvider` 贡献。`ConditionKeyUsage` 自带 `Action Jump`，跳转回 Effect Editor 由效果侧自己完成——条件系统不必认识效果系统，依赖方向保持 `Ale.Effect.Editor → Ale.Condition.Editor`，无环。
- **`ConditionUsageCollector`**（`Ale.Condition.Editor`）：写用法提供者的样板助手——`Collect(into, expr, asset, ownerId, where, jump)` 把一条表达式里的判定器键收成 `ConditionKeyUsage`，`Where(prefix, gi, ii)` **统一位置文案格式**为「{前缀} · 组N 第M项」，`ForEachAsset<T>` 包掉 `FindAssets` + `Load` 的样板。下沉的价值主要不在省代码（宿主要写的「走自己的字段」本就无法下沉），而在锁死用法列表里那一列**用户可见的文案**：各系统若各写各的格式，列表就会花。索引自身的扫描与效果侧提供者一并改用它。
- **`ConditionEditorCatalog`**（工程内全部条件库的条目索引，`AssetPostprocessor` 失效重建）与 **`EditorConditionRefListDrawer`**（宿主 Inspector 的条件 id 引用列表：目录菜单 / 拖拽重排 / 「打开」跳转 / 「未找到」标注 / 自由输入）；`ConditionDatabase` 资产 Inspector。
- **条件参数的候选来源注入点**：`ConditionParamDef` 新增可选 `catalogRef`（判定器自己声明「这个字符串参数装的是哪一类 id」，排在 `choices` 之后，既有位置参数调用点不受影响）+ `Editor/Condition/ConditionDrawerHooks`（`IConditionParamCatalogProvider { SystemName; CatalogRef; GetItems() }` / `ParamIdField` 整字段委托 / `RegisterProvider` / `CollectCandidates` / `DrawParamId`：委托 → 按系统名分组的下拉 → 文本框三级回落）。`ConditionExpressionDrawer` 的字符串参数（标量与数组两条路径）据此把裸文本框换成候选下拉；`catalogRef` 为空或该目录无候选时行为与 1.11.0 完全一致。
- 测试：`ConditionDatabaseTests`（11：归一 / 深拷贝 / schema 对账 / 校验各分支 / JSON 与二进制往返 / 数据管理器幂等·跨库先注册先得·自动登记为全局源 / 注册表本地优先 / 解析三级顺序 / fail-closed）、`ConditionEditorIndexTests`（7：诊断标记与 `Discovered`、抽象基类只作说明、键收集、过滤、目录索引、候选提供者登记 / 合并 / 注销）；测试程序集新增引用 `Ale.Condition.Runtime` / `Ale.Condition.Editor`。

### 变更

- 条件系统欢迎窗口（`Tools > Ale Toolkit > Condition System > Welcome`）：更正「条件系统本身不需要独立的配置 EditorWindow」的旧说明，内联判定器清单精简为计数 + 「查看全部判定器」按钮，并加「打开条件编辑器 / 新建条件库」——与效果系统欢迎窗口现在的形状一致。

### 说明

- 两种用法并存、互不排斥：条件既可继续**内联**在宿主字段里（`ConditionExpression` 字段 + Inspector 绘制器，行为完全不变），也可配成**具名条目**由多个系统按 id 共用。宿主可按需分批迁移。
- ⚠️ `ConditionCompare.Labels` 与 `GameplayTagMatchMode.Labels` **不进界面多语言表**——它们是通信格式（配置存索引、外部桥按标签反查），不是 UI 文案，有冻结断言测试守着。
- `ConditionExpressionDrawer` 的文案仍为硬编码中文（与效果侧的定义 / 幅度 / 修饰器 / 表达式绘制器口径一致）；新增的 Condition Editor 走 toolkit 三语。

## [1.11.0] - 2026-09-08

**Effect Editor 从「配效果库」扩成「先看实现、再配数据」的两页签窗口。** 1.10.0 把效果配置收拢进 `EffectDatabase` 之后，「这个工程里到底有哪些可用的效果实现」仍然只能在 Effect System 的 Welcome 窗口看到一份不能搜索、不能跳源码的两列清单；执行器键的正确性更是完全没有编辑期保障——`EffectDefinition.Validate` 只检查阶段名，键写错要进 Play 才在控制台看到一行「未注册的执行器键」。而两条发现通道（编辑期 `TypeCache` / 运行期反射）在重复键上行为**相反**（前者先到先得、后者后来覆盖），`[EffectExecutor("A")]` 里的那个字符串更是**从来没有被任何代码读过**（真正生效的是 `Key` 属性）——这些都会静默走偏。本版把 **Effect Executors** 页放到第一位：全工程执行器目录 + 源码跳转 + 静默失效体检 + 「配置 ↔ 实现」双向核对；原页签改名 **Effect Database** 并移至第二位，如实反映它是可选的补充数据层（效果库为空时第一页照样可用）。本版新增 5 个测试（共 203）。

### 新增

- **Effect Executors 页签**（`Ale.Effect.Editor`，`Editor/Effect/Database/EffectExecutorTab.cs`）：工程内全部 `IEffectExecutor` 实现的目录，按 `Category` 分组、可按 键 / 显示名 / 类型全名 / 程序集 搜索、可「只看有问题」；右列给出实现类型、程序集、源码路径与「打开脚本」（定位到类声明行）/「在 Project 中定位」/「复制 Key」（双击行等同打开脚本）、参数 schema（id / 类型 / 数组 / 枚举引用 / 固定选项）、被哪些效果配置引用（点「跳转」切到 Effect Database 页并定位到那条效果）、诊断摘要；顶部横幅列出「配置引用了但没有实现」的悬空键；底部「新增执行器速查」给内置阶段常量与可一键复制的最小实现模板。内容来自代码而非资产，**没有效果库时照常可用**。
- **`EffectExecutorIndex`**（`Editor/Effect/Database/EffectExecutorIndex.cs`）：`Rows` / `Usages` / `DanglingKeys` / `Categories` / `DiscoveredCount` / `UsagesOf(key)` / `Rebuild()`（`.asset` 增删改经 `AssetPostprocessor` 失效）。候选类型取 `TypeCache.GetTypesDerivedFrom<IEffectExecutor>()` 与 `GetTypesWithAttribute<EffectExecutorAttribute>()` 的**并集**——因此能看见 `EffectExecutorCatalog` 静默丢弃的类型，这正是体检的价值所在。七种诊断标记 `EExecutorIssue`：`MissingAttribute`（**非抽象**类实现了接口却没打特性——抽象基类不算，特性本就该由派生类携带）、`AttributeKeyMismatch`（特性里的键与 `Key` 属性不一致）、`DuplicateKey`（同键多实现，同时标在所有同键行上）、`EmptyKey`、`AbstractType`、`NoDefaultCtor`、`ConstructionFailed`；`Discovered` 表示该实现是否真会被目录与运行时注册表拾取（同键时按候选顺序第一个合格者胜出，与目录的先到先得一致）。其中 `AbstractType` 属**说明性**标记（`InformationalIssues`），`HasProblem` 据此把它排除在「真问题」之外——执行器基类（如 `ChronicleExecutorBase`）不标红、不计入「只看有问题」、诊断以 Info 而非 Error 呈现，行名以灰字表示「不会被发现但这是正常的」。引用扫描覆盖 `EffectDatabase` 的每条效果与独立的 `EffectDefinitionAsset`，逐项记录 键 / 资产 / 效果 id / 阶段。`BuildRows` / `CollectKeys` / `Filter` 是不碰 AssetDatabase / TypeCache / GUI 的纯函数，可直接单测。
- **`EditorScriptLocator`**（`Ale.Toolkit.Editor`，`Editor/Widgets/`）：类型 → `MonoScript` 定位器，`PathOf` / `Find` / `Open`（打开 IDE 并定位到类声明行）/ `Ping`（Project 窗口高亮）/ `ClearCache`，结果带缓存。⚠️ **不能只靠 `MonoScript.GetClass()`**——它对非 `MonoBehaviour` / `ScriptableObject` 的普通类返回 null，而执行器与判定器全是普通类；因此按「`GetClass()` 命中 → 文件名与类型名相同**且**脚本文本里确有该命名空间与声明 → 仅文件名相同」的顺序回退。脚本文本一律取 `MonoScript.text` 而非 `File`：经 `file:` 依赖挂载的包，`Packages/xxx/…` 是虚拟路径，磁盘上并不存在。来自预编译 DLL 的类型返回 null，面板显示「无源码」并禁用按钮。
- **框架** `EditorDatabaseWindowBase<TDb>`：`TabRequiresDatabase(int)`（虚，默认 true；覆写为 false 的页签在没有数据库资产时照常绘制，而不是被「创建新的数据文件」占位页顶掉）、`SelectSystemTab(int)` / `CurrentSystemTab`（供外部切页与读当前页），以及**系统页签索引记忆**（键为 `EditorPrefKey + ".SystemTab"`，仅在索引真的变化时落盘）。
- **列表行鼠标悬停高亮**（`ToolkitEditorStyles.HoverColor` / `TrackMouseHover(ctx)` / `DrawRowHover(rect)`）：可点击的列表行在鼠标经过时叠一层浅高亮，直观告诉用户「这行能点」。做在最底层的共用件上，因此三处行组件——主列表面板 `EditorMasterListPanel`、实体列表面板 `EditorEntityListPanel`、可拖拽单行列表 `EditorDraggableRowList`——以及 Effect Executors 页的执行器行**一并获得，宿主无需接线**。⚠️ IMGUI 窗口默认不接收 `MouseMove`（光标移动不触发重绘，悬停态就不会更新），`TrackMouseHover` 会为正在绘制的窗口自动打开 `wantsMouseMove`，并在移动 / 移出窗口时请求重绘。
- 测试：`EffectExecutorIndexTests`（5：诊断标记与 `Discovered` 判定、抽象 / 缺无参构造 / 空键 / 构造失败、抽象基类只留说明性标记且不计入「只看有问题」、`CollectKeys` 跨阶段组与空键跳过、搜索 / 分类 / 只看有问题的过滤）。

### 变更

- **Effect Editor 顶部页签由 1 个变为 2 个**：`Effect Executors`（第一位）+ `Effect Database`（第二位，原名「效果」）。两个页签名是与类型名对齐的英文专名，不走 `Tr()`。这是纯 UI 变更，不影响任何数据与序列化。
- `EffectEditorWindow.Open(db)` 与 `Open(db, effectId)` 现在会**显式切到 Effect Database 页**——否则技能 / 道具 Inspector 引用列表的「打开」、`EffectDatabase` 资产 Inspector 的「在 Effect Editor 中编辑」、Chronicle / Inventory 两个迁移窗口的「在 Effect Editor 中打开目标库」都会停在第一页，看起来像没反应。新增 `EffectEditorWindow.OpenExecutors()` 打开并切到第一页。
- 效果系统 Welcome 窗口（`Tools > Ale Toolkit > Effect System > Welcome`）内联的执行器清单与「刷新目录」按钮精简为一行计数 + 「查看全部执行器」按钮，避免与新页签两处维护同一份清单。

### 说明

- 页签记忆的影响面：只有继承 `EditorDatabaseWindowBase<TDb>` 的窗口——本包的 Effect Editor 与宿主的 Chronicle 编辑器（7 页签，一并受益）；Inventory 编辑器自带外壳，不继承本基类，不受影响。
- Effect Executors 页只读代码与资产、不写任何数据，也不触碰运行时的 `EffectRegistry`（后者只在进入 Play 模式时由 `[RuntimeInitializeOnLoadMethod]` 填充；编辑期强行预填会污染即将进入 Play 的全局状态）。
- 「配置引用了但没有实现」目前只在本页以横幅告警呈现，**不**升级为 `EffectDefinition.Validate` 的错误、也不阻断导出——那会改变既有的运行时校验语义。

## [1.10.0] - 2026-09-07

**效果配置从「各宿主自建一份」收拢为 toolkit 的共用效果库 + 共用 Effect Editor。** 1.9.0 之后，Chronicle 与 Inventory 各自在数据库里持有效果列表与 Gameplay 标签，并各有一份结构相同的「效果系统」编辑页（约 1000 行重复代码），效果只能在各宿主库内定义、跨系统共用只能靠全局注册表按 id 碰运气。本版把效果数据与效果编辑器下沉到 toolkit：效果在 `EffectDatabase` 里一处配置、所有上层系统按 id 引用；上层只保留「效果引用列表 + 跳转按钮」，并各自实现 `[EffectExecutor]` 执行器——效果本质是「对某个系统的操作」，系统如何被操作由该系统自己实现，新系统出现时不必改 toolkit。Chronicle 那种带本地化名称 / 描述 / 图标的包装实体 `ChronicleEffect` 上提为 `EffectEntry`，并新增**模板驱动的自定义属性列表**（模板定 schema、条目填值）。本版新增 17 个测试（共 197）。

### 破坏性变更

- ⚠️ **`Ale.Effect.Runtime` 新增引用 `Ale.Toolkit.Runtime` 与 `Ale.GameplayTags.Runtime`**（效果库需要属性系统与标签登记）；`Ale.Effect.Editor` 新增引用 `Ale.Toolkit.Editor`、`Ale.GameplayTags.Editor`、`Ale.GameplayTags.Runtime`。依赖方向不变、无环：`Ale.Toolkit.Runtime` / `Ale.Toolkit.Editor` 不反向引用任何子系统，`Ale.Effect.Core` 仍引擎无关。只引用 `Ale.Effect.Core` 的服务端 / 纯 C# 消费方不受影响；引用 `Ale.Effect.Runtime` 的宿主从此连带 toolkit 全家桶（含 TMP / Localization / Addressables 的可选宏链）——共享库需要属性系统，接受。
- **运行时引导拆分**：`EffectRuntime` 与 `GameplayTagRuntime` 各拆成 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 清空（效果定义注册表 + 缺键告警集 / 标签注册表）与 `[BeforeSceneLoad]` 只做加法（执行器自动注册 + `Resources` 加载）。此前 `EffectRuntime.Install` 在 BeforeSceneLoad 里清空注册表，宿主若在更早时机 `AddSource` 会被抹掉；现在清空只发生在 SubsystemRegistration，宿主在 BeforeSceneLoad / Awake 登记的来源稳定存活；关闭 Domain Reload 时同样由 SubsystemRegistration 统一复位。
- `EffectDefinitionRegistry.Default.AddSource` 改为幂等（同一来源不重复登记）；`Clear()` 同时清空来源列表。

### 新增

- **效果库 `EffectDatabase`**（`Ale.Effect.Runtime`，`Runtime/Effect/Unity/Database/`；`Create > Ale > Effect > Effect Database`）：四个顶层列表——`enumTypes`（自定义属性的枚举字段用）、`effectTemplates`、`effects`、`gameplayTags`；实现 `IEnumTypeSource` / `IEffectSchemaSource` / `IEffectDefinitionSource`（显式实现返回条目的 `definition`）；`GetEffect(id)` / `GetTemplate(name)` / `GetEnumType(name)`、`NormalizeAll` / `RebuildAllAttributes` / `AddEnumType` / `CloneFrom`；`Validate(out errors)`：枚举名 / 模板名 / 效果 id 重复、`templateRef` 悬空、定义为空、定义错误（`EffectDefinition.IsWarning` 的警告不阻断）、非法标签名。
  - **`EffectTemplate : ConfigTemplateBase`**：`name` / `color` / `attributes`（自定义属性 schema）+ `defaultDefinition`（从模板创建效果时深拷贝作为预设）。
  - **`EffectEntry : AttributeOwner`**（Chronicle `ChronicleEffect` 原样上提 + 模板字段）：`id` / `templateRef` / `displayText` / `descriptionText` / `iconValue`（`AttributeValue` Text / Text / Sprite）/ `values`（按模板 schema 的自定义属性）/ `definition`；`Normalize()` 把 `definition.id` / `displayName` 与条目同步；`RebuildAttributes(IEffectSchemaSource)` 按 schema 对账；`ResolveDisplayName()` / `PlainName()` / `Clone()`。
  - ⚠️ **序列化深度零余量**：库 → 条目 / 模板 → 定义 → 执行 → 组 → 项 → 门控 → 组 → 项 → 参数已是 9 层，`EffectTemplate.defaultDefinition` 必须是直接字段、`EffectEntry` 必须是顶层列表元素——再包一层即触及 Unity 序列化深度上限。
- **`EffectDataManager : ToolkitSingleton`**（`IEffectDefinitionSource` / `IEnumTypeSource` / `IEffectSchemaSource`）：`Register(db)`（幂等；`NormalizeAll`；首次登记时 `EffectDefinitionRegistry.Default.AddSource(this)`；标签并入 `GameplayTagRuntime.Register`）/ `Unregister` / `ClearDatabases`（空时从注册表移除）/ `LoadFromBinary(bytes)` / `LoadFromJson(json)`；惰性索引 `GetEffect` / `GetTemplate` / `GetEnumType` / `GetAllEffects`。`EffectRuntime.AutoLoadFromResources`（静态开关，默认 true）为真时启动自动 `Resources.LoadAll<EffectDatabase>` 逐个登记。
- **`EffectConfigSerializer`**（命名空间 `Ale.Effect.Serialization`）：魔数 `EFDB`、`Version = 1`；JSON（`JsonUtility`，定义直接内嵌）与二进制（定义以 `EffectJson` 串写入，属性值经 `ToolkitBinaryCodec` / `ToolkitDtoMapper`）的 `Export` / `Import` / `ImportInto` 往返；`IAssetRefResolver` 沿用 toolkit（运行时 `LoadFromBinary` 用空解析器，图标不解析）。
- **Effect Editor**（`Ale.Effect.Editor`，`Editor/Effect/Database/`；菜单 `Tools > Ale Toolkit > Effect System > Effect Editor`）：`EditorDatabaseWindowBase<EffectDatabase>` 外壳 + 一个三列「效果」页——左列子页签 **效果模板**（名称 / 色点 / 内联默认定义 / schema 绘制器）、**Gameplay 标签**（自 Chronicle 上提；名称非法红框、列出隐式登记的祖先）、**枚举类型**（`EditorEnumTypePanel` 闭合）；中列效果列表（模板过滤 / 搜索 / 从模板添加 = 克隆 `defaultDefinition` + 按 schema 建属性 / 快速添加；行列 ID / 名称 / 策略 / 内容摘要）；右列 Inspector（ID 查重、名称 / 描述 / 图标、只读来源模板、按 schema 的自定义属性（枚举经库解析）、内联 `EffectDefinition`（`ShowIdentityFields = false`，首次自动展开）、校验摘要）。查重（效果 id / 模板 name / 枚举 name）阻断导出；导出 JSON / 二进制经 `Validate` 拦截。`EffectEditorWindow.Open()` / `Open(db)` / `Open(db, effectId)`（定位到指定效果）/ `CreateDatabaseAsset()`。三语（`EffectEditorL10nTables`，`[InitializeOnLoad]` 登记到 `ToolkitEditorL10n`）。
- **`EffectEditorCatalog`**：汇总工程内全部 `EffectDatabase` 资产（`AssetPostprocessor` 失效重建）：`Databases` / `All` / `TryFind(id)` / `Find(id)` / `LabelOf` / `BuildMenu`（按库分组，同 id 先扫到者优先）；并作为 provider 把各库的 Gameplay 标签并入 `GameplayTagEditorCatalog`——编辑态不再往运行时注册表灌标签。
- **`EditorEffectRefListDrawer.Draw(ctx, refs, drag, header, noun, hint)`**：供宿主 Inspector 使用的效果 id 引用列表——「+」目录菜单、拖拽重排 / 删除、命中目录的行附「打开」按钮跳转到 Effect Editor 并定位、未命中标「未找到」（不阻断，运行时按 id 经全局注册表解析）、底部自由输入。
- **属性 id 目录 provider**：`IEffectAttributeCatalogProvider { SystemName; GetAttributes() → (id, display) }` + `EffectDefinitionDrawerHooks.RegisterAttributeProvider / UnregisterAttributeProvider / ClearAttributeProviders / CollectAttributeCandidates`；`DrawAttributeId` 顺序：整字段委托 `AttributeIdField`（保留、优先）→ provider 下拉（多系统按系统名分组，同 id 先登记者优先；悬空 / 未选择项保留在首位）→ 文本框。
- **框架**：`EditorEntityListPanel.RequestSelect(entity)` / `EditorThreeColumnTab.RequestSelect(entity)`（须在数据库设定之后调用；下一帧 Layout 激活右列 Inspector，实体不在列表则忽略）；`GameplayTagEditorCatalog.RegisterProvider / UnregisterProvider`。
- `EffectDatabase` 资产 Inspector（「在 Effect Editor 中编辑」+ 概览 + 原始数据视图）；效果系统欢迎窗口加「打开效果编辑器 / 新建效果库」。
- 测试：`EffectDatabaseTests`（15：Normalize / RebuildAttributes / Validate 各分支 / JSON 与二进制往返 / 数据管理器登记·注册表·标签·清空·幂等）、`EffectEditorCatalogTests`（2：索引构建、provider 登记 / 汇总 / 注销）；测试程序集新增引用 `Ale.Toolkit.Editor`。

### 说明

- 宿主迁移路径（Chronicle 0.5.0 / Inventory 1.13.0 落地）：宿主库里已存的效果与标签保留为隐藏 legacy 字段一个版本 + 一键迁移菜单搬入 `EffectDatabase`（id 冲突跳过并报告）；宿主的效果引用列表改用 `EditorEffectRefListDrawer`，属性候选经 provider 登记，执行器不变。
- Effect Editor 与引用绘制器走 toolkit 三语；既有的定义 / 幅度 / 修饰器 / 表达式绘制器仍为硬编码中文（不变）。

## [1.9.0] - 2026-09-07

**效果系统补齐 UE5 GAS `GameplayEffect` 的全貌，并新增层级标签系统；属性修饰器抽成独立的引擎无关程序集。** 此前效果系统只是一个「一次性派发层」（阶段组 + 执行器 + 门控），GAS 里让它成为「系统」的两根支柱——带句柄 / 时长 / 叠加的活动效果容器，以及 Gameplay Tag——都不存在，而 `ModifierDefinition` 上的时长 / 叠加字段也从未有运行时解释它们。三个宿主对 `Ale.Effect` 零引用、无序列化资产，正是补齐设计的时机。既有 `EffectExpression` / 执行器 / 门控原样保留（它们正对应 GAS 的 Executions），14 个既有测试一字未动；本版新增 94 个测试（共 180）。

### 破坏性变更

- **属性修饰器三型（`ModifierDefinition` / `ModifierStackEvaluator` / `EModifierOperation` 等三枚举）迁至新程序集 `Ale.Modifier.Core`，命名空间 `Ale.Toolkit.Runtime` → `Ale.Modifier`。** 起因：`Ale.Effect.Core` 是引擎无关程序集，不能引用引擎绑定的 `Ale.Toolkit.Runtime`，而效果的持续修饰器要直接产出 `ModifierDefinition` 交给宿主汇流，只能把修饰器也变成引擎无关。消费方需在 asmdef 加 `Ale.Modifier.Core` 并改 `using Ale.Modifier;`。文件保留 GUID、`[Serializable]` 类按字段内联序列化，**已存 YAML / JSON / DTO 不受影响**。`duration` / `durationDays` / `stackLimit` / `stackRule` 四个惰性字段原样保留（求值器与效果 GAS 层均不读取；时长 / 周期 / 叠加改由效果定义承载），宿主自建的修饰器运行时可继续按需解释。

### 新增

- **标签系统 · GameplayTag System**（四个程序集 `Ale.GameplayTags.Core` / `.Condition` / `.Runtime` / `.Editor`，命名空间 `Ale.GameplayTags`）。
  - `GameplayTag`：只读结构体包一条已归一的点分名，`MatchesTag(parent)` 为「自身或后代」（`Status.Debuff.Mental` 匹配 `Status`，纯前缀 `AB` 不匹配 `A`）。**序数大小写敏感**——与 toolkit 全部字符串键一致，中文段无折叠意义，仅大小写不同的手误交给注册表在配置期抓出。**不参与序列化**：容器存 `List<string>`、单标签字段存 `string`，对 DTO / 二进制 / Newtonsoft 零迁移。
  - ⚠️ **归一规则是数据格式，发布后冻结**（有测试钉住）：整体与逐段 Trim；空段（`A..B` / `.A` / `A.`）非法；段内空白与 `/` 非法（编辑器菜单以 `/` 分层）；CJK 等其余字符允许。
  - `GameplayTagContainer`（`[Serializable]`；层级 / 精确查询、`RemoveTag` 精确而 `RemoveTagsMatching` 移子树、`Filter`、`Normalize`；`HasAll(空) = true`、`HasAny(空) = false`）、`GameplayTagCountContainer`（运行时；显式计数 + 隐式祖先计数，O(1) 层级查询，`OnTagCountChanged` 先更新完全部计数再逐个通知）、`GameplayTagRequirements`（require 全持有且 ignore 一个不持有；`Validate` 对 require ∩ ignore 报错）、`GameplayTagDefinition`、`IGameplayTagOwner` / `IGameplayTagSource`。
  - **咨询性注册表** `GameplayTagRegistry`：登记自动补祖先；`Validate` 对未登记给警告、对仅大小写不同的已登记项指名；**运行时匹配永不查注册表**，未登记标签照常合法——它只服务编辑器下拉与配置期校验。
  - 条件桥 `Ale.GameplayTags.Condition`：`Condition.HasGameplayTag(tag, exact)`、`Condition.GameplayTags(tags[], 任一 / 全部 / 皆无, exact)`（模式标签同 `ConditionCompare.Labels` 的禁令：是通信格式，不本地化不调序）。**放桥程序集而非让 `Ale.Condition.Core` 引用标签**：保住其「零引用」承诺，只用条件系统的宿主（VN 桥 / 动画模拟器）不被强加标签依赖；`AutoRegisterFromAssemblies` 扫全 AppDomain，桥程序集 `autoReferenced` 必被加载。**不做 TagQuery**——与或非组合交给 `ConditionExpression`。
  - Unity 桥：`GameplayTagTable` 资产（`Resources` 下启动时自动登记；也可 `GameplayTagRuntime.Register` 显式登记）、`[GameplayTagField]`；编辑器：目录（注册表 ∪ 工程内全部表资产，惰性构建，表资产变动与注册表变化时失效）、标签树下拉（有子级的标签自身放子菜单首项）、容器 / 要求 / 单标签绘制器（提交即归一，非法红底、未登记黄底）、表 Inspector（校验 / 排序 / 登记预览）、Welcome。
  - 命名陷阱：命名空间取**复数** `Ale.GameplayTags`——若叫 `Ale.GameplayTag`，`namespace Ale.*` 下写 `GameplayTag t` 会先命中命名空间（CS0118）；特性叫 `GameplayTagFieldAttribute`（`[GameplayTagField]`），避免 `[GameplayTag]` 与类型二义（CS1614）。
- **效果系统 GAS 层**（`Ale.Effect.Core` 引用 += `Ale.GameplayTags.Core`、`Ale.Modifier.Core`；新目录 `Runtime/Effect/Core/Gameplay/`）。
  - `EffectDefinition`（≙ GameplayEffect）：`durationPolicy`（Instant / HasDuration / Infinite）、`duration` / `period` + `executePeriodicOnApplication`、叠加（`stackingType` 按来源 / 按目标聚合、`stackLimit`、刷新时长 / 重置周期 / 到期三策略）、标签（`assetTags` / `grantedTags` / `removeEffectsWithTags` / `grantedApplicationImmunityTags`、施加 / 持续标签要求）、`applicationCondition`（Condition）、`chanceToApply`、`modifiers`（`EffectModifier` + `EffectMagnitude`：Scalable / AttributeBased / SetByCaller，施加与叠层时快照）、`executions`（复用 `EffectExpression`；阶段常量 `EffectPhases.OnApply / OnStack / OnPeriod / OnExpire / OnRemove`）、`cueTags`；`Clone` / `Normalize` / `Validate`（错误与「警告:」前缀的警告：瞬时效果配了不生效的授予标签 / 周期 / 叠加、叠加等于未配、授予标签命中持续要求的禁止标签会自我抑制、非内置阶段…）。
  - ⚠️ `EffectRunner` 把**空 phase 当通配**，直接放进定义会在每周期 / 移除时重复执行——`Normalize()` 把空阶段改写为 `onApply`；`EffectJson.DefinitionFromJson` 反序列化后自动 `Normalize`。
  - `EffectContainer`（≙ ASC 的效果与标签部分；纯 C#）：施加管线「免疫 → 施加标签要求 → 施加条件（`Subject` = 目标）→ 概率 → 时长求值（≤ 0 为 `Invalid`，配置错误要暴露而不是「施加即到期」）→ 瞬时落地 / 叠加 / 新实例 → 按标签移除他者」；`Tick`（周期先于到期；跨多周期多次结算、余数保留、单次上限 1000；抑制中周期冻结、时长照走；到期三策略；`onExpire` → `onRemove`）；抑制（`OwnedTags` 任何变化自动重评，循环至收敛、振荡告警；抑制撤回授予标签、不汇流）；`CollectModifiers`（激活、未抑制、**非周期**）；按句柄 / 标签（assetTags ∪ grantedTags，不含自身）/ 来源 / id 移除、`RemoveAll`、静默 `Clear`；`RunPhase` / `SetLevel`；`ExportState` / `ImportState`（覆盖语义、幅度快照、定义缺失或改为瞬时时跳过并告警、句柄延续、**不跑阶段不发事件**）。时间单位由宿主决定。
  - **值 / 事分工**：持续 / 无限效果的修饰器经 `CollectModifiers` 临时汇流（每条修饰器只产出一条按层数缩放的 `ModifierDefinition`：Add / PercentAdd × S、Multiply (1+m)^S − 1、Override 原值，来源 `effect:{id}#{handle}`）；瞬时与周期结算经 `IEffectAttributeSink.ApplyPermanent` 永久落地——**周期效果不参与 `CollectModifiers`**，否则「每周期 +10 且持续 +10」被双算。
  - 契约：`IEffectDefinitionSource` + 聚合的 `EffectDefinitionRegistry.Default`（跨库按 id 引用；`EffectRuntime.Install` 每次播放清空）、`IEffectContainerSource`、`IEffectAttributeSource` / `IEffectAttributeSink`、`IEffectRandomSource`（缺服务用 `EffectContainer.DefaultRandom`）、`IEffectCueSink`（Applied / Executed / Removed；顺序先 Applied 再施加即结算的 Executed）、`IEffectExecutionInfo`（容器把宿主上下文包成 `Subject` = 目标的执行上下文，执行器经 `ctx.GetService` 取当前定义 / 实例 / 来源 / 等级 / 阶段）；`EffectContext : ConditionContext` + `SubjectEffectContext`；`EffectApplier.Apply(effectId, ctx)`（容器 ← 上下文容器源或主体本身；定义 ← 请求 → 上下文定义源 → 全局注册表）。
  - 内置执行器 `Effect.ApplyEffect(effectId, level, sourceTag)`（来源沿用外层执行信息）、`Effect.RemoveEffectsWithTag(tag)`、`Effect.RemoveEffectById(effectId)`；`EffectRegistry.EnsureAutoRegistered()`（与 Condition 侧对齐，`Clear()` 复位标志）；`EffectJson` 增定义往返。
  - 编辑器：`EffectDefinitionDrawer`（分节显隐；**量算与绘制共用同一布局代码**，不错位）、`EffectMagnitudeDrawer` / `EffectModifierDrawer`、宿主注入点 `EffectDefinitionDrawerHooks.AttributeIdField`（属性 id 下拉）/ `ShowIdentityFields`（宿主实体自带 id / 名称时隐藏「基本」节）、`EffectDefinitionAsset` + 校验 / 归一 Inspector、`EffectExpressionDrawer` 阶段下拉、Welcome 说明。

### 修复

- `ToolkitInfo.Version` 自 1.8.1 起与 `package.json` 漂移（仍为 `"1.8.0"`），本次改为 `"1.9.0"`。

### 说明

- 标签 / 效果的编辑器与条件 / 效果既有编辑器同款：硬编码中文、不引用 `Ale.Toolkit.Editor` 的三语服务，避免把 TMP / Localization 依赖链带进子系统；三个子系统如需三语可作为后续独立项统一接入。
- 有意不做：曲线表幅度（`perLevel` 线性即够，后续可加 kind 而不破坏数据）、非快照的属性捕获、网络复制；Cue 只是 `cueTags` + 一个接口三次回调，缺服务即无操作。

## [1.8.1] - 2026-08-17

**世界坐标 → UI 坐标的换算不再自己猜相机。** 换算方法新增可选的 `worldCamera` 形参，配套一个「直接把 `RectTransform` 摆到世界点」的便捷方法；三个相机来源全空时报警，而不再静默退化。既有调用方零改动——新形参可选，不传时行为与 1.8.0 逐位一致。

### 新增

- **`UIUtility.PositionAtWorldPos(rt, worldPos, canvas, worldCamera)`**：把矩形摆到世界点在 UI 上对应的位置，一步到位。
  - **基准取 `rt.parent` 而非 Canvas**——写的是 `localPosition`，原点就得跟父级走。此前是调用方各自「算完再赋给 `localPosition`」，而换算基准默认是 Canvas 自己：两者只在「父级恰好与 Canvas 原点重合」时等价，中间层一旦带了偏移或换了锚点，结果就整体错开那段偏移。**错法酷似「相机没对上」**，查起来会往完全错误的方向走。收进一个方法后，基准与赋值目标由同一处决定，不存在配错的可能。
  - 与「悬停弹窗定位」的 `PositionAtCursor` 名字像、语义不同：那个吃屏幕像素、写 `position`、夹取回屏内；这个吃世界坐标、写 `localPosition`、不夹取。差别写进了区块注释。
- **`UIUtility.ResolveWorldCamera(worldCamera, canvas)`**：把相机解析顺序公开出来——显式传入 > `Camera.main` > `canvas.worldCamera`。最后一档仅作兜底：它是 **UI 的**渲染相机，与渲染游戏世界的那台分离时拿它做世界投影会得到错误结果，Overlay 模式下更是恒为 `null`。
- **三个来源全空时告警**（按 Canvas 去重）：此时换算退化成「世界坐标的 XY 直接当屏幕像素」。2D 工程里世界坐标通常是个位数，于是**所有挂件塌到屏幕左下角那一小撮、彼此相差不到一个像素**。这是纯粹的配置事故，但退化本身悄无声息，症状又酷似「UI 预制体的锚点 / 轴心配错」，不报出来只会往错的方向查。跟随物体的挂件是每帧定位的，故必须去重。

### 变更

- **`WorldPosToUILocalPos` 的两个重载末尾新增可选形参 `Camera worldCamera = null`。**
  - 此前相机是方法内部自己挑的：`Camera.main ? Camera.main : canvas.worldCamera`。这依赖「场景里有且仅有一台打了 MainCamera 标签的相机」这条隐含约定——**标签漏打时它直接是 `null`**，分屏 / 多相机 / RenderTexture 下则取错那台。而调用方通常明确知道是哪台相机在渲染目标物体（动画模拟器就有 `playerCamera` 字段），交进来即可。
  - 解析顺序未变，只是在最前面插了「显式传入」一档，故不传时结果与此前完全相同。

## [1.8.0] - 2026-08-15

**把三样「每个宿主都得自己写一遍」的条件系统设施收进 `Ale.Condition.Core`。** 纯新增，无 API 破坏；内置判定器改用公共实现但行为逐位不变。

### 新增

- **`ConditionCompare`（比较符范式）**：五个索引常量 + `Labels` 下拉标签 + `CreateOpParam()` + `ReadOp()` + 两个 `Compare` 重载。此前比较符只以 `private` 形式存在于 `NumberCompareEvaluator` 内部，**下游想复用够不着，只能各抄一份**——实测已被抄了三份（角色系统、动画模拟器、Fs 游戏框架），且开始漂移：三份的形参顺序各不相同（`(a, op, b)` / `(value, amount, op)`），「等于」的容差也分成了 `1e-6` 与 `1e-9` 两派。
  - ⚠️ **`Labels` 是通信格式，不是 UI 文案。** 它以字符串形式进入使用方的配置与脚本——经 VN Framework 桥接后，Dialogue System 的对话条件里写的就是 `AleCond_Xxx("大于等于", 3)`，运行时再按标签反查索引；而索引本身又序列化进条件资产。**永远不要本地化它**（跟着编辑器语言变会让已写好的剧本集体失配），**也永远不要调整顺序**（会让已配置的条件集体错位）。这条禁令写在类注释里，并有测试钉住。
  - **默认容差取 `1e-6` 而非更严的值**：常见来源是 `float` 扩宽成 `double`，`10.1f` 实际是 `10.100000381469727`，与配置里存的 `double 10.1` 相差约 `3.8e-7`。容差比这个小，「等于」在浮点属性上就几乎永远判不成立。需要严格语义的显式传 `epsilon`。
  - 提供 `Compare(long, long, int)` 精确重载：年月日、等级这类整数比较不必绕道浮点容差。
- **`ConditionContext` + `SubjectConditionContext`（通用判定上下文）**：按类型登记领域服务，宿主不必再为「把数据源交给判定器」手写一个上下文类——此前 README 的用法示例就是让每个使用者照抄一份 `class MyCtx : IConditionContext`。
  - **刻意不提供全局默认实例**。「哪个是默认上下文」是宿主的策略，由 toolkit 提供一个全局实例只会和宿主自己的注册表形成两套并列的东西，让人搞不清该往哪注册。
  - **成员刻意不做 `virtual`**：`GetService<T>()` 在条件求值的热路径上（一次链接评估可能调几十次）。需要自定义解析策略的宿主（例如要按「自定义优先、内置回落」分层的）**直接实现 `IConditionContext` 即可**，本就不必继承。
  - `GetService<T>()` 按 `typeof(T)` **精确**查表，不做可赋值匹配：按接口登记的只能按接口取用。
  - `SubjectConditionContext` 用于「判定这个对象是否满足条件」——包一层而不改动共享上下文的 `Subject`，因而多处求值（乃至嵌套求值）之间不会互相踩。
- **`ConditionRegistry.EnsureAutoRegistered()`**：幂等的自动注册兜底。补的是这样一个缺口——`ConditionRuntime` 只在运行时启动阶段填表，而**编辑器工具在非播放态求值时注册表是空的**（资产预览、批量校验等），于是每个宿主都得自己兜一次。
  - ⚠️ `Clear()` 一并复位「已扫描」标志，否则清空之后再 `Ensure` 会静默变成空操作、注册表永久为空。这条有测试钉住。

### 变更

- **`NumberCompareEvaluator` 改用 `ConditionCompare`**，行为逐位不变：
  - 五个 `public const int` **保留为转发别名**（`Greater = ConditionCompare.Greater`），既有引用与测试零破坏；
  - 新增 `public const double Epsilon = 1e-9` 并在比较时显式传入。**刻意不跟随 `ConditionCompare.DefaultEpsilon`（`1e-6`）**——本判定器自 1.4.0 起就是 `1e-9`，跟随默认值等于悄悄放宽既有行为。数值来自宿主的 `IConditionNumberSource`，本就是 `double`，不像属性系统那样普遍存在 float 扩宽误差，严格容差是合适的。
- **`ConditionEngineTests` 新增 17 个用例**（原有 17 个一字未动）：比较符的整数 / 浮点重载与容差边界、标签顺序与转发常量的冻结断言、通用上下文与主体包装、`EnsureAutoRegistered` 的幂等性与「`Clear()` 后可重扫」。
  - 其中 `BuiltIn_NumberCompare_EqualUsesStrictEpsilon` 用 `10.1f` 扩宽值对 `10.1`，断言在本判定器下**不**相等、同时反证同一组数在默认容差下**相等**——谁哪天把 `Epsilon` 改掉或让它跟随默认值，测试立刻红。

## [1.7.10] - 2026-08-14

**修掉一个会让 Addressable 地址「一次失败、永久失效」的缺陷。** 纯修复，无 API 变更。

### 修复

- **`AddressableManager` 会把加载失败的条目长驻在静态表里**，导致同一地址此后再也取不回来。
  - 起因：`BeginLoad` 的完成回调无论成败都置 `Done = true`，失败时 `Result = null`。
    该 `Entry` 留在 `Entries` 中，于是后续每次 `LoadAsync(同一地址)` 都命中
    `if (e.Done) onLoaded(e.Result as T)` 这条分支——**直接回传 `null`，既不重发 Addressables 请求、
    也不再打印失败警告**。一次偶发失败（地址拼错、资源尚未加入分组、远端目录抖动）就把该地址永久毒化。
  - 现改为**原地重试**：再次请求一个「已完成但结果为空」的条目时，先释放旧句柄，
    把 `Done` 置回 `false` 并重新发起加载。资源加载成功后又被销毁（Unity 的「假 null」）走同一条路径。
  - ⚠️ **刻意不采用「删掉条目重来」**：`Entry` 同时充当引用计数账本，而 `OwnerAddrs` 里已登记了该地址。
    若删掉条目、之后又有人成功加载建出新条目，先前那些宿主销毁时的 `ReleaseAddress`
    就会去扣新条目的计数，**把还在使用中的句柄提前释放**。保留条目才能让计数配对始终成立。
  - 行为变化：地址确实不存在时，每次请求都会重新尝试并各打印一条失败警告
    （此前只在第一次打印）。这是有意的——静默吞掉失败正是这个缺陷难以察觉的原因。
- `ToolkitInfo.Version` 同步为 `1.7.10`（该常量在 1.7.0 与 1.7.9 各漂移过一次，本次一并核对）。

## [1.7.9] - 2026-08-11

**移除旧宏自动迁移：插件不再改写 PlayerSettings。** 自动改写对「自己是这个宏的唯一管理者」下了赌注，赌输的代价是编辑器陷入「Compiling Scripts」死循环。宏的增删从此一律由用户经欢迎窗口显式操作。

### 破坏性变更

- **移除 `ToolkitDefines.LegacyRename` 与 `ToolkitDefineChecker.MigrateLegacyDefines()`**，`IS_TMP` / `IS_LOCALIZATION` / `IS_ADDRESSABLE` → `ATK_*` 的自动迁移就此取消。
  - **起因**：在 `[InitializeOnLoad]` 里改写 PlayerSettings 是个危险动作。只要工程里还有别的插件按自己的规则管同一个宏（典型场景：A 插件按命名空间是否存在添加 `HAS_X`，B 插件把 `HAS_X` 当旧名删掉换成 `NEW_X`），两者就会在每次域重载里互相覆写；而每次 PlayerSettings 写入都会触发一次重编译 → 域重载 → 再写一次，**永不收敛**。
  - 迁移本身是一次性收益，却把这个死循环风险常驻在每个装了本包的工程里，不划算。
  - ⚠️ **仍在用 `IS_*` 旧宏的老工程需手动改一次**：在 `Tools > Ale Toolkit > Welcome` 勾上对应的 `ATK_*`，并到 Player Settings 的 Scripting Define Symbols 删掉 `IS_*`。1.5.1 之后建的工程不受影响。
  - `ToolkitDefineChecker` 保留「开了宏却没装对应包」的 Console 一致性提示——**只读不写**。

### 修复

- `ToolkitInfo.Version` 由 `1.7.4` 订正为 `1.7.9`（自 1.7.5 起漏同步；1.7.0 曾修过一次同类漂移）。

## [1.7.8] - 2026-08-11

**聚焦式顺序列表：让「静止时必对齐到某一条」成立——拖拽松手吸附、滚轮改按整数条步进。**

### 破坏性变更

- **`UiwFocusOrderList` 的滚轮不再按 `ScrollRect.scrollSensitivity` 的像素值位移，改为按<b>整数条</b>步进**，一档跨几条由新增的 `wheelRowsPerNotch`（默认 1）决定。
  - **起因**：焦点列表的语义是「停在哪条就选中哪条」，而滚轮步长此前是一份**手工维护、与行距重复**的像素值。行距本身是自动算出来的（格子高度 × `rowPitchScale`），两者一旦不等，滚轮就必然停在两条之间——既没有明确的选中项，焦点缩放曲线还会让上下两条都呈半放大态。旧文档只能靠「请把 Scroll Sensitivity 设成行距」这样的约定来回避，而约定是会被忘的：`rowPitchScale` 一改，就得记得回去同步这个像素值。
  - 现在位移量完全由「行距 × 档位条数」算出，**不存在需要人工同步的第二份数值**。
  - ⚠️ **`Scroll Sensitivity` 对聚焦式列表就此失效**（普通 `UiwVirtualOrderList` 不受影响，仍按它走）。该值仍会在 `Awake` 被取走并置 0——不置 0 的话 `ScrollRect` 会与本类重复处理同一次滚轮。原先靠把它设成 `2 × 行距` 来实现「一档两条」的，改配 `Wheel Rows Per Notch = 2`。
  - 滚轮起点会先**归到最近的整槽**再整条整条地走：常态下起点本就对齐，归整不改变它；若因拖拽（关掉了吸附）或外部写入停在半路，一档滚轮顺带把它拉回格上。
  - 滚轮同样在步进前清零 `ScrollRect.velocity`，避免残余惯性与补间抢写位置。

### 新增

- **`UiwFocusOrderList` 新增 `snapAfterDrag`（拖拽吸附），默认 `true`。** 松手后把当前焦点条目补间到正对焦点线的位置，列表不再停在两条之间。
  - 焦点列表的语义是「停在哪条就选中哪条」，停在两条之间既没有明确的选中项，焦点缩放曲线还会让上下两条都呈半放大态——本类的类注释此前把这一点列为已知缺口，本版补上。
  - **不吞惯性**：松手当帧只登记「待吸附」，先让 `ScrollRect` 的惯性照常滑，速度衰减到 `snapVelocityThreshold`（默认 200 像素/秒）以下才接管。「甩一下翻好几条」的手感因此保留；同时不必等指数衰减的长尾自然归零（`ScrollRect` 要 `|v| < 1` 才清零），省掉尾段那段无意义的慢速蠕动。`ScrollRect` 未开 `Inertia` 时松手即吸附。
  - **目标条目就是当前焦点**：吸附与 `FocusedIndex` 用的是同一条反解（槽位中心 = 留白 + 槽位×行距 + 半行距 - 滚动量，令其等于焦点线），故吸附过程中焦点**不会跳变**，只是把它从「最接近焦点线」挪到「正对焦点线」。
  - 补间时长由 `snapTweenDuration`（默认 0.15 秒）单独控制，与滚轮的 `scrollTweenDuration` 互不影响；两者共用同一套补间状态机，时长随每次启动传入。
  - 新增公开方法 `SnapToFocusLine(float duration)` 与只读属性 `IsPendingSnap`，供调用方在别的时机主动对齐 / 观察吸附状态。

### 修复

- **`FocusIndex` 不再被残余惯性带跑。** 它承诺「立即把指定条目滚到焦点线上」，却只取消了补间、没有清零 `ScrollRect.velocity`——松手后的滑行尚未停时调用它，位置会在随后的帧里被惯性推走。现一并清零。
  - 同一条也用在吸附上：补间开始前必须清零速度，否则 `ScrollRect` 自己的 `LateUpdate` 会与补间在同一帧抢写 `content.anchoredPosition`。

### 变更

- 拖拽事件的监听由 `IBeginDragHandler` 扩展为 `IBeginDragHandler + IEndDragHandler`。两者都**只用来知道拖拽何时开始 / 结束、不消费事件**——`ExecuteEvents` 会把拖拽派发给同一物体上的全部处理器，`ScrollRect` 照常收到并做它的拖拽与惯性。

- **「滚动量 ↔ 槽位」的两条互逆换算收敛为 `SlotAtFocusLine` / `ScrollYForSlot`。** 焦点判定、滚轮步进、拖拽吸附、`FocusIndex` 四处都要用它，此前是三份逐字抄写的同式——本版新增两处用法后必然会漂。集中后任何一处改动同时作用于四者。

## [1.7.7] - 2026-08-11

**顺序虚拟列表：1.7.6 的开关改名为 `reverseContentOrder`，`reverseScrollDirection` 让位给「滚轮反向」。**

### 破坏性变更

- **`UiwVirtualOrderList.reverseScrollDirection`（1.7.6 引入）更名为 `reverseContentOrder`。** 原名不准确——它改的是「条目怎么排」而非「滚轮往哪转」，两者是彼此独立的两件事，挤在一个名字下必然误导。
  - ⚠️ **字段名复用了，语义却变了**：Unity 按名匹配序列化，勾选过 1.7.6 那个 `reverseScrollDirection` 的预制体，升级后该值会落到**新的滚轮反向**字段上。请检查并改勾 `Reverse Content Order`，把 `Reverse Scroll Direction` 归零。1.7.6 只存活了很短时间，受影响面应当极小。

### 新增

- **`UiwVirtualOrderList` 新增 `reverseScrollDirection`（反向滚轮）**，默认 `false`。勾选后**鼠标滚轮**的滚动方向反过来。
  - **只影响滚轮，不影响拖拽**：拖拽是「抓着内容走」，方向天然正确，反过来反而别扭；而滚轮的方向常按设计目的或用户习惯来定。
  - **按需接管**：不勾选时本类**完全不插手**滚轮——不清零 `ScrollRect.scrollSensitivity`，事件照旧由 `ScrollRect` 原生消费，行为与本版之前一字不差。只有需要反向时才接管，普通顺序列表零风险。
    > 接管后必须清零 `scrollSensitivity`：`ScrollRect` 与列表通常挂在同一个 GameObject 上，而 `ExecuteEvents` 会把滚轮事件派发给该物体上**全部** `IScrollHandler`，不清零就会被原生逻辑先按原方向挪一次、再被本类按反方向挪一次，净效果是抖一下且方向仍错。
  - 滚轮接管的那套机械（`ScrollStep` / `ScrollTakenOver` / `TakeOverScroll` / `ResolveScrollDelta` / `MaxScroll`）由 `UiwFocusOrderList` **上提到本类**，两处不再各写一份；`UiwFocusOrderList` 覆写 `NeedsScrollTakeOver` 为恒 `true`（它要做平滑位移，无论反不反向都得接管）与 `MaxScroll`（其 Content 高度还含首尾留白），并在 `OnScroll` 里改调 `ResolveScrollDelta` 以自动获得反向能力。

## [1.7.6] - 2026-08-11

**顺序虚拟列表：新增反向排布开关。**

### 新增

- **`UiwVirtualOrderList` 新增 `reverseScrollDirection`（反向滚动）**，默认 `false`（正向：第 0 条在最上方，向下滚走向末条，与旧行为一字不差）。勾选后倒序排布——**第 0 条在最下方、最后一条在最上方**，「向下滚」于是从末条走向首条，适合聊天记录、日志这类自下而上追加的列表。
  - 实现上引入「**槽位**」这层概念：槽位 = 从 Content 顶端往下数的第几行，是纯几何量；数据索引到槽位的映射由 `SlotOf` 给出（正向为恒等，倒序为 `条目数-1-索引`）。定位、首尾留白、滚动换算、焦点反解一律只认槽位，于是反向**不需要动 Content 尺寸、锚点与滚动范围中的任何一个**——它们本就是按槽位算的。
    > 另一条路是把 Content 锚点翻到底部，但那样上述每一处都要各写一套正 / 反分支，`UiwFocusOrderList` 还要再抄一遍。两种做法的视觉结果完全一致。
  - `UiwFocusOrderList` 同步按槽位改写：`PositionOf`、`ComputeFirstIndex`、`FocusIndex` 的滚动量反解、焦点索引反解，以及外观曲线的可见区间（倒序时「槽位区间 → 索引区间」是**反序**的，两端需对调后再夹取）。因此 `FocusIndex(i)` 之后 `FocusedIndex` 仍读回 `i`，焦点语义在两个方向下都自洽。
  - 引擎的可见窗口恒为 `[first, first + PoolTarget - 1]` 这样的**升序索引区间**，而倒序下视口顶端对应的是**最大**索引，故 `ComputeFirstIndex` 要交出的是比它小一整个窗口跨度的那个索引。为此 `UiwVirtualListBase._poolTarget` 以只读属性 `PoolTarget` 暴露给布局策略。
  - 运行期翻转开关时已摆好的格子位置全部作废，故在 `RecomputeLayout` 里检测方向变化并整体收回重排——放在那里而非 `OnValidate`，是因为后者可能落在 Canvas Rebuild 循环内，就地回收 / 重排 UI 会报错。

## [1.7.5] - 2026-08-10

**顺序虚拟列表：行距与格子高度解耦（新增行距倍率），格子轴心由顶端改为正中（修复焦点条目对不准焦点线）。**

### 新增

- **`UiwVirtualOrderList` 新增 `rowPitchScale`（行距倍率）**，行距 = 格子高度 × 本倍率，默认 `1.0`（逐行紧贴，与旧行为一致）；大于 1 拉开间隙，小于 1 让相邻行重叠。
  - 此前 `_cellHeight` 一个值同时充当「格子自身高度」与「行距」，想调疏密只能改格子预制体的尺寸——而那会连带改变格子里所有元素的可用空间。现在两者分开：`CellHeight` 只决定实例的 `sizeDelta`，新增的 `RowPitch` 决定 Content 高度、定位、可见窗口与滚动换算。
  - `UiwFocusOrderList` 的首尾留白、焦点索引反解、`FocusIndex` 定位、滚轮步长兜底与外观曲线取样**一律改用行距**，因此焦点语义在任何倍率下都成立：滚一档仍正好换一条，第一条与最后一条仍能停到焦点线上。
  - 倍率是给美术在 Play 模式里对着调的，故 `OnValidate` 会把布局置脏、由 `LateUpdate` 重建（走既有的延迟重建通路，不在 `OnValidate` 里直接改 UI）。为此 `UiwVirtualListBase.SetViewportDirty` 由 private 提为 `protected`。

### 修复

- **`UiwFocusOrderList` 配了 `focusScaleCurve` 时，焦点条目的视觉中心并不在焦点线上**，而是低了 `(缩放 - 1) × 行距 / 2`——缩放峰值 1.5、行距 60 时正好偏 15 像素，表现为「焦点锚点选了 Center，条目却明显偏下」。
  - 成因：`UiwVirtualOrderList.SetupInstanceRect` 把格子的 pivot 设为顶端居中 `(0.5, 1)`。轴心即缩放中心，于是放大的格子只向下长开，几何中心随缩放下移，而布局与焦点反解算的一直是未缩放的行中心。
  - 修法：pivot 改为正中 `(0.5, 0.5)`，`PositionOf` 相应给出「行槽位的中心」而非顶端（补半个行距）。补偿之后「第 i 条的中心 = 留白 + i × 行距 + 行距/2」这条等式仍然成立，焦点反解一行未改。
  - **对没配缩放曲线的列表（`focusScaleCurve` 留空，以及所有 `UiwVirtualOrderList` 直接子类）排布逐像素不变**；配了曲线的列表则是修正其偏移。网格列表 `UiwVirtualGridList` 不受影响（它有自己的 `SetupInstanceRect`）。

## [1.7.4] - 2026-08-10

**修复 `ToolkitTween` 在编辑模式下泄漏 GameObject 且从不推进的问题。**

### 修复

- **`ToolkitTween` 在编辑模式下每次调用都新造一个 `[ToolkitTween]` 对象，且补间永不推进、完成回调永不触发。**
  - 成因：`ToolkitTweenRunner` 由门面惰性自建，而单例登记发生在 `Awake` 里——Unity 默认不在编辑模式调用 `Awake`，于是 `Instance` 始终为空，每次 `EnsureRunner()` 都重新创建；`LateUpdate` 同样不跑，作业表只进不出。
  - 更要紧的是**这些对象落进了用户当时打开的场景**（`hideFlags` 为 `None`，属可保存对象），一旦保存场景就会被写进场景文件。
  - 修法：给 `ToolkitTweenRunner` 标注 `[ExecuteAlways]`，并把推进逻辑从 `LateUpdate` 抽成 `Tick(scaledDelta, unscaledDelta)`；编辑模式改由 `EditorApplication.update` 驱动，用 `timeSinceStartup` 自算增量（编辑模式下 `Time.deltaTime` 不反映真实流逝），增量钳在 0.1 秒以内，避免编辑器因导入 / 编译停摆后补间一步跳完。
  - 运行器在**离开编辑模式时自毁**：编辑模式建的实例带 `HideAndDontSave`，进入播放模式不会被销毁，而静态单例引用会随域重载复位、导致播放模式又新建一个——两个运行器并存各自推进。
- **`ToolkitMonoSingleton<T>.Awake` 中两处仅限播放模式的调用改为按模式分支**，使子类能安全地标注 `[ExecuteAlways]`：
  - `DontDestroyOnLoad` 在编辑模式下会抛 `InvalidOperationException`（Unity 明确限定其只能用于播放模式）。编辑模式改设 `HideFlags.HideAndDontSave`——同样达成「不随场景保存、不被场景卸载带走」，并且把对象移出当前场景，不污染用户正在编辑的场景。
  - 重复实例分支的 `Destroy` 在编辑模式下会报错要求改用 `DestroyImmediate`，已按模式分派。
  - **对播放模式行为零影响**；未标注 `[ExecuteAlways]` 的子类（`ToolkitInputRunner`、宿主插件的管理器）行为完全不变。
- **`ToolkitInputBinder` 在编辑模式下不再自建运行器**。输入绑定在编辑模式没有意义（输入系统并不驱动玩家循环），而 `ToolkitInputRunner` 未标注 `[ExecuteAlways]`，此前同样会每次调用泄漏一个 `[ToolkitInput]` 对象到当前场景。现改为拒绝创建并给出一次明确告警，说明应把调用移到运行时。

## [1.7.3] - 2026-08-10

### 新增

- **`ToolkitTween.To(from, to, duration, onUpdate, …)` 通用浮点补间**，对应 DOTween 的 `DOTween.To(getter, setter, to, duration)`。此前 `ToolkitTween` 的七个方法都绑死在具体的写回目标上（`CanvasGroup.alpha` / `Graphic.color` / `SpriteRenderer.color` / `Transform` 的位移旋转缩放），**写回对象不是 `UnityEngine.Object` 时无路可走**——典型如第三方动画运行时暴露的裸结构体属性（Spine 的 `Skeleton.A`、Live2D 的 `CubismRenderController.Opacity`），它们既不是 Unity 对象、也无法用固定通道表达。新方法把插值结果交给调用方提供的 `Action<float>` 自行写回，补上这个缺口。
  - 内置通道能表达的场合仍请优先用内置通道——它们直接写字段，不经委托。
  - **不回读起始值**：写回路径是个只写委托，无从回读，`from` 由调用方给定并在起始时固定。需要「从当前值出发」时自行把当前值传进来。
  - **写回目标随宿主销毁时务必传 `owner`**，否则委托捕获的引用会让作业在宿主消失后继续写一个已失效的对象；传了 `owner` 也就能经 `Kill(owner)` 按宿主批量取消。
  - `onUpdate` 抛出的异常**就地捕获并记录**，该作业随即失效、完成回调不触发。这条与内置通道「用 `as` 转型 + 空守卫使类型不匹配退化为静默空操作」是同一个约定：`Write` 是在 runner 的作业循环内被调用的，单个作业的故障不能打断同帧其余作业。
  - 新增的 `Custom` 通道与 `Delay` 一样允许无 `owner`，故同样**刻意排在枚举末尾（值非 0）**——若它占 0，池复用后未填字段的脏作业会被当成合法的「无目标自定义补间」而永远存活。

## [1.7.2] - 2026-08-10

### 新增

- **`UiwFocusOrderList` 的滚轮平滑位移**（`scrollTweenDuration`，默认 0.1 秒；置 0 恢复瞬间跳变）。焦点列表的一档滚轮通常正好跨一整条，而原生 `ScrollRect` 是直接改写 `content.anchoredPosition` 的，表现为整条列表瞬间跳一格、焦点缩放曲线跟着突变。现在改由本类按缓出（quad out）曲线逐帧插值过去。
  - **一档的位移距离仍取自 `ScrollRect` 的 `Scroll Sensitivity`**，Inspector 上那个字段仍是「一档滚多远」的唯一入口，只是改由本类来应用它。
  - **本类会在 `Awake` 里把 `ScrollRect.scrollSensitivity` 取走并置 0**（仅运行期，不动预制体）。这一步是必须的：`ScrollRect` 与本类通常挂在同一个 GameObject 上，而 `ExecuteEvents` 会把滚轮事件派发给该物体上**全部** `IScrollHandler`——不清零就会先被 `ScrollRect` 瞬间挪一档、再被补间从头拉回，白抖一帧。
  - **连滚数档按目标位置累加**而非按当前位置：起点取「进行中的补间终点」，否则每档都从半路的实际位置重新起算，越滚越短、最后停在两条之间。
  - 拖拽 / 惯性 / 边界回弹仍完全是 `ScrollRect` 原生的，本类不碰；拖拽开始（`OnBeginDrag`）与 `SetItems` / `FocusIndex` 都会取消进行中的补间，避免两个位置来源打架。
  - 仍**不做**释放后吸附对齐——拖拽松手停在两条之间时，焦点缩放曲线会让上下两条都呈半放大态。

## [1.7.1] - 2026-08-10

### 修复

- **`UiwFocusOrderList` 补首尾留白**，修复「条目全挤在顶部、滚轮完全滚不动」。此前 Content 高度就是 `条目数 × 行高`、条目自顶端紧挨着排，于是：
  - **第一条与最后一条永远够不到焦点线**。焦点锚点取 `Center` 时，第 0 条的中心固定在视口顶端附近，滚到底也只能让焦点停在中间那几条上——首尾各有半个视口的条目**无法被选中**。
  - **条目少于一屏时 Content 比视口还矮**，`ScrollRect` 判定无内容可滚，滚轮与拖拽双双失效，条目则一律堆在视口顶部。这正是「转盘式列表只有 3 条时整个列表像卡死了」的成因。

  现在 Content 头尾各补一段留白（长度由焦点锚点与视口高度算出：居中锚点即上下各 `(视口高 - 行高) / 2`），使滚动量与焦点索引严格一一对应——滚到 0 即焦点第 0 条，滚到底即焦点末条，且只要有 2 条以上就必定可滚。留白随视口尺寸变化重算，变化时收回全部格子重排（否则已在窗口内的格子会停在按旧留白算出的位置上）。

## [1.7.0] - 2026-08-09

为「把上层插件从其它框架的底层依赖上摘下来」补齐四处缺口：世界坐标转 UI 坐标、纯地址取用资源、按名绑定输入、带焦点语义的虚拟列表。四项均为**纯加量**，既有 API 的签名与行为不变；唯一的破坏性变更是公开接口 `IAssetLoader` 新增了一个抽象成员（详见下方「破坏性变更」）。

### 新增

- **`UIUtility.WorldPosToUILocalPos` / `ScreenPosToUILocalPos`**：把世界坐标（或屏幕坐标）换算成某个 Canvas 下的局部坐标，用于让 UI 挂件「贴」在场景物体上（血条 / 名牌 / 操作菜单）。与既有的 `PositionAtCursor` 分工明确：那边输入是光标屏幕像素、直接写 `rt.position` 并夹取回屏内；这边返回值、不夹取，落到哪个 `RectTransform` 由调用方决定。带世界空间偏移的重载走世界空间而非屏幕像素——同样的像素偏移在不同分辨率下对应的世界距离不同，会让挂件与目标物体的相对位置随分辨率漂移。**世界投影固定用 `Camera.main`（缺省回退 `canvas.worldCamera`）**：`canvas.worldCamera` 是 UI 专用渲染相机，两者分离时拿它做投影会得到错误结果，它只在 `ScreenPosToUILocalPos` 内部喂给 `ScreenPointToLocalPointInRectangle`。
- **`ToolkitAssets.LoadByAddress<T>` / `InstantiateByAddress<T>` / `ReleaseAddress(string)`**：面向「调用方手上只有一个运行时拼出来的地址串、没有任何实时引用可回退」的取用路径（如按角色名拼出 `"…/Actors/{name}.prefab"`）。既有的 `Bind` / `Load` 都以 `AttributeValue`（或「实时引用 + 地址」二元组）为输入、有实时引用优先路径，覆盖不到这种场景。`InstantiateByAddress` 回调给出的是**新实例**而非源资源，两份生命周期各管各的：实例归调用方 `Destroy`，源资源句柄仍须 `ReleaseAddress` 按同一地址配对释放。
- **`Ale.Toolkit.Input.Runtime` 程序集与 `ToolkitInputBinder`**（受 `ATK_INPUT_SYSTEM` 约束，门控范式与 Addressables 层一致）：按「ActionMap 名 + Action 名」把回调接到 Input System 上。解决的是同一件麻烦事——**要绑定的时候输入源往往还不存在**：`PlayerInput` 常随玩家 / 角色在运行时生成，而调用方通常在自己的 `OnEnable` 里就想把回调挂上。`Bind` 立刻登记，能当场生效就当场生效，不能则挂起、由常驻运行器逐帧重试到输入源与对应 Action 可得为止，期间不丢回调；`Unbind` 对已生效与仍挂起的绑定都能正确撤销。输入源默认取场景中第一个 `PlayerInput` 的 actions，也可经 `ToolkitInputBinder.Actions` 显式指定（改写会把已生效的绑定全部退订并按新源重新解析）。
  - **回调订阅 `started` / `performed` / `canceled` 三个阶段**，而非只订 `performed`：调用方常靠「按下 / 抬起」成对触发维护拖拽之类的状态（典型写法是回调里 `ctx.ReadValue<float>()` 比对 0 / 1），只订 `performed` 会收不到抬起。只关心「触发了一次」的场合用无参 `Action` 重载，其包装体只放行 `performed`。
  - **不改动输入的启用状态**：只负责接线，不会 `Enable` 任何 ActionMap——启停由 `PlayerInput` 的 Default Map / `SwitchCurrentActionMap` 之类的输入状态机统一决定，两件事分开才不会互相打架。绑到一个当前禁用的 Map 时给出一次警告（每 Map 一次，不刷屏），因为这种情况下回调永远不触发而 Input System 本身毫无提示，极难排查。
  - 全部可变状态放在运行器实例上而非门面的静态字段：关闭 Domain Reload 时静态字段跨播放会话残留，会把上次播放留下的绑定（其 `InputAction` 已随输入系统重新初始化而失效）带进下一次；运行器随播放结束销毁，状态天然干净。`OnDestroy` 退订全部绑定——`InputActionAsset` 是 ScriptableObject 资源、在编辑器里跨播放存活，不退订会把委托永久留在资源的 Action 上。与 `ToolkitTween` / `ToolkitTweenRunner` 是同一套分工。
- **`UiwFocusOrderList<TData,TCell>` 与 `EFocusAnchor`**：带「焦点条目」语义的顺序虚拟列表，在 `UiwVirtualOrderList` 的单列纵向布局之上补两件事——① **焦点跟踪**，视口中某个位置（Top / Center / Bottom）被定为焦点线，中心最接近它的条目即焦点条目，改变时抛 `OnFocusChanged(prev, current)`；② **随焦点距离变化的外观**，两条可选曲线按「该格中心离焦点线有多远」（归一化到 `[-1,1]`，±1 对应视口边缘）驱动 `localScale` 与横向偏移，得到中间大两头小、并向两侧让开的弧形排布。这让「滚到哪儿就选中哪儿」的转盘 / 拨轮式交互不必额外接线：选中态直接由滚动位置派生，不需要点击、也不需要在格子上挂选中逻辑。
  - **不改动基类的滚动模型**：滚动仍由 `ScrollRect` 驱动（拖拽 / 滚轮 / 惯性都是它原生的），本类只读取滚动位置来派生焦点与外观，不接管输入、不做释放后吸附对齐。
  - **不自建「索引 → 格子」映射**：按滚动位置算出视口覆盖的索引区间，再逐个向基类既有的 `TryGetActiveCell` 要格子。避免与基类的回收 / 复用循环产生第二份需要同步的状态，叶子类也因此不必覆写任何额外钩子。
  - 外观回写有变化守卫（`Mathf.Approximately` 比对后才写 `localScale` / `anchoredPosition`）：写 transform 会让 UGUI 标脏并触发画布重建，静止时逐帧写入同样的值等于每帧白重建一次画布。横向偏移只改 `x`——`y` 是基类按 `PositionOf(i)` 定的行位置，覆盖它会把整个虚拟滚动的定位打乱。
  - `UpdateFocusAndAppearance` 有防重入守卫：订阅方若在 `OnFocusChanged` 回调里改数据（`SetItems`）会重新走进本方法，不拦住则可能形成事件递归。
- **`UiwVirtualOrderList.CellHeight`**（`protected`）：暴露由 `MeasureCell` 量得的行高，供子类做定位 / 焦点换算，免得各子类各自重量一遍、并在基类默认值变化时静默失配。
- **欢迎窗口新增 `ATK_INPUT_SYSTEM` 宏开关**，`ToolkitDefineChecker` 同步纳入包 / 宏一致性检查，编辑器界面文案补齐 zh / en / ja 三语。

### 破坏性变更

- **`IAssetLoader` 新增抽象成员 `LoadByAddress<T>(string address, GameObject owner, Action<T> onLoaded)`。** 包内两个实现（`DirectAssetLoader` / `AddressableAssetLoader`）均已补齐，**包外**若有第三方实现了本接口则需一并实现该方法。未采用默认接口实现（DIM），因其在 IL2CPP 下的支持历来不稳。
  - `AddressableAssetLoader` 委托既有的 `AddressableManager.LoadAsync`（按地址引用计数去重）。
  - `DirectAssetLoader` 没有实时引用可回退，故退到 `Resources.Load`（先去掉地址末段的扩展名——地址串通常带 `.prefab` 之类后缀，而 Resources 要求不带扩展名的相对路径，这是最常见的一处不匹配）；仍未命中则回调 `null` 并给出一条指明「装 Addressables + 开 `ATK_ADDRESSABLE`」的警告，而不是静默失败。

### 修复

- **`ToolkitInfo.Version` 与 `package.json` 的版本号对齐。** 此前前者停留在 `"1.5.0"`、后者已是 `1.6.0`，用 `ToolkitInfo.Version` 做版本门控的宿主会拿到落后一个 minor 的值。本次一并订正为 `1.7.0`。
- `package.json` 的 `keywords` 补 `input`。

## [1.6.0] - 2026-08-09

把中央 Tween 从「只会淡 alpha」补齐为**可整体承接 DOTween 常用单 tween 用法**的轻量门面：内部作业模型由「CanvasGroup 或 Graphic」双目标泛化为「通道 + 单一目标 + `Vector4` 载荷」的联合体，在此之上一次性补上 `SpriteRenderer` 淡入淡出、`Graphic` 整色过渡、`Transform` 位移 / 旋转 / 缩放、纯延时回调，以及 DOTween 目标登记表的等价物「按目标打断」。**导出格式与序列化结构不变、纯加量；`FadeCanvasGroup` / `FadeGraphic` 的签名与行为不变，`UiwListFadeCell` 行为不变。**

### 新增

- **`ToolkitTween.FadeSpriteRenderer(SpriteRenderer, endAlpha, …)`**：2D 精灵的 alpha 淡入淡出。`SpriteRenderer` 并非 `Graphic`，故与 `FadeGraphic` 分列。对应 DOTween 的 `spriteRenderer.DOFade`。
- **`ToolkitTween.TintGraphic(Graphic, endColor, …)`**：`Image` / 文本等 `Graphic` 的**整色（RGBA）**过渡，`FadeGraphic` 的全色版本。对应 DOTween 的 `graphic.DOColor`。
- **`ToolkitTween.MoveTransform` / `RotateTransform` / `ScaleTransform`**：`Transform` 的世界坐标 / 世界欧拉角 / 局部缩放补间，对应 DOTween 的 `DOMove` / `DORotate` / `DOScale`。旋转**逐轴独立走最短弧**（起始时经 `ShortestEuler` 烘焙终值，350° → 10° 只转 20° 而非 340°），等价于 DOTween 的 `RotateMode.Fast`；**不**支持 `FastBeyond360` 那样超过 360° 的多圈旋转。
- **`ToolkitTween.DelayedCall(delay, onComplete, unscaled = true, owner = null)`**：纯延时回调，不插值任何目标，对应 DOTween 的 `DOVirtual.DelayedCall`。可选 `owner` 绑定生命周期——owner 被 `Destroy` 后回调丢弃，且可经 `Kill(owner)` 按 owner 取消；不传则为独立于任何对象存亡的纯计时器（DOTween 的默认语义）。
- **`ToolkitTween.Kill(UnityEngine.Object target, bool complete = false)`**：打断该目标上全部在途作业并返回打断数，DOTween 目标登记表的等价物（对应 `target.DOKill()` / `target.DOComplete()`）。目标按**引用相等**匹配而非 Unity 的 `==`——后者会把两个已销毁对象都判为 null 而互相「相等」，用它会误杀无关作业；因此已被 `Destroy` 的目标依然能用本方法清理自己的作业。匹配是精确的对象身份，`Kill(gameObject)` 找不到挂在其上的组件的作业。
- **`ToolkitTween.ShortestEuler(fromEuler, toEuler)`**：把目标欧拉角折算为「自起始角出发、各轴独立走最短弧」的等价终点（逐轴 `from + Mathf.DeltaAngle(from, to)`）。`RotateTransform` 内部用它烘焙终值，公开出来供调用方复用。
- **`ToolkitTweenHandle.Complete()`**：立即完成——瞬置终值并触发完成回调（同步、在调用栈上）。等价 `Kill(true)`，对应 DOTween 的 `Tween.Complete()`。
- **`ToolkitTweenHandle : IEquatable<ToolkitTweenHandle>`** 及 `==` / `!=` / `GetHashCode`：可直接放进 `List<ToolkitTweenHandle>` 并用 `List.Remove(handle)` 移除。此前落到 `ValueType.Equals` 的反射逐字段比较，**结果相同但每次比较都要装箱**；现在走 `EqualityComparer<T>` 的 `IEquatable` 快路径，零分配。`operator ==` 此前无法书写，故无源码破坏。
- **测试 `Assets/Tests/ToolkitTweenTests.cs`**：21 个 EditMode 用例，覆盖 `ToolkitEase.Evaluate` 四种缓动的端点 / 越界钳制 / 单调性 / 值域、`ShortestEuler` 的跨 0° 与逐轴独立折算，以及空句柄、`DelayedCall` 与 `Kill` 的快路径守卫。此前 Tween 模块零测试覆盖。

### 变更

- **作业模型泛化**：内部 `TweenJob` 由「`CanvasGroup` XOR `Graphic`」双目标改为 `UnityEngine.Object Target` + `ETweenChannel` 通道 + `Vector4 From/To` 载荷，`SetAlpha(float)` 改为按通道分发的 `Apply(float k)` / `ApplyEnd()`。**单池、单作业表、单 `LateUpdate` 推进与近零 GC 均不变**；每作业约多 32 字节，数十个并发作业量级下可忽略。评估过「抽象基类 + 每类型子类 + 每类型池」，因 `ToolkitClassPool<T>` 是按闭合类型静态泛型的、多子类会退化为 N 个互不复用的池而否决。
- **插值改用 `LerpUnclamped`**：现有四种缓动的输出恒落在 [0,1]（已由 `Evaluate_AllEases_StayWithinUnitRange` 用例锁定），故与 `Mathf.Lerp` **结果逐位相同**；为将来可能引入的过冲缓动预留。收尾与「立即完成」走 `ApplyEnd()` **精确写入终值**、不经插值——`a + (b - a) * 1` 未必逐位等于 `b`，此举与此前 `SetAlpha(job.To)` 的语义保持一致。
- **`Runtime/Tween/` 拆分为三个文件**：`ToolkitTween.cs`（门面 + 句柄）/ `ToolkitTweenJob.cs`（通道枚举 + 作业）/ `ToolkitTweenRunner.cs`（运行器）。**`ToolkitTween.cs` 的 `.meta` GUID 保留**；迁出与新增的类型均为 `internal`（`ETweenChannel` / `TweenJob` / `ToolkitTweenRunner`）或非序列化值类型（`ToolkitTweenHandle` 无 `[Serializable]`，包内唯一使用处 `UiwListFadeCell._rootFade` 是私有非 `[SerializeField]` 字段），且 `ToolkitTweenRunner` 只在运行时由 `AddComponent` 创建、不存在于任何场景 / 预制体——**资源引用不受影响**。
- **`ToolkitTweenRunner` 移入同名文件**，消除此前「MonoBehaviour 类名与文件名不符」。
- `package.json` 的 `keywords` 补 `tween`（Tween 模块自 1.3.0 起随包发布，一直未列入）。

### 修复

- **订正 README 中 Tween 章节的过时表述**：包内三份 README 的正文仍写「当前提供 `CanvasGroup` 淡入淡出」，遗漏了 1.5.1 加入的 `FadeGraphic`；仓库根目录三份 README 的模块表同样只列了 `FadeCanvasGroup`。本次一并更正并补齐新增 API 与行为说明。

> **与 DOTween 的行为差异（迁移须知）**：① 本门面**不做覆盖管理**——对同一目标同一通道再起一个 tween 不会自动打断前一个，两者会在同一帧互相争写（DOTween 亦然，故 `DOKill(); DOFade(...)` 的写法可原样迁移为 `Kill(target); Fade…(target, …)`）；② 所有 API 的 `unscaled` 默认 `true`，而 DOTween 默认受 `Time.timeScale` 影响，需要还原 DOTween 行为时请显式传 `unscaled: false`；③ `DelayedCall(delay ≤ 0)` **同步立刻**触发回调并返回空句柄（DOTween 推迟到下一帧）——若调用方拿到句柄后才记进列表，请用 `if (h.IsActive) list.Add(h);` 守卫，否则回调里的 `list.Remove(h)` 会先于 `Add` 执行、留下永不移除的僵尸条目；④ 旋转等价 `RotateMode.Fast`，不支持多圈；⑤ 仍不提供 Sequence / 泛型链式 / 全套 Ease。

## [1.5.1] - 2026-08-04

为「虚拟滚动列表」补上一套**通用的单元格淡入淡出**：新增列表单元格契约接口与淡入淡出基类，`UiwVirtualListBase` 的默认 hook 自动驱动——任何继承的单元格白得「分配（滚入）淡入 / 回收（滚出）淡出」，各列表无需 override。`ToolkitTween` 扩展出 `Graphic` 淡入以支撑逐图片淡入。**默认无实现者时行为不变；导出格式与序列化结构不变、纯加量。**

### 新增

- **`UiwListFadeCell`（`Ale.Toolkit.Runtime.UI`）**：列表单元格「根 CanvasGroup 淡入淡出」通用基类（继承 `UiwHoverTooltipSource`、实现 `IUiwRecycleFadeCell`）。`PlayShowFade`（分配淡入）/ `FadeOutAndHide`（回收淡出，完成回调）/ `CancelRootFade` / `ResetRootVisible`；根 CanvasGroup 惰性获取 / 补挂；序列化时长 `rootFadeInDuration` / `rootFadeOutDuration`；安全阀 `RecycleFadeEnabled`（默认 true，可 override 退出，如根 CanvasGroup 另作它用时）。业务单元格继承即白得对称淡入淡出。
- **单元格契约接口（`Ale.Toolkit.Runtime.UI`）**：`IUiwRecycleFadeCell`（`PlayShowFade` / `FadeOutAndHide` / `CancelRootFade`）与 `IUiwDiffCell<TData>`（`MatchesSlot`，增量差异刷新跳过重绑）。供 `UiwVirtualListBase` 默认 hook 驱动。
- **`ToolkitTween.FadeGraphic(Graphic, …)`**：`Image` / 文本等 `Graphic` 的 alpha 淡入淡出。内部 `TweenJob` 泛化为「CanvasGroup 或 Graphic」双目标（互斥、近零 GC）；`FadeCanvasGroup` 行为不变。
- **`SpriteSlot.Bind(…, onApplied)`**：可选「图片就位」回调（有图 / 无图各触发一次，代次过期不触发），供调用方在 Sprite 就位后再淡入等；默认 null，现有调用零变化。
- **`UiwVirtualListBase.TryGetCellPrefabSize`**：读取 `cellPrefab` RectTransform 尺寸的 protected helper，供布局子类复用。

### 变更

- **`UiwVirtualListBase` 默认 hook 改为驱动接口**：`TryPlayRecycleAnim`（回收淡出）/ `CancelRecycleAnim` / `NeedsRebind`（增量差异）默认检测单元格是否实现对应接口并驱动之——**未实现时行为与旧逻辑等效**（即时回收 / 恒重绑）；`FillWindow` 生成后统一调 `PlayShowFade`（仅生成路径，不在就地刷新触发）。子类不再需要为「淡入淡出 / 差异刷新」逐个 override。回收淡出经内建 limbo 记账保持格子存活播放淡出、完成后再清空归还（不接管的列表零影响）。
- **`UiwVirtualGridList` / `UiwVirtualOrderList` 去重**：两者 `MeasureCell` 改用 `TryGetCellPrefabSize`，去除重复的 RectTransform 读取样板（行为等价，各自保留默认尺寸）。
- **虚拟列表脚本命名规范化**：若干列表 / 基类脚本命名统一（`.meta` GUID 保留，预制体引用不受影响）。

### 修复

- **补声明 Newtonsoft Json 依赖**：条件系统（`Ale.Condition.Core`，1.4.0 引入）的 `ConditionJson` 用 `Newtonsoft.Json` 序列化，其 asmdef 已按 `precompiledReferences: ["Newtonsoft.Json.dll"]` 正确引用，但包 `package.json` 一直**未声明该依赖**——在未安装 `com.unity.nuget.newtonsoft-json` 的工程里会编译报 `CS0246: 找不到 Newtonsoft`。现于 `dependencies` 声明 `com.unity.nuget.newtonsoft-json`（Unity 官方 registry 包，装本包时自动拉取）。

## [1.5.0] - 2026-08-02

新增轻量「展示文本」值类型 **`TextValue`**（纯文本 fallback + 可选原生 `LocalizedString`），作为 `AttributeValue` 的 `EFieldType.Text` 的**独立轻量版**——每实例仅一个 string（启用本地化时 +1 个 `LocalizedString`），无 `AttributeValue` 预分配六个类型后备列表的开销；且**直接内嵌 Unity 原生 `LocalizedString`**，Inspector 用原生表/条目选择器、选择即由原生序列化正确保存。另把 UI 组件里承载展示文本的 TMP/UGUI 文本类型别名 `InventoryText` 统一更名为 `UiText`（去领域化）。**导出格式与通用序列化结构不变、纯加量；仅 `UiwViewBase` 两个模式切换标签由 `string` 升级为 `TextValue`（见「变更」）。**

### 新增

- **`TextValue`（`Ale.Toolkit.Runtime`）**：轻量展示文本值。始终携带纯文本 fallback；启用 `ATK_LOCALIZATION` 时额外内嵌一个 Unity `LocalizedString`。`ResolveText()` 本地化优先、取不到回退 fallback；另有 `Fallback` / `Localized`（本地化宏下）/ `IsEmpty` / `Clone()`（深拷贝，本地化引用另建一份复制表/条目引用）。相比用 `AttributeValue` 的 `EFieldType.Text` 承载展示文本更省——无预分配的多类型后备列表，适合「在组件 / 配置上直接声明一个可本地化文本字段」的场合。
- **`TextValueDrawer`（`Ale.Toolkit.Editor`，`[CustomPropertyDrawer(typeof(TextValue))]`）**：一行纯文本 fallback + 启用本地化时内嵌 `LocalizedString` 的 **Unity 原生表/条目可搜索选择器**；由原生绘制器负责编辑与序列化，**选择即正确保存**。绘制器不含 `#if`——靠 `localized` 子属性是否存在判断本地化是否被编译进来（未启用时仅画 fallback）。
- **测试**：`Assets/Tests/TextValueTests.cs` 覆盖 fallback 取值 / 空判定 / 深拷贝 / `ResolveText` 回退等。

### 变更

- **`InventoryText` → `UiText`**：UI 组件顶部承载「TMP 或 UGUI 文本」的 `using` 类型别名（`ATK_TMP` 下为 `TMPro.TMP_Text`、否则 `UnityEngine.UI.Text`）由 `InventoryText` 更名为 `UiText`——toolkit 已通用化，别名不再沿用库存时代的 `Inventory` 前缀。涉及 `UiwTextLabel` / `UiwFilterTabBar` / `UiwFoldTab` / `UiwTabButton` / `UiwNumberCounter` / `UiwSortToolbar` / `UiwViewBase` 共 7 个文件，**纯别名改名，无行为变化**。
- **`UiwViewBase` 模式切换标签改用 `TextValue`**：顺序 ↔ 网格切换按钮的两个标签 `orderModeLabel` / `gridModeLabel` 由 `string` 升级为 `TextValue`（默认值仍为「列表」/「网格」），使其可本地化；`ApplyViewMode` 改经 `ResolveText()` 取文本。**注意**：字段类型由 `string` 变为 `TextValue`，Unity 不会迁移旧序列化值——预制体 / 场景上此前**自定义过**这两个标签文本的会回退到默认「列表」/「网格」，需重新填写（未改过的无影响）。

## [1.4.0] - 2026-08-01

四个面向「数据驱动配置」的通用运行时能力落位：**属性修饰器求值**（GAS 式分组结算 + 来源明细）、**数据库编辑器窗口外壳基类**，以及两个对称的独立子系统——**条件系统（Condition System）** 与 **效果系统（Effect System）**。两个子系统均为「引擎无关 Core（可上服务端）+ Unity 桥（启动自动注册）+ 真·内联 `[CustomPropertyDrawer]`（声明字段即在 Inspector 配置）」三层结构，通过 `[Attribute]` 反射 / TypeCache 自动发现上层实现，供任意上层插件（角色 / 战斗 / 技能…）扩展自己的判定与效果。**新增 6 个程序集**（`Ale.Condition.Core/.Runtime/.Editor` + `Ale.Effect.Core/.Runtime/.Editor`）；修饰器与窗口基类落在既有 `Ale.Toolkit.Runtime` / `Ale.Toolkit.Editor`。**导出格式与序列化结构不变、纯加量、无破坏性改动。**

### 新增

- **属性修饰器求值设施（`Ale.Toolkit.Runtime`）**：`ModifierDefinition`（`targetAttributeId` / `operation` / `magnitude` / `duration` / `durationDays` / `sourceTag` / `stackLimit` / `stackRule`）+ 操作枚举 `EModifierOperation`（`Add` / `PercentAdd` / `Multiply` / `Override`）/ `EModifierDuration`（`Instant` / `Timed` / `Permanent`）/ `EStackRule`（`Refresh` / `Add` / `EveryXStacks` / `OnMaxStacks`）+ 纯函数 `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown)`：按固定顺序分组结算（`base → +ΣAdd → ×(1+ΣPercentAdd) → 逐项 Multiply → 末位 Override 覆盖 → clamp`），返回 `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`（含逐来源 `ModifierContribution`）。静态、无状态、无 Unity 依赖；时长 / 叠层为「配置携带、运行时结算」。
- **数据库编辑器窗口基类 `EditorDatabaseWindowBase<TDb>`（`Ale.Toolkit.Editor`）**：把「持有 DB 资产对象字段 + 顶部页签条 + 校验 / 导出按钮钩子 + 查重扫描编排 + 状态栏 + Undo 订阅 + 上次 DB 路径记忆（EditorPrefs）」抽为泛型外壳，宿主窗口只提供页签集合 / 导出·校验回调 / 查重种类即可大幅变薄；实现 `IEditorDbContext<TDb>` 供各面板取用。
- **条件系统（Condition System · `Ale.Condition`）**：数据驱动的两级 AND/OR 条件（表达式 → 组 → 项 → 参数），「声明一个 `ConditionExpression` 字段即在 Inspector 内联配置」。
  - `Ale.Condition.Core`（引擎无关，`noEngineReferences`，引用 Newtonsoft）：纯 POCO 模型 `ConditionExpression` / `ConditionGroup` / `ConditionItem` / `ConditionParam`（三列扁平后备 + 5 标量类型 + 数组）；判定契约 `IConditionEvaluator`（`Key` / `DisplayName` / `Category` / `ParamSchema` / `Evaluate`）+ `[ConditionEvaluator("Ns.Key")]` + `ConditionRegistry`（`Default` + 反射 `AutoRegisterFromAssemblies`）；上下文 `IConditionContext`（`Subject` + `GetService<T>()`）；静态引擎 `ConditionEngine.Evaluate`（组内 / 顶层 And·Or × 每项 / 每组 NOT × 短路，空表达式 = 通过）；`ConditionJson`（Newtonsoft 往返，可换序列化器）；内置判定器 `Condition.AlwaysTrue` / `Condition.HasFlag`（`IConditionFlagSource`）/ `Condition.NumberCompare`（`IConditionNumberSource`）。
  - `Ale.Condition.Runtime`：`ConditionAsset`（可选 SO 容器）+ `ConditionRuntime`（`[RuntimeInitializeOnLoadMethod]` 启动把 `Default` 反射填满 + 去重缺键告警）。
  - `Ale.Condition.Editor`：真·`[CustomPropertyDrawer(typeof(ConditionExpression))]`（组 / 项 / 参数增删、And·Or 切换、NOT、按 Category 分组的判定器下拉、按 schema 动态参数区 + `choices` 固定选项下拉，全程 `SerializedProperty`、Undo 自动）+ `ConditionEvaluatorCatalog`（TypeCache 发现 + `SyncParameters`）+ `ConditionWelcomeWindow`（`Tools > Ale Toolkit > Condition System > Welcome`，总览已发现判定器）。
- **效果系统（Effect System · `Ale.Effect`）**：条件系统的**写侧镜像**——数据驱动、参数化的**离散触发式突变**，按「阶段组」组织、每项可挂可选条件门控。数值汇流由上述修饰器负责，效果只做离散动作（授予 / 移除、置标志、发事件、点燃…）。
  - `Ale.Effect.Core`（引擎无关，`noEngineReferences`，引用 `Ale.Condition.Core` + Newtonsoft）：模型 `EffectExpression`（一级阶段分组，去 AND/OR）/ `EffectGroup`（`phase` 时机标签 + 有序 items）/ `EffectItem`（`key` + `parameters` + 可选 `gate: ConditionExpression`）/ `EffectParam`（与 `ConditionParam` 同构、各自平行）；执行契约 `IEffectExecutor`（`Execute → EffectResult`）+ `[EffectExecutor("Ns.Key")]` + `EffectRegistry`（`Default` + `AutoRegisterFromAssemblies`）；上下文 `IEffectContext : IConditionContext`（同一上下文供 gate 读服务 + 效果写 Sink）；`EffectResult{ Outcome, Note }` + 聚合 `EffectRunReport`；静态运行器 `EffectRunner.Run(expr, ctx, phase, …)`（按序执行、phase 过滤含空 phase 通配、逐项 gate 走 `ConditionEngine`、缺键告警）；`EffectJson`（Newtonsoft）；内置执行器 `Effect.NoOp` / `Effect.SetFlag`（`IEffectFlagSink`）/ `Effect.AdjustNumber`（`IEffectNumberSink`）。
  - `Ale.Effect.Runtime`：`EffectAsset`（可选 SO 容器）+ `EffectRuntime`（`[RuntimeInitializeOnLoadMethod]` 启动自动注册 + 去重缺键告警）。
  - `Ale.Effect.Editor`：真·`[CustomPropertyDrawer(typeof(EffectExpression))]`（阶段组 / 效果项 / 参数增删、按 Category 分组的执行器下拉、schema 动态参数区 + `choices` 下拉、**每项内联渲染门控条件**——嵌套 `ConditionExpression` 由条件系统绘制器自动提供 UI）+ `EffectExecutorCatalog`（TypeCache + `SyncParameters`）+ `EffectWelcomeWindow`（`Tools > Ale Toolkit > Effect System > Welcome`）。

## [1.3.0] - 2026-07-29

新增两个通用运行时模块——**对象池**（供上层插件替代 Lean.Pool 一类第三方池：GameObject 预制体池 + 纯 C# 类池两套）与轻量**中央 Tween**（DOTween 式「单 Update 轮询作业表」，作业池化近零 GC），二者均落在 `Ale.Toolkit.Runtime`（仅依赖 UnityEngine，无新程序集 / 无新依赖）；并为视图基类 `UiwViewBase` 补「初始激活即自打开」。**导出格式与序列化结构不变。**

### 新增

- **`ToolkitGameObjectPool`（GameObject 预制体池，MonoBehaviour）**：`Spawn(pos,rot,parent)` 等三档重载 + `Despawn(clone, delay)` 延迟归还；`Preload` <b>惰性</b>预热（不受 `AddComponent` 后属性赋值时机影响）；`Capacity` + `Recycle`（达上限强制回收最早取用者来复用）；`Persist` 跨场景；`Spawned` / `Despawned` / `Total` 计数与 `DespawnAll` / `DespawnOldest` / `Clean` / `Clear`。
- **`IPoolable`（`OnSpawn` / `OnDespawn`）** + **嵌套枚举 `ToolkitGameObjectPool.PoolNotificationType`**（`None` / `SendMessage` / `BroadcastMessage` / `IPoolable` / `BroadcastIPoolable`）：取用 / 归还时按所选方式通知克隆体，默认 `IPoolable`。
- **`ToolkitPool`（静态门面）**：按预制体<b>自动建池</b>的泛型 `Spawn<T>` / 非泛型 `Spawn`（可就地替换 `Instantiate`）+ 全局 `Despawn(clone)`——经「克隆体 → 属主池」登记表 `Links` 路由，把任意克隆体归还其属主池；`DespawnAll` / `Detach`；关闭 Domain Reload 时于 `SubsystemRegistration` 复位静态登记表。
- **`ToolkitClassPool<T>`（纯 C# 引用类型对象池）**：`Spawn()` / `Spawn(Predicate<T>)` / `Spawn(Action<T>)` 组合重载（池空返回 `null`，构造留给调用方）+ `Despawn(T)` / `Despawn(T, Action<T>)`；用于池化非 Unity 对象、降低 GC；各闭合泛型经 `ToolkitSingletonRegistry` 在播放开始统一复位。
- **`ToolkitTween`（中央 Tween 静态门面）+ `ToolkitTweenRunner`（常驻运行器）**：`FadeCanvasGroup(target, endAlpha, duration, ease, unscaled, onComplete)` 对 `CanvasGroup.alpha` 做淡入 / 淡出，返回值类型句柄 `ToolkitTweenHandle`（`IsActive` 查询 / `Kill(complete)` 打断，零分配、按作业 ID 校验防误杀已被池复用的作业）；作业经 `ToolkitClassPool<TweenJob>` 池化、由常驻 `ToolkitTweenRunner`（`ToolkitMonoSingleton`，跨场景持久 + 关闭 Domain Reload 自动复位）单 `LateUpdate` 轮询推进，近零 GC。轻量作用域：不复刻 DOTween 的 Sequence / 链式 / 全套 Ease，按需增量扩展。
- **`ToolkitEase` / `EToolkitEase`（缓动求值最小集）**：`Evaluate(ease, t)` 把线性进度 `t∈[0,1]`（自动 Clamp01）映射为插值系数；缓动类型含 `Linear` / `InQuad` / `OutQuad` / `InOutQuad`。

### 变更

- **`UiwViewBase` 新增 `IsOpen` 状态并在 `Start` 自打开**：面板在场景中**初始即激活**（`activeInHierarchy`）时，`Start` 补一次 `Open()` 构建其内容（初始未激活则 Unity 不执行 `Start`、视图自然保持 `Close`）；若已被外部（管理器）先行 `Open`（`IsOpen` 为 true）则跳过、以免重复构建。`Open` / `Close` 同步维护 `IsOpen`。子类覆写 `Start` 时应在**末尾**调用 `base.Start()`。

## [1.2.0] - 2026-07-26

把「可选依赖宏」与「编辑器界面语言」这两项**项目级全局设定**下沉为 toolkit 统一管理，新增可对任意数据资产工作的通用工具窗口，并让这些通用工具**足以完全替代上层插件的专用工具**（预制体向导字体亦下沉为全局设定，并存入 `ProjectSettings/` 随仓库共享）。**宏改名为破坏性变更（见下，老项目自动迁移）**；导出 DTO 格式不变，`Tag` 的背景图字段并入属性系统（Unity 序列化结构有变，需一次性资产迁移，由宿主插件的迁移工具处理）。

### 变更

- **⚠️ 可选依赖宏改名**：`IS_TMP` / `IS_LOCALIZATION` / `IS_ADDRESSABLE` → `ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`（`IS_` 原为 Inventory System 缩写；宏下沉 toolkit 后改用 `ATK_` = Ale Toolkit）。老项目已设的旧宏由 `ToolkitDefineChecker` 在加载时**自动迁移**（补新宏、移旧宏，一次性幂等），无需手改。
- **`Tag.backgroundSprite` → `AttributeValue`（`backgroundSpriteValue`，`EFieldType.Sprite`）**：标签背景图并入属性系统，编辑器经 `EditorTagPanel` / 属性绘制器统一绘制；通用 Addressable 工具据此自动覆盖，无需固定字段特例。既有资产由宿主的一次性迁移工具搬运。
- **通用本地化窗口表绑定内化**：`ToolkitLocalizationToolWindow` 不再用 EditorPrefs 记录关联表，改为**从已生成字段的 `tableRef` 反推**（绑定随数据本身保留、可提交、团队共享）；首次生成前经窗口「关联多语言表」选择。

### 新增

- **宏中枢 `ToolkitDefines`** + **`ToolkitDefineChecker`**（`[InitializeOnLoad]`）：宏名常量 / 启用状态 / 包安装检测集中于此；加载时自动迁移旧宏 `IS_*` → `ATK_*`，并对「开了宏却没装对应包」做 Console 一致性提示。
- **`ToolkitWelcomeWindow`**（菜单 `Tools > Ale Toolkit > Welcome`）：承载项目级全局设定——界面语言（中 / English / 日本語）+ 枚举翻译开关 + 三个可选依赖宏开关（通用措辞）+ 通用工具入口 + 文档；页脚新增「启动时自动显示」开关（EditorPrefs 每人自定，默认开启），由 `ToolkitWelcomeChecker`（`[InitializeOnLoad]`）在每个 Unity 会话首次加载时自动弹出一次。
- **通用工具窗口**：`ToolkitAddressableToolWindow`（`Tools > Ale Toolkit > Addressable`）与 `ToolkitLocalizationToolWindow`（`Tools > Ale Toolkit > Localization`）——指定任意数据资产（`ScriptableObject`），自动遍历其全部 `AttributeValue` 批量处理（Addressable 处理 Sprite / Prefab 等对象值，本地化处理 Text 值，经新增的 `TextFieldWalker` 反射收集）。供上层插件复用。
- **`TextFieldWalker` id 感知 Key**：通用本地化收集器遍历列表元素时优先用其 `id` / `name` 作路径段（如 `库-Skills-fireball-displayText`），Key **稳定**（列表重排不失效）且可读。
- **`ToolkitProjectSettings` + `ToolkitPrefabFonts`（向导字体项目级设定，版本控制友好）**：预制体生成向导的「默认 TMP 字体」与「本地化字体」下沉为项目级全局设定，改在欢迎窗口 TextMeshPro / Unity Localization 宏方块下配置（勾选启用才显示）。经 `ScriptableSingleton` 持久化到 `ProjectSettings/AleToolkitSettings.asset`（**随仓库入库、按 GUID 引用资源、团队共享**）；`ToolkitPrefabFonts` 作为读写门面供各插件生成向导取用。

### 修复

- **`LocalizedStringHolder` 拆分遗留**：toolkit 的 `AttributeFieldDrawer`（`ATK_LOCALIZATION`）依赖它，却在拆分时被落在库存包内，导致开启本地化宏时报 `CS0246`。现迁入 toolkit（`Ale.Toolkit.Editor`）。
- **`AttributeFieldDrawer` 本地化 rect 绘制**：修正未定义变量 `y` → `rectY + lh + 标准间距`（拆分遗留、仅本地化宏开启时暴露）。
- **向导本地化字体无法持久化**：原经 `JsonUtility` 整体序列化 `LocalizedTmpFont`，但 `JsonUtility` 不会触发嵌套 `TableReference` / `TableEntryReference` 结构体的 `ISerializationCallbackReceiver`，读回时 `ReferenceType` 无法重建、引用变空（显示 None）。改由 `ToolkitProjectSettings`（`ScriptableSingleton`）的**原生序列化**存取，欢迎窗口把绘制器直接绑定到该设置对象——从根本上修复。

## [1.1.0] - 2026-07-26

在 1.0.0 拆分基础上补齐「独立复用」所需的完整性缺口，使 toolkit 脱离库存插件也能单独工作、界面不回退中文。**导出格式与序列化结构不变。**

### 新增

- **Addressables 运行时层**：`AddressableManager`（按地址引用计数加载 / 卸载）、`AssetOwnerTracker`（宿主销毁自动释放句柄）、`AddressableAssetLoader`（`IS_ADDRESSABLE` 启用时自动注册为 `ToolkitAssets.Loader`）迁入 `Ale.Toolkit.Addressables.Runtime`（原为只有 asmdef 的空壳）。至此 TMP / Localization / Addressables 三个可选依赖支持层对称完整。
- **宏开关工具 `DefineUtils`**（`Editor/Defines/`）：`ApplyDefine`（增删 PlayerSettings 脚本宏）+ `HasNamespace` / `HasClass`（探测某包是否已安装），供消费方自建可选依赖的宏开关面板。
- **编辑器多语言补全**：新增 `Table.Framework`（三列框架基类 + 通用页签 / 搜索控件文案）与 `Table.Tagging`（标签面板文案），补全 `Table.Attributes`（`类型` / `枚举类型` / `文本` 等绘制器标签），接线 `RegisterTagging`——纯 toolkit 环境下框架按钮、属性绘制器、搜索框、标签面板不再回退中文。

### 变更

- **标签面板去领域化**：`EditorTagPanel` 中「道具属性字段」「附加到道具后…」等含具体业务词的文案改为通用措辞（「属性字段」「附加后…目标的…」），toolkit 基类不再出现「道具」。

### 修复

- **`Ale.Toolkit.UI.Localization` asmdef 悬空引用**：引用名由不存在的 `Ale.Toolkit.UI` 改为实际的 `Ale.Toolkit.Runtime.UI`（`IS_LOCALIZATION` 门控，此前仅表现为 Unity 未解析引用警告）。

### 移除

- 删除无任何调用点的死代码 `EditorMutate`。

## [1.0.0] - 2026-07-26

首个版本。从 `com.ale.inventory` 1.8.0 拆分而来的全部通用能力已迁入到位。

### 新增

- 包骨架：`package.json`、六个 Assembly Definition（`Ale.Toolkit.Runtime` / `Ale.Toolkit.Runtime.UI` / `Ale.Toolkit.UI.Localization` / `Ale.Toolkit.Addressables.Runtime` / `Ale.Toolkit.Editor` / `Ale.Toolkit.Addressables.Editor`）、三语 README 与许可文件。
- `ToolkitInfo`：包名与版本常量，供宿主插件做版本检查。

### 已迁入

以下通用能力已从 `com.ale.inventory` 迁入本包（命名空间统一为 `Ale.Toolkit.*`，类型名多数不变）：

- 属性系统（`AttributeValue` 全家、自定义枚举类型、数字格式配置、配置模板基类、分组标签基类）
- 排序（`SortPriority` / `SortOption` / `ISortContext<TData>` / `SortContextBase<TData>` / `AttributeSortService` / `SortOptionSync`；`ISortId` / `SortFieldKeys`）
- 标签系统（`FunctionTag` 通用化为 `Tag`、标签序号排序 `TagOrderMap` / `TagSortContextBase`、标签编辑面板与勾选列表）
- 运行时基础（单例基类、存档契约、资源加载抽象、覆盖式 UI 宿主）
- 通用序列化（属性系统对应的 DTO、二进制编解码、DTO 映射辅助）
- UI（虚拟滚动列表引擎与网格 / 顺序布局、页签栏、过滤栏、Tooltip 基类、子项实例池、通用工具函数）
- 编辑器框架（三列布局页签、主列表面板、实体列表面板、分组标签面板、工具窗口基类）
- 编辑器控件（拖拽重排、键盘导航、可搜索列表、样式表、重复 ID 扫描）
- 属性绘制器、枚举类型面板、数字格式面板、标签面板、反射遍历 `AttributeValueWalker`（**整理选项面板 / 整理设置绘制器因字段发现属仓库业务，仍留在库存**）
- 编辑器界面三语服务（中 / English / 日本語）
- UGUI 预制体搭建工具箱
- 三个可选依赖的支持层：TextMeshPro（`IS_TMP`）、Unity Localization（`IS_LOCALIZATION`）、Addressables（`IS_ADDRESSABLE`），含本地化工具窗口与 Addressable 工具窗口

### 安装须知

Unity 的 Package Manager 不支持在 `package.json` 的 `dependencies` 中使用 git URL，因此**本包必须先于依赖它的插件手动安装**。详见 [README](README.md) 的安装章节。
