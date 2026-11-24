# UIの位置調整（MainScene）

## 獲得ログ

MainSceneに`AcquisitionLogsCanvas`を配置した。Prefabは`Assets/Prefab/UI/AcquisitionLogs.prefab`。

- 素材ログ：`AcquisitionLogsCanvas / MaterialDropLog`
- モンスター獲得ログ：`AcquisitionLogsCanvas / MonsterObtainLog`

Hierarchyで対象を選び、InspectorのRect Transformの**Pos X / Pos Y**を変更する。セーフエリアの左下が基準で、Xを増やすと右、Yを増やすと上へ移動する。1080×1920を基準に画面へ拡縮する。現在値は素材(24,24)、モンスター(24,350)。Sceneを保存すれば再生終了後も残る。Prefab側を編集すれば他の配置先にも反映できる。

従来のManagerはログの生成・プール・表示設定を担当し、MainSceneではシーン上の枠へ出力する。起動直後に生成した予備Canvasは表示先の切替時に破棄し、二重表示しない。シーンの枠がない場合は従来のCanvas生成へ戻り、ManagerのLog Positionを使う。シーン配置がある場合はRect Transformが優先され、Managerの他のInspector設定を変えても位置を上書きしない。

## その他のコード生成UI

`Canvas / RuntimeUiLayoutSettings`をシーンへ配置した。InspectorのPlacementsを展開し、対象名の**Position Offset**を変更する。X正=右、Y正=上。初期値(0,0)は現在の位置を維持する。Prefabは`Assets/Prefab/UI/RuntimeUiLayoutSettings.prefab`。

登録済み：記録/目標/ヘルプのカード、スキルボタン、スキル習得画面、装備比較、装備強化、ダンジョン装備詳細、自動売却設定、一括売却確認、パーティ行動指示。

共通のInventoryUiFactoryで生成されるUIは生成後に位置を反映する。シーンロード時には既存CanvasのRectTransformも一度登録するため、配置済みスキルUIにも適用できる。毎フレームのシーン検索は行わず、新しく生成されたUIとInspectorの変更時にだけ処理する。再登録してもオフセットを重ね掛けしない。

追加の対象はPlacementsへ要素を追加し、Labelに説明、Path SuffixにHierarchyの末尾のパスを記載する。例：`GameGuidePanel/Card/Goals/ClearTrackedGoals`。Position Offsetで移動できる。自動レイアウト対象の一覧行は親コンテナで調整する。画面全体を動かす場合はカード/パネルを対象にする。

既にPrefabで位置を編集できるUIは、シーンのRect Transformでも直接変更できる。レイアウト設定のオフセットを併用した場合は、その位置へ加算する。

## 今回MainSceneへ配置したもの

既存配置を維持し、未配置だった設定、図鑑メニュー、モンスター図鑑、装備図鑑、パーティプリセット、素材インベントリ、目標HUDをPrefabインスタンスとして追加した。メニュー6画面は非アクティブで保存し、既存の入口から開く。図鑑メニューは配置済みの各図鑑を優先して再利用する。既存の装備インベントリとスキルHUDは追加せず、そのまま使う。

獲得ログとコード生成UIの配置設定を合わせ、追加は9インスタンス。既存475文書のうちCanvasの子一覧とSceneRoots以外は変更していない。

## 確認

Unity本体でのPrefabインポート・Play・実機描画は未確認。MainSceneを開いてMissing参照がないこと、起動時にメニューが勝手に開かないこと、各入口から配置済み画面を再利用すること、ログ座標と配置オフセットが画面サイズ/セーフエリアに追従することを確認する。

代替API環境では実管理・UI・ログ・配置クラスを全体コンパイルし60項目成功。UI座標の反映/再反映、ログ表示先の切替と解除、既存のコンマ表示、5秒クールタイム、記録結果の復元、一括解除を確認した。Sceneの9インスタンス/一意ID/親子参照/メニュー閉状態と新Prefab14・3文書の内部参照も静的検査した。SaveManager等の協力クラスとUnity APIは代替で、実Unityの保存・レイアウト/描画検証ではない。


## 2026年10月30日：共通SafeArea配下への配置用に整理

