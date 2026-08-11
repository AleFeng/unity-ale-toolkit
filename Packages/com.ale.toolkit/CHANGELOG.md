# 更新日志（Changelog）

本文件记录 Ale Toolkit（`com.ale.toolkit`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

> 由来：本包自 `com.ale.inventory` 1.8.0 拆分而来。原先埋在库存系统里的通用能力被抽出，使其可被更多插件复用（例如后续的角色系统）。拆分过程中**导出格式与序列化结构不变**，类型的命名空间由 `Ale.Inventory.*` 改为 `Ale.Toolkit.*`。

## [1.7.6] - 2026-08-11

**顺序虚拟列表：新增反向排布开关。**

### 新增

- **`UiwVirtualOrderList` 新增 `reverseScrollDirection`（反向滚动）**，默认 `false`（正向：第 0 条在最上方，向下滚走向末条，与旧行为一字不差）。勾选后倒序排布——**第 0 条在最下方、最后一条在最上方**，「向下滚」于是从末条走向首条，适合聊天记录、日志这类自下而上追加的列表。
  - 实现上引入「**槽位**」这层概念：槽位 = 从 Content 顶端往下数的第几行，是纯几何量；数据索引到槽位的映射由 `SlotOf` 给出（正向为恒等，倒序为 `条目数-1-索引`）。定位、首尾留白、滚动换算、焦点反解一律只认槽位，于是反向**不需要动 Content 尺寸、锚点与滚动范围中的任何一个**——它们本就是按槽位算的。
    > 另一条路是把 Content 锚点翻到底部，但那样上述每一处都要各写一套正 / 反分支，`UiwFocusOrderList` 还要再抄一遍。两种做法的视觉结果完全一致。
  - `UiwFocusOrderList` 同步按槽位改写：`PositionOf`、`ComputeFirstIndex`、`FocusIndex` 的滚动量反解、焦点索引反解，以及外观曲线的可见区间（倒序时「槽位区间 → 索引区间」是**反序**的，两端需对调后再夹取）。因此 `FocusIndex(i)` 之后 `FocusedIndex` 仍读回 `i`，焦点语义在两个方向下都自洽。
  - 引擎的可见窗口恒为 `[first, first + PoolTarget - 1]` 这样的**升序索引区间**，而倒序下视口顶端对应的是**最大**索引，故 `ComputeFirstIndex` 要交出的是比它小一整个窗口跨度的那个索引。为此 `UiwVirtualListBase._poolTarget` 以只读属性 `PoolTarget` 暴露给布局策略。
  - 运行期翻转开关时已摆好的格子位置全部作废，故在 `RecomputeLayout` 里检测方向变化并整体收回重排——放在那里而非 `OnValidate`，是因为后者可能落在 Canvas Rebuild 循环内，就地回收 / 重排 UI 会报错。

## [1.7.5] - 2026-08-10

**顺序虚拟列表：行距与格子高度解耦（新增行距倍率），格子轴心由顶端改为正中（修复焦点条目对不准焦点线）。**

### 新增

- **`UiwVirtualOrderList` 新增 `rowPitchScale`（行距倍率）**，行距 = 格子高度 × 本倍率，默认 `1.0`（逐行紧贴，与旧行为一致）；大于 1 拉开间隙，小于 1 让相邻行重叠。
  - 此前 `_cellHeight` 一个值同时充当「格子自身高度」与「行距」，想调疏密只能改格子预制体的尺寸——而那会连带改变格子里所有元素的可用空间。现在两者分开：`CellHeight` 只决定实例的 `sizeDelta`，新增的 `RowPitch` 决定 Content 高度、定位、可见窗口与滚动换算。
  - `UiwFocusOrderList` 的首尾留白、焦点索引反解、`FocusIndex` 定位、滚轮步长兜底与外观曲线取样**一律改用行距**，因此焦点语义在任何倍率下都成立：滚一档仍正好换一条，第一条与最后一条仍能停到焦点线上。
  - 倍率是给美术在 Play 模式里对着调的，故 `OnValidate` 会把布局置脏、由 `LateUpdate` 重建（走既有的延迟重建通路，不在 `OnValidate` 里直接改 UI）。为此 `UiwVirtualListBase.SetViewportDirty` 由 private 提为 `protected`。

