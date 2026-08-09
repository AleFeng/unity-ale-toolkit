# 更新日志（Changelog）

本文件记录 Ale Toolkit（`com.ale.toolkit`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

> 由来：本包自 `com.ale.inventory` 1.8.0 拆分而来。原先埋在库存系统里的通用能力被抽出，使其可被更多插件复用（例如后续的角色系统）。拆分过程中**导出格式与序列化结构不变**，类型的命名空间由 `Ale.Inventory.*` 改为 `Ale.Toolkit.*`。

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