各UI Prefab内のSafeAreaを取り除いた。共通の`Canvas / SafeArea / Header・Body・Bottom`へ配置する。今回の変更ではMainScene内の配置・階層は変更せず、振り分けは利用者が行う。獲得ログの位置も、移動したAcquisitionLogsCanvas配下のMaterialDropLog/MonsterObtainLogで指定する。

RuntimeUiLayoutSettingsの既定パスはGameGuidePanel/Card、SkillHUD/SkillButtons、SkillHUD/SkillPanelへ更新。独自の古いパスは削除したPrefab内のSafeArea部分を取り除く。詳細は[MobileSafeArea.md](MobileSafeArea.md)。

## 2026年11月1日：配置後の入力エラー・HPバー警告への対応

FloatingJoystickのOnDisableはOnScreenControlの仮想メソッドをoverrideし、スティックをゼロへ戻した後、finallyでbase.OnDisableを呼ぶ。従来は同名のprivateメソッドで基底の登録解除を隠しており、UIの非表示/再表示やシーン終了時に仮想デバイス・入力イベントバッファの後処理が行われなかった。Input System 1.14.0の実OnScreenControlソースで登録解除の流れを確認した。ログの「Control count per binding cannot exceed byte.MaxValue=255」との関連は考えられるが、添付環境そのものの再現・解消確認はUnity上で行う必要がある。

CharacterWorldBar.prefabのSkillNameとHPSlider/LevelLabelにあるLayoutElementのm_Script GUIDを、uGUIの正しい306cc8c2b49d7114eaa3623786fc2126へ修正した。既存の部品ID、Ignore Layout、文字設定、座標、レベル/スキル名の参照は保持。TMP_Textやバーのプールを削除して警告を抑える対応は行わない。MainSceneおよび利用者が配置したHeader/Body/Bottomは変更しない。

更新後はPlayを停止しUnity Editorを一度終了・再起動して、以前の実行中に残った入力状態をリセットする。ConsoleをClearし、移動スティックを操作、親UIの非表示/再表示、Playの停止/再開、敵の出現/スキル発動を確認する。255件エラーやSkillName/LevelLabelのMissing Script警告が再発しないこと、移動が停止/再開できること、HP/Lv/スキル名が従来の位置に表示されることを確認する。

実FloatingJoystickとInput System 1.14.0の実OnScreenControlを代替プラットフォームAPIでコンパイルし、1000回の有効/無効、共有仮想デバイス、押下中の無効化、異なる指の解放、操作禁止/再開を8011項目で検査した。変更前はデバイスの解放検査で失敗し、変更後は成功。InputSystem/Unity/NativeArray等は代替であり、ネイティブ入力処理・255件エラーそのもの・Unityインポート/Play/実機描画の検証ではない。

## 編集時に配置する管理画面（2026-11-24）

ファームやスキル設定の追加操作、レイド／訓練場、転移先一覧、記録／目標／ヘルプ、家系図・個体名・同種比較、売却確認、自動売却、装備比較・強化、リザルト装備詳細を、保存されたUI部品として編集できます。

- 新しい画面プレファブは `Assets/Resources/UI/ScenePanels` にあります。ファーム・モンスター詳細などの既存プレファブにも追加ボタンと小画面を保存しています。
- MainSceneをUnity Editorで開くと、不足するレイド・訓練場・転移先一覧を共通Canvasの `SafeArea（またはSafeAreaHUD）/Body` に配置します。既存のSkillHUDには一括設定ボタン／小画面／説明スクロールを補い、ファーム通知もシーンへ保存します。Play操作は不要です。
- 既存の画面や参照がある場合はそのまま使います。以後の読み込みで位置・大きさを作り直しません。既存のHeader／Body／Bottomや他のUIを移動しません。
- パネルは初期状態で非表示です。Hierarchyで対象を一時的にActiveにして、子のCard・ボタン・テキストのRectTransformを調整してください。調整後は非表示に戻してシーンを保存します。レイド／訓練場では `RaidResultPanel` をActiveにすると結果画面も調整できます。
- ファームの20枠にはお気に入り・ロック・解除を保存しました。各ボタンのRectTransformを変更でき、実行中の固定座標への戻しを止めています。ファーム切り替え・レベルアップ・目標・通知確認・訓練場も編集できます。
- スキル設定の `AutoSetupSkills` は操作ボタンと同名の小画面があります。小画面側には `ProgressionPanelUI` が付いています。長い説明のContentは文字数に応じて高さが変わります。表示範囲は親の `DetailsScroll` で調整します。
- 記録／目標／ヘルプは設定プレファブの `GameGuidePanel/Card` 配下にページを保存しています。内容に合わせてRecords／Goals／Helpを切り替えて確認します。固定行の配置は保存し、件数・文章・アイコン・選択状態を更新します。
- リストの可変件数分の複製とテキストの高さ計算は従来どおり必要です。転移先一覧は保存済みの `DestinationTemplate` から複製するので、テンプレートで各行の見た目を変更できます。