### 修复

- **`UiwFocusOrderList` 配了 `focusScaleCurve` 时，焦点条目的视觉中心并不在焦点线上**，而是低了 `(缩放 - 1) × 行距 / 2`——缩放峰值 1.5、行距 60 时正好偏 15 像素，表现为「焦点锚点选了 Center，条目却明显偏下」。
  - 成因：`UiwVirtualOrderList.SetupInstanceRect` 把格子的 pivot 设为顶端居中 `(0.5, 1)`。轴心即缩放中心，于是放大的格子只向下长开，几何中心随缩放下移，而布局与焦点反解算的一直是未缩放的行中心。
  - 修法：pivot 改为正中 `(0.5, 0.5)`，`PositionOf` 相应给出「行槽位的中心」而非顶端（补半个行距）。补偿之后「第 i 条的中心 = 留白 + i × 行距 + 行距/2」这条等式仍然成立，焦点反解一行未改。
  - **对没配缩放曲线的列表（`focusScaleCurve` 留空，以及所有 `UiwVirtualOrderList` 直接子类）排布逐像素不变**；配了曲线的列表则是修正其偏移。网格列表 `UiwVirtualGridList` 不受影响（它有自己的 `SetupInstanceRect`）。

## [1.7.4] - 2026-08-10

**修复 `ToolkitTween` 在编辑模式下泄漏 GameObject 且从不推进的问题。**

### 修复

- **`ToolkitTween` 在编辑模式下每次调用都新造一个 `[ToolkitTween]` 对象，且补间永不推进、完成回调永不触发。**
  - 成因：`ToolkitTweenRunner` 由门面惰性自建，而单例登记发生在 `Awake` 里——Unity 默认不在编辑模式调用 `Awake`，于是 `Instance` 始终为空，每次 `EnsureRunner()` 都重新创建；`LateUpdate` 同样不跑，作业表只进不出。
  - 更要紧的是**这些对象落进了用户当时打开的场景**（`hideFlags` 为 `None`，属可保存对象），一旦保存场景就会被写进场景文件。
  - 修法：给 `ToolkitTweenRunner` 标注 `[ExecuteAlways]`，并把推进逻辑从 `LateUpdate` 抽成 `Tick(scaledDelta, unscaledDelta)`；编辑模式改由 `EditorApplication.update` 驱动，用 `timeSinceStartup` 自算增量（编辑模式下 `Time.deltaTime` 不反映真实流逝），增量钳在 0.1 秒以内，避免编辑器因导入 / 编译停摆后补间一步跳完。
  - 运行器在**离开编辑模式时自毁**：编辑模式建的实例带 `HideAndDontSave`，进入播放模式不会被销毁，而静态单例引用会随域重载复位、导致播放模式又新建一个——两个运行器并存各自推进。
- **`ToolkitMonoSingleton<T>.Awake` 中两处仅限播放模式的调用改为按模式分支**，使子类能安全地标注 `[ExecuteAlways]`：
  - `DontDestroyOnLoad` 在编辑模式下会抛 `InvalidOperationException`（Unity 明确限定其只能用于播放模式）。编辑模式改设 `HideFlags.HideAndDontSave`——同样达成「不随场景保存、不被场景卸载带走」，并且把对象移出当前场景，不污染用户正在编辑的场景。
  - 重复实例分支的 `Destroy` 在编辑模式下会报错要求改用 `DestroyImmediate`，已按模式分派。
  - **对播放模式行为零影响**；未标注 `[ExecuteAlways]` 的子类（`ToolkitInputRunner`、宿主插件的管理器）行为完全不变。
- **`ToolkitInputBinder` 在编辑模式下不再自建运行器**。输入绑定在编辑模式没有意义（输入系统并不驱动玩家循环），而 `ToolkitInputRunner` 未标注 `[ExecuteAlways]`，此前同样会每次调用泄漏一个 `[ToolkitInput]` 对象到当前场景。现改为拒绝创建并给出一次明确告警，说明应把调用移到运行时。

## [1.7.3] - 2026-08-10

### 新增

