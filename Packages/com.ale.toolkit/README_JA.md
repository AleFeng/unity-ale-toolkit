# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

Unity プラグイン開発向けの**汎用基盤ライブラリ**です。特定の業務ドメインの概念を一切含まず、複数のプラグインが属性設定・リスト・エディター基盤・多言語機能を共有できるようにします。

> 本パッケージは `com.ale.inventory` 1.8.0 から分離されました。インベントリシステム内に埋め込まれていた汎用機能（エディターの三列レイアウト基盤、バーチャルスクロールリスト、カスタム属性システム、エディター UI の三言語対応）をここへ抽出し、より多くのプラグインで再利用できるようにしたものです。

---

## 目次

- [収録モジュール](#収録モジュール)
- [アセンブリ](#アセンブリ)
- [使い方と主要 API](#使い方と主要-api)
  - [属性システム](#属性システム) · [ソート](#ソート) · [UI](#ui) · [オブジェクトプール](#オブジェクトプール) · [Tween（中央イージング）](#tween中央イージング)
  - [属性モディファイア](#属性モディファイア) · [タグシステム · GameplayTag System](#タグシステム--gameplaytag-system) · [条件システム · Condition System](#条件システム--condition-system) · [効果システム · Effect System](#効果システム--effect-system)
  - [エディター基盤](#エディター基盤) · [エディター多言語](#エディター多言語) · [オプション依存のサポート層](#オプション依存のサポート層) · [エディタ入口とグローバル設定](#エディタ入口とグローバル設定) · [汎用ツールウィンドウ](#汎用ツールウィンドウ)
- [ライセンス](#ライセンス)

---

## 収録モジュール

| モジュール | 内容 |
| --- | --- |
| **属性システム** | 20 種類以上のフィールドタイプを持つ `AttributeValue`、属性定義（スキーマ）、カスタム列挙型、数値フォーマット設定、軽量な表示テキスト `TextValue`（fallback + オプションのネイティブローカライズ）。「属性項目を設定する」場面ではすべてこれを使用します |
| **ソート** | 要素の型に依存しないソートエンジン。ホスト側が `ISortContext<TData>` を実装して比較に必要な情報を提供し、エンジンが多段優先度とタイブレークを処理します |
| **UI** | バーチャルスクロールリスト（グリッド / 順次、オブジェクトプール + 可視領域のみ描画；セルの割り当て / 回収時のフェードイン・アウトを `UiwListFadeCell` + エンジン既定フックで汎用駆動）、タブバー、フィルターバー、ツールチップ基底クラス、ウィジェットプール |
| **オブジェクトプール** | 汎用の GameObject / プレハブプール（`Spawn`/`Despawn` + `IPoolable` コールバック、プリロード / 容量リサイクル / 遅延デスポーン / シーン跨ぎ）と、純 C# 参照型プール `ToolkitClassPool<T>`（GC 削減）。Lean.Pool 等のサードパーティ製プールを置き換え可能 |
| **Tween** | 軽量な中央 Tween（DOTween 風の単一 Update ポーリング、ジョブをプール化して GC ほぼゼロ）：`FadeCanvasGroup` / `FadeGraphic` / `FadeSpriteRenderer` の alpha フェード、`TintGraphic` の全色トランジション、`MoveTransform` / `RotateTransform` / `ScaleTransform`、`DelayedCall`、ターゲット単位の `Kill(target)`。中断可能な値型ハンドルを返す。イージング最小セット `EToolkitEase` |
| **属性モディファイア** | GAS 風のモディファイア評価（エンジン非依存アセンブリ `Ale.Modifier.Core`、名前空間 `Ale.Modifier`）：`ModifierDefinition` + `ModifierStackEvaluator` によるグループ集計（Add→PercentAdd→Multiply→Override + clamp + ソース明細）。「基礎値 + 一連の加算 → 現在値」という数値集約はすべてこれを使用し、効果システムの持続モディファイアも直接この型を産出します |
| **タグシステム（GameplayTag System）** | UE GameplayTag 流の階層タグ：`GameplayTag`（`Status.Debuff.Mental`、子孫を持てば祖先にマッチ）、設定側コンテナ `GameplayTagContainer`、ランタイムのカウントコンテナ `GameplayTagCountContainer`、タグ要件 `GameplayTagRequirements`、参考用レジストリ + タグ表アセット + エディターのタグツリードロップダウン；条件ブリッジ `Condition.HasGameplayTag` / `Condition.GameplayTags` |
| **条件システム（Condition System）** | データ駆動の二段 AND/OR 条件：`ConditionExpression` フィールドを宣言するだけで Inspector 内にインラインで設定；上位が実装する `[ConditionEvaluator]` 判定器が自動的に発見され、既製の比較記号キットと判定コンテキストをそのまま再利用できます。エンジン非依存の Core はサーバーサイドでも動作可能。**1.12.0 から**、すべての上位システムが共用する条件ライブラリ `ConditionDatabase`（条件エントリは表示名 / 説明 / アイコン、テンプレート駆動のカスタム属性、そして式を持つ）と Condition Editor が付属し、条件を id でシステム横断に参照できます |
| **効果システム（Effect System）** | UE5 GAS `GameplayEffect` 流の完全な効果システム：`EffectDefinition`（持続ポリシー / 周期 / スタック / タグ / 適用条件と確率 / モディファイア / フェーズ別実行 / キュー）+ ランタイムの `EffectContainer`（適用パイプライン、Tick、抑制、免疫、タグ指定除去、集約、セーブ）；実行層は従来どおりデータ駆動のフェーズグループ + 各項目にオプションの条件ゲートで、上位が実装する `[EffectExecutor]` 実行器が自動的に発見されます。エンジン非依存の Core；**1.10.0 から**すべての上位システムが共用する効果ライブラリ `EffectDatabase`（効果エントリ = 表示名 / 説明 / アイコン + テンプレート駆動のカスタム属性 + GAS 定義；テンプレート / ゲームプレイタグ / 列挙）と Effect Editor を同梱し、上位システムは効果 id の参照リスト + ジャンプボタンのみを保持 |
| **エディター基盤** | 三列レイアウトのタブ基底クラス、データベースウィンドウのシェル基底クラス、マスターリストパネル、エンティティリストパネル、ツールウィンドウ基底クラス。いずれもデータベース型についてジェネリック化されています |
| **エディター多言語** | 中文 / English / 日本語 の三言語サービス。中国語原文をキーとし、訳文が無い場合は自動的にフォールバックします |
| **オプション依存のサポート層** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）のマクロ切り替えとアダプター |
| **エディタ入口とグローバル設定** | Ale Toolkit ウェルカムウィンドウ（`Tools > Ale Toolkit > Welcome`）：エディタ UI 言語 / 列挙翻訳 / 3 つのオプション機能マクロ / ウィザードのデフォルト・ローカライズフォント + 汎用ツール入口 +「起動時に自動表示」トグル。ウィザードフォントなどのプロジェクト単位の設定は `ProjectSettings/AleToolkitSettings.asset` に保存（リポジトリと共にコミット、アセット参照は GUID で保持）、言語 / 自動表示はユーザーごと（EditorPrefs）。マクロはウェルカムウィンドウから明示的に切り替えるのみで、パッケージが PlayerSettings を自動で書き換えることはありません |
| **汎用ツールウィンドウ** | 任意のデータアセット（`ScriptableObject`）の全 `AttributeValue` を走査して一括処理：Addressable 移行（Object ↔ GUID）とローカライズキー生成。`Tools > Ale Toolkit` 配下、上位プラグインで再利用可能 |

> 上記のモジュールはすべて配置済みです —— 1.1.0 以降、3 つのオプション依存サポート層（TMP / Localization / Addressables）が揃い、toolkit 単体のプロジェクトでもエディタ UI は 3 言語対応です。**1.2.0 以降はプロジェクト単位のグローバル設定（言語 / マクロ）を担い、任意のデータアセットで動作する汎用ツールウィンドウを提供します**。**1.3.0 以降は汎用オブジェクトプール（GameObject プール + 純 C# クラスプール）と軽量な中央 Tween を追加します**。**1.4.0 以降は属性モディファイア評価、データベースウィンドウのシェル基底クラス、および 2 つの独立したサブシステム —— 条件システム（`Ale.Condition`）と効果システム（`Ale.Effect`）—— を追加します**。**1.5.0 以降は軽量な表示テキスト値 `TextValue`（fallback + オプションのネイティブローカライズ、`AttributeValue` の `Text` タイプの独立軽量版）を追加します**。**1.5.1 から、仮想スクロールリストにセルの汎用フェードイン / アウト（`UiwListFadeCell` + `IUiwRecycleFadeCell` / `IUiwDiffCell`、`UiwVirtualListBase` の既定フックで駆動）と `ToolkitTween.FadeGraphic` を追加**。**1.6.0 から、中央 Tween に `SpriteRenderer` フェード、`Graphic` の全色トランジション、`Transform` の移動 / 回転 / スケール、遅延コールバック、ターゲット単位の Kill を追加し、DOTween の一般的な単一 tween 用途をひと通り置き換え可能に（Sequence は引き続き非対応）**。**1.8.0 から、条件システムが「ホストごとに書き直していた」3 つの設備を Core に取り込みます —— 比較記号キット `ConditionCompare`、汎用判定コンテキスト `ConditionContext` / `SubjectConditionContext`、冪等な `ConditionRegistry.EnsureAutoRegistered()`**。**1.9.0 から、階層タグシステム（`Ale.GameplayTags`）を追加し、効果システムを GAS `GameplayEffect` の全体像（`EffectDefinition` + `EffectContainer`）に拡充、属性モディファイアをエンジン非依存の `Ale.Modifier.Core` に分離（⚠️ 名前空間は `Ale.Modifier` に変更）**。**1.10.0 から、すべての上位システムが共用する効果ライブラリ `EffectDatabase` と Effect Editor を追加（効果エントリは表示名 / 説明 / アイコンとテンプレート駆動のカスタム属性を持ち、上位は効果 id の参照リスト + ジャンプのみを保持；⚠️ `Ale.Effect.Runtime` が `Ale.Toolkit.Runtime` / `Ale.GameplayTags.Runtime` に依存するように）**；**1.11.0 から Effect Editor に「Effect Executors」タブを追加——プロジェクト内の全 `[EffectExecutor]` 実装のカタログ（検索 / カテゴリ / パラメータ schema / ソースへジャンプ / サイレント失効の診断 / 設定との相互参照）。元のタブは Effect Database に改名して 2 番目へ。エディターウィンドウのシェルに「このタブはデータベースを必要とするか」のフックとタブ記憶を追加**；**1.12.0 から条件システムを効果システムと対称な水準に拡充——共用条件ライブラリ `ConditionDatabase`（条件を id でシステム横断に参照でき、解決できない場合は不成立として扱う）と 2 タブの Condition Editor（Condition Evaluators で実装を確認、Condition Database でデータを設定）を追加し、条件パラメータに候補ソースの注入点 `ConditionDrawerHooks` を追加。⚠️ `Ale.Condition.Runtime` が `Ale.Toolkit.Runtime` に依存するように**。詳細は [CHANGELOG](CHANGELOG.md) をご覧ください。

---

## アセンブリ

| Assembly Definition | 役割 | マクロ制約 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性システム、ソート、アセット読み込み抽象、共通シリアライズ、オブジェクトプール、中央 Tween | — |
| `Ale.Toolkit.UI` | バーチャルスクロールリストと汎用 UI コントロール | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 対応コンポーネント | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables の読み込みとハンドル管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | エディター基盤、データベースウィンドウのシェル基底クラス、属性ドロワー、多言語サービス、マクロ切り替え | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables のエディターツール | `ATK_ADDRESSABLE` |
| `Ale.Modifier.Core` | 属性モディファイア · `ModifierDefinition` / `ModifierStackEvaluator` / 3 つの列挙（`noEngineReferences`；1.9.0 で `Ale.Toolkit.Runtime` から分離、名前空間 `Ale.Modifier`） | — |
| `Ale.GameplayTags.Core` | タグシステム · エンジン非依存モデル：タグ / コンテナ / カウントコンテナ / 要件 / レジストリ（`noEngineReferences`、参照なし） | — |
| `Ale.GameplayTags.Condition` | タグシステム · 条件ブリッジ：組み込み判定器 `Condition.HasGameplayTag` / `Condition.GameplayTags` | `Ale.Condition.Core` + `Ale.GameplayTags.Core` を参照 |
| `Ale.GameplayTags.Runtime` | タグシステム · Unity ブリッジ（`GameplayTagTable` アセット + 起動時登録 + `[GameplayTagField]`） | — |
| `Ale.GameplayTags.Editor` | タグシステム · カタログ / タグツリードロップダウン / コンテナと要件のドロワー / 表 Inspector / ウェルカムウィンドウ | — |
| `Ale.Condition.Core` | 条件システム · エンジン非依存モデル / 判定エンジン / 登録とリフレクション発見 / JSON、およびホストが再利用できる比較記号キット `ConditionCompare` と汎用判定コンテキスト `ConditionContext`（`noEngineReferences`、サーバーサイド可） | Newtonsoft を参照 |
| `Ale.Condition.Runtime` | 条件システム · Unity ブリッジ（`ConditionAsset` + 起動時自動登録）と共用条件ライブラリ `ConditionDatabase` / データマネージャ / 設定シリアライズ（1.12.0 から） | 1.12.0 から `Ale.Toolkit.Runtime` を参照 |
| `Ale.Condition.Editor` | 条件システム · インラインドロワー / カタログ / ウェルカムウィンドウ / パラメータ候補の注入点、および 2 タブの Condition Editor（1.12.0 から） | 1.12.0 から `Ale.Toolkit.Runtime` + `Ale.Toolkit.Editor` を参照 |
| `Ale.Effect.Core` | 効果システム · エンジン非依存モデル（`EffectDefinition` / `EffectExpression`）/ ランタイムコンテナ `EffectContainer` / 実行ランナー / 登録とリフレクション発見 / JSON（`noEngineReferences`） | `Ale.Condition.Core` + `Ale.GameplayTags.Core` + `Ale.Modifier.Core` + Newtonsoft を参照 |
| `Ale.Effect.Runtime` | 効果システム · Unity ブリッジ：共用の効果ライブラリ `EffectDatabase`（エントリ / テンプレート / ゲームプレイタグ / 列挙）+ `EffectDataManager` + `EffectConfigSerializer`（JSON / バイナリ）、`EffectAsset` / `EffectDefinitionAsset`、起動ブートストラップ（リセット + 加算） | `Ale.Toolkit.Runtime` + `Ale.GameplayTags.Runtime` を参照（1.10.0 から） |
| `Ale.Effect.Editor` | 効果システム · Effect Editor（効果 / テンプレート / タグ / 列挙）、効果カタログ `EffectEditorCatalog`、ホスト向け参照リストドロワー `EditorEffectRefListDrawer`、定義 / 振幅 / モディファイア / 式のドロワー、属性 id カタログ provider、ウェルカムウィンドウ | `Ale.Toolkit.Editor` + `Ale.GameplayTags.Editor` を参照（1.10.0 から） |

依存の向きは一方向です：ホストプラグイン → `Ale.Toolkit.*` / `Ale.Modifier.*` / `Ale.GameplayTags.*` / `Ale.Condition.*` / `Ale.Effect.*`。本パッケージがホストプラグインを逆参照することはありません。各サブシステムは名前空間が独立しており（`Ale.Modifier` / `Ale.GameplayTags` / `Ale.Condition` / `Ale.Effect`）、`Ale.Modifier.Core`・`Ale.GameplayTags.Core`・`Ale.Condition.Core` の 3 つは互いに参照せず、`Ale.GameplayTags.Condition` と `Ale.Effect.Core` が合流点です（階層：タグ < 条件 < 効果）。1.10.0 から `Ale.Effect.Runtime` は `Ale.Toolkit.Runtime`（効果ライブラリが属性システムを必要とするため）と `Ale.GameplayTags.Runtime` を、`Ale.Effect.Editor` は `Ale.Toolkit.Editor` / `Ale.GameplayTags.Editor` を参照します。`Ale.Toolkit.*` がサブシステムを逆参照することはなく、各 `*.Core` は引き続きエンジン非依存で、循環はありません。

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

GAS 風のモディファイア評価（アセンブリ `Ale.Modifier.Core`、名前空間 `Ale.Modifier`；**1.9.0 より前は `Ale.Toolkit.Runtime` にありました**——アップグレード後は asmdef に参照を追加し `using Ale.Modifier;` に変更してください。保存済みデータは影響を受けません）。宣言的な `ModifierDefinition` を 1 つの属性に集約し、`ModifierStackEvaluator` が固定順序でグループ集計して「現在値 + ソースごとの明細」を算出します。静的・無状態・Unity 非依存で、持続時間の満了 / スタッキングのランタイムループは**含みません**——それは[効果システム](#効果システム--effect-system)の仕事です：持続効果の `EffectContainer.CollectModifiers` が産出するのがまさにこの型で、ホストは自前の他のソースと一緒に評価器へ渡します。

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

- `ModifierDefinition`：`targetAttributeId`（不透明なキー、評価器は解釈しない）/ `operation` / `magnitude` / `sourceTag`（ソース明細 + グループ単位の取り消し）；さらに設定のみを携える 4 つの休眠フィールド `duration` / `durationDays` / `stackLimit` / `stackRule`——評価器も効果システムも読み取らず、持続時間 / 周期 / スタックは `EffectDefinition` が担います。
- `ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers, collectBreakdown = true)` → `ModifierEvaluation{ BaseValue, RawValue, Value, Breakdown }`；軽量な `EvaluateValue(...)` は最終値のみ返す。集計順序は固定：`base → +ΣAdd → ×(1+ΣPercentAdd) → 各項 ×(1+magnitude) Multiply → 最後に Override で上書き → clamp[min,max]`。呼び出し側は先に `targetAttributeId` でグループ化する必要があり、持続時間 / スタッキングはランタイムで計算した後に渡します。
- 列挙：`EModifierOperation`（`Add`/`PercentAdd`/`Multiply`/`Override`）、`EModifierDuration`（`Instant`/`Timed`/`Permanent`）、`EStackRule`（`Refresh`/`Add`/`EveryXStacks`/`OnMaxStacks`）。

### タグシステム · GameplayTag System

UE GameplayTag 流の階層タグ（名前空間 `Ale.GameplayTags`、アセンブリ `Ale.GameplayTags.Core` / `.Condition` / `.Runtime` / `.Editor`）。ドット区切り名で階層を表し、`Status.Debuff.Mental` は `Status.Debuff` と `Status` にマッチします（**子孫を持てば祖先にマッチ**）。単なる前方一致は数えません（`AB` は `A` にマッチしない）。序数比較・大文字小文字を区別——toolkit の他の文字列キーと同じです；正規化規則（全体と各セグメントを Trim、空セグメント / セグメント内の空白 / `/` は不正）はデータ形式であり、リリース後は凍結、テストで固定されています。

```csharp
using Ale.GameplayTags;

// 設定側：コンテナは List<string> を保存し、Unity / Newtonsoft でそのまま往復；[GameplayTagField] で string フィールドにタグツリードロップダウン
public GameplayTagContainer assetTags = new GameplayTagContainer();
[GameplayTagField] public string cueTag;

// ランタイム：所有者のカウントコンテナ（明示カウント + 暗黙の祖先カウント、O(1) の階層照会）
var owned = new GameplayTagCountContainer();
owned.AddTag(new GameplayTag("Status.Debuff.Mental"));
bool mental = owned.HasMatchingTag(new GameplayTag("Status.Debuff"));   // true：子孫を所有
owned.OnTagCountChanged += (tag, count) => { /* 効果コンテナはこれで抑制を再評価 */ };

// 要件：requireTags をすべて所有し、ignoreTags をひとつも所有しない
var req = new GameplayTagRequirements();
req.requireTags.AddTag("State.Alive");
req.ignoreTags.AddTag("Immunity.Mental");
bool ok = req.IsMet(owned);
```

- `GameplayTag`（読み取り専用構造体、**シリアライズされない**）：`IsValid` / `Depth` / `Parent` / `Root` / `Leaf`、`MatchesTag(parent)` / `MatchesTagExact` / `IsDescendantOf`、`Normalize` / `TryParse` / `Parse`。
- `GameplayTagContainer`（`[Serializable]`、唯一のフィールドは `List<string> tags`）：`AddTag` / `RemoveTag`（厳密）/ `RemoveTagsMatching`（サブツリー）、`HasTag`（階層）/ `HasTagExact` / `HasAny` / `HasAll`（空集合：All は true、Any は false）/ `Filter`、`Normalize` / `Clone`。
- `GameplayTagCountContainer`（ランタイム）：`AddTag/RemoveTag(tag, count)`、`AddTags/RemoveTags(container)`、`HasMatchingTag` / `HasExactTag` / `GetTagCount`、`GetExplicitTags`、イベント `OnTagCountChanged`。
- `GameplayTagRequirements`：`requireTags` / `ignoreTags`、`IsMet(...)`、`Validate`（require ∩ ignore はエラー）。
- **レジストリは参考用**：`GameplayTagRegistry.Default`（登録時に祖先を自動補完；`Validate` が未登録や大文字小文字違いのタイプミスを検出）はエディターのドロップダウンと設定時の検証にのみ使われ、**ランタイムのマッチングはレジストリを参照しません**。未登録のタグも通常どおりマッチします。ソース：`Resources` 配下の `GameplayTagTable` アセット（`Create > Ale > GameplayTag > Gameplay Tag Table`、起動時に自動登録）、ホストによる `GameplayTagRuntime.Register(...)` の明示登録（データベース内のカスタムタグなど）。
- **条件ブリッジ**（`Ale.GameplayTags.Condition`）：組み込み判定器 `Condition.HasGameplayTag(tag, exact)`、`Condition.GameplayTags(tags[], いずれか / すべて / なし, exact)`。主体のタグはコンテキストの `IGameplayTagSource` サービス、または `Subject as IGameplayTagOwner` で解決。**TagQuery は別途作りません**——AND / OR / NOT の組み合わせは `ConditionExpression` に任せます。ブリッジは `Ale.Condition.Core` にタグを参照させず独立アセンブリに置き、その「参照ゼロ」の約束を守ります。
- エディター：`GameplayTagContainer` / `GameplayTagRequirements` / `[GameplayTagField]` の 3 ドロワー（タグツリードロップダウン、不正は赤、未登録は黄の下地）、タグ表 Inspector（検証 / ソート）、`Tools > Ale Toolkit > GameplayTag System > Welcome`。
- 命名の注意：名前空間は複数形の `Ale.GameplayTags`——`Ale.GameplayTag` にすると `namespace Ale.*` 内で `GameplayTag t` と書いた際に名前空間が先に解決されます（CS0118）；属性は型との曖昧さ（CS1614）を避けて `[GameplayTagField]`。

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

`ParamSchema` がエディターの動的パラメータ領域を駆動します；パラメータは 5 型：`String` / `Int` / `Float` / `Bool` / `Enum`（+ `isArray`）。固定的な選択肢は `ConditionParamDef` の `choices`（ドロップダウンとして描画、インデックスを保存）を使用 —— 比較記号は**既製の `ConditionCompare` をそのまま使えます**。ラベル配列を自前で宣言する必要はありません：

```csharp
ConditionCompare.CreateOpParam()                       // choices = ConditionCompare.Labels を持つ "op" パラメータと等価
int  op = ConditionCompare.ReadOp(ps);                 // 既定は「以上」
bool ok = ConditionCompare.Compare(cur, need, op);     // 整数は厳密比較；double オーバーロードは epsilon 指定可（既定 1e-6）
```

> ⚠️ `ConditionCompare.Labels` の**文言と順序は通信フォーマット**です：インデックスは条件アセットにシリアライズされ、ラベル自体も文字列として利用側のスクリプトに入ることがあります（VN Framework 経由の会話条件など）。**ローカライズしないこと、並べ替えないこと。**

**③ コンテキストを提供 + 評価（ランタイム）**

```csharp
var ctx = new ConditionContext();                           // 汎用コンテキスト：型でドメインサービスを登録
ctx.RegisterService<IMyStatSource>(myStatSource);

bool ok = expr.Evaluate(ctx).Passed;                        // 簡便メソッド
ConditionResult r = ConditionEngine.Evaluate(expr, ctx);    // またはエンジンを直接呼ぶ；r.FailedKeys は未達成のキー一覧

// 「このオブジェクトは条件を満たすか」——共有コンテキストの Subject を触らずにラップする
bool okForHero = expr.Evaluate(new SubjectConditionContext(ctx, hero)).Passed;
```

`ConditionContext.GetService<T>()` は `typeof(T)` で**厳密に**引きます（インターフェースで登録したものはインターフェースでしか取得できません）。また意図的に `virtual` にしていません —— 評価のホットパス上にあるためです。独自の解決戦略（「カスタム優先、組み込みにフォールバック」など）が必要なホストは、継承ではなく **`IConditionContext` を直接実装**してください。toolkit はグローバルな既定インスタンスも**意図的に提供しません**：「どれが既定のコンテキストか」はホスト側の方針なので、各ホストが静的インスタンスを自前で保持します。

ランタイムの `ConditionRuntime` は `[RuntimeInitializeOnLoadMethod]` で `ConditionRegistry.Default` をリフレクションで充填し、欠落キーの警告を接続します；サーバーサイド / テストでは手動で `new ConditionRegistry()` + `AutoRegisterFromAssemblies()`、あるいは 1 つずつ `Register` できます。**エディターツールが再生モード外で評価する**場合はレジストリが空です（`ConditionRuntime` がまだ走っていないため）。冪等な `ConditionRegistry.Default.EnsureAutoRegistered()` で一度だけ補ってください。

**組み込み判定器**：`Condition.AlwaysTrue`、`Condition.HasFlag`（`IConditionFlagSource`）、`Condition.NumberCompare`（`IConditionNumberSource`）。**JSON**：`ConditionJson.ToJson(expr)` / `FromJson(str)`（Newtonsoft；モデルは純 POCO でシリアライザ差し替え可能、セーブデータへの格納も可）。**総覧**：`Tools > Ale Toolkit > Condition System > Welcome`。

**④ 条件ライブラリと Condition Editor（1.12.0 から）**

条件はホストのフィールドにインラインで持つほかに、**id を持つ名前付きエントリ**として設定し、複数のシステムで共用できます——「力 ≥ 10 かつ勇敢特質を持つ」を特質 / 職業 / 称号 / スキルツリーでそれぞれ作り直す必要はありません。器は `ConditionDatabase`（`Create > Ale > Condition > Condition Database`）で、トップレベルのリストは 3 つ：**条件エントリ** `ConditionEntry`（`id` + 表示名 / 説明 / アイコンの 3 つの `AttributeValue` + テンプレート schema によるカスタム属性 `values` + 内包する `expression`。表示フィールドは「条件未達」の案内 UI にそのまま使えます）、**条件テンプレート** `ConditionTemplate`（`name` / `color` / カスタム属性 schema + `defaultExpression`。作成時にディープコピーしてプリセットにします）、**列挙型**。ゲームプレイタグは保持しません——タグはエフェクトライブラリが宣言します。

```csharp
// ランタイム：Resources 配下なら起動時に自動登録（ConditionRuntime.AutoLoadFromResources、既定 true）。明示登録も可
ConditionDataManager.Instance.Register(conditionDatabase);   // 冪等。NormalizeAll + グローバル名前付き条件レジストリへ合流
bool ok = ConditionResolver.IsSatisfied("knight_ready", ctx); // 解決順：コンテキストのソース → フォールバック → ConditionDefinitionRegistry.Default
ConditionEntry e = ConditionDataManager.Instance.GetEntry("knight_ready");
string why = e.descriptionText.ResolveText();                 // 未達のときプレイヤーに見せる一文
```

- ⚠️ **解決できなければ不成立（fail-closed）**：`ConditionResolver.Evaluate` は id が見つからない場合、不成立を返し、その id を `FailedKeys` に入れて警告します。ゲート用途では id の打ち間違いは開放ではなく施錠であるべきで、`ConditionEngine` が未登録の判定器キーを扱う方針と一致します。
- ブートストラップ：`[SubsystemRegistration]` でレジストリをクリア → `[BeforeSceneLoad]` は加算のみ（判定器 + `Resources`）。ホストが Awake で `Register` したライブラリは維持されます。
- **Condition Editor**（`Tools > Ale Toolkit > Condition System > Condition Editor`、またはアセットの Inspector の「Condition Editor で編集」）は 2 つのタブを持ち、Effect Editor と構造が対称です：
  - **Condition Evaluators**（1 つ目。内容は**コード**由来なので、条件ライブラリが無くても使えます）：プロジェクト内の全 `IConditionEvaluator` 実装のカタログ。カテゴリ別にグループ化し、キー / 表示名 / 型の完全名 / アセンブリで検索でき、「問題のみ」に絞り込めます。右列には実装型 / アセンブリ / ソースパスと「スクリプトを開く」（クラス宣言行へ移動）、「Project で表示」、「Key をコピー」（行のダブルクリックはスクリプトを開くのと同じ）、パラメータ schema、どの設定から参照されているか（「移動」で位置決め）、そして**サイレント失効の診断**——`[ConditionEvaluator]` の付け忘れ、属性のキーと `Key` プロパティの不一致、キーの重複、空のキー、public な引数なしコンストラクタの欠如、インスタンス化の失敗（抽象基底クラスは注記のみで問題として扱いません）。上部のバナーには、設定から参照されているのに実装が無い「宙に浮いたキー」を列挙します。参照元は条件ライブラリ、`ConditionAsset`、そして**エフェクトライブラリの各エフェクトの適用条件と実行項目のゲート**（エフェクト側が `ConditionEvaluatorIndex.RegisterUsageProvider` で提供するため、依存の向きは反転しません）をカバーします。
  - **Condition Database**（2 つ目）：左列は条件テンプレート / 列挙型、中列は条件リスト（テンプレートフィルター / 検索 / テンプレートから追加 / クイック追加）、右列は重複チェック付き ID + 名前 / 説明 / アイコン + カスタム属性 + インライン式 + 検証サマリー（「判定器が未選択」「判定器 X の実装がありません」をその場で報告）。重複はエクスポートをブロック、JSON / バイナリでエクスポート。`ConditionEditorWindow.Open(db, conditionId)` で特定の条件に位置決め、`OpenEvaluators()` で 1 つ目のタブへ。
- **ホスト側の組み込み**（Inspector に 1 行）：`EditorConditionRefListDrawer.Draw(ctx, skill.unlockConditionRefs, drag, "解除条件", "条件")`——カタログメニュー（データベース別にグループ化）、ドラッグ並べ替え、「開く」ジャンプ、「（未検出）」表示（ブロックしない）、自由入力。
- **ホストを相互参照に接続する**：条件はホストのフィールドにインラインのままで構いません。用法プロバイダを 1 つ書けば Condition Evaluators タブの「参照 / 宙に浮いたキー / 診断」に載ります——`[InitializeOnLoad]` の静的コンストラクタで `ConditionEvaluatorIndex.RegisterUsageProvider(Provide)` を呼び、`Provide` では `ConditionUsageCollector.ForEachAsset<T>` で自分のアセットを走査し、`ConditionUsageCollector.Collect(into, expr, asset, ownerId, "解除条件", jump)` で各箇所を収集します（位置の文言はヘルパーが統一整形）。`jump` はホストが渡します（自分のエディタウィンドウを開いて位置決め）。そのため条件システムがホストを知る必要はありません。toolkit 自身の `EffectConditionUsageProvider` もこの書き方です。
- **パラメータ候補の注入**：判定器は schema の `catalogRef` で「この文字列パラメータがどの種類の id を入れるか」を宣言し（例：`new ConditionParamDef("traitId", ConditionParamType.String, false, "特質ID", null, null, "Chronicle.Trait")`）、ホストは `ConditionDrawerHooks.RegisterProvider(IConditionParamCatalogProvider)` でそのカタログに候補を供給します。ドロワーは素のテキストフィールドをシステム名でグループ化したドロップダウンに置き換えます（宙に浮いた値は保持し「（不明）」と表示）。`catalogRef` が無い、または候補が無い場合の挙動は 1.11.0 と完全に同じです。

### 効果システム · Effect System

UE5 GAS `GameplayEffect` 流の効果システム（名前空間 `Ale.Effect`）。2 層構成です：**定義層** `EffectDefinition` + **ランタイムコンテナ** `EffectContainer`（1.9.0 から。GAS の GameplayEffect + ASC の効果部分に相当：持続 / 周期 / スタック / タグ / 免疫 / 抑制 / モディファイア / セーブ）、および**実行層** `EffectExpression`（1.4.0 から。GAS の Executions に相当：フェーズグループ + 各項目にオプションの条件ゲートを持つ離散アクション）。「フィールドを宣言すれば Inspector で設定」でき、上位が実装する `[EffectExecutor]` 実行器が自動的に発見されます。3 つのアセンブリ：`Ale.Effect.Core`（`Ale.Condition.Core` / `Ale.GameplayTags.Core` / `Ale.Modifier.Core` を参照、エンジン非依存）/ `.Runtime`（1.10.0 から共用効果ライブラリを担い、`Ale.Toolkit.Runtime` + `Ale.GameplayTags.Runtime` を参照）/ `.Editor`（`Ale.Toolkit.Editor` + `Ale.GameplayTags.Editor` を参照）。まず実行層（定義の `executions` フィールドがまさにそれ）、次に定義とコンテナを説明します。

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

**組み込み実行器**：`Effect.NoOp`、`Effect.SetFlag`（`IEffectFlagSink`）、`Effect.AdjustNumber`（`IEffectNumberSink`）—— それぞれ条件システムの `HasFlag` / `NumberCompare` の書き込み側の対偶；`Effect.ApplyEffect(effectId, level)` / `Effect.RemoveEffectsWithTag(tag)` / `Effect.RemoveEffectById(effectId)`—— 効果の合成と解除（コンテナと定義はコンテキストから解決）。**JSON**：`EffectJson.ToJson/FromJson`（式）と `ToJson(EffectDefinition)/DefinitionFromJson`（内包する gate / 条件 / タグもグラフと共に往復）。**総覧**：`Tools > Ale Toolkit > Effect System > Welcome`；**実装済み実行器の完全な一覧は Effect Editor の「Effect Executors」タブにあります（1.11.0 から）**——実行器を書いたら、そこで実際に検出されているか確認でき、行をダブルクリックするとソースへ戻れます。

**④ 効果定義とコンテナ（GAS 層）**

`EffectDefinition` は再利用できる「効果はどんな形か」の定義です：`durationPolicy`（Instant / HasDuration / Infinite）、`duration` / `period` + `executePeriodicOnApplication`、スタック（`stackingType` ソース別 / ターゲット別の集約、`stackLimit`、持続時間更新 / 周期リセット / 満了の 3 ポリシー）、タグ（`assetTags` / `grantedTags` / `removeEffectsWithTags` / `grantedApplicationImmunityTags`、`applicationTagRequirements` / `ongoingTagRequirements`）、`applicationCondition`（Condition）、`chanceToApply`、`modifiers`（`EffectModifier`：属性 id + 演算 + `EffectMagnitude`——Scalable / AttributeBased / SetByCaller）、`executions`（上記の `EffectExpression`、フェーズ定数 `EffectPhases.OnApply / OnStack / OnPeriod / OnExpire / OnRemove`）、`cueTags`。定義の置き場所：**1.10.0 の共用効果ライブラリ `EffectDatabase` の利用を推奨（⑤ 参照）**。ホストが自前のデータベースを作る場合は定義をデータベースの**トップレベルのリスト**に置き id で参照します（ネストは既に 8 段で、さらに 2 段包むと Unity のシリアライズ深度上限に達します）；toolkit 単体のユーザーは単一定義アセット `EffectDefinitionAsset` も使えます。`Normalize()` は空のフェーズを `onApply` に書き換えます（`EffectRunner` は空の phase をワイルドカードとみなすため、そのままだと周期 / 除去のたびに再実行されます）；`Validate(errors)` はエラーと「警告:」接頭辞付きの警告を報告します。

```csharp
using Ale.Effect; using Ale.Modifier; using Ale.GameplayTags;

// 定義：30「日」の +10 戦力バフ、ターゲット別に最大 3 スタック、Status.Buff.Might を付与
var buff = new EffectDefinition("battle_focus", EDurationPolicy.HasDuration) {
    duration = EffectMagnitude.Scalable(30f), stackingType = EEffectStackingType.AggregateByTarget, stackLimit = 3,
};
buff.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
buff.grantedTags.AddTag("Status.Buff.Might");

// 所有者ごとに 1 コンテナ；ホストの時間単位（世界日 / 秒…）は定義内の持続時間と同じ単位
var container = new EffectContainer(owner: heroId);
var ctx = new EffectContext { Subject = heroId };            // ConditionContext のサービス袋：インターフェースで登録
ctx.RegisterService<IEffectAttributeSink>(mySink);          // 瞬時 / 周期モディファイアの永久反映先
ctx.RegisterService<IEffectContainerSource>(myContainers);  // 組み込みの Effect.ApplyEffect などが主体からコンテナを探す

EffectApplyResult r = container.ApplyEffect(buff, ctx, level: 1, source: casterId);   // Applied / Stacked / Refreshed / BlockedBy…
container.Tick(1f, ctx);                                     // 1 時間単位進める：周期の結算、満了の除去
var mods = new List<ModifierDefinition>();
container.CollectModifiers("might", mods);                   // アクティブ・非抑制・非周期効果のスケール済みモディファイア → ModifierStackEvaluator へ
container.RemoveEffectsWithTags(new GameplayTagContainer("Status.Buff"), ctx);   // 解除
var save = container.ExportState();                          // セーブ；ImportState(state, definitions, ctx) で静かに復元
```

- **適用パイプライン**：免疫（アクティブかつ非抑制の効果の免疫タグが新来者の `assetTags` にヒット）→ 適用タグ要件 → 適用条件（`Subject` = ターゲット）→ 確率 → 持続時間の評価（≤ 0 は `Invalid`）→ 瞬時：モディファイアを `IEffectAttributeSink.ApplyPermanent` で反映 + `onApply`、コンテナには入れない / スタック：同キーのインスタンスに 1 層追加（上限では `Refreshed` を返し、ポリシーに従い更新のみ）+ `onStack` / 新インスタンス：タグ付与 + `onApply` + 適用即結算 → タグ指定で他の効果を除去。
- **Tick**：周期を満了より先に処理；delta が複数周期にまたがれば複数回結算し余りを保持；抑制中は周期を凍結するが**持続時間は進む**；満了はポリシーに従い全消去 / 1 層減らして更新 / 更新のみ、その後 `onExpire` → `onRemove`。
- **抑制**（`ongoingTagRequirements` 不成立）：モディファイアは集約されず、周期は凍結、付与タグは撤回；`OwnedTags` のあらゆる変化（付与 / `AddLooseTag` / ホストの直接書き込み）で自動的に再評価。
- **値と事の分担**：持続 / 無限効果のモディファイアは `CollectModifiers` で一時的に集約（モディファイア 1 件につきスタック数でスケールした `ModifierDefinition` 1 件、ソース `effect:{id}#{handle}`）；瞬時効果と周期結算のモディファイアは Sink で**永久反映**——**周期効果は `CollectModifiers` に参加しません**。さもないと「毎周期 +10 かつ持続 +10」が二重計上されます。
- 契約：`IEffectDefinitionSource`（+ 集約する `EffectDefinitionRegistry.Default`、データベース横断の id 参照用）、`IEffectContainerSource`、`IEffectAttributeSource`（AttributeBased 振幅は現在値を読む）、`IEffectAttributeSink`、`IEffectRandomSource`、`IEffectCueSink`（Applied / Executed / Removed で `cueTags` を受け取る）、`IEffectExecutionInfo`（実行器は `ctx.GetService` で現在の定義 / インスタンス / ソース / レベル / フェーズを取得）；`EffectApplier.Apply(effectId, ctx)` で id 指定の適用。イベント：`OnEffectAdded / Removed / StackChanged / InhibitedChanged / PeriodicExecuted / OnModifiersChanged`。エディター：`EffectDefinitionDrawer` はセクションを条件付きで表示；属性 id フィールドの候補はホストが登録した `IEffectAttributeCatalogProvider`（`EffectDefinitionDrawerHooks.RegisterAttributeProvider`、複数システムはシステム名でグループ化、1.10.0 から）またはフィールド全体を引き受けるデリゲート `AttributeIdField`（優先）から得られ、ホストのエンティティが id / 名前を持つ場合は `ShowIdentityFields` を false にして「基本」セクションを隠せます。

**⑤ 効果ライブラリと Effect Editor（1.10.0 から）**

`EffectDatabase`（`Create > Ale > Effect > Effect Database`）は、すべての上位システムが共用する効果設定の入れ物で、4 つのトップレベルリストを持ちます：**効果エントリ** `EffectEntry`（`id` + 表示名 / 説明 / アイコンの 3 つの `AttributeValue` + テンプレート schema に従うカスタム属性 `values` + 内包する `definition`；`Normalize()` が定義の id / 表示名をエントリから同期）、**効果テンプレート** `EffectTemplate`（`name` / `color` / カスタム属性 schema + `defaultDefinition`、テンプレートから作成する際にディープコピーされてプリセットになる）、**ゲームプレイタグ**、**列挙型**（カスタム属性の列挙フィールド用）。上位システムは効果 id だけを保存し、それぞれ `[EffectExecutor]` 実行器を実装します——効果の本質は「あるシステムへの操作」であり、そのシステムがどう操作されるかはシステム自身が実装します。

```csharp
// ランタイム：Resources 配下に置けば起動時に自動登録（EffectRuntime.AutoLoadFromResources、既定 true）、または明示的に登録
EffectDataManager.Instance.Register(effectDatabase);         // 冪等；NormalizeAll + グローバル効果レジストリへ参加 + ゲームプレイタグを登録
EffectEntry e = EffectDataManager.Instance.GetEffect("regen_draught");
string name   = e.ResolveDisplayName();                      // ローカライズされた表示名（無ければ id にフォールバック）
int power     = e.GetAttributeValue("power").GetInt(0);      // テンプレート schema が定義するカスタム属性
EffectApplier.Apply("regen_draught", ctx);                   // 定義は EffectDefinitionRegistry.Default から id で解決
EffectDataManager.Instance.LoadFromBinary(bytes);            // または EffectConfigSerializer.ExportJson / Export の出力
```

- ブートストラップ：`[SubsystemRegistration]` でレジストリをクリア → `[BeforeSceneLoad]` は加算のみ（実行器 + `Resources`）。ホストが Awake で `Register` したライブラリは維持されます。⚠️ シリアライズ深度に余裕はゼロ：`EffectEntry` はトップレベルリストの要素、`defaultDefinition` は直接のフィールドでなければなりません。
- **Effect Editor**（`Tools > Ale Toolkit > Effect System > Effect Editor`、またはアセットの Inspector の「Effect Editor で編集」）は **1.11.0 から 2 つのタブ**を持ちます：
  - **Effect Executors**（1 つ目。内容は**コード**由来なので、効果ライブラリが無くても使えます）：プロジェクト内の全 `IEffectExecutor` 実装のカタログ。カテゴリ別にグループ化し、キー / 表示名 / 型の完全名 / アセンブリで検索でき、「問題のみ」に絞り込めます。右列には実装型 / アセンブリ / ソースパスと「スクリプトを開く」（クラス宣言行へ移動）、「Project で表示」、「Key をコピー」（行のダブルクリックはスクリプトを開くのと同じ）、パラメータ schema、どの効果設定から参照されているか（「移動」で 2 つ目のタブに切り替えてその効果に位置決め）、そして**サイレント失効の診断**——`[EffectExecutor]` の付け忘れ、属性のキーと `Key` プロパティの不一致（属性の文字列は読まれません）、キーの重複（エディタのカタログは最初に見つかったもの、ランタイムのレジストリは最後に登録されたもの——逆になります）、public な引数なしコンストラクタの欠如、インスタンス化の失敗（抽象基底クラスは注記のみで、問題としては扱いません——属性は派生クラス側に付けるものだからです）。上部のバナーには、設定から参照されているのに実装が無い「宙に浮いたキー」を列挙します（`EffectDefinition.Validate` は検査せず、ランタイムでのみ警告）。下部の「実行器の追加ガイド」では組み込みフェーズ定数と、コピーできる最小実装テンプレートを提供します。
  - **Effect Database**（2 つ目。効果ライブラリは任意で、主に効果エントリへ追加データを付けるためのもの）：左列は効果テンプレート / ゲームプレイタグ / 列挙型、中列は効果リスト（テンプレートフィルター / 検索 / テンプレートから追加 / クイック追加）、右列は重複チェック付き ID + 名前 / 説明 / アイコン + カスタム属性 + インライン定義 + 検証サマリー。重複はエクスポートをブロック、JSON / バイナリでエクスポート。`EffectEditorWindow.Open(db, effectId)` はこのタブに切り替えて特定の効果に位置決めし、`OpenExecutors()` は 1 つ目のタブに切り替えます。
- **ホスト側の組み込み**（Inspector に 1 行）：`EditorEffectRefListDrawer.Draw(ctx, skill.onUseEffectRefs, drag, "使用時に適用する効果", "効果")`——カタログメニュー（`EffectEditorCatalog`、データベース別にグループ化）、ドラッグ並べ替え、「開く」ジャンプ、「（未検出）」表示（ブロックしない）、自由入力。属性 id の候補は `EffectDefinitionDrawerHooks.RegisterAttributeProvider(IEffectAttributeCatalogProvider)` で登録（システム名でグループ化したドロップダウン）、ホスト独自のタグカタログは `GameplayTagEditorCatalog.RegisterProvider` で提供します。

> **UE5 GAS との対応**：`EffectDefinition` ≙ GameplayEffect（Duration / Period / Stacking / Tags / Modifiers / Executions / Cues）、`EffectContainer` ≙ AbilitySystemComponent のアクティブ効果とタグの部分、`EffectExpression` + `[EffectExecutor]` ≙ Executions。未実装：カーブテーブルの振幅、非スナップショットの属性キャプチャ（振幅は適用 / スタック時にスナップショット）、ネットワーク複製。**モディファイアは「値」を、実行器は「事」を、コンテナは「寿命」を管理します。**

### エディター基盤

`Ale.Toolkit.Editor`。すべてデータベース型についてジェネリックで、ホストプラグインが継承して少数の抽象メンバーをオーバーライドするだけでエディターを構築できます。

- **データベースウィンドウのシェル** `EditorDatabaseWindowBase<TDb>`：「DB アセットのオブジェクトフィールド + 上部タブバー + 検証 / エクスポートボタンのフック + 重複スキャンのオーケストレーション + ステータスバー + Undo 購読 + 直近 DB パスの記憶（EditorPrefs）」を内蔵し、`IEditorDbContext<TDb>` を実装。ホスト側ウィンドウはタブ集合 / エクスポート・検証コールバック / 重複チェック種別を提供するだけで大幅に薄くできます。1.11.0 から：`TabRequiresDatabase(int)` を false にオーバーライドしたタブは**データベースアセットが無くても描画されます**（内容がアセットではなくコード由来のタブ向け）。`SelectSystemTab(int)` / `CurrentSystemTab` で外部からタブの切り替えと取得ができ、タブのインデックスは `EditorPrefKey` ごとに記憶されます。
- **三列タブ** `EditorThreeColumnTab<TDb,TEntity>`：左列サブタブ + マスターリスト、中列エンティティリスト、右列コンテキストインスペクター。`LeftPanels` / `EntityNoun` / `EntityList` / `DrawEntityList` / `DrawEntityInspector` などをオーバーライド。`RequestSelect(entity)` で外部から位置決めできます（1.10.0 から。データベース設定後に呼ぶこと。次の Layout フレームで右列インスペクターが有効化）。
- **マスターリストパネル** `EditorMasterListPanel<TDb,T>`（+ `IEditorMasterListPanel<TDb>`）、**エンティティリストパネル** `EditorEntityListPanel<TDb,TEntity,TTemplate>`。
- **ツールウィンドウ基底** `EditorToolWindowBase<TDb>`：「データベース選択 + フレームごとの時間予算ステップ + プログレスバー + ログ + キャンセル + 完了処理」を内蔵。`DrawOperations`（`RunSteps` でフレームごとのステップを開始）/ `OnRunComplete` / `OnRunFinished` をオーバーライド。
- コンテキスト `IEditorContext` / `IEditorDbContext<TDb>`；補助コントロール `EditorSearchableList` / `EditorDraggableRowList` / `EditorReorderableDrag` / `EditorListKeyboardNav` / `EditorFilterTabs` / `EditorIdScanner` / `ToolkitEditorStyles` / `EditorScriptLocator`（1.11.0 から：型 → `MonoScript` の解決。IDE をクラス宣言行で開いたり、Project でハイライトできます。`MonoBehaviour` を継承しない通常のクラスにも有効）。**クリックできるリスト行にはマウスホバーのハイライトが付きます**（`ToolkitEditorStyles.TrackMouseHover` / `DrawRowHover`、1.11.0 から。上記の共通行コンポーネント内で実装しているため、ホスト側の配線は不要）。

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

TextMeshPro / Unity Localization / Addressables のマクロ切り替えとランタイムアダプター。マクロはプロジェクト単位のグローバル設定（`ATK_TMP` / `ATK_LOCALIZATION` / `ATK_ADDRESSABLE`）で、ウェルカムウィンドウから切り替え。マクロが有効なのに対応パッケージが未導入の場合に Console で警告するだけで、**PlayerSettings を自動で書き換えることはありません**——自動書き換えは同名マクロを管理する他プラグインと競合し、書き込みのたびに再コンパイルが走ってエディタがループに陥ります。

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
