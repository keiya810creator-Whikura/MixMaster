# 共通SafeAreaでUIを管理する

2026年10月30日：各UIの内側にあったSafeAreaオブジェクトを除去し、配置先の共通SafeAreaで制御する構成へ変更した。シーンの配置・階層は変更しない。振り分けと座標調整は利用者が行う。

## 推奨する階層

Canvasの下へSafeAreaを1つ置き、その下のHeader・Body・BottomへUIを配置する。

| 階層 | 設定・用途 |
| --- | --- |
| Canvas | 通常のCanvas / CanvasScaler。設計基準1080×1920。 |
| SafeArea | SafeAreaLayoutを1つだけ付ける。既存のAssets/Prefab/UI/SafeArea.prefabも使用可能。 |
| SafeArea/Header | 上部HUDやボタンを配置。 |
| SafeArea/Body | メニュー、図鑑、インベントリなどを配置。 |
| SafeArea/Bottom | 下部HUD、獲得ログなどを配置。 |

最初はHeader・Body・BottomのRect Transformをストレッチにして、共通SafeArea全体を基準にすると既存UIの位置を維持しやすい。各領域を分割した高さへ変更する場合は、その領域を基準に各UIのアンカー・座標も調整する。Prefabインスタンスのルートを目的のグループへ移動し、個別SafeAreaは追加しない。

`SafeAreaLayout.minimumContentSize`の既定は1080×1920。画面の実セーフエリアに収めながら設計空間を維持する。画面回転・SafeArea変更・Canvas描画直前に追従し、空のSafeAreaが一時的に返る端末では前回の有効レイアウトを保持する。ワールド空間のCanvasには適用しない。

## 整理したPrefab

22個のUI PrefabからSafeAreaのGameObject・RectTransform・SafeAreaLayoutの3文書ずつを除去した。子オブジェクトを元の親へ繰り上げ、元の並び順、操作部品ID、座標、Script参照を維持した。既存の.meta/GUIDは変更しない。

- 獲得ログ、配合、消耗品ショップ/インベントリ/ショートカット
- ダンジョン入口/リザルト、記録結果/受取/記録HUD
- モンスター詳細/一覧、プレイヤー詳細、転生
- スキルHUD、図鑑メニュー、装備インベントリ、目標HUD
- 素材インベントリ、パーティプリセット、設定、スキル詳細

共通用のSafeArea単体Prefabは残す。もともと個別SafeAreaがなかったUIはそのまま。MainSceneや他の.unityファイルは変更しない。

## コード生成UIと互換性

スキル画面と記録/目標/ヘルプ画面もSafeAreaラッパーを生成せず、パネル自身のルートへ部品を生成する。`SafeAreaLayout.CreateContent`は呼出元のRectTransformを返し、新しいオブジェクトやSafeAreaLayoutを追加しない。既存のログ・装備詳細などの呼出元でも個別SafeAreaを再生成しない。

`SafeAreaLayout.ContentRoot`は旧配置済み・展開済みUIの互換性のために旧ラッパーの検索を維持する。整理済みPrefabでは自身のルートを返す。新しいPrefabの内側にSafeAreaを付け直す処理ではない。

RuntimeUiLayoutSettingsの既定パスも`GameGuidePanel/Card`、`SkillHUD/SkillButtons`、`SkillHUD/SkillPanel`へ変更した。利用者が独自のパス設定を保存している場合は、削除したPrefab内の`/SafeArea/`をパスから除く。Canvas直下の共通SafeAreaは削除対象ではない。

## 確認結果とUnityでの確認

- 22 Prefabの全ドキュメント・内部PPtr・親子関係・残存部品のID/スタイル/座標を比較。SafeAreaの66文書以外を維持し、子の順序と参照の付け替えを検査した。全31426件の静的アサーション成功。削除したSafeAreaへの外部参照は既存UI Prefab/MainSceneに見つからなかった。
- 実SafeAreaLayoutの幾何・ライフサイクル・旧ラッパー互換・新CreateContentの代替API検証6254項目成功。画面サイズ/Canvas縮尺/ノッチ/回転と1500件のランダム矩形を含む。
- 実SkillScreenUI、GameGuidePanelUI、共通UI Factory、RuntimeUiLayoutSettingsのクラス全体を代替環境でコンパイルし54項目成功。Header/Body/Bottomそれぞれで新しいSafeAreaを作らず、5スキル枠、8目標行、ヘルプ、戻る、パネル再利用、平坦化後の座標パスが使えることを確認。

Unity本体の全体コンパイル・Prefabインポート・Play・実機表示は未実施。共通SafeAreaを設置して配置後、PrefabのMissing参照がないこと、各画面の開閉・クリック・スクロール、縦横画面でのノッチ・下部バーとの重なりを確認する。