自動配置は編集時のみです。MainSceneが変更済みなら配置を追加してDirtyにするだけで、未保存の他の編集を勝手に保存しません。MainSceneが未変更なら配置後に保存します。別のシーンで使う場合は `MixMaster > UI > 不足している画面をシーンに配置` を実行できます。

MainScene本体はGit LFS管理で、この開発環境では新しいLFSオブジェクトのアップロード認証を利用できないため、今回のPRにはMainSceneの新しいLFSポインターを含めていません。上記の編集時配置でローカルのシーンへ反映する方式です。既存のシーンファイルを置き換えず、名前・種類・設定済み参照を確認して不足分だけ補います。

確認済みは、実ファーム／共通管理パネル／確認ダイアログのコードを代替Unity APIでコンパイルし、保存されたプレファブデータから読み込んだUIでの再利用、開き直し、ページ送り、育成目標、保護ボタン、配置保持など120項目です。全C#のPlayer／Editor構文とプレファブのID・参照を検査しています。Unity Editor本体でのコンパイル、インポート時配置、Play時の表示・クリック・レイド描画は未実施です。

## スキルのタグ表示・スクロール末尾の補正（2026-11-24）

図鑑のスキル詳細で `<b>` が文字として出ていたため、整形済み説明を表示する図鑑ポップアップ、転生側のスキル詳細、スキル設定の説明でリッチテキストを明示的に有効にしました。図鑑2プレファブ側も修正しています。

全28プレファブ内の62個のScrollRect（プルダウン27個／通常一覧・説明等35個）へ `ScrollViewLayout` を保存しました。プルダウンはテンプレートの項目Pivotを中央、項目の上端位置とContentの高さを上下12の余白込みにそろえています。従来の「上Pivot＋半行分のオフセット」は、TMPが複製した最後の項目をContent外へ半行分押し出していました。項目数が後で増えるプルダウンも最後までスクロールできます。行動モードのプルダウンに残っていたスクロール禁止も解除しました。

通常のLayoutGroup・テキストには最低12の下余白を確保し、固定行では最後の行が入るContent範囲を確保します。既に大きい余白は保持します。図鑑・素材の仮想リストは表示中のセル数でなく全行数に末尾の余白を足すため、セルの再利用を維持します。目標HUDなど独自の高さ計算を持つ画面は固定行の採寸から除外します。

外枠・パネル・ボタンの配置は変更していません。補正コンポーネントの `End Padding` はInspectorで調整できます。未配置の古いシーンコピーはシーン読込時に補完し、実行中に新しく生成するUIにも同じ補正を適用します。毎フレームの全画面検索は行いません。Editorでは既存の配置処理から不足コンポーネント・設定を補完します。MainSceneのLFSポインターは変更していません。

代替Unity APIで実ScrollViewLayout／EncyclopediaGridUI／InventoryUiFactory／SkillDetailsPopupUIをコンパイルし、修正前の半行はみ出し再現、27種類のテンプレート・1～2,000選択肢の端点、再表示時の余白非累積、固定行・テキスト・既存余白保持、仮想リスト0～5,000件の末尾とセル数上限、タグ設定を計763項目確認しました。62 ScrollRectと6,391 YAML文書の設定・参照を検査しています。Unity本体の全体コンパイル・実際のTMP描画・端末でのドラッグ確認は未実施です。図鑑のスキル説明と一覧／フィルターの一番下、転生・装備・記録・設定の各スクロール末尾をUnityで確認してください。
