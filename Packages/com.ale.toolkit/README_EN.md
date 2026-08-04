# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

A **general-purpose foundation library** for Unity plugin development. It carries no business-domain concepts, letting several plugins share one attribute-configuration system, list engine, editor framework and localization service.

> This package was split out of `com.ale.inventory` 1.8.0. The general-purpose capabilities that used to live inside the inventory system (the three-column editor framework, virtual scrolling lists, the custom attribute system, the trilingual editor UI) were extracted here so more plugins can reuse them.

---

## Table of Contents

- [Installation (read this first)](#-installation-read-this-first)
- [Modules](#modules)
- [Assemblies](#assemblies)
- [Usage & Main APIs](#usage--main-apis)
  - [Attribute system](#attribute-system) · [Sorting](#sorting) · [UI](#ui) · [Object pool](#object-pool) · [Tween](#tween)
  - [Attribute modifier](#attribute-modifier) · [Condition System](#condition-system) · [Effect System](#effect-system)
  - [Editor framework](#editor-framework) · [Editor localization](#editor-localization) · [Optional dependency support](#optional-dependency-support) · [Editor entry & global settings](#editor-entry--global-settings) · [General tool windows](#general-tool-windows)
- [License](#license)

---

## ⚠️ Installation (read this first)

**`com.ale.toolkit` must be installed before any plugin that depends on it.**

Unity's Package Manager **does not support git URLs in the `dependencies` field of `package.json`**, so a dependent plugin cannot pull this package automatically. You must install both manually, **in this order**:

`Window > Package Manager` → `+` in the top-left → `Install package from git URL...`

**Step 1 — install Toolkit first:**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.5.1
```

**Step 2 — then install the dependent plugin**, for example the inventory system:

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.11.1
```

> If the order is reversed or this package is missing, Unity reports compile errors such as `Ale.Toolkit.* not found`. Just install this package and wait for the recompile — there is no need to reinstall the other plugin.

Requires **Unity 2022.3** or newer (developed and maintained on Unity 6000.3).

---

## Modules

| Module | Contents |
| --- | --- |
| **Attribute system** | `AttributeValue` with 20+ field types, attribute definitions (schema), custom enum types, number-format configs, and the lightweight display text `TextValue` (fallback + optional native localization). Use it wherever configurable attribute entries are needed |
| **Sorting** | An element-type-agnostic sort engine: the host implements `ISortContext<TData>` to supply what comparison needs, the engine handles multi-level priorities and tiebreakers |
| **UI** | Virtual scrolling lists (grid / sequential, object pool + visible-region-only rendering; cell assign / recycle fade-in-out driven generically by `UiwListFadeCell` + the engine's default hooks), tab strips, filter bars, tooltip base classes, widget pools |
| **Object pool** | A general-purpose GameObject/prefab pool (`Spawn`/`Despawn` + `IPoolable` callbacks; preload / capacity-recycle / delayed despawn / cross-scene) plus a plain-C# reference-type pool `ToolkitClassPool<T>` (lower GC) — a drop-in replacement for third-party pools like Lean.Pool |
| **Tween** | A lightweight central tween (DOTween-style single-Update polling, pooled jobs, near-zero GC): `ToolkitTween.FadeCanvasGroup` / `FadeGraphic` fade a `CanvasGroup` / `Graphic` (Image / text) alpha, returning a killable value-type handle; minimal easing set `EToolkitEase` |
| **Attribute modifier** | GAS-style modifier evaluation: `ModifierDefinition` + `ModifierStackEvaluator` settle by group (Add→PercentAdd→Multiply→Override + clamp + source breakdown). Use it for any "base value + a stack of bonuses → current value" numeric convergence |
| **Condition System** | Data-driven two-level AND/OR conditions: declare a `ConditionExpression` field to configure it inline in the Inspector; upper layers implement `[ConditionEvaluator]` evaluators that are auto-discovered. The engine-agnostic Core is server-side ready |
| **Effect System** | The write-side mirror of the Condition System: data-driven discrete trigger-style mutations (phase groups + an optional per-item condition gate); upper layers implement `[EffectExecutor]` executors that are auto-discovered. Engine-agnostic Core |
| **Editor framework** | Three-column tab base class, database window shell base class, master list panel, entity list panel and tool window base — all generic over the database type |
| **Editor localization** | 中文 / English / 日本語 service, keyed by the Chinese source string, falling back automatically when a translation is missing |
| **Optional dependency support** | Macro toggles and adapters for TextMeshPro (`ATK_TMP`), Unity Localization (`ATK_LOCALIZATION`) and Addressables (`ATK_ADDRESSABLE`) |
| **Editor entry & global settings** | The Ale Toolkit Welcome Window (`Tools > Ale Toolkit > Welcome`): editor UI language / enum translation / the three optional feature macros / wizard default & localized fonts + general-tool entries + an "auto-show on startup" toggle; project-level settings such as the wizard fonts are saved to `ProjectSettings/AleToolkitSettings.asset` (committed with the repo, asset references stored by GUID), while language / auto-show are per-user (EditorPrefs); legacy `IS_*` macros are auto-migrated to `ATK_*` on load |
| **General tool windows** | Walk every `AttributeValue` of any data asset (`ScriptableObject`) for batch processing: Addressable migration (Object ↔ GUID) and localization key generation, under `Tools > Ale Toolkit`, reusable by upper-layer plugins |

> All modules above are in place — since 1.1.0 the three optional-dependency support layers (TMP / Localization / Addressables) are complete and the editor UI is trilingual even in a toolkit-only project; **since 1.2.0 it owns the project-level global settings (language / macros) and provides general tool windows that work on any data asset**; **since 1.3.0 it adds a general-purpose object pool (GameObject pool + plain-C# class pool) and a lightweight central tween**; **since 1.4.0 it adds attribute-modifier evaluation, a database window shell base class, and two independent subsystems — the Condition System (`Ale.Condition`) and the Effect System (`Ale.Effect`)**; **since 1.5.0 it adds the lightweight display-text value `TextValue` (fallback + optional native localization — a standalone lightweight version of `AttributeValue`'s `Text` type)**; **since 1.5.1 it adds generic cell fade-in/out for the virtual-scroll list (`UiwListFadeCell` + `IUiwRecycleFadeCell` / `IUiwDiffCell`, driven by `UiwVirtualListBase`'s default hooks) plus `ToolkitTween.FadeGraphic`**. See the [CHANGELOG](CHANGELOG.md) for details.

---

## Assemblies

| Assembly Definition | Purpose | Macro constraint |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | Attribute system, sorting, asset-loading abstraction, shared serialization, object pool, central tween, attribute-modifier evaluation | — |
| `Ale.Toolkit.UI` | Virtual scrolling lists and general UI widgets | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization adapter components | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables loading and handle management | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | Editor framework, database window shell base class, attribute drawers, localization service, macro toggles | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables editor tooling | `ATK_ADDRESSABLE` |
| `Ale.Condition.Core` | Condition System · engine-agnostic model / evaluation engine / registry & reflection discovery / JSON (`noEngineReferences`, server-side ready) | References Newtonsoft |
| `Ale.Condition.Runtime` | Condition System · Unity bridge (`ConditionAsset` + auto-register on startup) | — |
| `Ale.Condition.Editor` | Condition System · inline drawer / catalog / Welcome window | — |
| `Ale.Effect.Core` | Effect System · engine-agnostic model / execution runner / registry & reflection discovery / JSON (`noEngineReferences`) | References `Ale.Condition.Core` + Newtonsoft |
| `Ale.Effect.Runtime` | Effect System · Unity bridge (`EffectAsset` + auto-register on startup) | — |
| `Ale.Effect.Editor` | Effect System · inline drawer / catalog / Welcome window | — |

Dependencies flow one way: host plugin → `Ale.Toolkit.*` / `Ale.Condition.*` / `Ale.Effect.*`; this package never references a host plugin. The Condition and Effect subsystems have independent namespaces (`Ale.Condition` / `Ale.Effect`), and `Ale.Effect.Core` references `Ale.Condition.Core` one-way (for the optional condition gate on effect items).

---

## Usage & Main APIs

> Runtime types live in `Ale.Toolkit.Runtime` / `Ale.Toolkit.Runtime.UI`; editor types in `Ale.Toolkit.Editor`. Below is the typical usage and main entry points per module; the source XML docs are the authority on full signatures.

### Attribute system

`AttributeValue` carries "one typed value" (scalar in `[0]`, arrays in `[0..n]`); the type is an `EFieldType` (24 of them: Int / Float / String / Bool / Enum / Vector2~4 / Color / Sprite / Text / Prefab / AudioClip / StringIntPair / EnumIntPair, …). A field's schema is described by `AttributeDefinition`, and an entity reads values by field id through `AttributeOwner`.

```csharp
var v = new AttributeValue(EFieldType.Int);
v.SetInt(0, 10);
int hp      = v.GetInt(0);
string show = v.ToDisplayString();     // display string (arrays joined by a separator)
double key  = v.ToComparableNumber();  // numeric value for sorting

// read a value off an entity by field id
AttributeValue atk = owner.GetAttributeValue("attack");
```

- `AttributeValue`: `Type` / `IsArray` / `Count`; `GetInt/SetInt`, `GetFloat/SetFloat`, `GetString/SetString`, `GetObject/SetObject`, `GetColor/SetColor`, `GetVector2~4`, `GetTextValue/SetTextValue/ResolveText`, `SetStringIntPair/SetEnumIntPair`; array `AddElement/RemoveElement/ReorderElements`; `ToDisplayString()`, `ToComparableNumber()`, `ChangeType()`, `Clone()`.
- `AttributeDefinition.CreateValue()`; `AttributeOwner.GetEntry(id)` / `GetAttributeValue(id)`; `AttributeSync.Sync(...)` reconciles an entity's values against the schema.
- `ConfigTemplateBase` (`name` / `color` / `List<AttributeDefinition> attributes`); `EnumType` (`AddItem` / `GetItemByValue` / `GetDisplayName`) + `EnumItem`; `NumberFormatConfig.Format(long, langCode)`.
- **`TextValue`** (lightweight display text — a standalone lightweight version of `AttributeValue`'s `Text` type): `Fallback` (always present) + an embedded native Unity `LocalizedString` (`Localized`) when `ATK_LOCALIZATION` is enabled; `ResolveText()` prefers the localized value and falls back to the fallback when unavailable; `IsEmpty` / `Clone()`. Each instance holds just one string (plus one `LocalizedString` when localizing), without `AttributeValue`'s multi-type backing-list overhead. The editor `TextValueDrawer` (`[CustomPropertyDrawer(typeof(TextValue))]`) draws a "fallback row + native table/entry selector" — declare the field to configure it in the Inspector, and selections save correctly.

### Sorting

Implement `ISortContext<TData>` once for your data type (or derive from `SortContextBase<TData>` / `TagSortContextBase<TData>`) to reuse the domain-agnostic `AttributeSortService`: it walks the `SortPriority` list (field + ascending), comparing until a non-zero result.

```csharp
class MySortCtx : SortContextBase<MyData> { /* override OwnerOf / FindDefinition / OptionOf / TryCompareSpecial */ }

AttributeSortService.Sort(list, priorities, new MySortCtx());
int cmp = AttributeSortService.Compare(a, b, priorities, ctx);
```

- `AttributeSortService.Sort<TData>(list, priorities, ctx)` / `Compare(...)` / `CompareByField(...)`.
- `ISortContext<TData>`: `OwnerOf` / `FindDefinition` / `OptionOf` / `TryCompareSpecial`.
- `SortPriority` (field + direction), `SortOption` (per-field ignore list), `SortFieldKeys`, `ISortId`, `SortOptionSync`.

### UI

Reusable runtime widgets under `Ale.Toolkit.Runtime.UI`, all generic.

- **Virtual scrolling lists** `UiwVirtualGridList<TData,TCell>` (grid) / `UiwVirtualOrderList<TData,TCell>` (sequential): derive and implement `BindCell` / `ClearCell`, wire `cellPrefab` / `scrollRect` / `content` in the Inspector, then feed data — only the visible region is rendered, with rate-limited per-frame spawning. Main methods: `SetItems` / `UpdateItems` / `RefreshItemsData` / `SetSourceItems`, `ConfigureFilter` / `SetExtraFilter`, `ConfigureSort`, `ScrollToStart`.
- **Tab strip** `UiwTabStrip<TTab,TValue>` (plain C#): `Configure(prefab, container, bind, onSelect)` → `SetTabs(values, labels, …)` → `Select` / `SelectValue`; reuses instances instead of rebuilding the row. Filter tab bar `UiwFilterTabBar` (MonoBehaviour): `SetFilters(tagNames)` / `Clear`.
- **Hover tooltip** `UiwTooltipBase<TPayload>`: subclass implements `ApplyContent` / `ClearContent` and exposes its own `Show` (forwarding to `ShowTooltip`); `Hide()`.
- **Widget pool** `UiwWidgetPool<T>` (cursor-style reuse): `Configure` → `Begin` → `Next(out created)` → `End`.
- Others: `UiwViewBase` (`Open`/`Close`/`ToggleOpenClose`; an `IsOpen` state + `Start` auto-opens when `activeInHierarchy`, subclasses overriding `Start` must call `base.Start()` last), `UiwSortToolbar` (`SetOptions`/`SetSortPriorities`), `UiwNumberCounter` (`Configure`/`SetRange`/`SetValue`), `UiwTextLabel`, `SpriteSlot.Bind(image, value)`.

### Object pool

A drop-in replacement for third-party pools like Lean.Pool — a GameObject/prefab pool plus a plain-C# class pool (`Ale.Toolkit.Runtime`).

```csharp
// static facade: auto-creates a pool per prefab; replaces Instantiate / Destroy
var go = ToolkitPool.Spawn(prefab, pos, Quaternion.identity, parent);
ToolkitPool.Despawn(go);            // routed back via the ownership table; Despawn(go, delay) too

// or hold a pool component explicitly
var pool = host.AddComponent<ToolkitGameObjectPool>();
pool.Prefab = prefab; pool.Preload = 3;
var clone = pool.Spawn(pos, rot, parent);

// plain-C# objects, lower GC (returns null when empty)
var ctx = ToolkitClassPool<Ctx>.Spawn() ?? new Ctx();
ToolkitClassPool<Ctx>.Despawn(ctx, c => c.Reset());
```

- `ToolkitGameObjectPool`: `Prefab` / `Preload` / `Capacity` / `Recycle` / `Persist` / `Notification`; `Spawn(...)` / `Despawn(clone, delay)` / `DespawnAll` / `Clear`.
- `IPoolable` (`OnSpawn` / `OnDespawn`); `ToolkitPool.Spawn/Despawn/DespawnAll/Detach`, ownership table `Links`; `ToolkitClassPool<T>.Spawn(...)/Despawn(...)`.

### Tween

A lightweight central tween facade (DOTween-style "single-Update polling job list", `Ale.Toolkit.Runtime`). It currently fades a `CanvasGroup`; jobs are pooled via `ToolkitClassPool` and driven by a persistent runner in a single `LateUpdate`, with near-zero GC. It does not reproduce DOTween's sequences / chaining / full easing set — extend incrementally as needed.

```csharp
// fade a CanvasGroup to alpha=1 over 0.2s; returns a killable handle
var h = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, 0.2f, EToolkitEase.OutQuad,
                                     unscaled: true, onComplete: () => { /* done */ });
h.Kill(complete: true);    // interrupt, snap to the end value and fire onComplete; Kill(false) interrupts without the callback
bool running = h.IsActive;
```

- `ToolkitTween.FadeCanvasGroup(target, endAlpha, duration, ease = OutQuad, unscaled = true, onComplete = null)`: with `duration ≤ 0` or a null target it snaps into place and returns an empty handle.
- `ToolkitTweenHandle` (value type, zero-alloc): `IsActive` / `Kill(complete = false)`; `default` is an invalid handle whose `Kill` is a safe no-op.
- `ToolkitEase.Evaluate(EToolkitEase ease, float t)`; easing types `EToolkitEase`: `Linear` / `InQuad` / `OutQuad` / `InOutQuad`.

### Attribute modifier

GAS-style modifier evaluation (`Ale.Toolkit.Runtime`). Declarative `ModifierDefinition`s feed into one attribute, and `ModifierStackEvaluator` settles them by group in a fixed order to produce "the current value + a per-source breakdown". Static, stateless, no Unity dependency; it does **not** include the runtime loop for duration expiry / stacking (the config carries those; the host settles them at runtime and feeds in the effective modifiers).

```csharp
var mods = new List<ModifierDefinition> {
    new ModifierDefinition("atk", EModifierOperation.Add,        5f,   "trait:Brave"),
    new ModifierDefinition("atk", EModifierOperation.PercentAdd, 0.1f, "buff:Berserk"),
};
// base 10, clamp[0,100]; settlement: 10 → +5 → ×(1+0.1) = 16.5
ModifierEvaluation r = ModifierStackEvaluator.Evaluate(10f, 0f, 100f, mods);
float now = r.Value;                              // 16.5
foreach (var c in r.Breakdown)                    // per source: SourceTag / Operation / Magnitude / Delta
    Debug.Log($"{c.SourceTag} {c.Operation} {c.Delta}");
```

- `ModifierDefinition`: `targetAttributeId` (an opaque key the evaluator does not interpret) / `operation` / `magnitude` / `duration` / `durationDays` / `sourceTag` (source breakdown + grouped removal) / `stackLimit` / `stackRule`.
- `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown = true)` → `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`; the lightweight `EvaluateValue(...)` returns only the final value. The settlement order is fixed: `base → +ΣAdd → ×(1+ΣPercentAdd) → per-item ×(1+magnitude) Multiply → final Override → clamp[min,max]`. The caller must group by `targetAttributeId` first; duration / stacking are settled at runtime before being passed in.
- Enums: `EModifierOperation` (`Add`/`PercentAdd`/`Multiply`/`Override`), `EModifierDuration` (`Instant`/`Timed`/`Permanent`), `EStackRule` (`Refresh`/`Add`/`EveryXStacks`/`OnMaxStacks`).

### Condition System

Data-driven two-level AND/OR conditions (namespace `Ale.Condition`). Core idea: **declare a `ConditionExpression` field and a two-level condition editor appears inline in the Inspector**; upper-layer systems implement their own "atomic evaluators" that are auto-discovered, while the core knows no domain concepts. The engine-agnostic Core (`noEngineReferences`) is server-side ready. Three assemblies: `Ale.Condition.Core` (model + engine + registry + JSON) / `.Runtime` (Unity bridge) / `.Editor` (inline drawer + catalog + Welcome window).

**① Declare a condition field (config side, zero UI code)**

```csharp
// any MonoBehaviour / ScriptableObject / serializable config class
public ConditionExpression eligibility = new ConditionExpression();
```

In a custom Inspector, call `EditorGUILayout.PropertyField(prop, true)` on its `SerializedProperty` to get the full two-level AND/OR editor (add/remove groups / items / params, And·Or, NOT, an evaluator-category dropdown, a schema-driven dynamic parameter area), with automatic Undo. Or use the SO container `ConditionAsset` (`Create > Ale > Condition > Condition Asset`).

**② Extend evaluators (upper-layer implementation)**

```csharp
using Ale.Condition;

public interface IMyStatSource { float Get(string statId); }   // upper-layer custom read-side service (engine-agnostic)

[ConditionEvaluator("My.StatAtLeast")]
public sealed class StatAtLeastEvaluator : IConditionEvaluator
{
    private static readonly ConditionParamDef[] Schema = {
        new ConditionParamDef("stat",  ConditionParamType.String, false, "Stat"),
        new ConditionParamDef("value", ConditionParamType.Float,  false, "Threshold"),
    };
    public string Key => "My.StatAtLeast";
    public string DisplayName => "Stat at least";
    public string Category => "My";                            // editor dropdown grouping
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

`ParamSchema` drives the editor's dynamic parameter area; fixed options (such as comparison operators) use `ConditionParamDef`'s `choices` (rendered as a dropdown, storing the index). Five parameter types: `String` / `Int` / `Float` / `Bool` / `Enum` (+ `isArray`).

**③ Provide context + evaluate (runtime)**

```csharp
class MyCtx : IConditionContext {              // subject + service bag (host-implemented)
    public object Subject { get; set; }
    private readonly object[] _svc;
    public MyCtx(params object[] svc) { _svc = svc; }
    public T GetService<T>() where T : class { foreach (var s in _svc) if (s is T t) return t; return null; }
}

var ctx = new MyCtx(myStatSource);
bool ok = expr.Evaluate(ctx).Passed;                        // convenience method
ConditionResult r = ConditionEngine.Evaluate(expr, ctx);    // or call the engine directly; r.FailedKeys lists the unmet keys
```

At runtime `ConditionRuntime` fills `ConditionRegistry.Default` via reflection in `[RuntimeInitializeOnLoadMethod]` and wires up missing-key warnings; server-side / tests can manually `new ConditionRegistry()` + `AutoRegisterFromAssemblies()` or `Register` one by one.

**Built-in evaluators**: `Condition.AlwaysTrue`, `Condition.HasFlag` (`IConditionFlagSource`), `Condition.NumberCompare` (`IConditionNumberSource`). **JSON**: `ConditionJson.ToJson(expr)` / `FromJson(str)` (Newtonsoft; the model is a plain POCO — swap the serializer, persist to a save/database). **Overview**: `Tools > Ale Toolkit > Condition System > Welcome`.

### Effect System

The **write-side mirror** of the Condition System (namespace `Ale.Effect`): data-driven, parameterized **discrete trigger-style mutations**, organized by "phase groups", each item optionally gated by a condition. Numeric bonuses (buffs) are handled by the **attribute modifiers** above; effects do only discrete actions (grant / remove, set flag, raise event, ignite…). Likewise "declare an `EffectExpression` field to configure it in the Inspector", and upper-layer `[EffectExecutor]` executors are auto-discovered. Three assemblies: `Ale.Effect.Core` (references `Ale.Condition.Core` for gating) / `.Runtime` / `.Editor`.

**Structure**: `EffectExpression → EffectGroup(phase timing tag) → EffectItem(key + params + optional gate)`. A single field can hold multiple phase groups (e.g. `onGained` / `onLost`); items within a group **execute in order**, and the runtime filters by `phase` (an empty-phase group is a wildcard that runs for any phase).

**① Declare an effect field**

```csharp
public EffectExpression onGained = new EffectExpression();   // inline phase-group editor in the Inspector; or use an EffectAsset SO
```

**② Extend executors (upper-layer implementation, with an "ignite" example)**

```csharp
using Ale.Effect;

public interface ICombatEffectSink { void Ignite(float radius, int mode, int count); }

[EffectExecutor("Combat.Ignite")]
public sealed class IgniteEffect : IEffectExecutor
{
    private static readonly EffectParamDef[] Schema = {
        new EffectParamDef("radius", EffectParamType.Float, false, "Diameter (m)"),
        new EffectParamDef("target", EffectParamType.Int,   false, "Target selection",
            choices: new[] { "Random", "Nearest", "Farthest" }),  // fixed enum → dropdown stores the index
        new EffectParamDef("count",  EffectParamType.Int,   false, "Target count"),
    };
    public string Key => "Combat.Ignite";
    public string DisplayName => "Ignite";
    public string Category => "Combat";
    public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

    public EffectResult Execute(IReadOnlyList<EffectParam> ps, IEffectContext ctx)
    {
        var sink = ctx?.GetService<ICombatEffectSink>();
        if (sink == null) return EffectResult.Failed("missing ICombatEffectSink");
        sink.Ignite((float)ps.Find("radius").GetFloat(),
                    (int)ps.Find("target").GetInt(),
                    (int)ps.Find("count").GetInt());
        return EffectResult.Applied;
    }
}
```

**③ Context + execution (runtime)**

```csharp
// IEffectContext : IConditionContext — one context both serves the gate condition's read services and the effect's write sinks
class MyEffectCtx : IEffectContext {
    public object Subject { get; set; }
    private readonly object[] _svc;
    public MyEffectCtx(params object[] svc) { _svc = svc; }
    public T GetService<T>() where T : class { foreach (var s in _svc) if (s is T t) return t; return null; }
}

var ctx = new MyEffectCtx(combatSink, myFlagSource /* for the gate */);
EffectRunReport rep = onGained.Run(ctx, phase: "onGained");     // or EffectRunner.Run(onGained, ctx, "onGained")
Debug.Log($"applied {rep.Applied} / skipped {rep.Skipped} / failed {rep.Failed}");
```

If an item has a gate (an embedded `ConditionExpression`, configured inline in the editor), the runner first evaluates it via `ConditionEngine` and marks the item `Skipped` when unmet. At runtime `EffectRuntime` auto-registers all executors in `[RuntimeInitializeOnLoadMethod]`.

**Built-in executors**: `Effect.NoOp`, `Effect.SetFlag` (`IEffectFlagSink`), `Effect.AdjustNumber` (`IEffectNumberSink`) — the write-side duals of the Condition System's `HasFlag` / `NumberCompare` respectively. **JSON**: `EffectJson.ToJson/FromJson` (embedded gates round-trip with the graph). **Overview**: `Tools > Ale Toolkit > Effect System > Welcome`.

> **Boundary with UE5 GAS**: The numeric side of GAS's `GameplayEffect` (Modifiers / Duration / Stacking) is covered by the **attribute modifiers** above; the Effect System corresponds to its execution side (Executions / Cues / Conditional Effects) — discrete triggered actions. The division is clean: **modifiers manage "values", effects manage "events"**.

### Editor framework

`Ale.Toolkit.Editor`, all generic over the database type — derive and override a few abstract members to build an editor.

- **Database window shell** `EditorDatabaseWindowBase<TDb>`: built-in "DB-asset object field + top tab strip + validate / export button hooks + duplicate-scan orchestration + status bar + Undo subscription + last-DB-path memory (EditorPrefs)", implementing `IEditorDbContext<TDb>`; a host window becomes much thinner by supplying only its tab set / export·validate callbacks / duplicate-scan kinds.
- **Three-column tab** `EditorThreeColumnTab<TDb,TEntity>`: left sub-tabs + master list, middle entity list, right context inspector. Override `LeftPanels` / `EntityNoun` / `EntityList` / `DrawEntityList` / `DrawEntityInspector`, etc.
- **Master list panel** `EditorMasterListPanel<TDb,T>` (+ `IEditorMasterListPanel<TDb>`), **entity list panel** `EditorEntityListPanel<TDb,TEntity,TTemplate>`.
- **Tool window base** `EditorToolWindowBase<TDb>`: built-in "pick database + per-frame time-budget stepping + progress bar + log + cancel + completion". Override `DrawOperations` (start per-frame steps via `RunSteps`) / `OnRunComplete` / `OnRunFinished`.
- Contexts `IEditorContext` / `IEditorDbContext<TDb>`; helper controls `EditorSearchableList` / `EditorDraggableRowList` / `EditorReorderableDrag` / `EditorListKeyboardNav` / `EditorFilterTabs` / `EditorIdScanner` / `ToolkitEditorStyles`.

### Editor localization

Trilingual (中 / English / 日本語) service for editor UI text, keyed by the Chinese source string, falling back to Chinese when untranslated. Unrelated to runtime content localization.

```csharp
using static Ale.Toolkit.Editor.ToolkitEditorL10n;
EditorGUILayout.LabelField(Tr("快捷操作"));       // returns text for the current language
string name = TrEnum(EFieldType.Sprite);          // enum display name

// a host plugin registers its domain tables in [InitializeOnLoad]
ToolkitEditorL10n.Add("道具", "Item", "アイテム");
ToolkitEditorL10n.AddEnum(MyEnum.Foo, "Foo", "フー");
```

- `ToolkitEditorL10n.Tr(zh)` / `TrEnum(enumValue)`; `Current` (`EditorLanguage`) / `TranslateEnums`; `Add(zh, en, ja)` / `AddEnum(value, en, ja, zh = null)`.

### Optional dependency support

Macro toggles and runtime adapters for TextMeshPro / Unity Localization / Addressables. The macros are project-level globals (`ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`), toggled from the Welcome window; legacy `IS_*` macros auto-migrate on load.

- `ToolkitDefines`: macro-name constants `Tmp` / `Localization` / `Addressable`; `IsTmpEnabled()` / `IsLocalizationEnabled()` / `IsAddressableEnabled()`.
- `DefineUtils`: `ApplyDefine(...)` (add/remove PlayerSettings scripting defines), `HasNamespace(...)` / `HasClass(...)` (detect whether a package is installed) — build your own macro-toggle panel with these.
- Runtime asset facade `ToolkitAssets` (zero Addressables dependency in core): `Bind<T>(value, owner, set)` / `Bind<T>(liveRef, address, owner, set)` (auto-released when the owner is destroyed), `Load<T>` / `Release`; interface `IAssetLoader`; with `ATK_ADDRESSABLE`, `AddressableManager` does ref-counted load/unload by address.

### Editor entry & global settings

- `ToolkitWelcomeWindow` (menu **Tools > Ale Toolkit > Welcome**): editor UI language / enum-translation toggle / the three optional-dependency macros / wizard default & localized fonts / general-tool entries / auto-show on startup.
- `ToolkitProjectSettings` (`ScriptableSingleton`, saved to `ProjectSettings/AleToolkitSettings.asset`, committed with the repo, asset references by GUID): `SaveSettings()`; wizard fonts are read/written through the `ToolkitPrefabFonts` facade.

### General tool windows

Walk every `AttributeValue` of any data asset (`ScriptableObject`) for batch processing; reusable by upper-layer plugins.

- `ToolkitAddressableToolWindow` (menu **Tools > Ale Toolkit > Addressable**): batch-convert all asset fields of a database between "Object reference ↔ AssetReference (GUID)". A host can derive `EditorAddressableToolWindow<TDb>` and supply named Sprite fields outside the attribute system via `FixedFields`.
- `ToolkitLocalizationToolWindow` (menu **Tools > Ale Toolkit > Localization**): batch-generate localization keys; base class `EditorLocalizationToolWindow<TDb>`.
- Reflection helpers: `AttributeValueWalker` (walks all attribute object values in a database), `TextFieldWalker` / `TextFieldCollector` (walk text values, id-aware keys).

---

## License

[MIT](LICENSE.md)
