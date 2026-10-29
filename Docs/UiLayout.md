# UIの位置調整（MainScene）

## 獲得ログ

MainSceneに`AcquisitionLogsCanvas`を配置した。Prefabは`Assets/Prefab/UI/AcquisitionLogs.prefab`。

- 素材ログ：`AcquisitionLogsCanvas / SafeArea / MaterialDropLog`
- モンスター獲得ログ：`AcquisitionLogsCanvas / SafeArea / MonsterObtainLog`

Hierarchyで対象を選び、InspectorのRect Transformの**Pos X / Pos Y**を変更する。セーフエリアの左下が基準で、Xを増やすと右、Yを増やすと上へ移動する。1080×1920を基準に画面へ拡縮する。現在値は素材(24,24)、モンスター(24,350)。Sceneを保存すれば再生終了後も残る。Prefab側を編集すれば他の配置先にも反映できる。

従来のManagerはログの生成・プール・表示設定を担当し、MainSceneではシーン上の枠へ出力する。起動直後に生成した予備Canvasは表示先の切替時に破棄し、二重表示しない。シーンの枠がない場合は従来のCanvas生成へ戻り、ManagerのLog Positionを使う。シーン配置がある場合はRect Transformが優先され、Managerの他のInspector設定を変えても位置を上書きしない。

## その他のコード生成UI

`Canvas / RuntimeUiLayoutSettings`をシーンへ配置した。InspectorのPlacementsを展開し、対象名の**Position Offset**を変更する。X正=右、Y正=上。初期値(0,0)は現在の位置を維持する。Prefabは`Assets/Prefab/UI/RuntimeUiLayoutSettings.prefab`。

登録済み：記録/目標/ヘルプのカード、スキルボタン、スキル習得画面、装備比較、装備強化、ダンジョン装備詳細、自動売却設定、一括売却確認、パーティ行動指示。

共通のInventoryUiFactoryで生成されるUIは生成後に位置を反映する。シーンロード時には既存CanvasのRectTransformも一度登録するため、配置済みスキルUIにも適用できる。毎フレームのシーン検索は行わず、新しく生成されたUIとInspectorの変更時にだけ処理する。再登録してもオフセットを重ね掛けしない。

追加の対象はPlacementsへ要素を追加し、Labelに説明、Path SuffixにHierarchyの末尾のパスを記載する。例：`GameGuidePanel/SafeArea/Card/Goals/ClearTrackedGoals`。Position Offsetで移動できる。自動レイアウト対象の一覧行は親コンテナで調整する。画面全体を動かす場合はカード/パネルを対象にする。

既にPrefabで位置を編集できるUIは、シーンのRect Transformでも直接変更できる。レイアウト設定のオフセットを併用した場合は、その位置へ加算する。

## 今回MainSceneへ配置したもの

既存配置を維持し、未配置だった設定、図鑑メニュー、モンスター図鑑、装備図鑑、パーティプリセット、素材インベントリ、目標HUDをPrefabインスタンスとして追加した。メニュー6画面は非アクティブで保存し、既存の入口から開く。図鑑メニューは配置済みの各図鑑を優先して再利用する。既存の装備インベントリとスキルHUDは追加せず、そのまま使う。

獲得ログとコード生成UIの配置設定を合わせ、追加は9インスタンス。既存475文書のうちCanvasの子一覧とSceneRoots以外は変更していない。

## 確認

Unity本体でのPrefabインポート・Play・実機描画は未確認。MainSceneを開いてMissing参照がないこと、起動時にメニューが勝手に開かないこと、各入口から配置済み画面を再利用すること、ログ座標と配置オフセットが画面サイズ/セーフエリアに追従することを確認する。

代替API環境では実管理・UI・ログ・配置クラスを全体コンパイルし60項目成功。UI座標の反映/再反映、ログ表示先の切替と解除、既存のコンマ表示、5秒クールタイム、記録結果の復元、一括解除を確認した。Sceneの9インスタンス/一意ID/親子参照/メニュー閉状態と新Prefab14・3文書の内部参照も静的検査した。SaveManager等の協力クラスとUnity APIは代替で、実Unityの保存・レイアウト/描画検証ではない。
