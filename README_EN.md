<p align="center">
  <img alt="Unity 2022.3+" src="https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity">
  <img alt="Unity 6000.3+" src="https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity">
  <img alt="License MIT" src="https://img.shields.io/badge/license-MIT-blueviolet">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/AleFeng/unity-ale-toolkit?color=blue">
</p>

<p align="center">
  🌍
  <a href="./README.md">中文</a> |
  English |
  <a href="./README_JA.md">日本語</a>
</p>

<p align="center">
  📥
  <a href="#-installation-read-this-first">Installation</a> |
  <a href="#-included-modules">Modules</a> |
  <a href="Packages/com.ale.toolkit/README_EN.md">Full docs</a>
</p>

# Ale Toolkit

A **shared foundation library** for Unity plugin development. It contains no business-domain concepts — it lets multiple plugins share one custom attribute system, virtual-scroll lists, a three-column editor framework, a trilingual editor UI, and optional support layers for TextMeshPro / Localization / Addressables.

> This library was split out of the inventory plugin [`com.ale.inventory`](https://github.com/AleFeng/unity-ale-inventory-system) 1.8.0. The general-purpose capabilities that used to live inside the inventory system were extracted here so they can be reused by more plugins (e.g. a future character system). The split **keeps export formats and serialization structures unchanged**; only the type namespaces changed from `Ale.Inventory.*` to `Ale.Toolkit.*`.

---

## ⚠️ Installation (read this first)

**`com.ale.toolkit` must be installed before any plugin that depends on it.**

Unity's Package Manager **does not support git-URL entries in `package.json` `dependencies`**, so a dependent plugin cannot pull this package automatically. You must install it manually, and **the order matters**:

`Window > Package Manager` → the `+` in the top-left → `Install package from git URL...`

**Step 1 — install the Toolkit first:**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.0.0
```

**Step 2 — then install the plugin that depends on it**, e.g. the inventory system:

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.8.0
```

> If you install in the wrong order or forget this package, Unity reports compile errors like `Ale.Toolkit.* not found`. Just add this package and wait for the recompile — you do not need to reinstall the other plugin.

Minimum **Unity 2022.3** (developed and maintained on Unity 6000.3).

---

## Included Modules

| Module | Contents |
| --- | --- |
| **Attribute system** | `AttributeValue` with 20+ field types, attribute definitions (schema), custom enum types, number-format configs, tag system (`Tag`). Use it wherever you need "configurable attribute entries" |
| **Sorting** | An element-type-agnostic sort engine: the host implements `ISortContext<TData>` to supply what comparison needs; the engine handles multi-level priorities and tiebreakers. Primary-key / tag-order sorting works out of the box |
| **UI** | Virtual-scroll lists (grid / sequential, pooled + render-only-visible), tab strips, filter bars, a tooltip base, an item pool, and other general widgets |
| **Editor framework** | Three-column tab base, master-list panel, entity-list panel, tool-window base — all generic over the database type |
| **Editor localization** | A Chinese / English / Japanese service keyed by the Chinese source text, falling back automatically when a translation is missing |
| **UGUI prefab toolbox** | Domain-agnostic UGUI primitives and text / button builders (reusable by each plugin's one-click generation wizard) |
| **Optional support layers** | Macro switches and adapters for TextMeshPro (`IS_TMP`), Unity Localization (`IS_LOCALIZATION`), Addressables (`IS_ADDRESSABLE`), including the localization and Addressable tool windows |

See [Packages/com.ale.toolkit/README_EN.md](Packages/com.ale.toolkit/README_EN.md) for full details and [CHANGELOG](Packages/com.ale.toolkit/CHANGELOG.md) for the change history.

---

## Assemblies

| Assembly Definition | Description | Macro gate |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | Attribute system, sorting, tags, asset-loading abstractions, shared serialization | — |
| `Ale.Toolkit.Runtime.UI` | Virtual-scroll lists and general UI widgets | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization adapter components | `IS_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables asset loading and handle management | `IS_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | Editor framework, attribute drawers, localization service, prefab toolbox, macro switches | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables editor resolvers / tool windows | `IS_ADDRESSABLE` |

Dependencies flow one way: host plugin → `Ale.Toolkit.*`; this package never references any host plugin.

---

## License

[MIT](LICENSE)
