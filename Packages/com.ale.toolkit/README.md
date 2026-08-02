# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

面向 Unity 插件开发的**通用底层库**。不含任何具体业务领域概念，供多个插件共享同一套属性配置、列表、编辑器框架与多语言能力。

> 本包由 `com.ale.inventory` 1.8.0 拆分而来。原先埋在库存系统里的通用能力（编辑器三列框架、虚拟滚动列表、自定义属性系统、编辑器界面三语）被抽到这里，使其可被更多插件复用。

---

## 目录

- [安装（请先读这一段）](#-安装请先读这一段)
- [包含的模块](#包含的模块)
- [程序集](#程序集)
- [用法与主要 API](#用法与主要-api)
  - [属性系统](#属性系统) · [排序](#排序) · [UI](#ui) · [对象池](#对象池) · [Tween（中央缓动）](#tween中央缓动)
  - [属性修饰器](#属性修饰器) · [条件系统 · Condition System](#条件系统--condition-system) · [效果系统 · Effect System](#效果系统--effect-system)
  - [编辑器框架](#编辑器框架) · [编辑器多语言](#编辑器多语言) · [可选依赖支持层](#可选依赖支持层) · [编辑器入口与全局设置](#编辑器入口与全局设置) · [通用工具窗口](#通用工具窗口)
- [许可](#许可)

---

## ⚠️ 安装（请先读这一段）

**`com.ale.toolkit` 必须先于依赖它的插件安装。**

Unity 的 Package Manager **不支持在 `package.json` 的 `dependencies` 里写 git URL**，因此依赖本包的插件无法自动把它拉下来。你需要手动安装两次，且**顺序不能颠倒**：

`Window > Package Manager` → 左上角 `+` → `Install package from git URL...`

**第一步 —— 先装 Toolkit：**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.5.0
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
| **属性系统** | `AttributeValue` 与 20+ 字段类型、属性定义（schema）、自定义枚举类型、数字格式配置、轻量展示文本 `TextValue`（fallback + 可选原生本地化）。任何需要「配置属性条目」的场合都用它 |
| **排序** | 与元素类型无关的排序引擎：宿主实现 `ISortContext<TData>` 提供比较所需信息，引擎负责多级优先级与降级比较 |
| **UI** | 虚拟滚动列表（网格 / 顺序，对象池 + 仅渲染可见区）、页签栏、过滤栏、Tooltip 基类、子项实例池等通用控件 |
| **对象池** | 通用 GameObject 预制体池（`Spawn`/`Despawn` + `IPoolable` 回调、预热 / 容量回收 / 延迟归还 / 跨场景）与纯 C# 引用类型池 `ToolkitClassPool<T>`（降 GC），可替代 Lean.Pool 一类第三方池 |
| **Tween** | 轻量中央 Tween（DOTween 式单 Update 轮询、作业池化近零 GC）：`ToolkitTween.FadeCanvasGroup` 对 `CanvasGroup` 淡入淡出，返回值类型可打断句柄；缓动最小集 `EToolkitEase` |
| **属性修饰器** | GAS 式修饰器求值：`ModifierDefinition` + `ModifierStackEvaluator` 分组结算（Add→PercentAdd→Multiply→Override + clamp + 来源明细）。任何「基础值 + 一叠加成 → 当前值」的数值汇流都用它 |
| **条件系统（Condition System）** | 数据驱动的两级 AND/OR 条件：声明一个 `ConditionExpression` 字段即在 Inspector 内联配置；上层实现 `[ConditionEvaluator]` 判定器被自动发现。引擎无关 Core 可上服务端 |
| **效果系统（Effect System）** | 条件系统的写侧镜像：数据驱动的离散触发式突变（阶段组 + 每项可选条件门控）；上层实现 `[EffectExecutor]` 执行器被自动发现。引擎无关 Core |
| **编辑器框架** | 三列布局页签基类、数据库窗口外壳基类、主列表面板、实体列表面板、工具窗口基类，均对数据库类型泛型化 |
| **编辑器多语言** | 中 / English / 日本語 三语服务，以中文原文为键，缺译文自动回退 |
| **可选依赖支持层** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）的宏开关与适配 |
| **编辑器入口与全局设置** | Ale Toolkit 欢迎窗口（`Tools > Ale Toolkit > Welcome`）：界面语言 / 枚举翻译 / 三个可选依赖宏开关 / 向导默认字体 / 本地化字体 + 通用工具入口 + 页脚「启动时自动显示」；其中向导字体等项目级设定存入 `ProjectSettings/AleToolkitSettings.asset`（随仓库入库、按 GUID 引用资源），语言 / 自动显示为每人偏好（EditorPrefs）；旧宏 `IS_*` 加载时自动迁移为 `ATK_*` |
| **通用工具窗口** | 对任意数据资产（`ScriptableObject`）遍历其全部 `AttributeValue` 批量处理：Addressable 迁移（Object ↔ GUID）与本地化 Key 生成，挂 `Tools > Ale Toolkit`，供上层插件复用 |

> 上述模块已全部落位——1.1.0 起 TMP / Localization / Addressables 三个可选依赖支持层齐备、纯 toolkit 环境界面亦具三语；**1.2.0 起接管项目级全局设定（语言 / 宏）并提供可对任意数据资产工作的通用工具窗口**；**1.3.0 起新增通用对象池（GameObject 预制体池 + 纯 C# 类池）与轻量中央 Tween**；**1.4.0 起新增属性修饰器求值、数据库窗口外壳基类，以及两个独立子系统——条件系统（`Ale.Condition`）与效果系统（`Ale.Effect`）**；**1.5.0 起新增轻量展示文本值 `TextValue`（fallback + 可选原生本地化，`AttributeValue` 的 `Text` 类型的独立轻量版）**。完整变更见 [CHANGELOG](CHANGELOG.md)。

---

## 程序集

| Assembly Definition | 说明 | 宏门控 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性系统、排序、资源加载抽象、通用序列化、对象池、中央 Tween、属性修饰器求值 | — |
| `Ale.Toolkit.UI` | 虚拟滚动列表与通用 UI 控件 | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 适配组件 | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables 资源加载与句柄管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | 编辑器框架、数据库窗口外壳基类、属性绘制器、多语言服务、宏开关 | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables 编辑器工具 | `ATK_ADDRESSABLE` |
| `Ale.Condition.Core` | 条件系统 · 引擎无关模型 / 判定引擎 / 注册与反射发现 / JSON（`noEngineReferences`，可上服务端） | 引用 Newtonsoft |
| `Ale.Condition.Runtime` | 条件系统 · Unity 桥（`ConditionAsset` + 启动自动注册） | — |
| `Ale.Condition.Editor` | 条件系统 · 内联绘制器 / 目录 / 欢迎窗口 | — |
| `Ale.Effect.Core` | 效果系统 · 引擎无关模型 / 执行运行器 / 注册与反射发现 / JSON（`noEngineReferences`） | 引用 `Ale.Condition.Core` + Newtonsoft |
| `Ale.Effect.Runtime` | 效果系统 · Unity 桥（`EffectAsset` + 启动自动注册） | — |
| `Ale.Effect.Editor` | 效果系统 · 内联绘制器 / 目录 / 欢迎窗口 | — |

依赖方向单向：宿主插件 → `Ale.Toolkit.*` / `Ale.Condition.*` / `Ale.Effect.*`，本包不反向引用任何宿主插件。条件 / 效果两子系统命名空间独立（`Ale.Condition` / `Ale.Effect`），`Ale.Effect.Core` 单向引用 `Ale.Condition.Core`（供效果项的可选条件门控）。

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

- **虚拟滚动列表** `UiwVirtualGridList<TData,TCell>`（网格）/ `UiwVirtualOrderList<TData,TCell>`（顺序）：继承并实现 `BindCell` / `ClearCell`，Inspector 绑定 `cellPrefab` / `scrollRect` / `content`，喂入数据即只渲染可见区、逐帧限速生成。主要方法：`SetItems` / `UpdateItems` / `RefreshItemsData` / `SetSourceItems`、`ConfigureFilter` / `SetExtraFilter`、`ConfigureSort`、`ScrollToStart`。
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

轻量中央 Tween 门面（DOTween 式「单 Update 轮询作业表」，`Ale.Toolkit.Runtime`）。当前提供 `CanvasGroup` 淡入淡出，作业经 `ToolkitClassPool` 池化、由常驻 runner 单 `LateUpdate` 推进，近零 GC。不复刻 DOTween 的 Sequence / 链式 / 全套 Ease，按需增量扩展。

```csharp
// 对 CanvasGroup 淡入到 alpha=1，0.2s；返回可打断的句柄
var h = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, 0.2f, EToolkitEase.OutQuad,
                                     unscaled: true, onComplete: () => { /* 完成 */ });
h.Kill(complete: true);    // 打断并瞬置到终值 + 触发完成回调；Kill(false) 打断且不回调
bool running = h.IsActive; // 是否仍在进行
```

- `ToolkitTween.FadeCanvasGroup(target, endAlpha, duration, ease = OutQuad, unscaled = true, onComplete = null)`：`duration ≤ 0` 或目标为空时立即到位并返回空句柄。
- `ToolkitTweenHandle`（值类型，零分配）：`IsActive` / `Kill(complete = false)`；`default` 为无效句柄、`Kill` 安全空操作。
- `ToolkitEase.Evaluate(EToolkitEase ease, float t)`；缓动类型 `EToolkitEase`：`Linear` / `InQuad` / `OutQuad` / `InOutQuad`。

### 属性修饰器

GAS 式修饰器求值（`Ale.Toolkit.Runtime`）。声明式 `ModifierDefinition` 汇入一个属性，`ModifierStackEvaluator` 按固定顺序分组结算出「当前值 + 逐来源明细」。静态、无状态、无 Unity 依赖；**不含**时长到期 / 叠层的运行时循环（配置携带，宿主运行时结算后把有效修饰器喂进来）。

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

- `ModifierDefinition`：`targetAttributeId`（不透明键，求值器不解释）/ `operation` / `magnitude` / `duration` / `durationDays` / `sourceTag`（来源明细 + 分组撤销）/ `stackLimit` / `stackRule`。
- `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown = true)` → `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`；轻量 `EvaluateValue(...)` 只出最终值。结算顺序固定：`base → +ΣAdd → ×(1+ΣPercentAdd) → 逐项 ×(1+magnitude) Multiply → 末位 Override 覆盖 → clamp[min,max]`。调用方需先按 `targetAttributeId` 分组；时长 / 叠层由运行时结算后再传入。
- 枚举：`EModifierOperation`（`Add`/`PercentAdd`/`Multiply`/`Override`）、`EModifierDuration`（`Instant`/`Timed`/`Permanent`）、`EStackRule`（`Refresh`/`Add`/`EveryXStacks`/`OnMaxStacks`）。

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

`ParamSchema` 驱动编辑器动态参数区；固定选项（如比较符）用 `ConditionParamDef` 的 `choices`（渲染为下拉、存索引）。参数 5 型：`String` / `Int` / `Float` / `Bool` / `Enum`（+ `isArray`）。

**③ 提供上下文 + 求值（运行时）**

```csharp
class MyCtx : IConditionContext {              // 主体 + 服务包（宿主实现）
    public object Subject { get; set; }
    private readonly object[] _svc;
    public MyCtx(params object[] svc) { _svc = svc; }
    public T GetService<T>() where T : class { foreach (var s in _svc) if (s is T t) return t; return null; }
}

var ctx = new MyCtx(myStatSource);
bool ok = expr.Evaluate(ctx).Passed;                        // 便捷法
ConditionResult r = ConditionEngine.Evaluate(expr, ctx);    // 或直接调引擎；r.FailedKeys 列未满足键
```

运行时 `ConditionRuntime` 于 `[RuntimeInitializeOnLoadMethod]` 把 `ConditionRegistry.Default` 反射填满并接缺键告警；服务端 / 测试可手动 `new ConditionRegistry()` + `AutoRegisterFromAssemblies()` 或逐个 `Register`。

**内置判定器**：`Condition.AlwaysTrue`、`Condition.HasFlag`（`IConditionFlagSource`）、`Condition.NumberCompare`（`IConditionNumberSource`）。**JSON**：`ConditionJson.ToJson(expr)` / `FromJson(str)`（Newtonsoft；模型纯 POCO，可换序列化器、可入库存档）。**总览**：`Tools > Ale Toolkit > Condition System > Welcome`。

### 效果系统 · Effect System

条件系统的**写侧镜像**（命名空间 `Ale.Effect`）：数据驱动、参数化的**离散触发式突变**，按「阶段组」组织、每项可挂可选条件门控。数值加成（buff）由上面的**属性修饰器**负责；效果只做离散动作（授予 / 移除、置标志、发事件、点燃…）。同样「声明 `EffectExpression` 字段即在 Inspector 配置」，上层实现 `[EffectExecutor]` 执行器被自动发现。三个程序集：`Ale.Effect.Core`（引用 `Ale.Condition.Core` 供门控）/ `.Runtime` / `.Editor`。

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

**内置执行器**：`Effect.NoOp`、`Effect.SetFlag`（`IEffectFlagSink`）、`Effect.AdjustNumber`（`IEffectNumberSink`）——分别是条件系统 `HasFlag` / `NumberCompare` 的写侧对偶。**JSON**：`EffectJson.ToJson/FromJson`（内嵌 gate 随图往返）。**总览**：`Tools > Ale Toolkit > Effect System > Welcome`。

> **与 UE5 GAS 的边界**：GAS `GameplayEffect` 的数值侧（Modifiers / Duration / Stacking）由上面的**属性修饰器**覆盖；效果系统对应其执行侧（Executions / Cues / Conditional Effects）——离散触发动作。二者分工清晰：**修饰器管「值」，效果管「事」**。

### 编辑器框架

`Ale.Toolkit.Editor`，均对数据库类型泛型化，宿主插件继承后覆写少量抽象成员即可搭出编辑器。

- **数据库窗口外壳** `EditorDatabaseWindowBase<TDb>`：内建「DB 资产对象字段 + 顶部页签条 + 校验 / 导出按钮钩子 + 查重扫描编排 + 状态栏 + Undo 订阅 + 上次 DB 路径记忆（EditorPrefs）」，实现 `IEditorDbContext<TDb>`；宿主窗口只提供页签集合 / 导出·校验回调 / 查重种类即可大幅变薄。
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
