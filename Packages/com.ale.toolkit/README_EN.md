# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

A **general-purpose foundation library** for Unity plugin development. It carries no business-domain concepts, letting several plugins share one attribute-configuration system, list engine, editor framework and localization service.

> This package was split out of `com.ale.inventory` 1.8.0. The general-purpose capabilities that used to live inside the inventory system (the three-column editor framework, virtual scrolling lists, the custom attribute system, the trilingual editor UI) were extracted here so more plugins can reuse them.

---

## ⚠️ Installation (read this first)

**`com.ale.toolkit` must be installed before any plugin that depends on it.**

Unity's Package Manager **does not support git URLs in the `dependencies` field of `package.json`**, so a dependent plugin cannot pull this package automatically. You must install both manually, **in this order**:

`Window > Package Manager` → `+` in the top-left → `Install package from git URL...`

**Step 1 — install Toolkit first:**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.3.0
```

**Step 2 — then install the dependent plugin**, for example the inventory system:

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.10.0
```

> If the order is reversed or this package is missing, Unity reports compile errors such as `Ale.Toolkit.* not found`. Just install this package and wait for the recompile — there is no need to reinstall the other plugin.

Requires **Unity 2022.3** or newer (developed and maintained on Unity 6000.3).

---

## Modules

| Module | Contents |
| --- | --- |
| **Attribute system** | `AttributeValue` with 20+ field types, attribute definitions (schema), custom enum types, number-format configs. Use it wherever configurable attribute entries are needed |
| **Sorting** | An element-type-agnostic sort engine: the host implements `ISortContext<TData>` to supply what comparison needs, the engine handles multi-level priorities and tiebreakers |
| **UI** | Virtual scrolling lists (grid / sequential, object pool + visible-region-only rendering), tab strips, filter bars, tooltip base classes, widget pools |
| **Object pool** | A general-purpose GameObject/prefab pool (`Spawn`/`Despawn` + `IPoolable` callbacks; preload / capacity-recycle / delayed despawn / cross-scene) plus a plain-C# reference-type pool `ToolkitClassPool<T>` (lower GC) — a drop-in replacement for third-party pools like Lean.Pool |
| **Editor framework** | Three-column tab base class, master list panel, entity list panel and tool window base — all generic over the database type |
| **Editor localization** | 中文 / English / 日本語 service, keyed by the Chinese source string, falling back automatically when a translation is missing |
| **Optional dependency support** | Macro toggles and adapters for TextMeshPro (`ATK_TMP`), Unity Localization (`ATK_LOCALIZATION`) and Addressables (`ATK_ADDRESSABLE`) |
| **Editor entry & global settings** | The Ale Toolkit Welcome Window (`Tools > Ale Toolkit > Welcome`): editor UI language / enum translation / the three optional feature macros / wizard default & localized fonts + general-tool entries + an "auto-show on startup" toggle; project-level settings such as the wizard fonts are saved to `ProjectSettings/AleToolkitSettings.asset` (committed with the repo, asset references stored by GUID), while language / auto-show are per-user (EditorPrefs); legacy `IS_*` macros are auto-migrated to `ATK_*` on load |
| **General tool windows** | Walk every `AttributeValue` of any data asset (`ScriptableObject`) for batch processing: Addressable migration (Object ↔ GUID) and localization key generation, under `Tools > Ale Toolkit`, reusable by upper-layer plugins |

> All modules above are in place — since 1.1.0 the three optional-dependency support layers (TMP / Localization / Addressables) are complete and the editor UI is trilingual even in a toolkit-only project; **since 1.2.0 it owns the project-level global settings (language / macros) and provides general tool windows that work on any data asset**; **since 1.3.0 it adds a general-purpose object pool (GameObject pool + plain-C# class pool)**. See the [CHANGELOG](CHANGELOG.md) for details.

---

## Assemblies

| Assembly Definition | Purpose | Macro constraint |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | Attribute system, sorting, asset-loading abstraction, shared serialization, object pool | — |
| `Ale.Toolkit.UI` | Virtual scrolling lists and general UI widgets | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization adapter components | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables loading and handle management | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | Editor framework, attribute drawers, localization service, macro toggles | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables editor tooling | `ATK_ADDRESSABLE` |

Dependencies flow one way: host plugin → `Ale.Toolkit.*`. This package never references a host plugin.

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
- Others: `UiwViewBase` (`Open`/`Close`/`ToggleOpenClose`), `UiwSortToolbar` (`SetOptions`/`SetSortPriorities`), `UiwNumberCounter` (`Configure`/`SetRange`/`SetValue`), `UiwTextLabel`, `SpriteSlot.Bind(image, value)`.

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

### Editor framework

`Ale.Toolkit.Editor`, all generic over the database type — derive and override a few abstract members to build an editor.

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