- **`ToolkitTween.To(from, to, duration, onUpdate, …)` 通用浮点补间**，对应 DOTween 的 `DOTween.To(getter, setter, to, duration)`。此前 `ToolkitTween` 的七个方法都绑死在具体的写回目标上（`CanvasGroup.alpha` / `Graphic.color` / `SpriteRenderer.color` / `Transform` 的位移旋转缩放），**写回对象不是 `UnityEngine.Object` 时无路可走**——典型如第三方动画运行时暴露的裸结构体属性（Spine 的 `Skeleton.A`、Live2D 的 `CubismRenderController.Opacity`），它们既不是 Unity 对象、也无法用固定通道表达。新方法把插值结果交给调用方提供的 `Action<float>` 自行写回，补上这个缺口。
  - 内置通道能表达的场合仍请优先用内置通道——它们直接写字段，不经委托。
  - **不回读起始值**：写回路径是个只写委托，无从回读，`from` 由调用方给定并在起始时固定。需要「从当前值出发」时自行把当前值传进来。
  - **写回目标随宿主销毁时务必传 `owner`**，否则委托捕获的引用会让作业在宿主消失后继续写一个已失效的对象；传了 `owner` 也就能经 `Kill(owner)` 按宿主批量取消。
  - `onUpdate` 抛出的异常**就地捕获并记录**，该作业随即失效、完成回调不触发。这条与内置通道「用 `as` 转型 + 空守卫使类型不匹配退化为静默空操作」是同一个约定：`Write` 是在 runner 的作业循环内被调用的，单个作业的故障不能打断同帧其余作业。
  - 新增的 `Custom` 通道与 `Delay` 一样允许无 `owner`，故同样**刻意排在枚举末尾（值非 0）**——若它占 0，池复用后未填字段的脏作业会被当成合法的「无目标自定义补间」而永远存活。

## [1.7.2] - 2026-08-10

### 新增

- **`UiwFocusOrderList` 的滚轮平滑位移**（`scrollTweenDuration`，默认 0.1 秒；置 0 恢复瞬间跳变）。焦点列表的一档滚轮通常正好跨一整条，而原生 `ScrollRect` 是直接改写 `content.anchoredPosition` 的，表现为整条列表瞬间跳一格、焦点缩放曲线跟着突变。现在改由本类按缓出（quad out）曲线逐帧插值过去。
  - **一档的位移距离仍取自 `ScrollRect` 的 `Scroll Sensitivity`**，Inspector 上那个字段仍是「一档滚多远」的唯一入口，只是改由本类来应用它。
  - **本类会在 `Awake` 里把 `ScrollRect.scrollSensitivity` 取走并置 0**（仅运行期，不动预制体）。这一步是必须的：`ScrollRect` 与本类通常挂在同一个 GameObject 上，而 `ExecuteEvents` 会把滚轮事件派发给该物体上**全部** `IScrollHandler`——不清零就会先被 `ScrollRect` 瞬间挪一档、再被补间从头拉回，白抖一帧。
  - **连滚数档按目标位置累加**而非按当前位置：起点取「进行中的补间终点」，否则每档都从半路的实际位置重新起算，越滚越短、最后停在两条之间。
  - 拖拽 / 惯性 / 边界回弹仍完全是 `ScrollRect` 原生的，本类不碰；拖拽开始（`OnBeginDrag`）与 `SetItems` / `FocusIndex` 都会取消进行中的补间，避免两个位置来源打架。
  - 仍**不做**释放后吸附对齐——拖拽松手停在两条之间时，焦点缩放曲线会让上下两条都呈半放大态。

## [1.7.1] - 2026-08-10

### 修复

- **`UiwFocusOrderList` 补首尾留白**，修复「条目全挤在顶部、滚轮完全滚不动」。此前 Content 高度就是 `条目数 × 行高`、条目自顶端紧挨着排，于是：
  - **第一条与最后一条永远够不到焦点线**。焦点锚点取 `Center` 时，第 0 条的中心固定在视口顶端附近，滚到底也只能让焦点停在中间那几条上——首尾各有半个视口的条目**无法被选中**。
  - **条目少于一屏时 Content 比视口还矮**，`ScrollRect` 判定无内容可滚，滚轮与拖拽双双失效，条目则一律堆在视口顶部。这正是「转盘式列表只有 3 条时整个列表像卡死了」的成因。

  现在 Content 头尾各补一段留白（长度由焦点锚点与视口高度算出：居中锚点即上下各 `(视口高 - 行高) / 2`），使滚动量与焦点索引严格一一对应——滚到 0 即焦点第 0 条，滚到底即焦点末条，且只要有 2 条以上就必定可滚。留白随视口尺寸变化重算，变化时收回全部格子重排（否则已在窗口内的格子会停在按旧留白算出的位置上）。

