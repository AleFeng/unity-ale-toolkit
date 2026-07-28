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

## 许可

[MIT](LICENSE.md)
