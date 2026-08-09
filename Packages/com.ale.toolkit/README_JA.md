# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

Unity プラグイン開発向けの**汎用基盤ライブラリ**です。特定の業務ドメインの概念を一切含まず、複数のプラグインが属性設定・リスト・エディター基盤・多言語機能を共有できるようにします。

> 本パッケージは `com.ale.inventory` 1.8.0 から分離されました。インベントリシステム内に埋め込まれていた汎用機能（エディターの三列レイアウト基盤、バーチャルスクロールリスト、カスタム属性システム、エディター UI の三言語対応）をここへ抽出し、より多くのプラグインで再利用できるようにしたものです。

---

## 目次

- [インストール（最初にお読みください）](#-インストール最初にお読みください)
- [収録モジュール](#収録モジュール)
- [アセンブリ](#アセンブリ)
- [使い方と主要 API](#使い方と主要-api)
  - [属性システム](#属性システム) · [ソート](#ソート) · [UI](#ui) · [オブジェクトプール](#オブジェクトプール) · [Tween（中央イージング）](#tween中央イージング)
  - [属性モディファイア](#属性モディファイア) · [条件システム · Condition System](#条件システム--condition-system) · [効果システム · Effect System](#効果システム--effect-system)
  - [エディター基盤](#エディター基盤) · [エディター多言語](#エディター多言語) · [オプション依存のサポート層](#オプション依存のサポート層) · [エディタ入口とグローバル設定](#エディタ入口とグローバル設定) · [汎用ツールウィンドウ](#汎用ツールウィンドウ)
- [ライセンス](#ライセンス)

---

## ⚠️ インストール（最初にお読みください）

**`com.ale.toolkit` は、これに依存するプラグインより先にインストールする必要があります。**

Unity の Package Manager は **`package.json` の `dependencies` に git URL を書くことをサポートしていません**。そのため依存側のプラグインが本パッケージを自動取得することはできません。以下の**順序どおりに**手動で 2 回インストールしてください。

`Window > Package Manager` → 左上の `+` → `Install package from git URL...`

**手順 1 —— まず Toolkit をインストール：**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.5.1
```

**手順 2 —— 次に依存プラグイン**（例：インベントリシステム）をインストール：

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.11.1
```

> 順序が逆になったり本パッケージが未インストールの場合、Unity は `Ale.Toolkit.* が見つかりません` といったコンパイルエラーを出します。その際は本パッケージを追加インストールして再コンパイルを待つだけでよく、もう一方のプラグインを再インストールする必要はありません。

**Unity 2022.3** 以降が必要です（Unity 6000.3 で開発・保守）。

---

## 収録モジュール

| モジュール | 内容 |
| --- | --- |
| **属性システム** | 20 種類以上のフィールドタイプを持つ `AttributeValue`、属性定義（スキーマ）、カスタム列挙型、数値フォーマット設定、軽量な表示テキスト `TextValue`（fallback + オプションのネイティブローカライズ）。「属性項目を設定する」場面ではすべてこれを使用します |
| **ソート** | 要素の型に依存しないソートエンジン。ホスト側が `ISortContext<TData>` を実装して比較に必要な情報を提供し、エンジンが多段優先度とタイブレークを処理します |
| **UI** | バーチャルスクロールリスト（グリッド / 順次、オブジェクトプール + 可視領域のみ描画；セルの割り当て / 回収時のフェードイン・アウトを `UiwListFadeCell` + エンジン既定フックで汎用駆動）、タブバー、フィルターバー、ツールチップ基底クラス、ウィジェットプール |
| **オブジェクトプール** | 汎用の GameObject / プレハブプール（`Spawn`/`Despawn` + `IPoolable` コールバック、プリロード / 容量リサイクル / 遅延デスポーン / シーン跨ぎ）と、純 C# 参照型プール `ToolkitClassPool<T>`（GC 削減）。Lean.Pool 等のサードパーティ製プールを置き換え可能 |
| **Tween** | 軽量な中央 Tween（DOTween 風の単一 Update ポーリング、ジョブをプール化して GC ほぼゼロ）：`FadeCanvasGroup` / `FadeGraphic` / `FadeSpriteRenderer` の alpha フェード、`TintGraphic` の全色トランジション、`MoveTransform` / `RotateTransform` / `ScaleTransform`、`DelayedCall`、ターゲット単位の `Kill(target)`。中断可能な値型ハンドルを返す。イージング最小セット `EToolkitEase` |
| **属性モディファイア** | GAS 風のモディファイア評価：`ModifierDefinition` + `ModifierStackEvaluator` によるグループ集計（Add→PercentAdd→Multiply→Override + clamp + ソース明細）。「基礎値 + 一連の加算 → 現在値」という数値集約はすべてこれを使用します |
| **条件システム（Condition System）** | データ駆動の二段 AND/OR 条件：`ConditionExpression` フィールドを宣言するだけで Inspector 内にインラインで設定；上位が実装する `[ConditionEvaluator]` 判定器が自動的に発見されます。エンジン非依存の Core はサーバーサイドでも動作可能 |
| **効果システム（Effect System）** | 条件システムの書き込み側ミラー：データ駆動の離散トリガー式ミューテーション（フェーズグループ + 各項目にオプションの条件ゲート）；上位が実装する `[EffectExecutor]` 実行器が自動的に発見されます。エンジン非依存の Core |
| **エディター基盤** | 三列レイアウトのタブ基底クラス、データベースウィンドウのシェル基底クラス、マスターリストパネル、エンティティリストパネル、ツールウィンドウ基底クラス。いずれもデータベース型についてジェネリック化されています |
| **エディター多言語** | 中文 / English / 日本語 の三言語サービス。中国語原文をキーとし、訳文が無い場合は自動的にフォールバックします |
| **オプション依存のサポート層** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）のマクロ切り替えとアダプター |
| **エディタ入口とグローバル設定** | Ale Toolkit ウェルカムウィンドウ（`Tools > Ale Toolkit > Welcome`）：エディタ UI 言語 / 列挙翻訳 / 3 つのオプション機能マクロ / ウィザードのデフォルト・ローカライズフォント + 汎用ツール入口 +「起動時に自動表示」トグル。ウィザードフォントなどのプロジェクト単位の設定は `ProjectSettings/AleToolkitSettings.asset` に保存（リポジトリと共にコミット、アセット参照は GUID で保持）、言語 / 自動表示はユーザーごと（EditorPrefs）。旧マクロ `IS_*` は読み込み時に `ATK_*` へ自動移行 |
| **汎用ツールウィンドウ** | 任意のデータアセット（`ScriptableObject`）の全 `AttributeValue` を走査して一括処理：Addressable 移行（Object ↔ GUID）とローカライズキー生成。`Tools > Ale Toolkit` 配下、上位プラグインで再利用可能 |

> 上記のモジュールはすべて配置済みです —— 1.1.0 以降、3 つのオプション依存サポート層（TMP / Localization / Addressables）が揃い、toolkit 単体のプロジェクトでもエディタ UI は 3 言語対応です。**1.2.0 以降はプロジェクト単位のグローバル設定（言語 / マクロ）を担い、任意のデータアセットで動作する汎用ツールウィンドウを提供します**。**1.3.0 以降は汎用オブジェクトプール（GameObject プール + 純 C# クラスプール）と軽量な中央 Tween を追加します**。**1.4.0 以降は属性モディファイア評価、データベースウィンドウのシェル基底クラス、および 2 つの独立したサブシステム —— 条件システム（`Ale.Condition`）と効果システム（`Ale.Effect`）—— を追加します**。**1.5.0 以降は軽量な表示テキスト値 `TextValue`（fallback + オプションのネイティブローカライズ、`AttributeValue` の `Text` タイプの独立軽量版）を追加します**。**1.5.1 から、仮想スクロールリストにセルの汎用フェードイン / アウト（`UiwListFadeCell` + `IUiwRecycleFadeCell` / `IUiwDiffCell`、`UiwVirtualListBase` の既定フックで駆動）と `ToolkitTween.FadeGraphic` を追加**。**1.6.0 から、中央 Tween に `SpriteRenderer` フェード、`Graphic` の全色トランジション、`Transform` の移動 / 回転 / スケール、遅延コールバック、ターゲット単位の Kill を追加し、DOTween の一般的な単一 tween 用途をひと通り置き換え可能に（Sequence は引き続き非対応）**。詳細は [CHANGELOG](CHANGELOG.md) をご覧ください。

---

## アセンブリ

| Assembly Definition | 役割 | マクロ制約 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性システム、ソート、アセット読み込み抽象、共通シリアライズ、オブジェクトプール、中央 Tween、属性モディファイア評価 | — |
| `Ale.Toolkit.UI` | バーチャルスクロールリストと汎用 UI コントロール | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 対応コンポーネント | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables の読み込みとハンドル管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | エディター基盤、データベースウィンドウのシェル基底クラス、属性ドロワー、多言語サービス、マクロ切り替え | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables のエディターツール | `ATK_ADDRESSABLE` |
| `Ale.Condition.Core` | 条件システム · エンジン非依存モデル / 判定エンジン / 登録とリフレクション発見 / JSON（`noEngineReferences`、サーバーサイド可） | Newtonsoft を参照 |
| `Ale.Condition.Runtime` | 条件システム · Unity ブリッジ（`ConditionAsset` + 起動時自動登録） | — |
| `Ale.Condition.Editor` | 条件システム · インラインドロワー / カタログ / ウェルカムウィンドウ | — |
| `Ale.Effect.Core` | 効果システム · エンジン非依存モデル / 実行ランナー / 登録とリフレクション発見 / JSON（`noEngineReferences`） | `Ale.Condition.Core` + Newtonsoft を参照 |
| `Ale.Effect.Runtime` | 効果システム · Unity ブリッジ（`EffectAsset` + 起動時自動登録） | — |
| `Ale.Effect.Editor` | 効果システム · インラインドロワー / カタログ / ウェルカムウィンドウ | — |

依存の向きは一方向です：ホストプラグイン → `Ale.Toolkit.*` / `Ale.Condition.*` / `Ale.Effect.*`。本パッケージがホストプラグインを逆参照することはありません。条件 / 効果の 2 サブシステムは名前空間が独立しており（`Ale.Condition` / `Ale.Effect`）、`Ale.Effect.Core` は `Ale.Condition.Core` を一方向に参照します（効果項目のオプション条件ゲート用）。

---

## 使い方と主要 API

> ランタイム型は `Ale.Toolkit.Runtime` / `Ale.Toolkit.Runtime.UI`、エディター型は `Ale.Toolkit.Editor` にあります。以下はモジュールごとの典型的な使い方と主要な入口です。完全なシグネチャはソースの XML コメントを参照してください。

### 属性システム

`AttributeValue` は「型付きの 1 つの値」を保持します（スカラーは `[0]`、配列は `[0..n]`）。型は `EFieldType`（Int / Float / String / Bool / Enum / Vector2~4 / Color / Sprite / Text / Prefab / AudioClip / StringIntPair / EnumIntPair など 24 種）。フィールドのスキーマは `AttributeDefinition` が記述し、エンティティは `AttributeOwner` からフィールド id で値を取得します。

```csharp
var v = new AttributeValue(EFieldType.Int);
v.SetInt(0, 10);
int hp      = v.GetInt(0);
string show = v.ToDisplayString();     // 表示文字列（配列は区切り文字で連結）
double key  = v.ToComparableNumber();  // ソート用の数値

// エンティティ（AttributeOwner）からフィールド id で取得
AttributeValue atk = owner.GetAttributeValue("attack");
```

- `AttributeValue`：`Type` / `IsArray` / `Count`；`GetInt/SetInt`、`GetFloat/SetFloat`、`GetString/SetString`、`GetObject/SetObject`、`GetColor/SetColor`、`GetVector2~4`、`GetTextValue/SetTextValue/ResolveText`、`SetStringIntPair/SetEnumIntPair`；配列 `AddElement/RemoveElement/ReorderElements`；`ToDisplayString()`、`ToComparableNumber()`、`ChangeType()`、`Clone()`。
- `AttributeDefinition.CreateValue()`；`AttributeOwner.GetEntry(id)` / `GetAttributeValue(id)`；`AttributeSync.Sync(...)` はスキーマに従ってエンティティの値集合を同期。
- `ConfigTemplateBase`（`name` / `color` / `List<AttributeDefinition> attributes`）；`EnumType`（`AddItem` / `GetItemByValue` / `GetDisplayName`）+ `EnumItem`；`NumberFormatConfig.Format(long, langCode)`。
- **`TextValue`**（軽量な表示テキスト、`AttributeValue` の `Text` タイプの独立軽量版）：`Fallback`（常に存在）+ `ATK_LOCALIZATION` 有効時は Unity ネイティブの `LocalizedString`（`Localized`）を内包；`ResolveText()` はローカライズ優先で、取得できなければ fallback を返します；`IsEmpty` / `Clone()`。インスタンスごとに string は 1 つのみ（+ ローカライズ時に `LocalizedString` が 1 つ）で、`AttributeValue` のマルチタイプなバックアップリストのオーバーヘッドがありません。エディター `TextValueDrawer`（`[CustomPropertyDrawer(typeof(TextValue))]`）が「fallback 行 + ネイティブのテーブル / エントリ選択器」を描画し、フィールドを宣言すれば Inspector で設定でき、選択すればそのまま正しく保存されます。

### ソート

自分のデータ型に対して `ISortContext<TData>` を一度実装（または `SortContextBase<TData>` / `TagSortContextBase<TData>` を継承）すれば、ドメイン非依存の `AttributeSortService` を再利用できます：`SortPriority`（フィールド + 昇降順）を順に評価し、非ゼロになるまで比較します。

```csharp
class MySortCtx : SortContextBase<MyData> { /* OwnerOf / FindDefinition / OptionOf / TryCompareSpecial をオーバーライド */ }

AttributeSortService.Sort(list, priorities, new MySortCtx());
int cmp = AttributeSortService.Compare(a, b, priorities, ctx);
```

- `AttributeSortService.Sort<TData>(list, priorities, ctx)` / `Compare(...)` / `CompareByField(...)`。
- `ISortContext<TData>`：`OwnerOf` / `FindDefinition` / `OptionOf` / `TryCompareSpecial`。
- `SortPriority`（フィールド + 方向）、`SortOption`（フィールド別の無視リスト）、`SortFieldKeys`、`ISortId`、`SortOptionSync`。

### UI

`Ale.Toolkit.Runtime.UI` 配下の再利用可能なランタイムコントロール（すべてジェネリック）。

- **バーチャルスクロールリスト** `UiwVirtualGridList<TData,TCell>`（グリッド）/ `UiwVirtualOrderList<TData,TCell>`（順次）：継承して `BindCell` / `ClearCell` を実装し、Inspector で `cellPrefab` / `scrollRect` / `content` を接続、データを渡すと可視領域のみ描画・フレームごとに生成をレート制限。主なメソッド：`SetItems` / `UpdateItems` / `RefreshItemsData` / `SetSourceItems`、`ConfigureFilter` / `SetExtraFilter`、`ConfigureSort`、`ScrollToStart`。
- **タブバー** `UiwTabStrip<TTab,TValue>`（純 C#）：`Configure(prefab, container, bind, onSelect)` → `SetTabs(values, labels, …)` → `Select` / `SelectValue`；行を作り直さず差分再利用。フィルタータブバー `UiwFilterTabBar`（MonoBehaviour）：`SetFilters(tagNames)` / `Clear`。
- **ホバーツールチップ** `UiwTooltipBase<TPayload>`：サブクラスが `ApplyContent` / `ClearContent` を実装し、独自の `Show`（内部で `ShowTooltip` を呼ぶ）を公開；`Hide()`。
- **ウィジェットプール** `UiwWidgetPool<T>`（カーソル式の再利用）：`Configure` → `Begin` → `Next(out created)` → `End`。
- その他：`UiwViewBase`（`Open`/`Close`/`ToggleOpenClose`；`IsOpen` 状態 + `Start` 時に `activeInHierarchy` なら自動オープン、`Start` をオーバーライドするサブクラスは末尾で `base.Start()` を呼ぶこと）、`UiwSortToolbar`（`SetOptions`/`SetSortPriorities`）、`UiwNumberCounter`（`Configure`/`SetRange`/`SetValue`）、`UiwTextLabel`、`SpriteSlot.Bind(image, value)`。

### オブジェクトプール

Lean.Pool 等のサードパーティ製プールを置き換え。GameObject / プレハブプールと純 C# クラスプールの 2 種（`Ale.Toolkit.Runtime`）。

```csharp
// 静的ファサード：プレハブごとに自動でプール生成。Instantiate / Destroy を置き換え
var go = ToolkitPool.Spawn(prefab, pos, Quaternion.identity, parent);
ToolkitPool.Despawn(go);            // 所有テーブル経由で返却。Despawn(go, delay) も可

// またはプールコンポーネントを明示的に保持
var pool = host.AddComponent<ToolkitGameObjectPool>();
pool.Prefab = prefab; pool.Preload = 3;
var clone = pool.Spawn(pos, rot, parent);

// 純 C# オブジェクトで GC 削減（空なら null を返す）
var ctx = ToolkitClassPool<Ctx>.Spawn() ?? new Ctx();
ToolkitClassPool<Ctx>.Despawn(ctx, c => c.Reset());
```

- `ToolkitGameObjectPool`：`Prefab` / `Preload` / `Capacity` / `Recycle` / `Persist` / `Notification`；`Spawn(...)` / `Despawn(clone, delay)` / `DespawnAll` / `Clear`。
- `IPoolable`（`OnSpawn` / `OnDespawn`）；`ToolkitPool.Spawn/Despawn/DespawnAll/Detach`、所有テーブル `Links`；`ToolkitClassPool<T>.Spawn(...)/Despawn(...)`。

### Tween（中央イージング）

軽量な中央 Tween ファサード（DOTween 風の「単一 Update ポーリングのジョブ表」、`Ale.Toolkit.Runtime`）。`CanvasGroup` / `Graphic`（Image / テキスト）/ `SpriteRenderer` の alpha フェード、`Graphic` の全色トランジション、`Transform` の移動 / 回転 / スケール、および純粋な遅延コールバックを提供します。ジョブは `ToolkitClassPool` でプール化、常駐ランナーが単一 `LateUpdate` で推進、GC ほぼゼロ。DOTween の Sequence / チェーン / 全イージングは再現せず、必要に応じて増分拡張します。

```csharp
// CanvasGroup を 0.2 秒で alpha=1 へフェード；中断可能なハンドルを返す
var h = ToolkitTween.FadeCanvasGroup(canvasGroup, 1f, 0.2f, EToolkitEase.OutQuad,
                                     unscaled: true, onComplete: () => { /* 完了 */ });
h.Kill(complete: true);    // 中断して終端値へ即時設定 + onComplete 発火；Kill(false) は中断のみ（コールバックなし）
h.Complete();              // Kill(true) と同じ
bool running = h.IsActive;

// スプライトのフェード / キャラのスムーズ移動 / 遅延コールバック
ToolkitTween.Kill(spriteRenderer);                       // 先に当該ターゲットの実行中ジョブを中断（本ファサードは上書き管理をしない）
ToolkitTween.FadeSpriteRenderer(spriteRenderer, 1f, 0.3f);
ToolkitTween.MoveTransform(actor, targetPos, duration, EToolkitEase.InOutQuad);
ToolkitTween.Kill(actor, complete: true);                // DOTween の transform.DOComplete() 相当
var delay = ToolkitTween.DelayedCall(1.5f, () => Play(), owner: this);
```

- フェード / カラー：`FadeCanvasGroup(target, endAlpha, duration, ease = OutQuad, unscaled = true, onComplete = null)`、`FadeGraphic(…)`、`FadeSpriteRenderer(…)`、`TintGraphic(target, endColor, …)`（全 RGBA）。
- Transform：`MoveTransform(target, endPosition, …)`、`RotateTransform(target, endEulerAngles, …)`、`ScaleTransform(target, endScale, …)`。回転は**軸ごとに最短弧**を通ります（DOTween の `RotateMode.Fast` 相当。多回転には非対応）。換算式は `ShortestEuler(fromEuler, toEuler)` として公開。
- 遅延：`DelayedCall(delay, onComplete, unscaled = true, owner = null)`。任意の `owner` で寿命を紐付け：`Destroy` されるとコールバックは破棄され、`Kill(owner)` でキャンセルできます。
- 中断：`Kill(target, complete = false)` は当該ターゲットの実行中ジョブをすべて中断し、その件数を返します（DOTween のターゲット登録表に相当、`DOKill` / `DOComplete`）。照合は**参照等価**なので、破棄済みターゲットでも自分のジョブを掃除できます。`Kill(gameObject)` ではそこに付いたコンポーネントのジョブは見つかりません。
- `ToolkitTweenHandle`（値型、ゼロアロケーション）：`IsActive` / `Kill(complete = false)` / `Complete()`；`IEquatable<>` と `==` を実装し、`List<>` にそのまま入れて `Remove` できます。`default` は無効ハンドルで `Kill` / `Complete` は安全な no-op。
- `ToolkitEase.Evaluate(EToolkitEase ease, float t)`；イージング種別 `EToolkitEase`：`Linear` / `InQuad` / `OutQuad` / `InOutQuad`。
- すべての入口は `duration ≤ 0` またはターゲットが空なら即座に終端へ設定し、空ハンドルを返します。

DOTween との差異が 3 点あります：**①上書き管理をしない**——同一ターゲット・同一チャンネルに再度 tween を掛けても前のものは中断されません（DOTween も同様）。先に `Kill(target)` を呼んでください。**②`unscaled` の既定は `true`**（DOTween は既定で `Time.timeScale` の影響を受ける）。DOTween の挙動に揃えるなら `unscaled: false` を明示してください。**③`DelayedCall(delay ≤ 0)` は同期的に即発火**します（DOTween は 1 フレーム遅延）。ハンドルをリストに登録する場合は `if (h.IsActive) list.Add(h);` でガードしてください。

### 属性モディファイア

GAS 風のモディファイア評価（`Ale.Toolkit.Runtime`）。宣言的な `ModifierDefinition` を 1 つの属性に集約し、`ModifierStackEvaluator` が固定順序でグループ集計して「現在値 + ソースごとの明細」を算出します。静的・無状態・Unity 非依存で、持続時間の満了 / スタッキングのランタイムループは**含みません**（設定として携え、ホストがランタイムで計算した後、有効なモディファイアを渡します）。

```csharp
var mods = new List<ModifierDefinition> {
    new ModifierDefinition("atk", EModifierOperation.Add,        5f,   "trait:勇敢"),
    new ModifierDefinition("atk", EModifierOperation.PercentAdd, 0.1f, "buff:狂暴"),
};
// base 10、clamp[0,100]；集計：10 → +5 → ×(1+0.1) = 16.5
ModifierEvaluation r = ModifierStackEvaluator.Evaluate(10f, 0f, 100f, mods);
float now = r.Value;                              // 16.5
foreach (var c in r.Breakdown)                    // ソースごと：SourceTag / Operation / Magnitude / Delta
    Debug.Log($"{c.SourceTag} {c.Operation} {c.Delta}");
```

- `ModifierDefinition`：`targetAttributeId`（不透明なキー、評価器は解釈しない）/ `operation` / `magnitude` / `duration` / `durationDays` / `sourceTag`（ソース明細 + グループ単位の取り消し）/ `stackLimit` / `stackRule`。
- `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown = true)` → `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`；軽量な `EvaluateValue(...)` は最終値のみ返す。集計順序は固定：`base → +ΣAdd → ×(1+ΣPercentAdd) → 各項 ×(1+magnitude) Multiply → 最後に Override で上書き → clamp[min,max]`。呼び出し側は先に `targetAttributeId` でグループ化する必要があり、持続時間 / スタッキングはランタイムで計算した後に渡します。
- 列挙：`EModifierOperation`（`Add`/`PercentAdd`/`Multiply`/`Override`）、`EModifierDuration`（`Instant`/`Timed`/`Permanent`）、`EStackRule`（`Refresh`/`Add`/`EveryXStacks`/`OnMaxStacks`）。

### 条件システム · Condition System

データ駆動の二段 AND/OR 条件（名前空間 `Ale.Condition`）。中核となる考え方：**`ConditionExpression` フィールドを 1 つ宣言すれば、Inspector に二段条件エディターがその場で現れる**；上位システムが自前の「原子的判定器」を実装すると自動的に発見され、コアはいかなるドメイン概念も知りません。エンジン非依存の Core（`noEngineReferences`）はサーバーサイドでも動作可能です。3 つのアセンブリ：`Ale.Condition.Core`（モデル + エンジン + 登録 + JSON）/ `.Runtime`（Unity ブリッジ）/ `.Editor`（インラインドロワー + カタログ + ウェルカムウィンドウ）。

**① 条件フィールドを宣言（設定側、UI コードゼロ）**

```csharp
// 任意の MonoBehaviour / ScriptableObject / シリアライズ可能な設定クラス
public ConditionExpression eligibility = new ConditionExpression();
```

カスタム Inspector 内でその `SerializedProperty` に `EditorGUILayout.PropertyField(prop, true)` を呼ぶだけで、完全な二段 AND/OR エディター（グループ / 項目 / パラメータの増減、And·Or、NOT、判定器のカテゴリ別ドロップダウン、スキーマに応じた動的パラメータ領域）が得られ、Undo も自動対応。または SO コンテナ `ConditionAsset`（`Create > Ale > Condition > Condition Asset`）を使用。

**② 判定器を拡張（上位で実装）**

```csharp
using Ale.Condition;

public interface IMyStatSource { float Get(string statId); }   // 上位のカスタム読み取り側サービス（エンジン非依存）

[ConditionEvaluator("My.StatAtLeast")]
public sealed class StatAtLeastEvaluator : IConditionEvaluator
{
    private static readonly ConditionParamDef[] Schema = {
        new ConditionParamDef("stat",  ConditionParamType.String, false, "属性"),
        new ConditionParamDef("value", ConditionParamType.Float,  false, "閾値"),
    };
    public string Key => "My.StatAtLeast";
    public string DisplayName => "属性が基準達成";
    public string Category => "My";                            // エディターのドロップダウン分類
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

`ParamSchema` がエディターの動的パラメータ領域を駆動します；固定的な選択肢（比較記号など）は `ConditionParamDef` の `choices`（ドロップダウンとして描画、インデックスを保存）を使用。パラメータは 5 型：`String` / `Int` / `Float` / `Bool` / `Enum`（+ `isArray`）。

**③ コンテキストを提供 + 評価（ランタイム）**

```csharp
class MyCtx : IConditionContext {              // 主体 + サービスパック（ホストが実装）
    public object Subject { get; set; }
    private readonly object[] _svc;
    public MyCtx(params object[] svc) { _svc = svc; }
    public T GetService<T>() where T : class { foreach (var s in _svc) if (s is T t) return t; return null; }
}

var ctx = new MyCtx(myStatSource);
bool ok = expr.Evaluate(ctx).Passed;                        // 簡便メソッド
ConditionResult r = ConditionEngine.Evaluate(expr, ctx);    // またはエンジンを直接呼ぶ；r.FailedKeys は未達成のキー一覧
```

ランタイムの `ConditionRuntime` は `[RuntimeInitializeOnLoadMethod]` で `ConditionRegistry.Default` をリフレクションで充填し、欠落キーの警告を接続します；サーバーサイド / テストでは手動で `new ConditionRegistry()` + `AutoRegisterFromAssemblies()`、あるいは 1 つずつ `Register` できます。

**組み込み判定器**：`Condition.AlwaysTrue`、`Condition.HasFlag`（`IConditionFlagSource`）、`Condition.NumberCompare`（`IConditionNumberSource`）。**JSON**：`ConditionJson.ToJson(expr)` / `FromJson(str)`（Newtonsoft；モデルは純 POCO でシリアライザ差し替え可能、セーブデータへの格納も可）。**総覧**：`Tools > Ale Toolkit > Condition System > Welcome`。

### 効果システム · Effect System

条件システムの**書き込み側ミラー**（名前空間 `Ale.Effect`）：データ駆動・パラメータ化された**離散トリガー式ミューテーション**で、「フェーズグループ」で組織し、各項目にオプションの条件ゲートを掛けられます。数値加算（buff）は上記の**属性モディファイア**が担当し、効果は離散的なアクション（付与 / 除去、フラグ設定、イベント発火、着火…）のみ行います。同じく「`EffectExpression` フィールドを宣言すれば Inspector で設定」でき、上位が実装する `[EffectExecutor]` 実行器が自動的に発見されます。3 つのアセンブリ：`Ale.Effect.Core`（ゲート用に `Ale.Condition.Core` を参照）/ `.Runtime` / `.Editor`。

**構造**：`EffectExpression → EffectGroup(phase タイミングタグ) → EffectItem(key + パラメータ + オプションの gate)`。同一フィールドに複数のフェーズグループ（`onGained` / `onLost` など）を置けます；グループ内は**順次実行**、ランタイムは `phase` で絞り込みます（空 phase のグループはワイルドカードで、任意の phase で実行）。

**① 効果フィールドを宣言**

```csharp
public EffectExpression onGained = new EffectExpression();   // Inspector 内のインラインフェーズグループエディター；または EffectAsset SO を使用
```

**② 実行器を拡張（上位で実装、「着火」の例を含む）**

```csharp
using Ale.Effect;

public interface ICombatEffectSink { void Ignite(float radius, int mode, int count); }

[EffectExecutor("Combat.Ignite")]
public sealed class IgniteEffect : IEffectExecutor
{
    private static readonly EffectParamDef[] Schema = {
        new EffectParamDef("radius", EffectParamType.Float, false, "直径(メートル)"),
        new EffectParamDef("target", EffectParamType.Int,   false, "ターゲット選択",
            choices: new[] { "ランダム", "最も近い", "最も遠い" }),  // 固定列挙 → ドロップダウンでインデックス保存
        new EffectParamDef("count",  EffectParamType.Int,   false, "ターゲット数"),
    };
    public string Key => "Combat.Ignite";
    public string DisplayName => "着火";
    public string Category => "Combat";
    public IReadOnlyList<EffectParamDef> ParamSchema => Schema;

    public EffectResult Execute(IReadOnlyList<EffectParam> ps, IEffectContext ctx)
    {
        var sink = ctx?.GetService<ICombatEffectSink>();
        if (sink == null) return EffectResult.Failed("ICombatEffectSink がありません");
        sink.Ignite((float)ps.Find("radius").GetFloat(),
                    (int)ps.Find("target").GetInt(),
                    (int)ps.Find("count").GetInt());
        return EffectResult.Applied;
    }
}
```

**③ コンテキスト + 実行（ランタイム）**

```csharp
// IEffectContext : IConditionContext —— 同一コンテキストが gate 条件の読み取りサービスと効果の書き込み Sink の両方を提供
class MyEffectCtx : IEffectContext {
    public object Subject { get; set; }
    private readonly object[] _svc;
    public MyEffectCtx(params object[] svc) { _svc = svc; }
    public T GetService<T>() where T : class { foreach (var s in _svc) if (s is T t) return t; return null; }
}

var ctx = new MyEffectCtx(combatSink, myFlagSource /* gate 用 */);
EffectRunReport rep = onGained.Run(ctx, phase: "onGained");     // または EffectRunner.Run(onGained, ctx, "onGained")
Debug.Log($"適用 {rep.Applied} / スキップ {rep.Skipped} / 失敗 {rep.Failed}");
```

各項目に gate（内包する `ConditionExpression` を 1 つ、エディター内でその場に展開して設定）を掛けた場合、ランナーはまず `ConditionEngine` で評価し、不満足なら `Skipped` とします。ランタイムの `EffectRuntime` は `[RuntimeInitializeOnLoadMethod]` で全実行器を自動登録します。

**組み込み実行器**：`Effect.NoOp`、`Effect.SetFlag`（`IEffectFlagSink`）、`Effect.AdjustNumber`（`IEffectNumberSink`）—— それぞれ条件システムの `HasFlag` / `NumberCompare` の書き込み側の対偶です。**JSON**：`EffectJson.ToJson/FromJson`（内包する gate もグラフと共に往復）。**総覧**：`Tools > Ale Toolkit > Effect System > Welcome`。

> **UE5 GAS との境界**：GAS `GameplayEffect` の数値側（Modifiers / Duration / Stacking）は上記の**属性モディファイア**がカバーします；効果システムはその実行側（Executions / Cues / Conditional Effects）—— 離散トリガーアクション —— に対応します。両者の分担は明確です：**モディファイアは「値」を、効果は「事」を管理**。

### エディター基盤

`Ale.Toolkit.Editor`。すべてデータベース型についてジェネリックで、ホストプラグインが継承して少数の抽象メンバーをオーバーライドするだけでエディターを構築できます。

- **データベースウィンドウのシェル** `EditorDatabaseWindowBase<TDb>`：「DB アセットのオブジェクトフィールド + 上部タブバー + 検証 / エクスポートボタンのフック + 重複スキャンのオーケストレーション + ステータスバー + Undo 購読 + 直近 DB パスの記憶（EditorPrefs）」を内蔵し、`IEditorDbContext<TDb>` を実装。ホスト側ウィンドウはタブ集合 / エクスポート・検証コールバック / 重複チェック種別を提供するだけで大幅に薄くできます。
- **三列タブ** `EditorThreeColumnTab<TDb,TEntity>`：左列サブタブ + マスターリスト、中列エンティティリスト、右列コンテキストインスペクター。`LeftPanels` / `EntityNoun` / `EntityList` / `DrawEntityList` / `DrawEntityInspector` などをオーバーライド。
- **マスターリストパネル** `EditorMasterListPanel<TDb,T>`（+ `IEditorMasterListPanel<TDb>`）、**エンティティリストパネル** `EditorEntityListPanel<TDb,TEntity,TTemplate>`。
- **ツールウィンドウ基底** `EditorToolWindowBase<TDb>`：「データベース選択 + フレームごとの時間予算ステップ + プログレスバー + ログ + キャンセル + 完了処理」を内蔵。`DrawOperations`（`RunSteps` でフレームごとのステップを開始）/ `OnRunComplete` / `OnRunFinished` をオーバーライド。
- コンテキスト `IEditorContext` / `IEditorDbContext<TDb>`；補助コントロール `EditorSearchableList` / `EditorDraggableRowList` / `EditorReorderableDrag` / `EditorListKeyboardNav` / `EditorFilterTabs` / `EditorIdScanner` / `ToolkitEditorStyles`。

### エディター多言語

エディター UI テキストの三言語（中 / 英 / 日）サービス。中国語原文をキーとし、未翻訳時は中国語にフォールバック。ランタイムのコンテンツ多言語化とは無関係。

```csharp
using static Ale.Toolkit.Editor.ToolkitEditorL10n;
EditorGUILayout.LabelField(Tr("快捷操作"));       // 現在の言語のテキストを返す
string name = TrEnum(EFieldType.Sprite);          // 列挙の表示名

// ホストプラグインは [InitializeOnLoad] でドメイン訳表を登録
ToolkitEditorL10n.Add("道具", "Item", "アイテム");
ToolkitEditorL10n.AddEnum(MyEnum.Foo, "Foo", "フー");
```

- `ToolkitEditorL10n.Tr(zh)` / `TrEnum(enumValue)`；`Current`（`EditorLanguage`）/ `TranslateEnums`；`Add(zh, en, ja)` / `AddEnum(value, en, ja, zh = null)`。

### オプション依存のサポート層

TextMeshPro / Unity Localization / Addressables のマクロ切り替えとランタイムアダプター。マクロはプロジェクト単位のグローバル設定（`ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`）で、ウェルカムウィンドウから切り替え。旧マクロ `IS_*` は読み込み時に自動移行。

- `ToolkitDefines`：マクロ名定数 `Tmp` / `Localization` / `Addressable`；`IsTmpEnabled()` / `IsLocalizationEnabled()` / `IsAddressableEnabled()`。
- `DefineUtils`：`ApplyDefine(...)`（PlayerSettings のスクリプト定義を増減）、`HasNamespace(...)` / `HasClass(...)`（パッケージがインストール済みか検出）。これらで独自のマクロ切り替えパネルを構築可能。
- ランタイム資源ファサード `ToolkitAssets`（コアは Addressables 非依存）：`Bind<T>(value, owner, set)` / `Bind<T>(liveRef, address, owner, set)`（ホスト破棄時に自動解放）、`Load<T>` / `Release`；インターフェース `IAssetLoader`；`ATK_ADDRESSABLE` 有効時は `AddressableManager` がアドレス単位で参照カウントによるロード / アンロード。

### エディタ入口とグローバル設定

- `ToolkitWelcomeWindow`（メニュー **Tools > Ale Toolkit > Welcome**）：UI 言語 / 列挙翻訳の切り替え / 3 つのオプション依存マクロ / ウィザードのデフォルト・ローカライズフォント / 汎用ツール入口 / 起動時自動表示。
- `ToolkitProjectSettings`（`ScriptableSingleton`、`ProjectSettings/AleToolkitSettings.asset` に保存、リポジトリと共有、資源は GUID 参照）：`SaveSettings()`；ウィザードフォントはファサード `ToolkitPrefabFonts` 経由で読み書き。

### 汎用ツールウィンドウ

任意のデータアセット（`ScriptableObject`）の全 `AttributeValue` を走査して一括処理。上位プラグインで再利用可能。

- `ToolkitAddressableToolWindow`（メニュー **Tools > Ale Toolkit > Addressable**）：データベースの全資源フィールドを「Object 参照 ↔ AssetReference(GUID)」間で一括変換。ホストは `EditorAddressableToolWindow<TDb>` を継承し、属性システム外の名前付き Sprite フィールドを `FixedFields` で提供可能。
- `ToolkitLocalizationToolWindow`（メニュー **Tools > Ale Toolkit > Localization**）：ローカライズ Key を一括生成；基底クラス `EditorLocalizationToolWindow<TDb>`。
- リフレクション補助：`AttributeValueWalker`（DB 内の全属性オブジェクト値を走査）、`TextFieldWalker` / `TextFieldCollector`（テキスト値・id 対応 Key を走査）。

---

## ライセンス

[MIT](LICENSE.md)