## [1.7.0] - 2026-08-09

为「把上层插件从其它框架的底层依赖上摘下来」补齐四处缺口：世界坐标转 UI 坐标、纯地址取用资源、按名绑定输入、带焦点语义的虚拟列表。四项均为**纯加量**，既有 API 的签名与行为不变；唯一的破坏性变更是公开接口 `IAssetLoader` 新增了一个抽象成员（详见下方「破坏性变更」）。

### 新增

- **`UIUtility.WorldPosToUILocalPos` / `ScreenPosToUILocalPos`**：把世界坐标（或屏幕坐标）换算成某个 Canvas 下的局部坐标，用于让 UI 挂件「贴」在场景物体上（血条 / 名牌 / 操作菜单）。与既有的 `PositionAtCursor` 分工明确：那边输入是光标屏幕像素、直接写 `rt.position` 并夹取回屏内；这边返回值、不夹取，落到哪个 `RectTransform` 由调用方决定。带世界空间偏移的重载走世界空间而非屏幕像素——同样的像素偏移在不同分辨率下对应的世界距离不同，会让挂件与目标物体的相对位置随分辨率漂移。**世界投影固定用 `Camera.main`（缺省回退 `canvas.worldCamera`）**：`canvas.worldCamera` 是 UI 专用渲染相机，两者分离时拿它做投影会得到错误结果，它只在 `ScreenPosToUILocalPos` 内部喂给 `ScreenPointToLocalPointInRectangle`。
- **`ToolkitAssets.LoadByAddress<T>` / `InstantiateByAddress<T>` / `ReleaseAddress(string)`**：面向「调用方手上只有一个运行时拼出来的地址串、没有任何实时引用可回退」的取用路径（如按角色名拼出 `"…/Actors/{name}.prefab"`）。既有的 `Bind` / `Load` 都以 `AttributeValue`（或「实时引用 + 地址」二元组）为输入、有实时引用优先路径，覆盖不到这种场景。`InstantiateByAddress` 回调给出的是**新实例**而非源资源，两份生命周期各管各的：实例归调用方 `Destroy`，源资源句柄仍须 `ReleaseAddress` 按同一地址配对释放。
- **`Ale.Toolkit.Input.Runtime` 程序集与 `ToolkitInputBinder`**（受 `ATK_INPUT_SYSTEM` 约束，门控范式与 Addressables 层一致）：按「ActionMap 名 + Action 名」把回调接到 Input System 上。解决的是同一件麻烦事——**要绑定的时候输入源往往还不存在**：`PlayerInput` 常随玩家 / 角色在运行时生成，而调用方通常在自己的 `OnEnable` 里就想把回调挂上。`Bind` 立刻登记，能当场生效就当场生效，不能则挂起、由常驻运行器逐帧重试到输入源与对应 Action 可得为止，期间不丢回调；`Unbind` 对已生效与仍挂起的绑定都能正确撤销。输入源默认取场景中第一个 `PlayerInput` 的 actions，也可经 `ToolkitInputBinder.Actions` 显式指定（改写会把已生效的绑定全部退订并按新源重新解析）。
  - **回调订阅 `started` / `performed` / `canceled` 三个阶段**，而非只订 `performed`：调用方常靠「按下 / 抬起」成对触发维护拖拽之类的状态（典型写法是回调里 `ctx.ReadValue<float>()` 比对 0 / 1），只订 `performed` 会收不到抬起。只关心「触发了一次」的场合用无参 `Action` 重载，其包装体只放行 `performed`。
  - **不改动输入的启用状态**：只负责接线，不会 `Enable` 任何 ActionMap——启停由 `PlayerInput` 的 Default Map / `SwitchCurrentActionMap` 之类的输入状态机统一决定，两件事分开才不会互相打架。绑到一个当前禁用的 Map 时给出一次警告（每 Map 一次，不刷屏），因为这种情况下回调永远不触发而 Input System 本身毫无提示，极难排查。
  - 全部可变状态放在运行器实例上而非门面的静态字段：关闭 Domain Reload 时静态字段跨播放会话残留，会把上次播放留下的绑定（其 `InputAction` 已随输入系统重新初始化而失效）带进下一次；运行器随播放结束销毁，状态天然干净。`OnDestroy` 退订全部绑定——`InputActionAsset` 是 ScriptableObject 资源、在编辑器里跨播放存活，不退订会把委托永久留在资源的 Action 上。与 `ToolkitTween` / `ToolkitTweenRunner` 是同一套分工。
