<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/AleFeng/unity-ale-toolkit?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/AleFeng/unity-ale-toolkit/total?color=green">
  <img alt="Unity Version" src="https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity">
    <img alt="Unity Version" src="https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity">
  <img alt="GitHub Repo License" src="https://img.shields.io/badge/license-MIT-blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/AleFeng/unity-ale-toolkit?color=yellow">
</p>

<p align="center">
  🌍
  中文 |
  <a href="./README_EN.md">English</a> |
  <a href="./README_JA.md">日本語</a>
</p>

<p align="center">
  📥
  <a href="#-安装请先读这一段">安装</a> |
  <a href="#包含的模块">模块</a> |
  <a href="Packages/com.ale.toolkit/README.md">详细文档</a>
</p>

# Ale Toolkit

面向 Unity 插件开发的**通用底层库**。不含任何具体业务领域概念，供多个插件共享同一套自定义属性系统、虚拟滚动列表、编辑器三列框架、编辑器界面多语言、属性修饰器求值，以及两个数据驱动、声明即配即用的独立子系统——**条件系统（`Ale.Condition`）** 与 **效果系统（`Ale.Effect`）**；另含 TextMeshPro / Localization / Addressables 的可选支持层。

---

## ⚠️ 安装（请先读这一段）

**`com.ale.toolkit` 必须先于依赖它的插件安装。**

Unity 的 Package Manager **不支持在 `package.json` 的 `dependencies` 里写 git URL**，因此依赖本包的插件无法自动把它拉下来。你需要手动安装，且**顺序不能颠倒**：

`Window > Package Manager` → 左上角 `+` → `Install package from git URL...`

**第一步 —— 先装 Toolkit：**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit
```

**第二步 —— 再装依赖它的插件**，例如库存系统：

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory
```

> 若顺序颠倒或漏装本包，Unity 会报 `找不到 Ale.Toolkit.*` 一类的编译错误。此时补装本包并等待重新编译即可，无需重装另一个插件。

最低支持 **Unity 2022.3**（基于 Unity 6000.3 开发与维护）。

---

## 包含的模块

| 模块 | 内容 |
| --- | --- |
| **属性系统** | `AttributeValue` 与 20+ 字段类型、属性定义（schema）、自定义枚举类型、数字格式配置、标签系统（`Tag`）、轻量展示文本 `TextValue`（fallback + 可选原生本地化）。任何需要「配置属性条目」的场合都用它 |
| **排序** | 与元素类型无关的排序引擎：宿主实现 `ISortContext<TData>` 提供比较所需信息，引擎负责多级优先级与降级比较；主键 / 标签序号排序开箱即用 |
| **UI** | 虚拟滚动列表（网格 / 顺序，对象池 + 仅渲染可见区）、页签栏、过滤栏、Tooltip 基类、子项实例池等通用控件 |
| **对象池** | 通用 GameObject 预制体池 + 纯 C# 引用类型池 `ToolkitClassPool<T>`（`Spawn`/`Despawn`、`IPoolable` 回调、预热 / 容量回收 / 跨场景、降 GC），可替代 Lean.Pool 一类第三方池 |
| **Tween** | 轻量中央 Tween（DOTween 式单 Update 轮询、作业池化近零 GC）：`FadeCanvasGroup` / `FadeGraphic` / `FadeSpriteRenderer` 淡入淡出，`TintGraphic` 整色过渡，`MoveTransform` / `RotateTransform` / `ScaleTransform` 位移·旋转·缩放，`DelayedCall` 延时回调，`Kill(target)` 按目标打断；返回值类型可打断句柄 |
| **属性修饰器** | GAS 式修饰器求值：`ModifierDefinition` + `ModifierStackEvaluator` 分组结算（Add→PercentAdd→Multiply→Override + clamp + 来源明细）。数值汇流「基础值 + 一叠加成 → 当前值」 |
| **条件系统（`Ale.Condition`）** | 数据驱动的两级 AND/OR 条件：声明一个 `ConditionExpression` 字段即在 Inspector 内联配置；上层实现 `[ConditionEvaluator]` 判定器被自动发现。引擎无关 Core 可上服务端 |
| **效果系统（`Ale.Effect`）** | 条件系统的写侧镜像：数据驱动的离散触发式突变（阶段组 + 每项可选条件门控）；上层实现 `[EffectExecutor]` 执行器被自动发现。引擎无关 Core |
| **编辑器框架** | 数据库窗口外壳基类、三列布局页签基类、主列表面板、实体列表面板、工具窗口基类，均对数据库类型泛型化 |
| **编辑器多语言** | 中 / English / 日本語 三语服务，以中文原文为键，缺译文自动回退 |
| **UGUI 预制体工具箱** | 与领域无关的 UGUI 原语与文本 / 按钮搭建（供各插件的一键生成向导复用） |
| **可选依赖支持层** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）的宏开关与适配，含本地化工具窗口与 Addressable 工具窗口 |

完整说明见 [Packages/com.ale.toolkit/README.md](Packages/com.ale.toolkit/README.md)，变更历史见 [CHANGELOG](Packages/com.ale.toolkit/CHANGELOG.md)。

---

## 程序集

| Assembly Definition | 说明 | 宏门控 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性系统、排序、标签、资源加载抽象、通用序列化、对象池、中央 Tween、属性修饰器求值 | — |
| `Ale.Toolkit.Runtime.UI` | 虚拟滚动列表与通用 UI 控件 | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 适配组件 | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables 资源加载与句柄管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | 编辑器框架、数据库窗口外壳基类、属性绘制器、多语言服务、预制体工具箱、宏开关 | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables 编辑器解析器 / 工具窗口 | `ATK_ADDRESSABLE` |
| `Ale.Condition.Core` / `.Runtime` / `.Editor` | 条件系统：引擎无关模型·引擎·注册·JSON（可上服务端）/ Unity 桥 + 启动自动注册 / 内联绘制器 + 目录 + 欢迎窗口 | Core 引用 Newtonsoft |
| `Ale.Effect.Core` / `.Runtime` / `.Editor` | 效果系统：引擎无关模型·运行器·注册·JSON / Unity 桥 + 启动自动注册 / 内联绘制器 + 目录 + 欢迎窗口 | Core 引用 `Ale.Condition.Core` + Newtonsoft |

依赖方向单向：宿主插件 → `Ale.Toolkit.*` / `Ale.Condition.*` / `Ale.Effect.*`，本包不反向引用任何宿主插件。条件 / 效果两子系统命名空间独立（`Ale.Condition` / `Ale.Effect`）。

---

## 许可

[MIT](LICENSE)
