# モンスター・装備図鑑

## ボタンの接続

設置した通常のUI Buttonへ `EncyclopediaButton` コンポーネントを追加する。クリックで選択パネルを開き、「モンスター図鑑」「装備図鑑」から選ぶ。OnClickの手動登録は不要。

UIは最初のクリックでボタンのCanvas内へ読み込み、以後は同じ画面を再利用する。新しいHUDボタンは自動生成しない。事前にシーンへ配置して調整する場合は、Canvasを選択して `MixMaster > UI > 図鑑Prefabを配置` を実行できる。配置済みのPrefabを上書き・重複生成しない。

## Prefab

保存先：`Assets/Resources/UI/Encyclopedia/`

| Prefab | 内容 |
| --- | --- |
| EncyclopediaMenu.prefab | 選択パネル、背景、SafeArea、両図鑑への参照 |
| MonsterEncyclopedia.prefab | 横6列の一覧、モンスター詳細、称号・スキル詳細ポップアップ |
| EquipmentEncyclopedia.prefab | 横6列の一覧、装備詳細、入手ダンジョン |
| EncyclopediaCell.prefab | 一覧のアイコン、名称、未討伐の「？」画像、Button |
| EncyclopediaLink.prefab | 称号・スキルを開くアイコン付きButton |

`UnknownMonster.png` は未討伐のアイコンを覆う不透明の「？」Sprite。差し替える場合はCellの `UnknownOverlay` を編集する。画像・文字サイズ・色・各パネルの配置はPrefabで調整できる。一覧の列数・セル寸法・間隔は各図鑑の `List/GridScroll` のEncyclopediaGridUIに保持している。初期値は6列、136×164、間隔12×14。

一覧は表示範囲＋前後の行を再利用する。全件分のUIを同時に生成せず、同じ行内のスクロールでは再バインドしない。称号・スキルのButtonも詳細を切り替える際に再利用する。

## 掲載データ

| 表示 | 参照元 |
| --- | --- |
| モンスター一覧 | MonsterCatalogSO.Monsters。数値を含むID順。重複ID・空ID・nullを除く |
| 基礎値 | MonsterSO.baseStatsの入力値。Lv成長・個体値・称号・パッシブ・敵追加倍率を適用しない |
| 素材名・アイコン | MonsterSO.uniqueMaterial |
| 生息地 | MapCatalogSOのnormalMonsters、bossMonster、旧monstersから該当種族を検索。複数マップ対応 |
| 称号 | MonsterSO.titlePool。タップで説明とステータス補正 |
| スキル | availableSkillsとenemySkillsの重複を除いた一覧。タップで説明、習得条件、MP・再使用・威力、パッシブ、追加効果 |
| 装備一覧 | EquipmentCatalogSO.Equipment。全件閲覧可能 |
| 装備補正 | EquipmentSO.modifiers。装備Lv.1の基礎補正 |
| 入手ダンジョン | 各MapSO.dungeonEquipmentを検索。複数マップ対応 |

新しい定義もCatalogに登録すれば反映される。図鑑は定義・所持品・戦闘ステータスを変更しない。

## 討伐解放とセーブ

MonsterRuntimeSetupの死亡処理で種族IDをEncyclopediaManagerへ記録する。討伐者に関係なく、通常敵・ダンジョン敵・ボス・経験値0の敵が対象。最初の討伐だけ通知し、同じ種族を繰り返し倒しても図鑑更新を連発しない。

未討伐は「？」画像をアイコンに重ねて名称を隠し、Buttonを無効化する。詳細を開く処理でも解放状態を再確認する。履歴の復元で未解放になった詳細を開いていた場合は一覧へ戻す。

GameSaveDataをversion 15へ更新し、`defeatedMonsterIds` を既存の保存・復元・バックアップ経路に追加した。以前のセーブには種族別討伐履歴がないため、更新後に倒した種族から記録する。所持個体・配合個体・素材所持から討伐済みとは推定しない。旧データのロードと、欠落・null・重複IDに対応する。

## 確認

Unity API代替環境で、実ランタイムクラスと生成Prefabのシリアライズ参照を読み込み、ボタンから選択・一覧・詳細・ポップアップ・戻る、未討伐の禁止、再利用、複数の生息地/入手先、600種族のスクロールを検証。ウォームアップ後のスクロール200回で追加UI生成なし。実SaveManager・bootstrap・MonsterRuntimeSetupを使い、死亡からJSONファイルへの保存、再ロード、旧v14、null履歴、破損時のバックアップ復元を検証した。

これはUnityの描画・レイアウト・アセットインポートを再現するものではない。Unity Editorの全体コンパイル、Play Mode、実機表示は未実施。

Unityでの確認手順：ボタンへEncyclopediaButtonを追加し、未討伐が押せないこと、敵討伐後の解放、各詳細と戻る、アプリ再起動後の解放維持、縦スクロールと最終行、端末のSafeArea内の表示を確認する。