- **`UiwFocusOrderList<TData,TCell>` 与 `EFocusAnchor`**：带「焦点条目」语义的顺序虚拟列表，在 `UiwVirtualOrderList` 的单列纵向布局之上补两件事——① **焦点跟踪**，视口中某个位置（Top / Center / Bottom）被定为焦点线，中心最接近它的条目即焦点条目，改变时抛 `OnFocusChanged(prev, current)`；② **随焦点距离变化的外观**，两条可选曲线按「该格中心离焦点线有多远」（归一化到 `[-1,1]`，±1 对应视口边缘）驱动 `localScale` 与横向偏移，得到中间大两头小、并向两侧让开的弧形排布。这让「滚到哪儿就选中哪儿」的转盘 / 拨轮式交互不必额外接线：选中态直接由滚动位置派生，不需要点击、也不需要在格子上挂选中逻辑。
  - **不改动基类的滚动模型**：滚动仍由 `ScrollRect` 驱动（拖拽 / 滚轮 / 惯性都是它原生的），本类只读取滚动位置来派生焦点与外观，不接管输入、不做释放后吸附对齐。
  - **不自建「索引 → 格子」映射**：按滚动位置算出视口覆盖的索引区间，再逐个向基类既有的 `TryGetActiveCell` 要格子。避免与基类的回收 / 复用循环产生第二份需要同步的状态，叶子类也因此不必覆写任何额外钩子。
  - 外观回写有变化守卫（`Mathf.Approximately` 比对后才写 `localScale` / `anchoredPosition`）：写 transform 会让 UGUI 标脏并触发画布重建，静止时逐帧写入同样的值等于每帧白重建一次画布。横向偏移只改 `x`——`y` 是基类按 `PositionOf(i)` 定的行位置，覆盖它会把整个虚拟滚动的定位打乱。
  - `UpdateFocusAndAppearance` 有防重入守卫：订阅方若在 `OnFocusChanged` 回调里改数据（`SetItems`）会重新走进本方法，不拦住则可能形成事件递归。
- **`UiwVirtualOrderList.CellHeight`**（`protected`）：暴露由 `MeasureCell` 量得的行高，供子类做定位 / 焦点换算，免得各子类各自重量一遍、并在基类默认值变化时静默失配。
- **欢迎窗口新增 `ATK_INPUT_SYSTEM` 宏开关**，`ToolkitDefineChecker` 同步纳入包 / 宏一致性检查，编辑器界面文案补齐 zh / en / ja 三语。

### 破坏性变更

- **`IAssetLoader` 新增抽象成员 `LoadByAddress<T>(string address, GameObject owner, Action<T> onLoaded)`。** 包内两个实现（`DirectAssetLoader` / `AddressableAssetLoader`）均已补齐，**包外**若有第三方实现了本接口则需一并实现该方法。未采用默认接口实现（DIM），因其在 IL2CPP 下的支持历来不稳。
  - `AddressableAssetLoader` 委托既有的 `AddressableManager.LoadAsync`（按地址引用计数去重）。
  - `DirectAssetLoader` 没有实时引用可回退，故退到 `Resources.Load`（先去掉地址末段的扩展名——地址串通常带 `.prefab` 之类后缀，而 Resources 要求不带扩展名的相对路径，这是最常见的一处不匹配）；仍未命中则回调 `null` 并给出一条指明「装 Addressables + 开 `ATK_ADDRESSABLE`」的警告，而不是静默失败。

