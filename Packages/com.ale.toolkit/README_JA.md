# Ale Toolkit

[简体中文](README.md) · [English](README_EN.md) · [日本語](README_JA.md)

Unity プラグイン開発向けの**汎用基盤ライブラリ**です。特定の業務ドメインの概念を一切含まず、複数のプラグインが属性設定・リスト・エディター基盤・多言語機能を共有できるようにします。

> 本パッケージは `com.ale.inventory` 1.8.0 から分離されました。インベントリシステム内に埋め込まれていた汎用機能（エディターの三列レイアウト基盤、バーチャルスクロールリスト、カスタム属性システム、エディター UI の三言語対応）をここへ抽出し、より多くのプラグインで再利用できるようにしたものです。

---

## ⚠️ インストール（最初にお読みください）

**`com.ale.toolkit` は、これに依存するプラグインより先にインストールする必要があります。**

Unity の Package Manager は **`package.json` の `dependencies` に git URL を書くことをサポートしていません**。そのため依存側のプラグインが本パッケージを自動取得することはできません。以下の**順序どおりに**手動で 2 回インストールしてください。

`Window > Package Manager` → 左上の `+` → `Install package from git URL...`

**手順 1 —— まず Toolkit をインストール：**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.3.0
```

**手順 2 —— 次に依存プラグイン**（例：インベントリシステム）をインストール：

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.10.0
```

> 順序が逆になったり本パッケージが未インストールの場合、Unity は `Ale.Toolkit.* が見つかりません` といったコンパイルエラーを出します。その際は本パッケージを追加インストールして再コンパイルを待つだけでよく、もう一方のプラグインを再インストールする必要はありません。

**Unity 2022.3** 以降が必要です（Unity 6000.3 で開発・保守）。

---

## 収録モジュール

| モジュール | 内容 |
| --- | --- |
| **属性システム** | 20 種類以上のフィールドタイプを持つ `AttributeValue`、属性定義（スキーマ）、カスタム列挙型、数値フォーマット設定。「属性項目を設定する」場面ではすべてこれを使用します |
| **ソート** | 要素の型に依存しないソートエンジン。ホスト側が `ISortContext<TData>` を実装して比較に必要な情報を提供し、エンジンが多段優先度とタイブレークを処理します |
| **UI** | バーチャルスクロールリスト（グリッド / 順次、オブジェクトプール + 可視領域のみ描画）、タブバー、フィルターバー、ツールチップ基底クラス、ウィジェットプール |
| **オブジェクトプール** | 汎用の GameObject / プレハブプール（`Spawn`/`Despawn` + `IPoolable` コールバック、プリロード / 容量リサイクル / 遅延デスポーン / シーン跨ぎ）と、純 C# 参照型プール `ToolkitClassPool<T>`（GC 削減）。Lean.Pool 等のサードパーティ製プールを置き換え可能 |
| **エディター基盤** | 三列レイアウトのタブ基底クラス、マスターリストパネル、エンティティリストパネル、ツールウィンドウ基底クラス。いずれもデータベース型についてジェネリック化されています |
| **エディター多言語** | 中文 / English / 日本語 の三言語サービス。中国語原文をキーとし、訳文が無い場合は自動的にフォールバックします |
| **オプション依存のサポート層** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）のマクロ切り替えとアダプター |
| **エディタ入口とグローバル設定** | Ale Toolkit ウェルカムウィンドウ（`Tools > Ale Toolkit > Welcome`）：エディタ UI 言語 / 列挙翻訳 / 3 つのオプション機能マクロ / ウィザードのデフォルト・ローカライズフォント + 汎用ツール入口 +「起動時に自動表示」トグル。ウィザードフォントなどのプロジェクト単位の設定は `ProjectSettings/AleToolkitSettings.asset` に保存（リポジトリと共にコミット、アセット参照は GUID で保持）、言語 / 自動表示はユーザーごと（EditorPrefs）。旧マクロ `IS_*` は読み込み時に `ATK_*` へ自動移行 |
| **汎用ツールウィンドウ** | 任意のデータアセット（`ScriptableObject`）の全 `AttributeValue` を走査して一括処理：Addressable 移行（Object ↔ GUID）とローカライズキー生成。`Tools > Ale Toolkit` 配下、上位プラグインで再利用可能 |

> 上記のモジュールはすべて配置済みです —— 1.1.0 以降、3 つのオプション依存サポート層（TMP / Localization / Addressables）が揃い、toolkit 単体のプロジェクトでもエディタ UI は 3 言語対応です。**1.2.0 以降はプロジェクト単位のグローバル設定（言語 / マクロ）を担い、任意のデータアセットで動作する汎用ツールウィンドウを提供します**。**1.3.0 以降は汎用オブジェクトプール（GameObject プール + 純 C# クラスプール）を追加します**。詳細は [CHANGELOG](CHANGELOG.md) をご覧ください。

---

## アセンブリ

| Assembly Definition | 役割 | マクロ制約 |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性システム、ソート、アセット読み込み抽象、共通シリアライズ、オブジェクトプール | — |
| `Ale.Toolkit.UI` | バーチャルスクロールリストと汎用 UI コントロール | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization 対応コンポーネント | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables の読み込みとハンドル管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | エディター基盤、属性ドロワー、多言語サービス、マクロ切り替え | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables のエディターツール | `ATK_ADDRESSABLE` |

依存の向きは一方向です：ホストプラグイン → `Ale.Toolkit.*`。本パッケージがホストプラグインを参照することはありません。

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
- その他：`UiwViewBase`（`Open`/`Close`/`ToggleOpenClose`）、`UiwSortToolbar`（`SetOptions`/`SetSortPriorities`）、`UiwNumberCounter`（`Configure`/`SetRange`/`SetValue`）、`UiwTextLabel`、`SpriteSlot.Bind(image, value)`。

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

### エディター基盤

`Ale.Toolkit.Editor`。すべてデータベース型についてジェネリックで、継承して少数の抽象メンバーをオーバーライドするだけでエディターを構築できます。

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
