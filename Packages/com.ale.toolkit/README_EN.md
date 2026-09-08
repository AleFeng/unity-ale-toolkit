# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

A **general-purpose foundation library** for Unity plugin development. It carries no business-domain concepts, letting several plugins share one attribute-configuration system, list engine, editor framework and localization service.

> This package was split out of `com.ale.inventory` 1.8.0. The general-purpose capabilities that used to live inside the inventory system (the three-column editor framework, virtual scrolling lists, the custom attribute system, the trilingual editor UI) were extracted here so more plugins can reuse them.

---

## Table of Contents

- [Modules](#modules)
- [Assemblies](#assemblies)
- [Usage & Main APIs](#usage--main-apis)
  - [Attribute system](#attribute-system) · [Sorting](#sorting) · [UI](#ui) · [Object pool](#object-pool) · [Tween](#tween)
  - [Attribute modifier](#attribute-modifier) · [GameplayTag System](#gameplaytag-system) · [Condition System](#condition-system) · [Effect System](#effect-system)
  - [Editor framework](#editor-framework) · [Editor localization](#editor-localization) · [Optional dependency support](#optional-dependency-support) · [Editor entry & global settings](#editor-entry--global-settings) · [General tool windows](#general-tool-windows)
- [License](#license)

---

## Modules

| Module | Contents |
| --- | --- |
| **Attribute system** | `AttributeValue` with 20+ field types, attribute definitions (schema), custom enum types, number-format configs, and the lightweight display text `TextValue` (fallback + optional native localization). Use it wherever configurable attribute entries are needed |
| **Sorting** | An element-type-agnostic sort engine: the host implements `ISortContext<TData>` to supply what comparison needs, the engine handles multi-level priorities and tiebreakers |
| **UI** | Virtual scrolling lists (grid / sequential, object pool + visible-region-only rendering; cell assign / recycle fade-in-out driven generically by `UiwListFadeCell` + the engine's default hooks), tab strips, filter bars, tooltip base classes, widget pools |
| **Object pool** | A general-purpose GameObject/prefab pool (`Spawn`/`Despawn` + `IPoolable` callbacks; preload / capacity-recycle / delayed despawn / cross-scene) plus a plain-C# reference-type pool `ToolkitClassPool<T>` (lower GC) — a drop-in replacement for third-party pools like Lean.Pool |
| **Tween** | A lightweight central tween (DOTween-style single-Update polling, pooled jobs, near-zero GC): `FadeCanvasGroup` / `FadeGraphic` / `FadeSpriteRenderer` for alpha, `TintGraphic` for full colour, `MoveTransform` / `RotateTransform` / `ScaleTransform`, `DelayedCall`, and `Kill(target)` to kill by target; returns a killable value-type handle; minimal easing set `EToolkitEase` |
| **Attribute modifier** | GAS-style modifier evaluation (engine-agnostic assembly `Ale.Modifier.Core`, namespace `Ale.Modifier`): `ModifierDefinition` + `ModifierStackEvaluator` settle by group (Add→PercentAdd→Multiply→Override + clamp + source breakdown). Use it for any "base value + a stack of bonuses → current value" numeric convergence; the Effect System's duration modifiers produce it directly |
| **GameplayTag System** | UE-GameplayTag-style hierarchical tags: `GameplayTag` (`Status.Debuff.Mental` — owning a descendant matches the ancestor), the config container `GameplayTagContainer`, the runtime `GameplayTagCountContainer`, `GameplayTagRequirements`, an advisory registry + tag-table assets + an editor tag-tree dropdown; condition bridge `Condition.HasGameplayTag` / `Condition.GameplayTags` |
| **Condition System** | Data-driven two-level AND/OR conditions: declare a `ConditionExpression` field to configure it inline in the Inspector; upper layers implement `[ConditionEvaluator]` evaluators that are auto-discovered, and can reuse the ready-made comparison-operator kit and evaluation context. The engine-agnostic Core is server-side ready |
| **Effect System** | A complete UE5-GAS-`GameplayEffect`-style effect system: `EffectDefinition` (duration policy / period / stacking / tags / application condition & chance / modifiers / per-phase executions / cues) + the runtime `EffectContainer` (application pipeline, Tick, inhibition, immunity, remove-by-tag, modifier collection, save state); the execution layer is still data-driven phase groups + an optional per-item condition gate, with upper-layer `[EffectExecutor]` executors auto-discovered. Engine-agnostic Core; **since 1.10.0** it ships the effect library `EffectDatabase` shared by every upper system (effect entries = display name / description / icon + template-driven custom attributes + GAS definition; templates / gameplay tags / enums) and the Effect Editor — upper systems keep only effect-id reference lists + a jump button |
| **Editor framework** | Three-column tab base class, database window shell base class, master list panel, entity list panel and tool window base — all generic over the database type |
| **Editor localization** | 中文 / English / 日本語 service, keyed by the Chinese source string, falling back automatically when a translation is missing |
| **Optional dependency support** | Macro toggles and adapters for TextMeshPro (`ATK_TMP`), Unity Localization (`ATK_LOCALIZATION`) and Addressables (`ATK_ADDRESSABLE`) |
| **Editor entry & global settings** | The Ale Toolkit Welcome Window (`Tools > Ale Toolkit > Welcome`): editor UI language / enum translation / the three optional feature macros / wizard default & localized fonts + general-tool entries + an "auto-show on startup" toggle; project-level settings such as the wizard fonts are saved to `ProjectSettings/AleToolkitSettings.asset` (committed with the repo, asset references stored by GUID), while language / auto-show are per-user (EditorPrefs); macros are only ever toggled explicitly from the Welcome window — the package never rewrites PlayerSettings on its own |
| **General tool windows** | Walk every `AttributeValue` of any data asset (`ScriptableObject`) for batch processing: Addressable migration (Object ↔ GUID) and localization key generation, under `Tools > Ale Toolkit`, reusable by upper-layer plugins |

> All modules above are in place — since 1.1.0 the three optional-dependency support layers (TMP / Localization / Addressables) are complete and the editor UI is trilingual even in a toolkit-only project; **since 1.2.0 it owns the project-level global settings (language / macros) and provides general tool windows that work on any data asset**; **since 1.3.0 it adds a general-purpose object pool (GameObject pool + plain-C# class pool) and a lightweight central tween**; **since 1.4.0 it adds attribute-modifier evaluation, a database window shell base class, and two independent subsystems — the Condition System (`Ale.Condition`) and the Effect System (`Ale.Effect`)**; **since 1.5.0 it adds the lightweight display-text value `TextValue` (fallback + optional native localization — a standalone lightweight version of `AttributeValue`'s `Text` type)**; **since 1.5.1 it adds generic cell fade-in/out for the virtual-scroll list (`UiwListFadeCell` + `IUiwRecycleFadeCell` / `IUiwDiffCell`, driven by `UiwVirtualListBase`'s default hooks) plus `ToolkitTween.FadeGraphic`**; **since 1.6.0 the central tween gains `SpriteRenderer` fading, `Graphic` full-colour tinting, `Transform` move / rotate / scale, delayed callbacks and kill-by-target, so it can take over DOTween's common single-tween usage (still no sequences)**; **since 1.8.0 the Condition System moves three "every host writes its own" facilities into Core — the comparison-operator kit `ConditionCompare`, the general-purpose contexts `ConditionContext` / `SubjectConditionContext`, and the idempotent `ConditionRegistry.EnsureAutoRegistered()`**; **since 1.9.0 it adds the hierarchical GameplayTag System (`Ale.GameplayTags`), completes the Effect System into a full GAS `GameplayEffect` (`EffectDefinition` + `EffectContainer`), and extracts attribute modifiers into the engine-agnostic `Ale.Modifier.Core` (⚠️ namespace is now `Ale.Modifier`)**; **since 1.10.0 it adds the effect library `EffectDatabase` shared by every upper system plus the Effect Editor (effect entries carry a display name / description / icon and template-driven custom attributes; upper systems keep only effect-id reference lists + a jump button; ⚠️ `Ale.Effect.Runtime` now depends on `Ale.Toolkit.Runtime` / `Ale.GameplayTags.Runtime`)**; **since 1.11.0 the Effect Editor gains an Effect Executors tab — a catalog of every `[EffectExecutor]` implementation in the project (search / category / param schema / jump to source / silent-failure diagnostics / configuration cross-reference); the original tab is renamed Effect Database and moved to second place, and the editor window shell gains a per-tab “needs a database” hook plus tab memory**. See the [CHANGELOG](CHANGELOG.md) for details.

---

## Assemblies

| Assembly Definition | Purpose | Macro constraint |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | Attribute system, sorting, asset-loading abstraction, shared serialization, object pool, central tween | — |
| `Ale.Toolkit.UI` | Virtual scrolling lists and general UI widgets | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization adapter components | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables loading and handle management | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | Editor framework, database window shell base class, attribute drawers, localization service, macro toggles | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables editor tooling | `ATK_ADDRESSABLE` |
| `Ale.Modifier.Core` | Attribute modifier · `ModifierDefinition` / `ModifierStackEvaluator` / the three enums (`noEngineReferences`; extracted from `Ale.Toolkit.Runtime` in 1.9.0, namespace `Ale.Modifier`) | — |
| `Ale.GameplayTags.Core` | GameplayTag System · engine-agnostic model: tag / container / count container / requirements / registry (`noEngineReferences`, no references) | — |
| `Ale.GameplayTags.Condition` | GameplayTag System · condition bridge: built-in evaluators `Condition.HasGameplayTag` / `Condition.GameplayTags` | References `Ale.Condition.Core` + `Ale.GameplayTags.Core` |
| `Ale.GameplayTags.Runtime` | GameplayTag System · Unity bridge (`GameplayTagTable` asset + registration on startup + `[GameplayTagField]`) | — |
| `Ale.GameplayTags.Editor` | GameplayTag System · catalog / tag-tree dropdown / container & requirements drawers / table inspector / Welcome window | — |
| `Ale.Condition.Core` | Condition System · engine-agnostic model / evaluation engine / registry & reflection discovery / JSON, plus the reusable comparison-operator kit `ConditionCompare` and the general-purpose `ConditionContext` (`noEngineReferences`, server-side ready) | References Newtonsoft |
| `Ale.Condition.Runtime` | Condition System · Unity bridge (`ConditionAsset` + auto-register on startup) | — |
| `Ale.Condition.Editor` | Condition System · inline drawer / catalog / Welcome window | — |
| `Ale.Effect.Core` | Effect System · engine-agnostic model (`EffectDefinition` / `EffectExpression`) / runtime `EffectContainer` / execution runner / registry & reflection discovery / JSON (`noEngineReferences`) | References `Ale.Condition.Core` + `Ale.GameplayTags.Core` + `Ale.Modifier.Core` + Newtonsoft |
| `Ale.Effect.Runtime` | Effect System · Unity bridge: the shared effect library `EffectDatabase` (entries / templates / gameplay tags / enums) + `EffectDataManager` + `EffectConfigSerializer` (JSON / binary), `EffectAsset` / `EffectDefinitionAsset`, startup bootstrap (reset + additive) | References `Ale.Toolkit.Runtime` + `Ale.GameplayTags.Runtime` (since 1.10.0) |
| `Ale.Effect.Editor` | Effect System · Effect Editor (effects / templates / tags / enums), effect catalog `EffectEditorCatalog`, host-side reference-list drawer `EditorEffectRefListDrawer`, definition / magnitude / modifier / expression drawers, attribute-id catalog providers, Welcome window | References `Ale.Toolkit.Editor` + `Ale.GameplayTags.Editor` (since 1.10.0) |

Dependencies flow one way: host plugin → `Ale.Toolkit.*` / `Ale.Modifier.*` / `Ale.GameplayTags.*` / `Ale.Condition.*` / `Ale.Effect.*`; this package never references a host plugin. Each subsystem has its own namespace (`Ale.Modifier` / `Ale.GameplayTags` / `Ale.Condition` / `Ale.Effect`); `Ale.Modifier.Core`, `Ale.GameplayTags.Core` and `Ale.Condition.Core` never reference one another, while `Ale.GameplayTags.Condition` and `Ale.Effect.Core` are the convergence points (layering: tags < conditions < effects). Since 1.10.0 `Ale.Effect.Runtime` references `Ale.Toolkit.Runtime` (the effect library needs the attribute system) and `Ale.GameplayTags.Runtime`, and `Ale.Effect.Editor` references `Ale.Toolkit.Editor` / `Ale.GameplayTags.Editor`; `Ale.Toolkit.*` never references a subsystem, every `*.Core` stays engine-agnostic, and the graph stays acyclic.

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

A lightweight central tween facade (DOTween-style "single-Update polling job list", `Ale.Toolkit.Runtime`). It fades the alpha of a `CanvasGroup` / `Graphic` (Image / text) / `SpriteRenderer`, tints a `Graphic`'s full colour, moves / rotates / scales a `Transform`, and schedules plain delayed callbacks. Jobs are pooled via `ToolkitClassPool` and driven by a persistent runner in a single `LateUpdate`, with near-zero GC. It does not reproduce DOTween's sequences / chaining / full easing set — extend incrementally as needed.

```csharp
// fade a CanvasGroup to alpha=1 over 0.2s; returns a killable handle
var h = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, 0.2f, EToolkitEase.OutQuad,
                                     unscaled: true, onComplete: () => { /* done */ });
h.Kill(complete: true);    // interrupt, snap to the end value and fire onComplete; Kill(false) interrupts without the callback
h.Complete();              // same as Kill(true)
bool running = h.IsActive;

// sprite fade / smooth actor move / delayed callback
ToolkitTween.Kill(spriteRenderer);                       // kill in-flight jobs on that target first (no overwrite management here)
ToolkitTween.FadeSpriteRenderer(spriteRenderer, 1f, 0.3f);
ToolkitTween.MoveTransform(actor, targetPos, duration, EToolkitEase.InOutQuad);
ToolkitTween.Kill(actor, complete: true);                // equivalent to DOTween's transform.DOComplete()
var delay = ToolkitTween.DelayedCall(1.5f, () => Play(), owner: this);
```

- Fade / colour: `FadeCanvasGroup(target, endAlpha, duration, ease = OutQuad, unscaled = true, onComplete = null)`, `FadeGraphic(…)`, `FadeSpriteRenderer(…)`, `TintGraphic(target, endColor, …)` (full RGBA).
- Transform: `MoveTransform(target, endPosition, …)`, `RotateTransform(target, endEulerAngles, …)`, `ScaleTransform(target, endScale, …)`. Rotation takes the **shortest arc per axis** (equivalent to DOTween's `RotateMode.Fast`; multi-turn spins are not supported); the conversion itself is exposed as `ShortestEuler(fromEuler, toEuler)`.
- Delay: `DelayedCall(delay, onComplete, unscaled = true, owner = null)`. The optional `owner` binds lifetime: once it is destroyed the callback is dropped, and `Kill(owner)` cancels it.
- Kill: `Kill(target, complete = false)` kills every in-flight job on that target and returns how many — the equivalent of DOTween's target registry (`DOKill` / `DOComplete`). Matching is by **reference equality**, so a destroyed target can still clean up its own jobs, and `Kill(gameObject)` will not find jobs on components attached to it.
- `ToolkitTweenHandle` (value type, zero-alloc): `IsActive` / `Kill(complete = false)` / `Complete()`; it implements `IEquatable<>` and `==`, so it goes straight into a `List<>` and supports `Remove`. `default` is an invalid handle whose `Kill` / `Complete` are safe no-ops.
- `ToolkitEase.Evaluate(EToolkitEase ease, float t)`; easing types `EToolkitEase`: `Linear` / `InQuad` / `OutQuad` / `InOutQuad`.
- Every entry point snaps into place and returns an empty handle when `duration ≤ 0` or the target is null.

Three differences from DOTween are worth noting: **(1) no overwrite management** — starting another tween on the same target and channel does not kill the previous one (DOTween behaves the same), so call `Kill(target)` first; **(2) `unscaled` defaults to `true`**, whereas DOTween is affected by `Time.timeScale` by default — pass `unscaled: false` to restore DOTween's behaviour; **(3) `DelayedCall(delay ≤ 0)` fires synchronously** (DOTween defers a frame), so guard list bookkeeping with `if (h.IsActive) list.Add(h);`.

### Attribute modifier

GAS-style modifier evaluation (assembly `Ale.Modifier.Core`, namespace `Ale.Modifier`; **before 1.9.0 it lived in `Ale.Toolkit.Runtime`** — after upgrading, add the asmdef reference and switch to `using Ale.Modifier;`; stored data is unaffected). Declarative `ModifierDefinition`s feed into one attribute, and `ModifierStackEvaluator` settles them by group in a fixed order to produce "the current value + a per-source breakdown". Static, stateless, no Unity dependency; it does **not** include the runtime loop for duration expiry / stacking — that is the [Effect System](#effect-system)'s job: a duration effect's `EffectContainer.CollectModifiers` produces exactly this type, and the host feeds it into the evaluator together with its other sources.

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

- `ModifierDefinition`: `targetAttributeId` (an opaque key the evaluator does not interpret) / `operation` / `magnitude` / `sourceTag` (source breakdown + grouped removal); plus four config-only inert fields `duration` / `durationDays` / `stackLimit` / `stackRule` — neither the evaluator nor the Effect System reads them; duration / period / stacking are carried by `EffectDefinition`.
- `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown = true)` → `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`; the lightweight `EvaluateValue(...)` returns only the final value. The settlement order is fixed: `base → +ΣAdd → ×(1+ΣPercentAdd) → per-item ×(1+magnitude) Multiply → final Override → clamp[min,max]`. The caller must group by `targetAttributeId` first; duration / stacking are settled at runtime before being passed in.
- Enums: `EModifierOperation` (`Add`/`PercentAdd`/`Multiply`/`Override`), `EModifierDuration` (`Instant`/`Timed`/`Permanent`), `EStackRule` (`Refresh`/`Add`/`EveryXStacks`/`OnMaxStacks`).

### GameplayTag System

UE-GameplayTag-style hierarchical tags (namespace `Ale.GameplayTags`; assemblies `Ale.GameplayTags.Core` / `.Condition` / `.Runtime` / `.Editor`). Dotted names express the hierarchy: `Status.Debuff.Mental` matches `Status.Debuff` and `Status` (**owning a descendant matches the ancestor**); a bare prefix does not count (`AB` does not match `A`). Ordinal, case-sensitive — consistent with every other string key in the toolkit; the normalization rules (trim the whole and each segment; empty segments, whitespace inside a segment and `/` are invalid) are a data format, frozen at release and pinned by tests.

```csharp
using Ale.GameplayTags;

// Config side: the container stores List<string> and round-trips through Unity / Newtonsoft as-is; [GameplayTagField] gives a string field the tag-tree dropdown
public GameplayTagContainer assetTags = new GameplayTagContainer();
[GameplayTagField] public string cueTag;

// Runtime: the owner's count container (explicit counts + implicit ancestor counts, O(1) hierarchical queries)
var owned = new GameplayTagCountContainer();
owned.AddTag(new GameplayTag("Status.Debuff.Mental"));
bool mental = owned.HasMatchingTag(new GameplayTag("Status.Debuff"));   // true: a descendant is owned
owned.OnTagCountChanged += (tag, count) => { /* the effect container re-evaluates inhibition on this */ };

// Requirements: must own all requireTags and none of the ignoreTags
var req = new GameplayTagRequirements();
req.requireTags.AddTag("State.Alive");
req.ignoreTags.AddTag("Immunity.Mental");
bool ok = req.IsMet(owned);
```

- `GameplayTag` (readonly struct, **never serialized**): `IsValid` / `Depth` / `Parent` / `Root` / `Leaf`, `MatchesTag(parent)` / `MatchesTagExact` / `IsDescendantOf`, `Normalize` / `TryParse` / `Parse`.
- `GameplayTagContainer` (`[Serializable]`, its only field is `List<string> tags`): `AddTag` / `RemoveTag` (exact) / `RemoveTagsMatching` (subtree), `HasTag` (hierarchical) / `HasTagExact` / `HasAny` / `HasAll` (empty set: All is true, Any is false) / `Filter`, `Normalize` / `Clone`.
- `GameplayTagCountContainer` (runtime): `AddTag/RemoveTag(tag, count)`, `AddTags/RemoveTags(container)`, `HasMatchingTag` / `HasExactTag` / `GetTagCount`, `GetExplicitTags`, event `OnTagCountChanged`.
- `GameplayTagRequirements`: `requireTags` / `ignoreTags`, `IsMet(...)`, `Validate` (errors on require ∩ ignore).
- **The registry is advisory**: `GameplayTagRegistry.Default` (registering auto-adds ancestors; `Validate` catches unregistered names and case-only typos) only serves the editor dropdown and config-time validation — **runtime matching never consults the registry**, unregistered tags match as usual. Sources: `GameplayTagTable` assets under `Resources` (`Create > Ale > GameplayTag > Gameplay Tag Table`, registered on startup) and explicit `GameplayTagRuntime.Register(...)` by the host (e.g. custom tags in a database).
- **Condition bridge** (`Ale.GameplayTags.Condition`): built-in evaluators `Condition.HasGameplayTag(tag, exact)` and `Condition.GameplayTags(tags[], any / all / none, exact)`; the subject's tags are resolved through the context's `IGameplayTagSource` service or `Subject as IGameplayTagOwner`. **No separate TagQuery** — and/or/not composition is left to `ConditionExpression`. The bridge is its own assembly rather than a reference from `Ale.Condition.Core`, preserving that assembly's "zero references" promise.
- Editor: drawers for `GameplayTagContainer` / `GameplayTagRequirements` / `[GameplayTagField]` (tag-tree dropdown; red tint for invalid names, yellow for unregistered ones), a tag-table inspector (validate / sort), `Tools > Ale Toolkit > GameplayTag System > Welcome`.
- Naming note: the namespace is the plural `Ale.GameplayTags` — with `Ale.GameplayTag`, writing `GameplayTag t` inside `namespace Ale.*` would resolve to the namespace first (CS0118); the attribute is `[GameplayTagField]` to avoid ambiguity with the type (CS1614).

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

`ParamSchema` drives the editor's dynamic parameter area; five parameter types: `String` / `Int` / `Float` / `Bool` / `Enum` (+ `isArray`). Fixed options use `ConditionParamDef`'s `choices` (rendered as a dropdown, storing the index) — for comparison operators **just use the ready-made `ConditionCompare`** instead of declaring your own label array:

```csharp
ConditionCompare.CreateOpParam()                       // equivalent to an "op" param with choices = ConditionCompare.Labels
int  op = ConditionCompare.ReadOp(ps);                 // defaults to "greater or equal"
bool ok = ConditionCompare.Compare(cur, need, op);     // exact for integers; the double overload takes an epsilon (default 1e-6)
```

> ⚠️ The **text and order of `ConditionCompare.Labels` are a wire format**: the index is serialized into condition assets, and the label itself may reach the consumer's scripts as a string (e.g. dialogue conditions bridged through VN Framework). **Do not localize it, do not reorder it.**

**③ Provide context + evaluate (runtime)**

```csharp
var ctx = new ConditionContext();                           // general-purpose context: register domain services by type
ctx.RegisterService<IMyStatSource>(myStatSource);

bool ok = expr.Evaluate(ctx).Passed;                        // convenience method
ConditionResult r = ConditionEngine.Evaluate(expr, ctx);    // or call the engine directly; r.FailedKeys lists the unmet keys

// "does this object satisfy the condition?" — wrap it, without touching the shared context's Subject
bool okForHero = expr.Evaluate(new SubjectConditionContext(ctx, hero)).Passed;
```

`ConditionContext.GetService<T>()` looks up by **exact** `typeof(T)` (register by interface, retrieve by interface), and is deliberately non-`virtual` — it sits on the evaluation hot path. Hosts needing a custom resolution strategy (e.g. "custom first, built-in fallback") should **implement `IConditionContext` directly** rather than inherit. The toolkit also **deliberately ships no global default instance**: which context is "the default" is a host policy, so each host holds its own static instance.

At runtime `ConditionRuntime` fills `ConditionRegistry.Default` via reflection in `[RuntimeInitializeOnLoadMethod]` and wires up missing-key warnings; server-side / tests can manually `new ConditionRegistry()` + `AutoRegisterFromAssemblies()` or `Register` one by one. **Editor tooling evaluating outside play mode** sees an empty registry (`ConditionRuntime` has not run yet) — call the idempotent `ConditionRegistry.Default.EnsureAutoRegistered()` to cover that.

**Built-in evaluators**: `Condition.AlwaysTrue`, `Condition.HasFlag` (`IConditionFlagSource`), `Condition.NumberCompare` (`IConditionNumberSource`). **JSON**: `ConditionJson.ToJson(expr)` / `FromJson(str)` (Newtonsoft; the model is a plain POCO — swap the serializer, persist to a save/database). **Overview**: `Tools > Ale Toolkit > Condition System > Welcome`.

### Effect System

A UE5-GAS-`GameplayEffect`-style effect system (namespace `Ale.Effect`) in two layers: the **definition layer** `EffectDefinition` + the **runtime container** `EffectContainer` (since 1.9.0; the counterpart of GAS's GameplayEffect + the effect part of the ASC: duration / period / stacking / tags / immunity / inhibition / modifiers / save state), and the **execution layer** `EffectExpression` (since 1.4.0; the counterpart of GAS Executions: phase groups of discrete actions, each optionally gated by a condition). "Declare a field and configure it in the Inspector"; upper-layer `[EffectExecutor]` executors are auto-discovered. Three assemblies: `Ale.Effect.Core` (references `Ale.Condition.Core` / `Ale.GameplayTags.Core` / `Ale.Modifier.Core`; engine-agnostic) / `.Runtime` (hosts the shared effect library since 1.10.0; references `Ale.Toolkit.Runtime` + `Ale.GameplayTags.Runtime`) / `.Editor` (references `Ale.Toolkit.Editor` + `Ale.GameplayTags.Editor`). The execution layer comes first (a definition's `executions` field is exactly that), then definitions and the container.

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

**Built-in executors**: `Effect.NoOp`, `Effect.SetFlag` (`IEffectFlagSink`), `Effect.AdjustNumber` (`IEffectNumberSink`) — the write-side duals of the Condition System's `HasFlag` / `NumberCompare` respectively; `Effect.ApplyEffect(effectId, level)` / `Effect.RemoveEffectsWithTag(tag)` / `Effect.RemoveEffectById(effectId)` — effect composition and dispelling (container and definition resolved from the context). **JSON**: `EffectJson.ToJson/FromJson` (expressions) and `ToJson(EffectDefinition)/DefinitionFromJson` (embedded gates / conditions / tags round-trip with the graph). **Overview**: `Tools > Ale Toolkit > Effect System > Welcome`; **the full list of implemented executors lives on the Effect Editor's Effect Executors tab (since 1.11.0)** — after writing an executor you can confirm there that it is actually discovered, and double-click the row to jump back to its source.

**④ Effect definitions and the container (the GAS layer)**

An `EffectDefinition` is a reusable "what the effect looks like": `durationPolicy` (Instant / HasDuration / Infinite), `duration` / `period` + `executePeriodicOnApplication`, stacking (`stackingType` aggregate by source / by target, `stackLimit`, the duration-refresh / period-reset / expiration policies), tags (`assetTags` / `grantedTags` / `removeEffectsWithTags` / `grantedApplicationImmunityTags`, `applicationTagRequirements` / `ongoingTagRequirements`), `applicationCondition` (Condition), `chanceToApply`, `modifiers` (`EffectModifier`: attribute id + operation + `EffectMagnitude` — Scalable / AttributeBased / SetByCaller), `executions` (the `EffectExpression` above; phase constants `EffectPhases.OnApply / OnStack / OnPeriod / OnExpire / OnRemove`) and `cueTags`. Where definitions live: **prefer the shared effect library `EffectDatabase` from 1.10.0 (see ⑤)**; a host that builds its own database must keep definitions in a **top-level list** and reference them by id (the nesting is already 8 levels deep; two more wrappers would hit Unity's serialization depth limit); toolkit-only users can also use the single-definition asset `EffectDefinitionAsset`. `Normalize()` rewrites an empty phase to `onApply` (`EffectRunner` treats an empty phase as a wildcard, which would otherwise re-run on every period / removal); `Validate(errors)` reports errors plus warnings prefixed with `警告:`.

```csharp
using Ale.Effect; using Ale.Modifier; using Ale.GameplayTags;

// Definition: a 30-"day" +10 might buff, aggregated by target up to 3 stacks, granting Status.Buff.Might
var buff = new EffectDefinition("battle_focus", EDurationPolicy.HasDuration) {
    duration = EffectMagnitude.Scalable(30f), stackingType = EEffectStackingType.AggregateByTarget, stackLimit = 3,
};
buff.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
buff.grantedTags.AddTag("Status.Buff.Might");

// One container per owner; the host's time unit (world days / seconds…) is the unit of the definition's durations
var container = new EffectContainer(owner: heroId);
var ctx = new EffectContext { Subject = heroId };            // ConditionContext's service bag: register by interface
ctx.RegisterService<IEffectAttributeSink>(mySink);          // where instant / periodic modifiers land permanently
ctx.RegisterService<IEffectContainerSource>(myContainers);  // built-ins like Effect.ApplyEffect find containers by subject

EffectApplyResult r = container.ApplyEffect(buff, ctx, level: 1, source: casterId);   // Applied / Stacked / Refreshed / BlockedBy…
container.Tick(1f, ctx);                                     // advance one time unit: periodic execution, expiry removal
var mods = new List<ModifierDefinition>();
container.CollectModifiers("might", mods);                   // scaled modifiers of active, uninhibited, non-periodic effects → feed ModifierStackEvaluator
container.RemoveEffectsWithTags(new GameplayTagContainer("Status.Buff"), ctx);   // dispel
var save = container.ExportState();                          // save; ImportState(state, definitions, ctx) restores silently
```

- **Application pipeline**: immunity (an active, uninhibited effect's immunity tags match the newcomer's `assetTags`) → application tag requirements → application condition (`Subject` = target) → chance → duration evaluation (≤ 0 is `Invalid`) → Instant: modifiers land through `IEffectAttributeSink.ApplyPermanent` + `onApply`, never stored / Stack: the matching instance gains a stack (at the cap it returns `Refreshed` and still refreshes per policy) + `onStack` / New instance: granted tags + `onApply` + execute-on-application → remove other effects by tag.
- **Tick**: periods before expiry; multiple executions when the delta spans several periods, remainder kept; while inhibited the period is frozen but **duration keeps running**; expiry clears the stack / removes one stack and refreshes / only refreshes, per policy, then `onExpire` → `onRemove`.
- **Inhibition** (`ongoingTagRequirements` unmet): modifiers are excluded, the period freezes, granted tags are revoked; any change to `OwnedTags` (grants / `AddLooseTag` / direct host writes) re-evaluates automatically.
- **Values vs. events**: duration / infinite effects contribute temporary modifiers through `CollectModifiers` (one stack-scaled `ModifierDefinition` per modifier, source `effect:{id}#{handle}`); instant effects and periodic executions land **permanently** through the sink — **periodic effects do not participate in `CollectModifiers`**, otherwise "+10 every period and +10 while active" would be double-counted.
- Contracts: `IEffectDefinitionSource` (+ the aggregating `EffectDefinitionRegistry.Default` for cross-database references by id), `IEffectContainerSource`, `IEffectAttributeSource` (AttributeBased magnitudes read current values), `IEffectAttributeSink`, `IEffectRandomSource`, `IEffectCueSink` (receives `cueTags` on Applied / Executed / Removed), `IEffectExecutionInfo` (executors call `ctx.GetService` to get the current definition / instance / source / level / phase); `EffectApplier.Apply(effectId, ctx)` applies by id. Events: `OnEffectAdded / Removed / StackChanged / InhibitedChanged / PeriodicExecuted / OnModifiersChanged`. Editor: `EffectDefinitionDrawer` shows sections conditionally; attribute-id candidates come from host-registered `IEffectAttributeCatalogProvider`s (`EffectDefinitionDrawerHooks.RegisterAttributeProvider`, grouped by system name when several systems contribute, since 1.10.0) or from the whole-field delegate `AttributeIdField` (takes precedence); set `ShowIdentityFields` to false to hide the "Basic" section when the host entity carries its own id / name.

**⑤ Effect library & Effect Editor (since 1.10.0)**

`EffectDatabase` (`Create > Ale > Effect > Effect Database`) is the effect configuration shared by every upper system — four top-level lists: **effect entries** `EffectEntry` (`id` + three `AttributeValue`s for display name / description / icon + custom attributes `values` following the template schema + the embedded `definition`; `Normalize()` syncs the definition's id / display name from the entry), **effect templates** `EffectTemplate` (`name` / `color` / custom-attribute schema + `defaultDefinition`, deep-copied as the preset when an effect is created from the template), **gameplay tags** and **enum types** (for enum custom attributes). Upper systems store only effect ids and implement their own `[EffectExecutor]` executors — an effect is "an operation on some system", and how that system gets operated on is up to the system itself.

```csharp
// Runtime: put the asset under Resources to auto-register on startup (EffectRuntime.AutoLoadFromResources, default true), or register explicitly
EffectDataManager.Instance.Register(effectDatabase);         // idempotent; NormalizeAll + joins the global effect registry + registers gameplay tags
EffectEntry e = EffectDataManager.Instance.GetEffect("regen_draught");
string name   = e.ResolveDisplayName();                      // localized display name (falls back to the id)
int power     = e.GetAttributeValue("power").GetInt(0);      // custom attribute defined by the template schema
EffectApplier.Apply("regen_draught", ctx);                   // the definition resolves by id through EffectDefinitionRegistry.Default
EffectDataManager.Instance.LoadFromBinary(bytes);            // or the output of EffectConfigSerializer.ExportJson / Export
```

- Bootstrap: `[SubsystemRegistration]` clears the registries → `[BeforeSceneLoad]` only adds (executors + `Resources`), so a database a host registers in Awake survives. ⚠️ Zero serialization-depth headroom: `EffectEntry` must be a top-level list element and `defaultDefinition` a direct field.
- **Effect Editor** (`Tools > Ale Toolkit > Effect System > Effect Editor`, or "Edit in Effect Editor" on the asset's inspector) has **two tabs since 1.11.0**:
  - **Effect Executors** (first tab; its content comes from **code**, so it works without any effect library): a catalog of every `IEffectExecutor` implementation in the project, grouped by category, searchable by key / display name / full type name / assembly, with an "issues only" toggle. The right column shows the implementing type, assembly and source path plus "Open Script" (jumps to the class declaration line), "Show in Project" and "Copy Key" (double-clicking a row opens the script), the param schema, which effect configurations reference it ("Go" switches to the second tab and locates that effect), and **silent-failure diagnostics** — a missing `[EffectExecutor]`, an attribute key that disagrees with the `Key` property (the attribute string is never read), a duplicate key (the editor catalog keeps the first found while the runtime registry keeps the last registered — the opposite), an abstract class, a missing public parameterless constructor, or a failed instantiation. A banner lists dangling keys that configurations reference but nothing implements (`EffectDefinition.Validate` does not check this; only the runtime warns); a cheat sheet at the bottom gives the built-in phase constants and a copyable minimal implementation template.
  - **Effect Database** (second tab; the effect library is optional and mostly there to attach extra data to effect entries): left column effect templates / gameplay tags / enum types, middle column the effect list (template filter / search / add from template / quick add), right column duplicate-checked ID + name / description / icon + custom attributes + inline definition + validation summary; duplicates block export; export as JSON / binary. `EffectEditorWindow.Open(db, effectId)` switches to this tab and locates a specific effect, `OpenExecutors()` switches to the first one.
- **Host integration** (one line in an inspector): `EditorEffectRefListDrawer.Draw(ctx, skill.onUseEffectRefs, drag, "Effects applied on use", "effect")` — catalog menu (`EffectEditorCatalog`, grouped by database), drag-reorder, "Open" jump, "(not found)" marking (non-blocking), free-form input; attribute-id candidates are registered through `EffectDefinitionDrawerHooks.RegisterAttributeProvider(IEffectAttributeCatalogProvider)` (dropdown grouped by system name); a host's own tag catalog contributes through `GameplayTagEditorCatalog.RegisterProvider`.

> **Mapping to UE5 GAS**: `EffectDefinition` ≙ GameplayEffect (Duration / Period / Stacking / Tags / Modifiers / Executions / Cues), `EffectContainer` ≙ the active-effect and tag part of the AbilitySystemComponent, `EffectExpression` + `[EffectExecutor]` ≙ Executions. Not done: curve-table magnitudes, non-snapshot attribute capture (magnitudes are snapshotted on apply / stack), network replication. **Modifiers manage "values", executors manage "events", the container manages "lifetime".**

### Editor framework

`Ale.Toolkit.Editor`, all generic over the database type — derive and override a few abstract members to build an editor.

- **Database window shell** `EditorDatabaseWindowBase<TDb>`: built-in "DB-asset object field + top tab strip + validate / export button hooks + duplicate-scan orchestration + status bar + Undo subscription + last-DB-path memory (EditorPrefs)", implementing `IEditorDbContext<TDb>`; a host window becomes much thinner by supplying only its tab set / export·validate callbacks / duplicate-scan kinds. Since 1.11.0: a tab whose `TabRequiresDatabase(int)` is overridden to false **still draws when no database asset is set** (for tabs whose content comes from code rather than assets), `SelectSystemTab(int)` / `CurrentSystemTab` let outside code switch and read the current tab, and the tab index is remembered per `EditorPrefKey`.
- **Three-column tab** `EditorThreeColumnTab<TDb,TEntity>`: left sub-tabs + master list, middle entity list, right context inspector. Override `LeftPanels` / `EntityNoun` / `EntityList` / `DrawEntityList` / `DrawEntityInspector`, etc.; `RequestSelect(entity)` lets outside code locate an entity (since 1.10.0; call it after the database is set — the right-column inspector activates on the next Layout frame).
- **Master list panel** `EditorMasterListPanel<TDb,T>` (+ `IEditorMasterListPanel<TDb>`), **entity list panel** `EditorEntityListPanel<TDb,TEntity,TTemplate>`.
- **Tool window base** `EditorToolWindowBase<TDb>`: built-in "pick database + per-frame time-budget stepping + progress bar + log + cancel + completion". Override `DrawOperations` (start per-frame steps via `RunSteps`) / `OnRunComplete` / `OnRunFinished`.
- Contexts `IEditorContext` / `IEditorDbContext<TDb>`; helper controls `EditorSearchableList` / `EditorDraggableRowList` / `EditorReorderableDrag` / `EditorListKeyboardNav` / `EditorFilterTabs` / `EditorIdScanner` / `ToolkitEditorStyles` / `EditorScriptLocator` (since 1.11.0: type → `MonoScript` lookup that can open the IDE at the class declaration line or highlight the script in Project; it also works for plain classes that do not derive from `MonoBehaviour`). **Clickable list rows all get a mouse-hover highlight** (`ToolkitEditorStyles.TrackMouseHover` / `DrawRowHover`, since 1.11.0; implemented inside the shared row components above, so hosts need no wiring).

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

Macro toggles and runtime adapters for TextMeshPro / Unity Localization / Addressables. The macros are project-level globals (`ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`), toggled from the Welcome window. The package only warns in the Console when a macro is on but its package is missing; it **never rewrites PlayerSettings by itself** — doing so would fight other plugins managing the same macro, and every write triggers a recompile, trapping the editor in a loop.

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