### 修复

- **`ToolkitInfo.Version` 与 `package.json` 的版本号对齐。** 此前前者停留在 `"1.5.0"`、后者已是 `1.6.0`，用 `ToolkitInfo.Version` 做版本门控的宿主会拿到落后一个 minor 的值。本次一并订正为 `1.7.0`。
- `package.json` 的 `keywords` 补 `input`。

## [1.6.0] - 2026-08-09

把中央 Tween 从「只会淡 alpha」补齐为**可整体承接 DOTween 常用单 tween 用法**的轻量门面：内部作业模型由「CanvasGroup 或 Graphic」双目标泛化为「通道 + 单一目标 + `Vector4` 载荷」的联合体，在此之上一次性补上 `SpriteRenderer` 淡入淡出、`Graphic` 整色过渡、`Transform` 位移 / 旋转 / 缩放、纯延时回调，以及 DOTween 目标登记表的等价物「按目标打断」。**导出格式与序列化结构不变、纯加量；`FadeCanvasGroup` / `FadeGraphic` 的签名与行为不变，`UiwListFadeCell` 行为不变。**

### 新增

- **`ToolkitTween.FadeSpriteRenderer(SpriteRenderer, endAlpha, …)`**：2D 精灵的 alpha 淡入淡出。`SpriteRenderer` 并非 `Graphic`，故与 `FadeGraphic` 分列。对应 DOTween 的 `spriteRenderer.DOFade`。
- **`ToolkitTween.TintGraphic(Graphic, endColor, …)`**：`Image` / 文本等 `Graphic` 的**整色（RGBA）**过渡，`FadeGraphic` 的全色版本。对应 DOTween 的 `graphic.DOColor`。
- **`ToolkitTween.MoveTransform` / `RotateTransform` / `ScaleTransform`**：`Transform` 的世界坐标 / 世界欧拉角 / 局部缩放补间，对应 DOTween 的 `DOMove` / `DORotate` / `DOScale`。旋转**逐轴独立走最短弧**（起始时经 `ShortestEuler` 烘焙终值，350° → 10° 只转 20° 而非 340°），等价于 DOTween 的 `RotateMode.Fast`；**不**支持 `FastBeyond360` 那样超过 360° 的多圈旋转。
- **`ToolkitTween.DelayedCall(delay, onComplete, unscaled = true, owner = null)`**：纯延时回调，不插值任何目标，对应 DOTween 的 `DOVirtual.DelayedCall`。可选 `owner` 绑定生命周期——owner 被 `Destroy` 后回调丢弃，且可经 `Kill(owner)` 按 owner 取消；不传则为独立于任何对象存亡的纯计时器（DOTween 的默认语义）。
- **`ToolkitTween.Kill(UnityEngine.Object target, bool complete = false)`**：打断该目标上全部在途作业并返回打断数，DOTween 目标登记表的等价物（对应 `target.DOKill()` / `target.DOComplete()`）。目标按**引用相等**匹配而非 Unity 的 `==`——后者会把两个已销毁对象都判为 null 而互相「相等」，用它会误杀无关作业；因此已被 `Destroy` 的目标依然能用本方法清理自己的作业。匹配是精确的对象身份，`Kill(gameObject)` 找不到挂在其上的组件的作业。
- **`ToolkitTween.ShortestEuler(fromEuler, toEuler)`**：把目标欧拉角折算为「自起始角出发、各轴独立走最短弧」的等价终点（逐轴 `from + Mathf.DeltaAngle(from, to)`）。`RotateTransform` 内部用它烘焙终值，公开出来供调用方复用。
- **`ToolkitTweenHandle.Complete()`**：立即完成——瞬置终值并触发完成回调（同步、在调用栈上）。等价 `Kill(true)`，对应 DOTween 的 `Tween.Complete()`。
- **`ToolkitTweenHandle : IEquatable<ToolkitTweenHandle>`** 及 `==` / `!=` / `GetHashCode`：可直接放进 `List<ToolkitTweenHandle>` 并用 `List.Remove(handle)` 移除。此前落到 `ValueType.Equals` 的反射逐字段比较，**结果相同但每次比较都要装箱**；现在走 `EqualityComparer<T>` 的 `IEquatable` 快路径，零分配。`operator ==` 此前无法书写，故无源码破坏。
- **测试 `Assets/Tests/ToolkitTweenTests.cs`**：21 个 EditMode 用例，覆盖 `ToolkitEase.Evaluate` 四种缓动的端点 / 越界钳制 / 单调性 / 值域、`ShortestEuler` 的跨 0° 与逐轴独立折算，以及空句柄、`DelayedCall` 与 `Kill` 的快路径守卫。此前 Tween 模块零测试覆盖。

