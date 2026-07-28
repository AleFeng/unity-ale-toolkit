# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

面向 Unity 插件开发的**通用底层库**。不含任何具体业务领域概念，供多个插件共享同一套属性配置、列表、编辑器框架与多语言能力。

> 本包由 `com.ale.inventory` 1.8.0 拆分而来。原先埋在库存系统里的通用能力（编辑器三列框架、虚拟滚动列表、自定义属性系统、编辑器界面三语）被抽到这里，使其可被更多插件复用。

---

## ⚠️ 安装（请先读这一段）

**`com.ale.toolkit` 必须先于依赖它的插件安装。**

Unity 的 Package Manager **不支持在 `package.json` 的 `dependencies` 里写 git URL**，因此依赖本包的插件无法自动把它拉下来。你需要手动安装两次，且**顺序不能颠倒**：

`Window > Package Manager` → 左上角 `+` → `Install package from git URL...`

**第一步 —— 先装 Toolkit：**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.3.0
```

**第二步 —— 再装依赖它的插件**，例如库存系统：

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.10.0
```

> 若顺序颠倒或漏装本包，Unity 会报 `找不到 Ale.Toolkit.*` 一类的编译错误。此时补装本包并等待重新编译即可，无需重装另一个插件。

最低支持 **Unity 2022.3**（基于 Unity 6000.3 开发与维护）。

---

## 包含的模块

| 模块 | 内容 |
| --- | --- |
| **属性系统** | `AttributeValue` 与 20+ 字段类型、属性定义（schema）、自定义枚举类型、数字格式配置。任何需要「配置属性条目」的场合都用它 |
| **排序** | 与元素类型无关的排序引擎：宿主实现 `ISortContext<TData>` 提供比较所需信息，引擎负责多级优先级与降级比较 |
| **UI** | 虚拟滚动列表（网格 / 顺序，对象池 + 仅渲染可见区）、页签栏、过滤栏、Tooltip 基类、子项实例池等通用控件 |
| **对象池** | 通用 GameObject 预制体池（`Spawn`/`Despawn` + `IPoolable` 回调、预热 / 容量回收 / 延迟归还 / 跨场景）与纯 C# 引用类型池 `ToolkitClassPool<T>`（降 GC），可替代 Lean.Pool 一类第三方池 |
| **编辑器框架** | 三列布局页签基类、主列表面板、实体列表面板、工具窗口基类，均对数据库类型泛型化 |
| **编辑器多语言** | 中 / English / 日本語 三语服务，以中文原文为键，缺译文自动回退 |
| **可选依赖支持层** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）的宏开关与适配 |
| **编辑器入口与全局设置** | Ale Toolkit 欢迎窗口（`Tools > Ale Toolkit > Welcome`）：界面语言 / 枚举翻译 / 三个可选依赖宏开关 / 向导默认字体 / 本地化字体 + 通用工具入口 + 页脚「启动时自动显示」；其中向导字体等项目级设定存入 `ProjectSettings/AleToolkitSettings.asset`（随仓库入库、按 GUID 引用资源），语言 / 自动显示为每人偏好（EditorPrefs）；旧宏 `IS_*` 加载时自动迁移为 `ATK_*` |
| **通用工具窗口** | 对任意数据资产（`ScriptableObject`）遍历其全部 `AttributeValue` 批量处理：Addressable 迁移（Object ↔ GUID）与本地化 Key 生成，挂 `Tools > Ale Toolkit`，供上层插件复用 |

> 上述模块已全部落位——1.1.0 起 TMP / Localization / Addressables 三个可选依赖支持层齐备、纯 toolkit 环境界面亦具三语；**1.2.0 起接管项目级全局设定（语言 / 宏）并提供可对任意数据资产工作的通用工具窗口**；**1.3.0 起新增通用对象池（GameObject 预制体池 + 纯 C# 类池）**。完整变更见 [CHANGELOG](CHANGELOG.md)。

---

## 程序集

| Assembly Definition | 说明 | 宏门控 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性系统、排序、资源加载抽象、通用序列化、对象池 | — |
| `Ale.Toolkit.UI` | 虚拟滚动列表与通用 UI 控件 | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 适配组件 | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables 资源加载与句柄管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | 编辑器框架、属性绘制器、多语言服务、宏开关 | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables 编辑器工具 | `ATK_ADDRESSABLE` |

依赖方向单向：宿主插件 → `Ale.Toolkit.*`，本包不反向引用任何宿主插件。

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

- **虚拟滚动列表** `UiwVirtualGridList<TData,TCell>`（网格）/ `UiwVirtualOrderList<TData,TCell>`（顺序）：继承并实现 `BindCell` / `ClearCell`，Inspector 绑定 `cellPrefab` / `scrollRect` / `content`，喂入数据即只渲染可见区、逐帧限速生成。主要方法：`SetItems` / `UpdateItems` / `RefreshItemsData` / `SetSourceItems`、`ConfigureFilter` / `SetExtraFilter`、`ConfigureSort`、`ScrollToStart`。
- **页签条** `UiwTabStrip<TTab,TValue>`（纯 C#）：`Configure(prefab, container, bind, onSelect)` → `SetTabs(values, labels, …)` → `Select` / `SelectValue`；差异复用不整排重建。过滤页签栏 `UiwFilterTabBar`（MonoBehaviour）：`SetFilters(tagNames)` / `Clear`。
- **悬停弹窗** `UiwTooltipBase<TPayload>`：子类实现 `ApplyContent` / `ClearContent` 并暴露自己的 `Show`（内部转 `ShowTooltip`）；`Hide()`。
- **子项实例池** `UiwWidgetPool<T>`（游标式复用）：`Configure` → `Begin` → `Next(out created)` → `End`。
- 其它：`UiwViewBase`（`Open`/`Close`/`ToggleOpenClose`）、`UiwSortToolbar`（`SetOptions`/`SetSortPriorities`）、`UiwNumberCounter`（`Configure`/`SetRange`/`SetValue`）、`UiwTextLabel`、`SpriteSlot.Bind(image, value)`。

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

### 编辑器框架

`Ale.Toolkit.Editor`，均对数据库类型泛型化，宿主插件继承后覆写少量抽象成员即可搭出编辑器。

- **三列页签** `EditorThreeColumnTab<TDb,TEntity>`：左列子页签 + 主列表、中列实体列表、右列上下文 Inspector。子类覆写 `LeftPanels` / `EntityNoun` / `EntityList` / `DrawEntityList` / `DrawEntityInspector` 等。
- **主列表面板** `EditorMasterListPanel<TDb,T>`（+ `IEditorMasterListPanel<TDb>`）、**实体列表面板** `EditorEntityListPanel<TDb,TEntity,TTemplate>`。
- **工具窗口基类** `EditorToolWindowBase<TDb>`：内建「选数据库 + 逐帧时间预算步进 + 进度条 + 日志 + 取消 + 完成收尾」；子类覆写 `DrawOperations`（用 `RunSteps` 启动逐帧步骤）/ `OnRunComplete` / `OnRunFinished`。
- 上下文 `IEditorContext` / `IEditorDbContext<TDb>`；辅助控件 `EditorSearchableList` / `EditorDraggableRowList` / `EditorReorderableDrag` / `EditorListKeyboardNav` / `EditorFilterTabs` / `EditorIdScanner` / `ToolkitEditorStyles`。

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

TextMeshPro / Unity Localization / Addressables 的宏开关与运行时适配。宏为项目级全局设定（`ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`），由欢迎窗口统一开关；旧宏 `IS_*` 加载时自动迁移。

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
