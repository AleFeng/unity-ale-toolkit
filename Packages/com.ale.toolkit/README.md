# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

面向 Unity 插件开发的**通用底层库**。不含任何具体业务领域概念，供多个插件共享同一套属性配置、列表、编辑器框架与多语言能力。

> 本包由 `com.ale.inventory` 1.8.0 拆分而来。原先埋在库存系统里的通用能力（编辑器三列框架、虚拟滚动列表、自定义属性系统、编辑器界面三语）被抽到这里，使其可被更多插件复用。

---

## 目录

- [包含的模块](#包含的模块)
- [程序集](#程序集)
- [用法与主要 API](#用法与主要-api)
  - [属性系统](#属性系统) · [排序](#排序) · [UI](#ui) · [对象池](#对象池) · [Tween（中央缓动）](#tween中央缓动)
  - [属性修饰器](#属性修饰器) · [标签系统 · GameplayTag System](#标签系统--gameplaytag-system) · [条件系统 · Condition System](#条件系统--condition-system) · [效果系统 · Effect System](#效果系统--effect-system)
  - [编辑器框架](#编辑器框架) · [编辑器多语言](#编辑器多语言) · [可选依赖支持层](#可选依赖支持层) · [编辑器入口与全局设置](#编辑器入口与全局设置) · [通用工具窗口](#通用工具窗口)
- [许可](#许可)

---

## 包含的模块

| 模块 | 内容 |
| --- | --- |
| **属性系统** | `AttributeValue` 与 20+ 字段类型、属性定义（schema）、自定义枚举类型、数字格式配置、轻量展示文本 `TextValue`（fallback + 可选原生本地化）。任何需要「配置属性条目」的场合都用它 |
| **排序** | 与元素类型无关的排序引擎：宿主实现 `ISortContext<TData>` 提供比较所需信息，引擎负责多级优先级与降级比较 |
| **UI** | 虚拟滚动列表（网格 / 顺序，对象池 + 仅渲染可见区；单元格分配 / 回收淡入淡出经 `UiwListFadeCell` + 引擎默认 hook 通用驱动）、页签栏、过滤栏、Tooltip 基类、右键上下文菜单 `UiwContextMenu`、模态弹窗基类 `UiwModalPopupBase`（1.13.0 起）、子项实例池等通用控件 |
| **对象池** | 通用 GameObject 预制体池（`Spawn`/`Despawn` + `IPoolable` 回调、预热 / 容量回收 / 延迟归还 / 跨场景）与纯 C# 引用类型池 `ToolkitClassPool<T>`（降 GC），可替代 Lean.Pool 一类第三方池 |
| **Tween** | 轻量中央 Tween（DOTween 式单 Update 轮询、作业池化近零 GC）：`FadeCanvasGroup` / `FadeGraphic` / `FadeSpriteRenderer` 淡入淡出，`TintGraphic` 整色过渡，`MoveTransform` / `RotateTransform` / `ScaleTransform` 位移·旋转·缩放，`DelayedCall` 延时回调，`Kill(target)` 按目标打断；返回值类型可打断句柄；缓动最小集 `EToolkitEase` |
| **属性修饰器** | GAS 式修饰器求值（引擎无关程序集 `Ale.Modifier.Core`，命名空间 `Ale.Modifier`）：`ModifierDefinition` + `ModifierStackEvaluator` 分组结算（Add→PercentAdd→Multiply→Override + clamp + 来源明细）。任何「基础值 + 一叠加成 → 当前值」的数值汇流都用它；效果系统的持续修饰器也直接产出它 |
| **标签系统（GameplayTag System）** | UE GameplayTag 范式的层级标签：`GameplayTag`（`Status.Debuff.Mental`，持有后代即匹配祖先）、配置容器 `GameplayTagContainer`、运行时计数容器 `GameplayTagCountContainer`、标签要求 `GameplayTagRequirements`、咨询性注册表 + 标签表资产 + 编辑器标签树下拉；条件桥 `Condition.HasGameplayTag` / `Condition.GameplayTags` |
| **条件系统（Condition System）** | 数据驱动的两级 AND/OR 条件：声明一个 `ConditionExpression` 字段即在 Inspector 内联配置；上层实现 `[ConditionEvaluator]` 判定器被自动发现，并可直接复用现成的比较符范式与判定上下文。引擎无关 Core 可上服务端；**1.12.0 起**附带所有上层系统共用的条件库 `ConditionDatabase`（条件条目 = 显示名 / 描述 / 图标 + 模板驱动的自定义属性 + 表达式）与 Condition Editor，条件可按 id 跨系统引用 |
| **效果系统（Effect System）** | UE5 GAS `GameplayEffect` 范式的完整效果系统：`EffectDefinition`（时长策略 / 周期 / 叠加 / 标签 / 施加条件与概率 / 修饰器 / 各阶段执行 / 线索）+ 运行时 `EffectContainer`（施加管线、Tick、抑制、免疫、按标签移除、汇流、存档）；执行层仍是数据驱动的阶段组 + 每项可选条件门控，上层实现 `[EffectExecutor]` 执行器被自动发现。引擎无关 Core；**1.10.0 起**附带所有上层系统共用的效果库 `EffectDatabase`（效果条目 = 显示名 / 描述 / 图标 + 模板驱动的自定义属性 + GAS 定义；效果模板 / Gameplay 标签 / 枚举）与 Effect Editor，上层系统只保留效果 id 引用列表 + 跳转 |
| **编辑器框架** | 三列布局页签基类、数据库窗口外壳基类、主列表面板、实体列表面板、工具窗口基类，均对数据库类型泛型化 |
| **编辑器多语言** | 中 / English / 日本語 三语服务，以中文原文为键，缺译文自动回退 |
| **可选依赖支持层** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）的宏开关与适配 |
| **编辑器入口与全局设置** | Ale Toolkit 欢迎窗口（`Tools > Ale Toolkit > Welcome`）：界面语言 / 枚举翻译 / 三个可选依赖宏开关 / 向导默认字体 / 本地化字体 + 通用工具入口 + 页脚「启动时自动显示」；其中向导字体等项目级设定存入 `ProjectSettings/AleToolkitSettings.asset`（随仓库入库、按 GUID 引用资源），语言 / 自动显示为每人偏好（EditorPrefs）；宏只由欢迎窗口显式开关，插件不会自动改写 PlayerSettings |
| **通用工具窗口** | 对任意数据资产（`ScriptableObject`）遍历其全部 `AttributeValue` 批量处理：Addressable 迁移（Object ↔ GUID）与本地化 Key 生成，挂 `Tools > Ale Toolkit`，供上层插件复用 |

> 上述模块已全部落位——1.1.0 起 TMP / Localization / Addressables 三个可选依赖支持层齐备、纯 toolkit 环境界面亦具三语；**1.2.0 起接管项目级全局设定（语言 / 宏）并提供可对任意数据资产工作的通用工具窗口**；**1.3.0 起新增通用对象池（GameObject 预制体池 + 纯 C# 类池）与轻量中央 Tween**；**1.4.0 起新增属性修饰器求值、数据库窗口外壳基类，以及两个独立子系统——条件系统（`Ale.Condition`）与效果系统（`Ale.Effect`）**；**1.5.0 起新增轻量展示文本值 `TextValue`（fallback + 可选原生本地化，`AttributeValue` 的 `Text` 类型的独立轻量版）**；**1.5.1 起为虚拟滚动列表新增通用单元格淡入淡出（`UiwListFadeCell` + `IUiwRecycleFadeCell` / `IUiwDiffCell`，`UiwVirtualListBase` 默认 hook 驱动）与 `ToolkitTween.FadeGraphic`**；**1.6.0 起中央 Tween 补齐 `SpriteRenderer` 淡入淡出、`Graphic` 整色过渡、`Transform` 位移 / 旋转 / 缩放、延时回调与「按目标 Kill」，可整体承接 DOTween 的常用单 tween 用法（仍不含 Sequence）**；**1.8.0 起条件系统把三样「每个宿主都得自己写一遍」的设施收进 Core——比较符范式 `ConditionCompare`、通用判定上下文 `ConditionContext` / `SubjectConditionContext`、幂等的 `ConditionRegistry.EnsureAutoRegistered()`**；**1.9.0 起新增层级标签系统（`Ale.GameplayTags`），效果系统补齐 GAS `GameplayEffect` 全貌（`EffectDefinition` + `EffectContainer`），属性修饰器抽成引擎无关的 `Ale.Modifier.Core`（⚠️ 命名空间改为 `Ale.Modifier`）**；**1.10.0 起新增所有上层系统共用的效果库 `EffectDatabase` 与 Effect Editor（效果条目带显示名 / 描述 / 图标与模板驱动的自定义属性；上层只保留效果 id 引用列表 + 跳转；⚠️ `Ale.Effect.Runtime` 新增依赖 `Ale.Toolkit.Runtime` / `Ale.GameplayTags.Runtime`）**；**1.11.0 起 Effect Editor 增设「Effect Executors」页——工程内全部 `[EffectExecutor]` 实现的目录（搜索 / 分类 / 参数 schema / 跳转源码 / 静默失效体检 / 配置引用交叉核对），原页签改名 Effect Database 并移至第二位；编辑器窗口外壳新增「本页签是否需要数据库」钩子与页签记忆**；**1.12.0 起条件系统补齐到与效果系统对称——新增共用条件库 `ConditionDatabase`（条件可按 id 跨系统引用，解析不到时 fail-closed）与两页签 Condition Editor（Condition Evaluators 看实现 / Condition Database 配数据），条件参数新增候选来源注入点 `ConditionDrawerHooks`；⚠️ `Ale.Condition.Runtime` 新增依赖 `Ale.Toolkit.Runtime`**。完整变更见 [CHANGELOG](CHANGELOG.md)。

---

## 程序集

| Assembly Definition | 说明 | 宏门控 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性系统、排序、资源加载抽象、通用序列化、对象池、中央 Tween | — |
| `Ale.Toolkit.UI` | 虚拟滚动列表与通用 UI 控件 | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 适配组件 | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables 资源加载与句柄管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | 编辑器框架、数据库窗口外壳基类、属性绘制器、多语言服务、宏开关 | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables 编辑器工具 | `ATK_ADDRESSABLE` |
| `Ale.Modifier.Core` | 属性修饰器 · `ModifierDefinition` / `ModifierStackEvaluator` / 三枚举（`noEngineReferences`；1.9.0 自 `Ale.Toolkit.Runtime` 抽出，命名空间 `Ale.Modifier`） | — |
| `Ale.GameplayTags.Core` | 标签系统 · 引擎无关模型：标签 / 容器 / 计数容器 / 要求 / 注册表（`noEngineReferences`，无引用） | — |
| `Ale.GameplayTags.Condition` | 标签系统 · 条件桥：内置判定器 `Condition.HasGameplayTag` / `Condition.GameplayTags` | 引用 `Ale.Condition.Core` + `Ale.GameplayTags.Core` |
| `Ale.GameplayTags.Runtime` | 标签系统 · Unity 桥（`GameplayTagTable` 资产 + 启动登记 + `[GameplayTagField]`） | — |
| `Ale.GameplayTags.Editor` | 标签系统 · 目录 / 标签树下拉 / 容器与要求绘制器 / 表 Inspector / 欢迎窗口 | — |
| `Ale.Condition.Core` | 条件系统 · 引擎无关模型 / 判定引擎 / 注册与反射发现 / JSON，以及供宿主复用的比较符范式 `ConditionCompare` 与通用判定上下文 `ConditionContext`（`noEngineReferences`，可上服务端） | 引用 Newtonsoft |
| `Ale.Condition.Runtime` | 条件系统 · Unity 桥（`ConditionAsset` + 启动自动注册）与共用条件库 `ConditionDatabase` / 数据管理器 / 配置序列化（1.12.0 起） | 1.12.0 起引用 `Ale.Toolkit.Runtime` |
| `Ale.Condition.Editor` | 条件系统 · 内联绘制器 / 目录 / 欢迎窗口 / 参数候选注入点，以及 Condition Editor 两页签（1.12.0 起） | 1.12.0 起引用 `Ale.Toolkit.Runtime` + `Ale.Toolkit.Editor` |
| `Ale.Effect.Core` | 效果系统 · 引擎无关模型（`EffectDefinition` / `EffectExpression`）/ 运行时容器 `EffectContainer` / 执行运行器 / 注册与反射发现 / JSON（`noEngineReferences`） | 引用 `Ale.Condition.Core` + `Ale.GameplayTags.Core` + `Ale.Modifier.Core` + Newtonsoft |
| `Ale.Effect.Runtime` | 效果系统 · Unity 桥：共用效果库 `EffectDatabase`（效果条目 / 模板 / Gameplay 标签 / 枚举）+ `EffectDataManager` + `EffectConfigSerializer`（JSON / 二进制）、`EffectAsset` / `EffectDefinitionAsset`、启动引导（清空 + 加法） | 引用 `Ale.Toolkit.Runtime` + `Ale.GameplayTags.Runtime`（1.10.0 起） |
| `Ale.Effect.Editor` | 效果系统 · Effect Editor（效果 / 模板 / 标签 / 枚举）、效果目录 `EffectEditorCatalog`、宿主用引用列表绘制器 `EditorEffectRefListDrawer`、定义 / 幅度 / 修饰器 / 表达式绘制器、属性 id 目录 provider、欢迎窗口 | 引用 `Ale.Toolkit.Editor` + `Ale.GameplayTags.Editor`（1.10.0 起） |

依赖方向单向：宿主插件 → `Ale.Toolkit.*` / `Ale.Modifier.*` / `Ale.GameplayTags.*` / `Ale.Condition.*` / `Ale.Effect.*`，本包不反向引用任何宿主插件。各子系统命名空间独立（`Ale.Modifier` / `Ale.GameplayTags` / `Ale.Condition` / `Ale.Effect`）；`Ale.Modifier.Core`、`Ale.GameplayTags.Core`、`Ale.Condition.Core` 三者互不引用，`Ale.GameplayTags.Condition` 与 `Ale.Effect.Core` 是汇合点（层次：标签 < 条件 < 效果）。1.10.0 起 `Ale.Effect.Runtime` 引用 `Ale.Toolkit.Runtime`（效果库需要属性系统）与 `Ale.GameplayTags.Runtime`，`Ale.Effect.Editor` 引用 `Ale.Toolkit.Editor` / `Ale.GameplayTags.Editor`；`Ale.Toolkit.*` 不反向引用任何子系统，各 `*.Core` 仍引擎无关、无环。

---

## 用法与主要 API

> 运行时类型位于 `Ale.Toolkit.Runtime` / `Ale.Toolkit.Runtime.UI`，编辑器类型位于 `Ale.Toolkit.Editor`。以下按模块给出典型用法与主要入口；完整签名以源码 XML 注释为准。

### 属性系统

以 `AttributeValue` 承载「带类型的一个值」（标量存 `[0]`，数组存 `[0..n]`），类型由 `EFieldType` 决定（Int / Float / String / Bool / Enum / Vector2~4 / Color / Sprite / Text / Prefab / AudioClip / StringIntPair / EnumIntPair 等 24 种）。字段的 schema 由 `AttributeDefinition` 描述，实体经 `AttributeOwner` 按字段 id 取值。

```csharp
var v = new AttributeValue(EFieldType.Int);
v.SetInt(0, 10);
int hp      = v.GetInt(0);
string show = v.ToDisplayString();     // 展示串（数组用分隔符连接）
double key  = v.ToComparableNumber();  // 排序用数值

// 实体（AttributeOwner）按字段 id 取值
AttributeValue atk = owner.GetAttributeValue("attack");
```

- `AttributeValue`：`Type` / `IsArray` / `Count`；读写 `GetInt/SetInt`、`GetFloat/SetFloat`、`GetString/SetString`、`GetObject/SetObject`、`GetColor/SetColor`、`GetVector2~4`、`GetTextValue/SetTextValue/ResolveText`、`SetStringIntPair/SetEnumIntPair`；数组 `AddElement/RemoveElement/ReorderElements`；`ToDisplayString()`、`ToComparableNumber()`、`ChangeType()`、`Clone()`。
- `AttributeDefinition.CreateValue()` 按定义造值；`AttributeOwner.GetEntry(id)` / `GetAttributeValue(id)`；`AttributeSync.Sync(...)` 按 schema 同步实体属性值集合。
- `ConfigTemplateBase`（`name` / `color` / `List<AttributeDefinition> attributes`）；`EnumType`（`AddItem` / `GetItemByValue` / `GetDisplayName`）+ `EnumItem`；`NumberFormatConfig.Format(long, langCode)` 数字格式化。
- **`TextValue`**（轻量展示文本，`AttributeValue` 的 `Text` 类型的独立轻量版）：`Fallback`（始终存在）+ 启用 `ATK_LOCALIZATION` 时内嵌 Unity 原生 `LocalizedString`（`Localized`）；`ResolveText()` 本地化优先、取不到回退 fallback；`IsEmpty` / `Clone()`。每实例仅一个 string（+ 本地化时一个 `LocalizedString`），无 `AttributeValue` 的多类型后备列表开销。编辑器 `TextValueDrawer`（`[CustomPropertyDrawer(typeof(TextValue))]`）画「fallback 行 + 原生表/条目选择器」，声明字段即在 Inspector 配置、选择即正确保存。

### 排序

宿主为自己的数据类型实现一次 `ISortContext<TData>`（或继承 `SortContextBase<TData>` / `TagSortContextBase<TData>`），即可复用与领域无关的 `AttributeSortService`：逐条 `SortPriority`（字段 + 升降序）取可比较值比较，相等落下一条。

```csharp
class MySortCtx : SortContextBase<MyData> { /* 覆写 OwnerOf / FindDefinition / OptionOf / TryCompareSpecial */ }

// 按优先级列表原地排序
AttributeSortService.Sort(list, priorities, new MySortCtx());
int cmp = AttributeSortService.Compare(a, b, priorities, ctx);
```

- `AttributeSortService.Sort<TData>(list, priorities, ctx)` / `Compare(...)` / `CompareByField(...)`。
- `ISortContext<TData>`：`OwnerOf` / `FindDefinition` / `OptionOf` / `TryCompareSpecial`。
- `SortPriority`（字段 + 升降序）、`SortOption`（每字段忽略列表）、`SortFieldKeys`、`ISortId`、`SortOptionSync`。

### UI

`Ale.Toolkit.Runtime.UI` 下的运行时控件，均为泛型 / 可复用组件。

- **虚拟滚动列表** `UiwVirtualGridList<TData,TCell>`（网格）/ `UiwVirtualOrderList<TData,TCell>`（顺序）：继承并实现 `BindCell` / `ClearCell`，Inspector 绑定 `cellPrefab` / `scrollRect` / `content`，喂入数据即只渲染可见区、逐帧限速生成。主要方法：`SetItems` / `UpdateItems` / `RefreshItemsData` / `SetSourceItems`、`ConfigureFilter` / `SetExtraFilter`、`ConfigureSort`、`ScrollToStart`。顺序列表另有 `rowPitchScale`（行距倍率：行距 = 格子高度 × 倍率，默认 1.0 即逐行紧贴），以及带「焦点条目」语义的子类 `UiwFocusOrderList<TData,TCell>`（滚到哪儿就选中哪儿，`OnFocusChanged` / `FocusIndex`，可配焦点停靠位置与随焦点距离变化的缩放 / 横向偏移曲线）。
- **页签条** `UiwTabStrip<TTab,TValue>`（纯 C#）：`Configure(prefab, container, bind, onSelect)` → `SetTabs(values, labels, …)` → `Select` / `SelectValue`；差异复用不整排重建。过滤页签栏 `UiwFilterTabBar`（MonoBehaviour）：`SetFilters(tagNames)` / `Clear`。
- **悬停弹窗** `UiwTooltipBase<TPayload>`：子类实现 `ApplyContent` / `ClearContent` 并暴露自己的 `Show`（内部转 `ShowTooltip`）；`Hide()`。
- **子项实例池** `UiwWidgetPool<T>`（游标式复用）：`Configure` → `Begin` → `Next(out created)` → `End`。
- 其它：`UiwViewBase`（`Open`/`Close`/`ToggleOpenClose`；`IsOpen` 状态 + `Start` 时按 `activeInHierarchy` 自打开，子类覆写 `Start` 须在末尾调 `base.Start()`）、`UiwSortToolbar`（`SetOptions`/`SetSortPriorities`）、`UiwNumberCounter`（`Configure`/`SetRange`/`SetValue`）、`UiwTextLabel`、`SpriteSlot.Bind(image, value)`。

### 对象池

替代 Lean.Pool 一类第三方池。GameObject 预制体池 + 纯 C# 类池两套（`Ale.Toolkit.Runtime`）。

```csharp
// 静态门面：按预制体自动建池，可直接替换 Instantiate / Destroy
var go = ToolkitPool.Spawn(prefab, pos, Quaternion.identity, parent);
ToolkitPool.Despawn(go);            // 按归属登记表归还，支持 Despawn(go, delay)

// 或显式持有池组件
var pool = host.AddComponent<ToolkitGameObjectPool>();
pool.Prefab = prefab; pool.Preload = 3;
var clone = pool.Spawn(pos, rot, parent);

// 纯 C# 对象降 GC（池空返回 null）
var ctx = ToolkitClassPool<Ctx>.Spawn() ?? new Ctx();
ToolkitClassPool<Ctx>.Despawn(ctx, c => c.Reset());
```

- `ToolkitGameObjectPool`：`Prefab` / `Preload` / `Capacity` / `Recycle` / `Persist` / `Notification`；`Spawn(...)` / `Despawn(clone, delay)` / `DespawnAll` / `Clear`。
- `IPoolable`（`OnSpawn` / `OnDespawn`）；`ToolkitPool.Spawn/Despawn/DespawnAll/Detach`、登记表 `Links`；`ToolkitClassPool<T>.Spawn(...)/Despawn(...)`。

### Tween（中央缓动）

轻量中央 Tween 门面（DOTween 式「单 Update 轮询作业表」，`Ale.Toolkit.Runtime`）。提供 `CanvasGroup` / `Graphic`（Image / 文本）/ `SpriteRenderer` 的 alpha 淡入淡出、`Graphic` 的整色过渡、`Transform` 的位移 / 旋转 / 缩放，以及纯延时回调。作业经 `ToolkitClassPool` 池化、由常驻 runner 单 `LateUpdate` 推进，近零 GC。不复刻 DOTween 的 Sequence / 链式 / 全套 Ease，按需增量扩展。

```csharp
// 对 CanvasGroup 淡入到 alpha=1，0.2s；返回可打断的句柄
var h = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, 0.2f, EToolkitEase.OutQuad,
                                     unscaled: true, onComplete: () => { /* 完成 */ });
h.Kill(complete: true);    // 打断并瞬置到终值 + 触发完成回调；Kill(false) 打断且不回调
h.Complete();              // 同 Kill(true)
bool running = h.IsActive; // 是否仍在进行

// 2D 精灵淡入 / 角色平滑移动 / 延时回调
ToolkitTween.Kill(spriteRenderer);                       // 先打断该目标上的在途作业（本门面不做覆盖管理）
ToolkitTween.FadeSpriteRenderer(spriteRenderer, 1f, 0.3f);
ToolkitTween.MoveTransform(actor, targetPos, duration, EToolkitEase.InOutQuad);
ToolkitTween.Kill(actor, complete: true);                // 等价 DOTween 的 transform.DOComplete()
var delay = ToolkitTween.DelayedCall(1.5f, () => Play(), owner: this);
```

- 淡入淡出 / 颜色：`FadeCanvasGroup(target, endAlpha, duration, ease = OutQuad, unscaled = true, onComplete = null)`、`FadeGraphic(…)`、`FadeSpriteRenderer(…)`、`TintGraphic(target, endColor, …)`（整色 RGBA）。
- Transform：`MoveTransform(target, endPosition, …)`、`RotateTransform(target, endEulerAngles, …)`、`ScaleTransform(target, endScale, …)`。旋转**逐轴走最短弧**（等价 DOTween 的 `RotateMode.Fast`，不支持多圈），折算公式另经 `ShortestEuler(fromEuler, toEuler)` 公开。
- 延时：`DelayedCall(delay, onComplete, unscaled = true, owner = null)`。可选 `owner` 绑定生命周期：被 `Destroy` 后回调丢弃，且可经 `Kill(owner)` 取消。
- 打断：`Kill(target, complete = false)` 打断该目标上全部在途作业并返回打断数，即 DOTween 目标登记表的等价物（`DOKill` / `DOComplete`）。按**引用相等**匹配，故已被 `Destroy` 的目标仍能清理自己的作业；`Kill(gameObject)` 找不到挂在其上的组件的作业。
- `ToolkitTweenHandle`（值类型，零分配）：`IsActive` / `Kill(complete = false)` / `Complete()`；实现 `IEquatable<>` 与 `==`，可直接进 `List<>` 并 `Remove`。`default` 为无效句柄，其 `Kill` / `Complete` 为安全空操作。
- `ToolkitEase.Evaluate(EToolkitEase ease, float t)`；缓动类型 `EToolkitEase`：`Linear` / `InQuad` / `OutQuad` / `InOutQuad`。
- 所有入口在 `duration ≤ 0` 或目标为空时立即到位并返回空句柄。

三条与 DOTween 的差异值得留意：**①不做覆盖管理**——同目标同通道再起一个 tween 不会自动打断前一个（DOTween 亦然），需先 `Kill(target)`；**②`unscaled` 默认 `true`**，而 DOTween 默认受 `Time.timeScale` 影响，要还原 DOTween 行为请显式传 `unscaled: false`；**③`DelayedCall(delay ≤ 0)` 同步立刻触发**（DOTween 推迟一帧），若要把句柄记进列表请用 `if (h.IsActive) list.Add(h);` 守卫。

### 属性修饰器

GAS 式修饰器求值（程序集 `Ale.Modifier.Core`，命名空间 `Ale.Modifier`；**1.9.0 前位于 `Ale.Toolkit.Runtime`**——升级后请在 asmdef 加引用并改 `using Ale.Modifier;`，已存数据不受影响）。声明式 `ModifierDefinition` 汇入一个属性，`ModifierStackEvaluator` 按固定顺序分组结算出「当前值 + 逐来源明细」。静态、无状态、无 Unity 依赖；**不含**时长到期 / 叠层的运行时循环——那是[效果系统](#效果系统--effect-system)的事：持续效果的 `EffectContainer.CollectModifiers` 产出的就是本类型，宿主把它与自己的其它来源一起喂进求值器。

```csharp
var mods = new List<ModifierDefinition> {
    new ModifierDefinition("atk", EModifierOperation.Add,        5f,   "trait:勇敢"),
    new ModifierDefinition("atk", EModifierOperation.PercentAdd, 0.1f, "buff:狂暴"),
};
// base 10，clamp[0,100]；结算：10 → +5 → ×(1+0.1) = 16.5
ModifierEvaluation r = ModifierStackEvaluator.Evaluate(10f, 0f, 100f, mods);
float now = r.Value;                              // 16.5
foreach (var c in r.Breakdown)                    // 逐来源：SourceTag / Operation / Magnitude / Delta
    Debug.Log($"{c.SourceTag} {c.Operation} {c.Delta}");
```

- `ModifierDefinition`：`targetAttributeId`（不透明键，求值器不解释）/ `operation` / `magnitude` / `sourceTag`（来源明细 + 分组撤销）；另有四个仅承载配置的惰性字段 `duration` / `durationDays` / `stackLimit` / `stackRule`——求值器与效果系统均不读取，时长 / 周期 / 叠加由 `EffectDefinition` 承载。
- `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown = true)` → `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`；轻量 `EvaluateValue(...)` 只出最终值。结算顺序固定：`base → +ΣAdd → ×(1+ΣPercentAdd) → 逐项 ×(1+magnitude) Multiply → 末位 Override 覆盖 → clamp[min,max]`。调用方需先按 `targetAttributeId` 分组；时长 / 叠层由运行时结算后再传入。
- 枚举：`EModifierOperation`（`Add`/`PercentAdd`/`Multiply`/`Override`）、`EModifierDuration`（`Instant`/`Timed`/`Permanent`）、`EStackRule`（`Refresh`/`Add`/`EveryXStacks`/`OnMaxStacks`）。

### 标签系统 · GameplayTag System

UE GameplayTag 范式的层级标签（命名空间 `Ale.GameplayTags`，程序集 `Ale.GameplayTags.Core` / `.Condition` / `.Runtime` / `.Editor`）。点分名表达层级：`Status.Debuff.Mental` 匹配 `Status.Debuff` 与 `Status`（**持有后代即匹配祖先**），纯前缀不算（`AB` 不匹配 `A`）。序数比较、大小写敏感——与 toolkit 其它字符串键一致；归一规则（整体与逐段 Trim、空段 / 段内空白 / `/` 非法）是数据格式，发布后冻结并有测试钉住。

```csharp
using Ale.GameplayTags;

// 配置侧：容器存 List<string>，Unity / Newtonsoft 直接往返；[GameplayTagField] 让 string 字段得到标签树下拉
public GameplayTagContainer assetTags = new GameplayTagContainer();
[GameplayTagField] public string cueTag;

// 运行时：拥有者的计数容器（显式计数 + 隐式祖先计数，O(1) 层级查询）
var owned = new GameplayTagCountContainer();
owned.AddTag(new GameplayTag("Status.Debuff.Mental"));
bool mental = owned.HasMatchingTag(new GameplayTag("Status.Debuff"));   // true：持有后代
owned.OnTagCountChanged += (tag, count) => { /* 效果容器据此重评抑制 */ };

// 要求：必须持有全部 requireTags 且不持有任一 ignoreTags
var req = new GameplayTagRequirements();
req.requireTags.AddTag("State.Alive");
req.ignoreTags.AddTag("Immunity.Mental");
bool ok = req.IsMet(owned);
```

- `GameplayTag`（只读结构体，**不参与序列化**）：`IsValid` / `Depth` / `Parent` / `Root` / `Leaf`、`MatchesTag(parent)` / `MatchesTagExact` / `IsDescendantOf`、`Normalize` / `TryParse` / `Parse`。
- `GameplayTagContainer`（`[Serializable]`，唯一字段 `List<string> tags`）：`AddTag` / `RemoveTag`（精确）/ `RemoveTagsMatching`（子树）、`HasTag`（层级）/ `HasTagExact` / `HasAny` / `HasAll`（空集：All 为 true、Any 为 false）/ `Filter`、`Normalize` / `Clone`。
- `GameplayTagCountContainer`（运行时）：`AddTag/RemoveTag(tag, count)`、`AddTags/RemoveTags(container)`、`HasMatchingTag` / `HasExactTag` / `GetTagCount`、`GetExplicitTags`、事件 `OnTagCountChanged`。
- `GameplayTagRequirements`：`requireTags` / `ignoreTags`、`IsMet(...)`、`Validate`（require ∩ ignore 报错）。
- **注册表是咨询性的**：`GameplayTagRegistry.Default`（登记自动补祖先；`Validate` 抓未登记 / 仅大小写不同的手误）只服务编辑器下拉与配置期校验，**运行时匹配永不查注册表**，未登记标签照常匹配。来源：`Resources` 下的 `GameplayTagTable` 资产（`Create > Ale > GameplayTag > Gameplay Tag Table`，启动时自动登记）、宿主经 `GameplayTagRuntime.Register(...)` 显式登记（如数据库里的自定义标签）。
- **条件桥**（`Ale.GameplayTags.Condition`）：内置判定器 `Condition.HasGameplayTag(tag, exact)`、`Condition.GameplayTags(tags[], 任一 / 全部 / 皆无, exact)`，主体标签经上下文的 `IGameplayTagSource` 服务或 `Subject as IGameplayTagOwner` 解析。**不另造 TagQuery**——与或非组合交给 `ConditionExpression`。桥放在独立程序集而不让 `Ale.Condition.Core` 引用标签，保住其「零引用」承诺。
- 编辑器：`GameplayTagContainer` / `GameplayTagRequirements` / `[GameplayTagField]` 三个绘制器（标签树下拉、非法红底、未登记黄底）、标签表 Inspector（校验 / 排序）、`Tools > Ale Toolkit > GameplayTag System > Welcome`。
- 命名提示：命名空间取复数 `Ale.GameplayTags`——若叫 `Ale.GameplayTag`，`namespace Ale.*` 下写 `GameplayTag t` 会先命中命名空间（CS0118）；特性叫 `[GameplayTagField]` 以免与类型二义（CS1614）。

### 条件系统 · Condition System

数据驱动的两级 AND/OR 条件（命名空间 `Ale.Condition`）。核心思想：**声明一个 `ConditionExpression` 字段，Inspector 里就地出现两级条件编辑器**；上层系统实现自己的「原子判定器」被自动发现，核心不认识任何领域概念。引擎无关 Core（`noEngineReferences`）可上服务端。三个程序集：`Ale.Condition.Core`（模型 + 引擎 + 注册 + JSON）/ `.Runtime`（Unity 桥）/ `.Editor`（内联绘制器 + 目录 + 欢迎窗口）。

**① 声明条件字段（配置侧，零 UI 代码）**

```csharp
// 任意 MonoBehaviour / ScriptableObject / 可序列化配置类
public ConditionExpression eligibility = new ConditionExpression();
```

在自定义 Inspector 里对其 `SerializedProperty` 调 `EditorGUILayout.PropertyField(prop, true)`，即得完整两级 AND/OR 编辑器（组 / 项 / 参数增删、And·Or、NOT、判定器分类下拉、按 schema 动态参数区），Undo 自动。或用 SO 容器 `ConditionAsset`（`Create > Ale > Condition > Condition Asset`）。

**② 扩展判定器（上层实现）**

```csharp
using Ale.Condition;

public interface IMyStatSource { float Get(string statId); }   // 上层自定义读侧服务（引擎无关）

[ConditionEvaluator("My.StatAtLeast")]
public sealed class StatAtLeastEvaluator : IConditionEvaluator
{
    private static readonly ConditionParamDef[] Schema = {
        new ConditionParamDef("stat",  ConditionParamType.String, false, "属性"),
        new ConditionParamDef("value", ConditionParamType.Float,  false, "阈值"),
    };
    public string Key => "My.StatAtLeast";
    public string DisplayName => "属性达标";
    public string Category => "My";                            // 编辑器下拉分组
    public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

    public bool Evaluate(IReadOnlyList<ConditionParam> ps, IConditionContext ctx)
    {
        var src = ctx?.GetService<IMyStatSource>();
        if (src == null) return false;
        string stat = ps.Find("stat")?.GetString();
        float  need = (float)(ps.Find("value")?.GetFloat() ?? 0);
        return !string.IsNullOrEmpty(stat) && src.Get(stat) >= need;
    }
}
```

`ParamSchema` 驱动编辑器动态参数区；参数 5 型：`String` / `Int` / `Float` / `Bool` / `Enum`（+ `isArray`）。固定选项用 `ConditionParamDef` 的 `choices`（渲染为下拉、存索引）——**比较符直接用现成的 `ConditionCompare`**，不必自己声明一份标签数组：

```csharp
ConditionCompare.CreateOpParam()                       // 等价于带 choices = ConditionCompare.Labels 的 "op" 参数
int  op = ConditionCompare.ReadOp(ps);                 // 缺省「大于等于」
bool ok = ConditionCompare.Compare(cur, need, op);     // 整数精确；浮点重载可传 epsilon（默认 1e-6）
```

> ⚠️ `ConditionCompare.Labels` 的**文本与顺序是通信格式**：索引会序列化进条件资产，标签本身还可能以字符串形式进入使用方的脚本（如经 VN Framework 桥接后的对话条件）。**不要本地化、不要调序。**

**③ 提供上下文 + 求值（运行时）**

```csharp
var ctx = new ConditionContext();                           // 通用上下文：按类型登记领域服务
ctx.RegisterService<IMyStatSource>(myStatSource);

bool ok = expr.Evaluate(ctx).Passed;                        // 便捷法
ConditionResult r = ConditionEngine.Evaluate(expr, ctx);    // 或直接调引擎；r.FailedKeys 列未满足键

// 「判定这个对象是否满足条件」——包一层，不改动共享上下文的 Subject
bool okForHero = expr.Evaluate(new SubjectConditionContext(ctx, hero)).Passed;
```

`ConditionContext.GetService<T>()` 按 `typeof(T)` **精确**查表（按接口登记的只能按接口取用），且刻意不做 `virtual`——它在求值热路径上。需要自定义解析策略（如「自定义优先、内置回落」分层）的宿主**直接实现 `IConditionContext` 即可**，不必继承。toolkit 也**刻意不提供全局默认实例**：「哪个是默认上下文」是宿主的策略，各宿主自行持有一个静态实例即可。

运行时 `ConditionRuntime` 于 `[RuntimeInitializeOnLoadMethod]` 把 `ConditionRegistry.Default` 反射填满并接缺键告警；服务端 / 测试可手动 `new ConditionRegistry()` + `AutoRegisterFromAssemblies()` 或逐个 `Register`。**编辑器工具在非播放态求值**时注册表是空的（那时 `ConditionRuntime` 还没跑），用幂等的 `ConditionRegistry.Default.EnsureAutoRegistered()` 兜一次即可。

**内置判定器**：`Condition.AlwaysTrue`、`Condition.HasFlag`（`IConditionFlagSource`）、`Condition.NumberCompare`（`IConditionNumberSource`）。**JSON**：`ConditionJson.ToJson(expr)` / `FromJson(str)`（Newtonsoft；模型纯 POCO，可换序列化器、可入库存档）。**总览**：`Tools > Ale Toolkit > Condition System > Welcome`。

**④ 条件库与 Condition Editor（1.12.0 起）**

条件除了内联在宿主字段里，也可以配成**有 id 的具名条目**由多个系统共用——同一句「力量 ≥ 10 且拥有勇敢特质」不必在特质 / 职业 / 头衔 / 技能树里各配一遍。载体是 `ConditionDatabase`（`Create > Ale > Condition > Condition Database`）：三个顶层列表——**条件条目** `ConditionEntry`（`id` + 显示名 / 描述 / 图标三个 `AttributeValue` + 按模板 schema 的自定义属性 `values` + 内嵌 `expression`；显示字段可直接喂给「未满足时」的玩家提示 UI）、**条件模板** `ConditionTemplate`（`name` / `color` / 自定义属性 schema + `defaultExpression`，从模板创建时深拷贝作预设）、**枚举类型**。本库不持有 Gameplay 标签——标签归效果库统一声明。

```csharp
// 运行时：放在 Resources 下随启动自动登记（ConditionRuntime.AutoLoadFromResources，默认 true），或显式登记
ConditionDataManager.Instance.Register(conditionDatabase);   // 幂等；NormalizeAll + 并入全局具名条件注册表
bool ok = ConditionResolver.IsSatisfied("knight_ready", ctx); // 解析：上下文条件源 → 回落源 → ConditionDefinitionRegistry.Default
ConditionEntry e = ConditionDataManager.Instance.GetEntry("knight_ready");
string why = e.descriptionText.ResolveText();                 // 未满足时给玩家看的那句话
```

- ⚠️ **解析不到即判否（fail-closed）**：`ConditionResolver.Evaluate` 找不到 id 时返回不通过、把该 id 放进 `FailedKeys` 并告警。门控场景下「id 写错」应当锁住内容而不是放行，与 `ConditionEngine` 对未注册判定器键的处理一致。
- 引导：`[SubsystemRegistration]` 清空注册表 → `[BeforeSceneLoad]` 只做加法（判定器 + `Resources`），宿主在 Awake 里 `Register` 的库稳定存活。
- **Condition Editor**（`Tools > Ale Toolkit > Condition System > Condition Editor`，或资产 Inspector 的「在 Condition Editor 中编辑」）有两个页签，与 Effect Editor 结构对称：
  - **Condition Evaluators**（第一页；内容来自**代码**，没有条件库也能用）：工程内全部 `IConditionEvaluator` 实现的目录，按分类分组、可按 键 / 显示名 / 类型全名 / 程序集 搜索、可「只看有问题」。右列给出实现类型 / 程序集 / 源码路径与「打开脚本」（定位到类声明行）、「在 Project 中定位」、「复制 Key」（双击行等同打开脚本），参数 schema，被哪些配置引用（点「跳转」定位过去），以及**静默失效体检**——漏打 `[ConditionEvaluator]`、特性里的键与 `Key` 属性不一致、重复键、空键、缺公开无参构造 / 实例化失败（抽象基类只作说明、不算问题）。顶部横幅列出「配置引用了但没有实现」的悬空键。引用来源覆盖条件库、`ConditionAsset`，以及**效果库里每条效果的施加条件与执行项门控**（由效果侧经 `ConditionEvaluatorIndex.RegisterUsageProvider` 贡献，保持依赖方向不反转）。
  - **Condition Database**（第二页）：左列 条件模板 / 枚举类型，中列条件列表（模板过滤 / 搜索 / 从模板添加 / 快速添加），右列 ID 查重 + 名称 / 描述 / 图标 + 自定义属性 + 内联表达式 + 校验摘要（会当场报「未选择判定器」与「判定器 X 没有对应实现」）；查重阻断导出，导出 JSON / 二进制。`ConditionEditorWindow.Open(db, conditionId)` 定位到指定条件，`OpenEvaluators()` 切到第一页。
- **宿主接入**（Inspector 一行代码）：`EditorConditionRefListDrawer.Draw(ctx, skill.unlockConditionRefs, drag, "解锁条件", "条件")`——目录菜单（按库分组）、拖拽重排、「打开」跳转、「未找到」标注（不阻断）、自由输入。
- **宿主接上引用核对**：条件继续内联在宿主字段里也没关系，写一个用法提供者即可让它们进 Condition Evaluators 页的「被引用 / 悬空键 / 实现体检」——`[InitializeOnLoad]` 里 `ConditionEvaluatorIndex.RegisterUsageProvider(Provide)`，`Provide` 用 `ConditionUsageCollector.ForEachAsset<T>` 遍历自己的资产、`ConditionUsageCollector.Collect(into, expr, asset, ownerId, "获得条件", jump)` 逐处收集（位置文案由助手统一格式化）。`jump` 由宿主给出（跳回自己的编辑器窗口并定位），于是条件系统不必认识宿主。toolkit 自己的 `EffectConditionUsageProvider` 就是这么写的。
- **参数候选注入**：判定器在 schema 里用 `catalogRef` 声明「这个字符串参数装的是哪一类 id」（如 `new ConditionParamDef("traitId", ConditionParamType.String, false, "特质ID", null, null, "Chronicle.Trait")`），宿主经 `ConditionDrawerHooks.RegisterProvider(IConditionParamCatalogProvider)` 为该目录供给候选，绘制器便把裸文本框换成按系统名分组的下拉（悬空值保留并标「（未知）」）。`catalogRef` 为空或无候选时行为与 1.11.0 完全一致。

### 效果系统 · Effect System

UE5 GAS `GameplayEffect` 范式的效果系统（命名空间 `Ale.Effect`），分两层：**定义层** `EffectDefinition` + **运行时容器** `EffectContainer`（1.9.0 起，对应 GAS 的 GameplayEffect + ASC 效果部分：时长 / 周期 / 叠加 / 标签 / 免疫 / 抑制 / 修饰器 / 存档），以及**执行层** `EffectExpression`（1.4.0 起，对应 GAS 的 Executions：阶段组 + 每项可选条件门控的离散动作）。「声明字段即在 Inspector 配置」，上层实现 `[EffectExecutor]` 执行器被自动发现。三个程序集：`Ale.Effect.Core`（引用 `Ale.Condition.Core` / `Ale.GameplayTags.Core` / `Ale.Modifier.Core`，引擎无关）/ `.Runtime`（1.10.0 起承载共用效果库，引用 `Ale.Toolkit.Runtime` + `Ale.GameplayTags.Runtime`）/ `.Editor`（引用 `Ale.Toolkit.Editor` + `Ale.GameplayTags.Editor`）。先讲执行层（定义层的 `executions` 字段就是它），再讲定义与容器。

**结构**：`EffectExpression → EffectGroup(phase 时机标签) → EffectItem(key + 参数 + 可选 gate)`。同一字段里可放多个阶段组（如 `onGained` / `onLost`），组内**按序执行**，运行时按 `phase` 过滤（空 phase 组为通配，任意 phase 都执行）。

**① 声明效果字段**

```csharp
public EffectExpression onGained = new EffectExpression();   // Inspector 内联阶段组编辑器；或用 EffectAsset SO
```

**② 扩展执行器（上层实现，含「点燃」示例）**

```csharp
using Ale.Effect;

public interface ICombatEffectSink { void Ignite(float radius, int mode, int count); }

[EffectExecutor("Combat.Ignite")]
public sealed class IgniteEffect : IEffectExecutor
{
    private static readonly EffectParamDef[] Schema = {
        new EffectParamDef("radius", EffectParamType.Float, false, "直径(米)"),
        new EffectParamDef("target", EffectParamType.Int,   false, "目标选择",
            choices: new[] { "随机", "最近", "最远" }),        // 固定枚举 → 下拉存索引
        new EffectParamDef("count",  EffectParamType.Int,   false, "目标数"),
    };
    public string Key => "Combat.Ignite";
    public string DisplayName => "点燃";
    public string Category => "Combat";
    public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

    public EffectResult Execute(IReadOnlyList<EffectParam> ps, IEffectContext ctx)
    {
        var sink = ctx?.GetService<ICombatEffectSink>();
        if (sink == null) return EffectResult.Failed("缺少 ICombatEffectSink");
        sink.Ignite((float)ps.Find("radius").GetFloat(),
                    (int)ps.Find("target").GetInt(),
                    (int)ps.Find("count").GetInt());
        return EffectResult.Applied;
    }
}
```

**③ 上下文 + 执行（运行时）**

```csharp
// IEffectContext : IConditionContext —— 同一上下文既供 gate 条件读服务，又供效果写 Sink
class MyEffectCtx : IEffectContext {
    public object Subject { get; set; }
    private readonly object[] _svc;
    public MyEffectCtx(params object[] svc) { _svc = svc; }
    public T GetService<T>() where T : class { foreach (var s in _svc) if (s is T t) return t; return null; }
}

var ctx = new MyEffectCtx(combatSink, myFlagSource /* 供 gate 用 */);
EffectRunReport rep = onGained.Run(ctx, phase: "onGained");     // 或 EffectRunner.Run(onGained, ctx, "onGained")
Debug.Log($"应用 {rep.Applied} / 跳过 {rep.Skipped} / 失败 {rep.Failed}");
```

每项若配了 gate（一个内嵌 `ConditionExpression`，编辑器里就地展开配置），运行器先走 `ConditionEngine` 求值，不满足即 `Skipped`。运行时 `EffectRuntime` 于 `[RuntimeInitializeOnLoadMethod]` 自动注册所有执行器。

**内置执行器**：`Effect.NoOp`、`Effect.SetFlag`（`IEffectFlagSink`）、`Effect.AdjustNumber`（`IEffectNumberSink`）——分别是条件系统 `HasFlag` / `NumberCompare` 的写侧对偶；`Effect.ApplyEffect(effectId, level)` / `Effect.RemoveEffectsWithTag(tag)` / `Effect.RemoveEffectById(effectId)`——效果组合与驱散（容器与定义从上下文解析）。**JSON**：`EffectJson.ToJson/FromJson`（表达式）与 `ToJson(EffectDefinition)/DefinitionFromJson`（内嵌 gate / 条件 / 标签随图往返）。**总览**：`Tools > Ale Toolkit > Effect System > Welcome`；**已实现执行器的完整清单见 Effect Editor 的「Effect Executors」页（1.11.0 起）**——写完一个执行器可在那里确认它是否真的被发现，并双击行跳回源码。

**④ 效果定义与容器（GAS 层）**

`EffectDefinition` 是一份可复用的「效果长什么样」：`durationPolicy`（Instant / HasDuration / Infinite）、`duration` / `period` + `executePeriodicOnApplication`、叠加（`stackingType` 按来源 / 按目标聚合、`stackLimit`、刷新时长 / 重置周期 / 到期三策略）、标签（`assetTags` / `grantedTags` / `removeEffectsWithTags` / `grantedApplicationImmunityTags`、`applicationTagRequirements` / `ongoingTagRequirements`）、`applicationCondition`（Condition）、`chanceToApply`、`modifiers`（`EffectModifier`：属性 id + 运算 + `EffectMagnitude`——Scalable / AttributeBased / SetByCaller）、`executions`（上面的 `EffectExpression`，阶段常量 `EffectPhases.OnApply / OnStack / OnPeriod / OnExpire / OnRemove`）、`cueTags`。定义的存放：**推荐直接用 1.10.0 的共用效果库 `EffectDatabase`（见 ⑤）**；宿主自建库时须把定义放在数据库**顶层列表**、以 id 引用（嵌套已达 8 层，再多包两层会触及 Unity 序列化深度上限）；纯 toolkit 用户也可用单定义资产 `EffectDefinitionAsset`。`Normalize()` 会把空阶段改写为 `onApply`（`EffectRunner` 视空 phase 为通配，否则会在每周期 / 移除时重复执行）；`Validate(errors)` 报错误与「警告:」前缀的警告。

```csharp
using Ale.Effect; using Ale.Modifier; using Ale.GameplayTags;

// 定义：30「天」的 +10 战力 Buff，按目标聚合最多 3 层，授予 Status.Buff.Might
var buff = new EffectDefinition("battle_focus", EDurationPolicy.HasDuration) {
    duration = EffectMagnitude.Scalable(30f), stackingType = EEffectStackingType.AggregateByTarget, stackLimit = 3,
};
buff.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
buff.grantedTags.AddTag("Status.Buff.Might");

// 每个拥有者一个容器；宿主时间单位（世界日 / 秒…）与定义里的时长同单位
var container = new EffectContainer(owner: heroId);
var ctx = new EffectContext { Subject = heroId };            // ConditionContext 的服务袋：按接口登记
ctx.RegisterService<IEffectAttributeSink>(mySink);          // 瞬时 / 周期修饰器永久落地处
ctx.RegisterService<IEffectContainerSource>(myContainers);  // 内置 Effect.ApplyEffect 等按主体找容器

EffectApplyResult r = container.ApplyEffect(buff, ctx, level: 1, source: casterId);   // Applied / Stacked / Refreshed / BlockedBy…
container.Tick(1f, ctx);                                     // 推进 1 个时间单位：周期结算、到期移除
var mods = new List<ModifierDefinition>();
container.CollectModifiers("might", mods);                   // 激活、未抑制、非周期效果的缩放修饰器 → 交给 ModifierStackEvaluator 汇流
container.RemoveEffectsWithTags(new GameplayTagContainer("Status.Buff"), ctx);   // 驱散
var save = container.ExportState();                          // 存档；ImportState(state, definitions, ctx) 静默恢复
```

- **施加管线**：免疫（激活且未抑制效果的免疫标签命中来者 `assetTags`）→ 施加标签要求 → 施加条件（`Subject` = 目标）→ 概率 → 时长求值（≤ 0 为 `Invalid`）→ 瞬时：修饰器经 `IEffectAttributeSink.ApplyPermanent` 落地 + `onApply`，不入容器 / 叠加：同键实例加层（封顶返回 `Refreshed`，仍按策略刷新）+ `onStack` / 新实例：授予标签 + `onApply` + 施加即结算 → 按标签移除他者。
- **Tick**：周期先于到期；跨多周期多次结算、余数保留；抑制中周期冻结、**时长照走**；到期按策略整清 / 减一层刷新 / 只刷新，`onExpire` → `onRemove`。
- **抑制**（`ongoingTagRequirements` 不满足）：修饰器不汇流、周期冻结、授予标签撤回；`OwnedTags` 任何变化（授予 / `AddLooseTag` / 宿主直写）都会自动重评。
- **值 / 事分工**：持续 / 无限效果的修饰器经 `CollectModifiers` 临时汇流（每条修饰器一条按层数缩放的 `ModifierDefinition`，来源 `effect:{id}#{handle}`）；瞬时与周期结算的修饰器经 Sink **永久落地**——**周期效果不参与 `CollectModifiers`**，否则「每周期 +10 且持续 +10」双算。
- 契约：`IEffectDefinitionSource`（+ 聚合的 `EffectDefinitionRegistry.Default`，供跨库按 id 引用）、`IEffectContainerSource`、`IEffectAttributeSource`（AttributeBased 幅度读当前值）、`IEffectAttributeSink`、`IEffectRandomSource`、`IEffectCueSink`（随 Applied / Executed / Removed 收到 `cueTags`）、`IEffectExecutionInfo`（执行器经 `ctx.GetService` 取当前定义 / 实例 / 来源 / 等级 / 阶段）；`EffectApplier.Apply(effectId, ctx)` 按 id 施加。事件：`OnEffectAdded / Removed / StackChanged / InhibitedChanged / PeriodicExecuted / OnModifiersChanged`。编辑器：`EffectDefinitionDrawer` 分节显隐；属性 id 字段的候选来自宿主登记的 `IEffectAttributeCatalogProvider`（`EffectDefinitionDrawerHooks.RegisterAttributeProvider`，多系统按系统名分组，1.10.0 起）或整字段接管委托 `AttributeIdField`（优先）；宿主实体自带 id / 名称时把 `ShowIdentityFields` 置 false 隐藏「基本」节。

**⑤ 效果库与 Effect Editor（1.10.0 起）**

所有上层系统共用的效果配置载体 `EffectDatabase`（`Create > Ale > Effect > Effect Database`）：四个顶层列表——**效果条目** `EffectEntry`（`id` + 显示名 / 描述 / 图标三个 `AttributeValue` + 按模板 schema 的自定义属性 `values` + 内嵌 `definition`；`Normalize()` 把定义的 id / 显示名与条目同步）、**效果模板** `EffectTemplate`（`name` / `color` / 自定义属性 schema + `defaultDefinition`，从模板创建时深拷贝作预设）、**Gameplay 标签**、**枚举类型**（自定义属性的枚举字段用）。上层系统只存效果 id、各自实现 `[EffectExecutor]` 执行器——效果本质是「对某个系统的操作」，系统如何被操作由该系统自己实现。

```csharp
// 运行时：放在 Resources 下随启动自动登记（EffectRuntime.AutoLoadFromResources，默认 true），或显式登记
EffectDataManager.Instance.Register(effectDatabase);         // 幂等；NormalizeAll + 并入全局效果注册表 + 登记 Gameplay 标签
EffectEntry e = EffectDataManager.Instance.GetEffect("regen_draught");
string name   = e.ResolveDisplayName();                      // 本地化显示名（缺省回退 id）
int power     = e.GetAttributeValue("power").GetInt(0);      // 模板 schema 定义的自定义属性
EffectApplier.Apply("regen_draught", ctx);                   // 定义经 EffectDefinitionRegistry.Default 按 id 解析
EffectDataManager.Instance.LoadFromBinary(bytes);            // 或 EffectConfigSerializer.ExportJson / Export 的产物
```

- 引导：`[SubsystemRegistration]` 清空注册表 → `[BeforeSceneLoad]` 只做加法（执行器 + `Resources`），宿主在 Awake 里 `Register` 的库稳定存活。⚠️ 序列化深度零余量：`EffectEntry` 必须是顶层列表元素、`defaultDefinition` 必须是直接字段。
- **Effect Editor**（`Tools > Ale Toolkit > Effect System > Effect Editor`，或资产 Inspector 的「在 Effect Editor 中编辑」）自 **1.11.0 起有两个页签**：
  - **Effect Executors**（第一页；内容来自**代码**，没有效果库也能用）：工程内全部 `IEffectExecutor` 实现的目录，按分类分组、可按 键 / 显示名 / 类型全名 / 程序集 搜索、可「只看有问题」。右列给出实现类型 / 程序集 / 源码路径与「打开脚本」（定位到类声明行）、「在 Project 中定位」、「复制 Key」（双击行等同打开脚本），参数 schema，被哪些效果配置引用（点「跳转」切到第二页并定位到那条效果），以及**静默失效体检**——漏打 `[EffectExecutor]`、特性里的键与 `Key` 属性不一致（特性字符串从不被读取）、重复键（编辑器目录先到先得 / 运行时注册表后者覆盖，两边相反）、缺公开无参构造 / 实例化失败（抽象基类只作说明、不算问题：特性本就该由派生类携带）。顶部横幅列出「配置引用了但没有实现」的悬空键（`EffectDefinition.Validate` 不查这个，运行时才报警告）；底部「新增执行器速查」给内置阶段常量与可一键复制的最小实现模板。
  - **Effect Database**（第二页；效果库是可选项，通常用于给效果条目配置额外数据）：左列 效果模板 / Gameplay 标签 / 枚举类型，中列效果列表（模板过滤 / 搜索 / 从模板添加 / 快速添加），右列 ID 查重 + 名称 / 描述 / 图标 + 自定义属性 + 内联定义 + 校验摘要；查重阻断导出，导出 JSON / 二进制。`EffectEditorWindow.Open(db, effectId)` 切到本页并定位到指定效果，`OpenExecutors()` 切到第一页。
- **宿主接入**（Inspector 一行代码）：`EditorEffectRefListDrawer.Draw(ctx, skill.onUseEffectRefs, drag, "使用时施加的效果", "效果")`——目录菜单（`EffectEditorCatalog`，按库分组）、拖拽重排、「打开」跳转、「未找到」标注（不阻断）、自由输入；属性 id 候选经 `EffectDefinitionDrawerHooks.RegisterAttributeProvider(IEffectAttributeCatalogProvider)` 登记（多系统按系统名分组下拉）；宿主自己的标签目录经 `GameplayTagEditorCatalog.RegisterProvider` 贡献。

> **与 UE5 GAS 的对照**：`EffectDefinition` ≙ GameplayEffect（Duration / Period / Stacking / Tags / Modifiers / Executions / Cues），`EffectContainer` ≙ AbilitySystemComponent 的活动效果与标签部分，`EffectExpression` + `[EffectExecutor]` ≙ Executions。未做：曲线表幅度、非快照的属性捕获（幅度在施加 / 叠层时快照）、网络复制。**修饰器管「值」，执行器管「事」，容器管「生命周期」。**

### 编辑器框架

`Ale.Toolkit.Editor`，均对数据库类型泛型化，宿主插件继承后覆写少量抽象成员即可搭出编辑器。

- **数据库窗口外壳** `EditorDatabaseWindowBase<TDb>`：内建「DB 资产对象字段 + 顶部页签条 + 校验 / 导出按钮钩子 + 查重扫描编排 + 状态栏 + Undo 订阅 + 上次 DB 路径记忆（EditorPrefs）」，实现 `IEditorDbContext<TDb>`；宿主窗口只提供页签集合 / 导出·校验回调 / 查重种类即可大幅变薄。1.11.0 起：`TabRequiresDatabase(int)` 覆写为 false 的页签在**没有数据库资产时照常绘制**（内容来自代码而非资产的页签用），`SelectSystemTab(int)` / `CurrentSystemTab` 供外部切页与读当前页，页签索引按 `EditorPrefKey` 记忆。
- **三列页签** `EditorThreeColumnTab<TDb,TEntity>`：左列子页签 + 主列表、中列实体列表、右列上下文 Inspector。子类覆写 `LeftPanels` / `EntityNoun` / `EntityList` / `DrawEntityList` / `DrawEntityInspector` 等；`RequestSelect(entity)` 供外部定位（1.10.0 起；须在数据库设定之后调用，下一帧 Layout 激活右列 Inspector）。
- **主列表面板** `EditorMasterListPanel<TDb,T>`（+ `IEditorMasterListPanel<TDb>`）、**实体列表面板** `EditorEntityListPanel<TDb,TEntity,TTemplate>`。
- **工具窗口基类** `EditorToolWindowBase<TDb>`：内建「选数据库 + 逐帧时间预算步进 + 进度条 + 日志 + 取消 + 完成收尾」；子类覆写 `DrawOperations`（用 `RunSteps` 启动逐帧步骤）/ `OnRunComplete` / `OnRunFinished`。
- 上下文 `IEditorContext` / `IEditorDbContext<TDb>`；辅助控件 `EditorSearchableList` / `EditorDraggableRowList` / `EditorReorderableDrag` / `EditorListKeyboardNav` / `EditorFilterTabs` / `EditorIdScanner` / `ToolkitEditorStyles` / `EditorScriptLocator`（1.11.0 起：类型 → `MonoScript` 定位，可打开 IDE 到类声明行或在 Project 中高亮；对非 `MonoBehaviour` 的普通类同样有效）。**可点击的列表行统一带鼠标悬停高亮**（`ToolkitEditorStyles.TrackMouseHover` / `DrawRowHover`，1.11.0 起；做在上述共用行组件里，宿主无需接线）。

### 编辑器多语言

编辑器界面文本三语（中 / 英 / 日）服务，以中文原文为键，缺译回退中文。与运行时内容本地化无关。

```csharp
using static Ale.Toolkit.Editor.ToolkitEditorL10n;
EditorGUILayout.LabelField(Tr("快捷操作"));       // 按当前语言返回
string name = TrEnum(EFieldType.Sprite);          // 枚举显示名

// 宿主插件在 [InitializeOnLoad] 里登记领域译表
ToolkitEditorL10n.Add("道具", "Item", "アイテム");
ToolkitEditorL10n.AddEnum(MyEnum.Foo, "Foo", "フー");
```

- `ToolkitEditorL10n.Tr(zh)` / `TrEnum(enumValue)`；`Current`（`EditorLanguage`）/ `TranslateEnums`；`Add(zh, en, ja)` / `AddEnum(value, en, ja, zh = null)`。

### 可选依赖支持层

TextMeshPro / Unity Localization / Addressables 的宏开关与运行时适配。宏为项目级全局设定（`ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`），由欢迎窗口统一开关。插件只在「开了宏却没装对应包」时给出 Console 提示，**绝不自动改写 PlayerSettings**——自动改写会与其他插件对同名宏的管理逻辑互相覆盖，每次写入触发一次重编译，编辑器会陷入死循环。

- `ToolkitDefines`：宏名常量 `Tmp` / `Localization` / `Addressable`，`IsTmpEnabled()` / `IsLocalizationEnabled()` / `IsAddressableEnabled()`。
- `DefineUtils`：`ApplyDefine(...)`（增删 PlayerSettings 脚本宏）、`HasNamespace(...)` / `HasClass(...)`（探测包是否安装），供消费方自建宏开关面板。
- 运行时资源门面 `ToolkitAssets`（对 Addressables 零依赖）：`Bind<T>(value, owner, set)` / `Bind<T>(liveRef, address, owner, set)`（宿主销毁自动释放）、`Load<T>` / `Release`；接口 `IAssetLoader`；启用 `ATK_ADDRESSABLE` 后 `AddressableManager` 按地址引用计数加载 / 卸载。

### 编辑器入口与全局设置

- `ToolkitWelcomeWindow`（菜单 **Tools > Ale Toolkit > Welcome**）：界面语言 / 枚举翻译开关 / 三个可选依赖宏开关 / 向导默认与本地化字体 / 通用工具入口 / 启动自动显示。
- `ToolkitProjectSettings`（`ScriptableSingleton`，存 `ProjectSettings/AleToolkitSettings.asset`，随仓库共享、按 GUID 引用资源）：`SaveSettings()`；向导字体经门面 `ToolkitPrefabFonts` 读写。

### 通用工具窗口

对任意数据资产（`ScriptableObject`）遍历其全部 `AttributeValue` 批量处理，供上层插件复用。

- `ToolkitAddressableToolWindow`（菜单 **Tools > Ale Toolkit > Addressable**）：在「Object 引用 ↔ AssetReference(GUID)」间批量互转全库资源字段。宿主可继承 `EditorAddressableToolWindow<TDb>` 并经 `FixedFields` 提供属性系统之外的具名 Sprite 字段。
- `ToolkitLocalizationToolWindow`（菜单 **Tools > Ale Toolkit > Localization**）：批量生成本地化 Key；基类 `EditorLocalizationToolWindow<TDb>`。
- 反射遍历辅助：`AttributeValueWalker`（遍历全库属性对象值）、`TextFieldWalker` / `TextFieldCollector`（遍历文本值、id 感知 Key）。

---

## 许可

[MIT](LICENSE.md)