### 变更

- **作业模型泛化**：内部 `TweenJob` 由「`CanvasGroup` XOR `Graphic`」双目标改为 `UnityEngine.Object Target` + `ETweenChannel` 通道 + `Vector4 From/To` 载荷，`SetAlpha(float)` 改为按通道分发的 `Apply(float k)` / `ApplyEnd()`。**单池、单作业表、单 `LateUpdate` 推进与近零 GC 均不变**；每作业约多 32 字节，数十个并发作业量级下可忽略。评估过「抽象基类 + 每类型子类 + 每类型池」，因 `ToolkitClassPool<T>` 是按闭合类型静态泛型的、多子类会退化为 N 个互不复用的池而否决。
- **插值改用 `LerpUnclamped`**：现有四种缓动的输出恒落在 [0,1]（已由 `Evaluate_AllEases_StayWithinUnitRange` 用例锁定），故与 `Mathf.Lerp` **结果逐位相同**；为将来可能引入的过冲缓动预留。收尾与「立即完成」走 `ApplyEnd()` **精确写入终值**、不经插值——`a + (b - a) * 1` 未必逐位等于 `b`，此举与此前 `SetAlpha(job.To)` 的语义保持一致。
- **`Runtime/Tween/` 拆分为三个文件**：`ToolkitTween.cs`（门面 + 句柄）/ `ToolkitTweenJob.cs`（通道枚举 + 作业）/ `ToolkitTweenRunner.cs`（运行器）。**`ToolkitTween.cs` 的 `.meta` GUID 保留**；迁出与新增的类型均为 `internal`（`ETweenChannel` / `TweenJob` / `ToolkitTweenRunner`）或非序列化值类型（`ToolkitTweenHandle` 无 `[Serializable]`，包内唯一使用处 `UiwListFadeCell._rootFade` 是私有非 `[SerializeField]` 字段），且 `ToolkitTweenRunner` 只在运行时由 `AddComponent` 创建、不存在于任何场景 / 预制体——**资源引用不受影响**。
- **`ToolkitTweenRunner` 移入同名文件**，消除此前「MonoBehaviour 类名与文件名不符」。
- `package.json` 的 `keywords` 补 `tween`（Tween 模块自 1.3.0 起随包发布，一直未列入）。

### 修复

- **订正 README 中 Tween 章节的过时表述**：包内三份 README 的正文仍写「当前提供 `CanvasGroup` 淡入淡出」，遗漏了 1.5.1 加入的 `FadeGraphic`；仓库根目录三份 README 的模块表同样只列了 `FadeCanvasGroup`。本次一并更正并补齐新增 API 与行为说明。

> **与 DOTween 的行为差异（迁移须知）**：① 本门面**不做覆盖管理**——对同一目标同一通道再起一个 tween 不会自动打断前一个，两者会在同一帧互相争写（DOTween 亦然，故 `DOKill(); DOFade(...)` 的写法可原样迁移为 `Kill(target); Fade…(target, …)`）；② 所有 API 的 `unscaled` 默认 `true`，而 DOTween 默认受 `Time.timeScale` 影响，需要还原 DOTween 行为时请显式传 `unscaled: false`；③ `DelayedCall(delay ≤ 0)` **同步立刻**触发回调并返回空句柄（DOTween 推迟到下一帧）——若调用方拿到句柄后才记进列表，请用 `if (h.IsActive) list.Add(h);` 守卫，否则回调里的 `list.Remove(h)` 会先于 `Add` 执行、留下永不移除的僵尸条目；④ 旋转等价 `RotateMode.Fast`，不支持多圈；⑤ 仍不提供 Sequence / 泛型链式 / 全套 Ease。

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
