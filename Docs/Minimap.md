# ミニマップ

Assets/Prefab/UI/MinimapHUD.prefabをMainSceneのCanvas / SafeArea / Header配下へPrefabインスタンスで配置しました。既存のUI配置は動かしていません。初期状態はOFFで、「ミニマップ」のトグルを押すと小さな点マップを開き、再度押すと閉じます。表示中もゲームは通常どおり進みます。ON/OFFは実行中の状態のみで、新しい保存設定は追加していません。

## 表示

| 点 | 対象 |
| --- | --- |
| 黄色 | 現在のマップのMapExitTransition。移動先設定あり・有効な出入り口 |
| 緑 | 有効なMapBuildingAccessのAltar（活性化祭壇） |
| 青 | 有効なMapBuildingAccessのDungeonEntrance |
| 白（少し大きい点） | 主人公 |

北が上で、Tilemapの使用範囲を縦横比を保って縮小します。地形・敵は描画せず、位置を示す点のみです。町・フィールドの移動と主人公の位置に追従します。他マップや無効な建物は表示せず、同じマップの範囲内にあるルート外配置の建物にも対応します。ダンジョン内は生成Groundを使い、入口部屋を黄色・主人公を白で表示します。

## Inspectorでの調整

- MinimapHUDルートのRect Transform：全体の表示位置。初期はHeaderの右上基準、Pos X -24 / Pos Y -300。
- Toggle：トグルの位置・サイズとラベル。
- MapPanel：開いたパネル。初期320×334、トグルの下50。
- MapPanel / MapArea：点の表示領域。初期296×254。
- MinimapHUDスクリプト：各色、Point Size、Player Point Size、Padding、更新間隔。
- MapPanel内の凡例：文字・色の見た目。
- DotTemplate：点のGraphic。通常は非表示のままにしてください。実際の点はここから必要数だけ生成し、再利用します。

別のシーンではPrefabを既存CanvasのHeaderなどへ配置します。カメラやMapSOの追加設定は不要です。既存のTilemapMapRoot/Ground、MapExitTransitionのDestination Map、MapBuildingAccessの種類と有効状態、PlayerControllerを参照します。個別SafeAreaやCanvasは生成しません。

## 更新と検証

専用カメラ・RenderTexture・画像テクスチャを生成しません。点はMinimapDotGraphicが20三角形の円として描画します。マップ情報と地点の検索は開いている間だけ初期1秒間隔、点の位置更新は0.05秒間隔です。閉じている間は検索・位置更新を行いません。点のGameObjectはプールで再利用し、開閉・マップ移動で作り直しません。

実MinimapHUD/MinimapDotGraphicをUnity/world APIの代替環境でコンパイルし553項目を確認しました。座標・縦横比・範囲外クランプ、500ケースの射影、Tilemapの位置/回転/拡大縮小、空マップ、町/フィールド移動、色分け、無効な祭壇、別マップ除外、主人公追従、閉状態の検索なし、点の再利用、ダンジョン入口、円メッシュを含みます。Unityの実描画・MapExitTransitionの実遷移・物理/Tilemapネイティブ処理を実行した検証ではありません。

Prefab60文書の一意ID・内部参照・初期OFF・スクリプトGUIDを検査し、MainSceneはHeaderの子一覧への1項目追加とPrefabInstance/stripped RectTransformの2文書だけを変更しました。既存の他ノード・配置を保持しています。変更コードを含む164 C#のPlayer/Editor構文エラー0。Unity本体の全体コンパイル・Prefabインポート・Play・実機描画・Profilerは未実施です。

UnityではMainSceneを開き、ミニマップON/OFF、白い点の移動、各色の地点、町と隣接マップへの移動、ダンジョン出入り、非表示/無効な建物の除外を確認してください。位置はMinimapHUDのRect Transformから調整できます。
