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
  <a href="./README.md">中文</a> |
  <a href="./README_EN.md">English</a> |
  日本語
</p>

<p align="center">
  📥
  <a href="#-インストール最初にお読みください">インストール</a> |
  <a href="#-含まれるモジュール">モジュール</a> |
  <a href="Packages/com.ale.toolkit/README_JA.md">詳細ドキュメント</a>
</p>

# Ale Toolkit

Unity プラグイン開発向けの**共通基盤ライブラリ**です。具体的な業務ドメインの概念を一切含まず、複数のプラグインが同一のカスタム属性システム・仮想スクロールリスト・エディタ 3 カラムフレームワーク・エディタ UI の多言語対応、および TextMeshPro / Localization / Addressables の任意サポート層を共有できるようにします。

---

## ⚠️ インストール（最初にお読みください）

**`com.ale.toolkit` は、これに依存するプラグインより先にインストールする必要があります。**

Unity の Package Manager は **`package.json` の `dependencies` での git URL 指定に対応していない**ため、依存側のプラグインが本パッケージを自動取得することはできません。手動でインストールする必要があり、**順序を逆にしてはいけません**：

`Window > Package Manager` → 左上の `+` → `Install package from git URL...`

**ステップ 1 —— まず Toolkit をインストール：**

```
https://github.com/AleFeng/unity-ale-toolkit.git?path=/Packages/com.ale.toolkit#1.2.0
```

**ステップ 2 —— 次にそれに依存するプラグイン**（例：在庫システム）をインストール：

```
https://github.com/AleFeng/unity-ale-inventory-system.git?path=/Packages/com.ale.inventory#1.10.0
```

> 順序を逆にしたり本パッケージを入れ忘れたりすると、Unity は `Ale.Toolkit.* が見つからない` といったコンパイルエラーを出します。その場合は本パッケージを追加して再コンパイルを待つだけでよく、もう一方のプラグインを入れ直す必要はありません。

最低対応は **Unity 2022.3**（Unity 6000.3 で開発・保守）。

---

## 含まれるモジュール

| モジュール | 内容 |
| --- | --- |
| **属性システム** | `AttributeValue` と 20 種以上のフィールド型、属性定義（スキーマ）、カスタム列挙型、数値フォーマット設定、タグシステム（`Tag`）。「属性項目を設定する」あらゆる場面で使用 |
| **ソート** | 要素の型に依存しないソートエンジン。ホストが `ISortContext<TData>` を実装して比較に必要な情報を提供し、エンジンが多段優先度とタイブレークを処理。主キー / タグ順ソートは標準対応 |
| **UI** | 仮想スクロールリスト（グリッド / 順次、プール + 可視領域のみ描画）、タブバー、フィルターバー、Tooltip 基底、アイテムプールなどの汎用ウィジェット |
| **エディタフレームワーク** | 3 カラムタブ基底、マスターリストパネル、エンティティリストパネル、ツールウィンドウ基底。いずれもデータベース型でジェネリック化 |
| **エディタ多言語** | 中国語 / English / 日本語のサービス。中国語原文をキーとし、訳が無い場合は自動フォールバック |
| **UGUI プレハブツールボックス** | ドメイン非依存の UGUI プリミティブとテキスト / ボタン構築（各プラグインのワンクリック生成ウィザードで再利用可能） |
| **任意サポート層** | TextMeshPro（`ATK_TMP`）、Unity Localization（`ATK_LOCALIZATION`）、Addressables（`ATK_ADDRESSABLE`）のマクロ切り替えとアダプタ。ローカライズ / Addressable ツールウィンドウを含む |

詳細は [Packages/com.ale.toolkit/README_JA.md](Packages/com.ale.toolkit/README_JA.md)、変更履歴は [CHANGELOG](Packages/com.ale.toolkit/CHANGELOG.md) を参照してください。

---

## アセンブリ

| Assembly Definition | 説明 | マクロゲート |
| --- | --- | --- |
| `Ale.Toolkit.Runtime` | 属性システム、ソート、タグ、アセット読み込み抽象、共通シリアライズ | — |
| `Ale.Toolkit.Runtime.UI` | 仮想スクロールリストと汎用 UI ウィジェット | — |
| `Ale.Toolkit.UI.Localization` | Unity Localization アダプタコンポーネント | `ATK_LOCALIZATION` |
| `Ale.Toolkit.Addressables.Runtime` | Addressables のアセット読み込みとハンドル管理 | `ATK_ADDRESSABLE` |
| `Ale.Toolkit.Editor` | エディタフレームワーク、属性ドロワー、多言語サービス、プレハブツールボックス、マクロ切り替え | — |
| `Ale.Toolkit.Addressables.Editor` | Addressables のエディタリゾルバ / ツールウィンドウ | `ATK_ADDRESSABLE` |

依存方向は一方向：ホストプラグイン → `Ale.Toolkit.*`。本パッケージがホストプラグインを逆参照することはありません。

---

## ライセンス

[MIT](LICENSE)
