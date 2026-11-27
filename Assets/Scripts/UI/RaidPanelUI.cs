using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class RaidPanelUI : MonoBehaviour
    {
        private MapBuildingUIRouter router;
        private RaidReception reception;
        private MonsterManager monsters;
        private MonsterCatalogSO catalog;
        private RaidBattlefield battle;
        [SerializeField] private GameObject preview;
        [SerializeField] private RawImage battlefield;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultDamage;
        [SerializeField] private TMP_Text resultRewards;
        [SerializeField] private Image resultBabyIcon;
        private int farmIndex;
        private int tier;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text bossName;
        [SerializeField] private Slider bossHp;
        [SerializeField] private Button start;
        [SerializeField] private Button close;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private Button[] farms = new Button[MonsterManager.FarmPresetCount];
        [SerializeField] private Image[] allies = new Image[MonsterManager.MaxFarmSize];
        [SerializeField] private Image boss;
        [SerializeField] private TMP_Text heading;
        [SerializeField] private bool trainingView;
        [SerializeField] private Button performanceButton, retryButton, receptionButton;
        internal bool IsTrainingView => trainingView;
        private bool bound;
        private bool training;

        private void Awake() => BindView();
        private void BindView()
        {
            if (bound || performanceButton == null) return;
            bound = true;
            previous.onClick.AddListener(() => { tier--; RefreshReception(); });
            next.onClick.AddListener(() => { tier++; RefreshReception(); });
            start.onClick.AddListener(StartBattle);
            close.onClick.AddListener(CloseOrRetreat);
            performanceButton.onClick.AddListener(ShowPerformance);
            retryButton.onClick.AddListener(() => { ReturnToReception(); StartBattle(); });
            receptionButton.onClick.AddListener(ReturnToReception);
            for (int i = 0; i < farms.Length; i++)
            {
                int index = i;
                farms[i].onClick.AddListener(() => { farmIndex = index; RefreshReception(); });
            }
        }
        private readonly List<MonsterSO> trainingTargets = new List<MonsterSO>();
        private ProgressionPanelUI performancePanel;

        internal static void OpenTraining(Transform parent)
        {
            RaidPanelUI panel = null;
            foreach (var candidate in FindObjectsByType<RaidPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.trainingView) { panel = candidate; break; }
            if (panel == null) { panel = Create(parent, true); panel.trainingView = true; }
            panel.BindView();
            panel.router = null;
            panel.reception = null;
            panel.training = true;
            panel.monsters = FindFirstObjectByType<MonsterManager>();
            panel.catalog = MonsterCatalogSO.Load();
            panel.farmIndex = panel.monsters != null ? panel.monsters.ActiveFarmIndex : 0;
            panel.tier = 0;
            panel.trainingTargets.Clear();
            if (panel.catalog != null)
                foreach (var species in EncyclopediaContent.Monsters(panel.catalog))
                    if (species.enemyPrefab != null && panel.monsters != null &&
                        panel.monsters.HasAcquiredMonster(species.monsterId))
                        panel.trainingTargets.Add(species);
            panel.CloseBattle();
            panel.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            panel.heading.text = "訓練場";
            panel.RefreshReception();
        }

        private MonsterSO SelectedTarget => training
            ? (tier >= 0 && tier < trainingTargets.Count ? trainingTargets[tier] : null)
            : reception.GetBoss(tier, catalog);

        internal static RaidPanelUI Create(Transform parent, bool forTraining = false)
        {
            foreach (var existing in FindObjectsByType<RaidPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (existing.trainingView == forTraining) { existing.BindView(); return existing; }
            if (parent.GetComponentInParent<Canvas>() == null)
                foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace)
                    { parent = SafeAreaLayout.ContentRoot(candidate.transform); break; }
            var prefab = Resources.Load<RaidPanelUI>("UI/ScenePanels/" + (forTraining ? "TrainingPanel" : "RaidPanel"));
            if (prefab != null)
            {
                var view = Instantiate(prefab, parent, false);
                view.BindView();
                return view;
            }
            var root = new GameObject("RaidPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 180;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            var panel = root.AddComponent<RaidPanelUI>();
            panel.Build();
            return panel;
        }

        internal void Prepare(MapBuildingUIRouter owner, RaidReception source)
        {
            BindView();
            training = false;
            heading.text = "ファームレイド";
            router = owner;
            reception = source;
            monsters = FindFirstObjectByType<MonsterManager>();
            catalog = MonsterCatalogSO.Load();
            farmIndex = monsters != null ? monsters.ActiveFarmIndex : 0;
            tier = Mathf.Min(monsters != null ? monsters.ClearedRaidTiers : 0, MonsterManager.RaidTierCount - 1);
            CloseBattle();
            RefreshReception();
        }

        private void Build()
        {
            var content = new GameObject("Arena", typeof(RectTransform), typeof(Image));
            content.transform.SetParent(transform, false);
            var layout = (RectTransform)content.transform;
            var background = content.GetComponent<Image>();
            background.color = new Color(.07f, .12f, .18f);
            Place(layout, 0, 0, 1, 1);
            heading = Text(layout, "Title", "ファームレイド", .08f, .87f, .92f, .94f, 48);
            bossName = Text(layout, "BossName", "", .18f, .80f, .82f, .87f, 38);
            previous = AddButton(layout, "PreviousTier", "◀", .04f, .805f, .17f, .86f,
                () => { tier--; RefreshReception(); });
            next = AddButton(layout, "NextTier", "▶", .83f, .805f, .96f, .86f,
                () => { tier++; RefreshReception(); });
            preview = new GameObject("FormationPreview", typeof(RectTransform));
            preview.transform.SetParent(layout, false);
            var previewLayout = (RectTransform)preview.transform;
            Place(previewLayout, 0f, 0f, 1f, 1f);
            boss = CreateIcon(previewLayout, "Boss", .25f, .51f, .75f, .79f);

            for (int i = 0; i < allies.Length; i++)
            {
                float left = .045f + (i % 5) * .185f;
                float bottom = .224f + (3 - i / 5) * .069f;
                allies[i] = CreateIcon(previewLayout, $"Ally{i + 1}", left, bottom, left + .17f, bottom + .067f);
            }

            var viewport = new GameObject("BattleViewport", typeof(RectTransform));
            viewport.transform.SetParent(layout, false);
            Place((RectTransform)viewport.transform, .025f, .224f, .975f, .79f);
            var imageObject = new GameObject("ArenaView", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(viewport.transform, false);
            battlefield = imageObject.GetComponent<RawImage>();
            battlefield.raycastTarget = false;
            var aspect = imageObject.AddComponent<AspectRatioFitter>();
            aspect.aspectRatio = 1f;
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            viewport.SetActive(false);

            status = Text(layout, "Status", "", .04f, .181f, .96f, .222f, 36);
            bossHp = BuildBossHpSlider(layout);
            for (int i = 0; i < farms.Length; i++)
            {
                int index = i;
                float left = .045f + i * .305f;
                farms[i] = AddButton(layout, $"Farm{i + 1}", "", left, .092f, left + .292f, .144f,
                    () => { farmIndex = index; RefreshReception(); });
            }
            start = AddButton(layout, "Start", "挑戦", .045f, .032f, .495f, .087f, StartBattle);
            close = AddButton(layout, "Close", "閉じる", .505f, .032f, .955f, .087f, CloseOrRetreat);
            BuildResultPanel(layout);
        }

        private void RefreshReception()
        {
            var species = SelectedTarget;
            bossName.text = (training ? "標的　" : $"難易度 {tier + 1}　") +
                (species != null ? species.displayName : "未登録");
            SetIcon(boss, species);
            var farm = monsters?.GetFarm(farmIndex);
            int ready = 0;
            for (int i = 0; i < allies.Length; i++)
            {
                MonsterSO ally = farm != null && i < farm.Count && catalog != null
                    ? catalog.GetMonster(farm[i].monsterId) : null;
                SetIcon(allies[i], ally);
                if (ally != null) ready++;
            }

            int unlocked = monsters != null ? monsters.ClearedRaidTiers : 0;
            previous.interactable = tier > 0;
            next.interactable = tier + 1 < (training ? trainingTargets.Count : MonsterManager.RaidTierCount);
            for (int i = 0; i < farms.Length; i++)
            {
                int count = monsters != null ? monsters.GetFarm(i).Count : 0;
                Caption(farms[i]).text = $"ファーム{i + 1}（{count}体）";
                farms[i].interactable = monsters != null && i != farmIndex;
            }

            bool dungeon = FindFirstObjectByType<DungeonManager>()?.IsDungeonActive == true;
            // MonsterRuntimeSetup supplies the enemy components at spawn time.
            bool bossReady = species != null && species.enemyPrefab != null;
            start.interactable = !dungeon && bossReady && (training || tier <= unlocked) && ready > 0 &&
                (training || FindFirstObjectByType<SaveManager>() != null);
            if (training)
            {
                status.text = dungeon ? "ダンジョン探索中は利用できません" :
                    !bossReady ? "敵用Prefabのある獲得済みモンスターが必要です" :
                    ready == 0 ? "ファームにモンスターを登録してください" :
                    "60秒間、不死身の標的と模擬戦。レイド想定の補正で、育成・報酬は発生しません。";
                Caption(start).text = "訓練開始";
                Caption(close).text = "閉じる";
                return;
            }
            status.text = dungeon ? "ダンジョン探索中は挑戦できません" :
                tier > unlocked ? "前の難易度をクリアすると解放" :
                species == null ? "受付の Inspector にボスを設定してください" :
                !bossReady ? "ボスの敵用Prefabを設定してください" :
                ready == 0 ? "出撃するファームにモンスターを登録してください" :
                $"勝利報酬　レイド勲晶 ×{reception.GetReward(tier):N0}";
            Caption(close).text = "閉じる";
        }

        private void StartBattle()
        {
            if (battle != null) return;

            RefreshReception();
            if (!start.interactable) return;
            battle = RaidBattlefield.Create(SelectedTarget, training ? 1 : reception.GetBossLevel(tier),
                training ? null : reception.ArenaFloor, monsters.GetFarm(farmIndex), catalog,
                FindFirstObjectByType<EquipmentManager>(), training);
            if (battle == null)
            {
                status.text = "レイドフィールドを開けませんでした";
                return;
            }

            battlefield.texture = battle.View;
            battlefield.transform.parent.gameObject.SetActive(true);
            preview.SetActive(false);
            UpdateBossHp();
            bossHp.gameObject.SetActive(true);
            start.interactable = previous.interactable = next.interactable = false;
            foreach (Button button in farms) button.interactable = false;
            Caption(close).text = "撤退";
        }

        private void Update()
        {
            if (!training && (reception == null || !reception.isActiveAndEnabled))
            {
                router.CloseAll();
                return;
            }
            if (battle == null || resultPanel.activeSelf) return;
            UpdateBossHp();
            if (battle.Result == RaidBattlefield.Outcome.Fighting)
            {
                status.text = $"残り {Mathf.CeilToInt(battle.Remaining)} 秒　味方 {battle.LivingAllies}/{battle.AllyCount}体";
                return;
            }

            bool won = battle.Result == RaidBattlefield.Outcome.Victory;
            ShowResult(won ? "勝利" : battle.Result == RaidBattlefield.Outcome.Timeout ? "時間切れ" : "全滅", won);
        }

        private void CloseOrRetreat()
        {
            if (battle == null)
            {
                if (training) gameObject.SetActive(false);
                else router.CloseAll();
            }
            else if (battle.Result == RaidBattlefield.Outcome.Fighting) ShowResult("撤退", false);
        }

        private void ShowResult(string outcome, bool won)
        {
            if (resultPanel.activeSelf) return;
            battle.Freeze();
            resultTitle.text = training ? "訓練終了" : outcome;
            resultDamage.text = $"与ダメージ {battle.DamageDealt:N0}　経過 {battle.Elapsed:0.0}秒";
            resultRewards.text = "獲得物　なし";
            SetIcon(resultBabyIcon, null);
            resultPanel.SetActive(true);

            if (training)
            {
                resultRewards.text = "訓練のため獲得物なし";
                return;
            }
            if (!won) return;
            GameAudio.Play(SoundCue.Clear);

            int materials = reception.GetReward(tier);
            var baby = reception.GetRewardBaby(tier, catalog);
            monsters.CompleteRaid(tier, materials);
            var obtained = baby != null ? monsters.ObtainMonster(baby.monsterId,
                MonsterDropRoller.RollIndividualValues(), bypassAutoSell: true) : null;
            bool saved = FindFirstObjectByType<SaveManager>()?.SaveGame() == true;

            resultRewards.text = $"獲得物\nレイド勲晶 ×{materials:N0}\n" +
                (obtained != null ? $"{baby.displayName}（Lv1）×1" : "ベビー報酬が未登録です") +
                (saved ? "" : "\nセーブに失敗しました");
            if (obtained != null) SetIcon(resultBabyIcon, baby);
        }

        private void BuildResultPanel(RectTransform parent)
        {
            resultPanel = new GameObject("RaidResultPanel", typeof(RectTransform), typeof(Image));
            resultPanel.transform.SetParent(parent, false);
            var overlay = (RectTransform)resultPanel.transform;
            Place(overlay, 0, 0, 1, 1);
            resultPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, .72f);

            var cardObject = new GameObject("ResultCard", typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(overlay, false);
            var card = (RectTransform)cardObject.transform;
            Place(card, .07f, .23f, .93f, .77f);
            cardObject.GetComponent<Image>().color = new Color(.10f, .16f, .24f);

            Text(card, "Heading", "戦闘結果", .06f, .86f, .60f, .97f, 46);
            AddButton(card, "Performance", "個別成績", .64f, .88f, .95f, .97f, ShowPerformance);
            resultTitle = Text(card, "Outcome", "", .06f, .71f, .94f, .86f, 64);
            resultDamage = Text(card, "DamageDealt", "", .06f, .59f, .94f, .71f, 38);
            resultRewards = Text(card, "Rewards", "", .06f, .32f, .94f, .58f, 36);
            resultBabyIcon = CreateIcon(card, "RewardBaby", .32f, .14f, .68f, .32f);
            AddButton(card, "Retry", "再挑戦", .05f, .035f, .47f, .14f, () =>
            {
                ReturnToReception();
                StartBattle();
            });
            AddButton(card, "Reception", "受付へ戻る", .53f, .035f, .95f, .14f, ReturnToReception);
            resultPanel.SetActive(false);
        }

        private void ShowPerformance()
        {
            if (battle == null) return;
            if (performancePanel == null) performancePanel = ProgressionPanelUI.Create(transform, "BattlePerformance");
            var rows = new List<ProgressionPanelUI.Item>();
            int rank = 0;
            foreach (var record in battle.Performance)
            {
                rank++;
                rows.Add(new ProgressionPanelUI.Item($"{rank}　{record.name}\n" +
                    $"与ダメージ {record.damage:N0}　回復量 {record.healing:N0}\n" +
                    $"DPS {record.DamagePerSecond(battle.Elapsed):N1}　生存 {record.survivalSeconds:0.0}秒" +
                    (record.survived ? "（生存）" : "（戦闘不能）")));
            }
            performancePanel.Show("個別成績", rows,
                "与ダメージは対象へ実際に与えた量、回復量は実際に回復したHPです。DPSは戦闘全体の経過時間で計算します。");
        }

        private void ReturnToReception()
        {
            CloseBattle();
            RefreshReception();
        }

        private void OnDisable() => CloseBattle();

        private void CloseBattle()
        {
            if (performancePanel != null) performancePanel.Close();
            if (battlefield != null)
            {
                battlefield.texture = null;
                battlefield.transform.parent.gameObject.SetActive(false);
            }
            if (battle != null) battle.Close();
            battle = null;
            if (resultPanel != null) resultPanel.SetActive(false);
            if (preview != null) preview.SetActive(true);
            if (bossHp != null)
            {
                bossHp.gameObject.SetActive(false);
            }
            if (start != null) Caption(start).text = "挑戦";
        }

        private void UpdateBossHp()
        {
            float ratio = battle.BossMaxHp > 0 ? (float)((double)battle.BossHp / battle.BossMaxHp) : 0f;
            bossHp.SetValueWithoutNotify(Mathf.Clamp01(ratio));
        }

        private static Slider BuildBossHpSlider(RectTransform parent)
        {
            var root = new GameObject("BossHpSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            Place(rect, .08f, .15f, .92f, .175f);
            var track = root.GetComponent<Image>();
            track.color = new Color(.22f, .12f, .17f);
            track.raycastTarget = false;

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var fillRect = (RectTransform)fillArea.transform;
            Place(fillRect, 0, 0, 1, 1);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var image = fill.GetComponent<Image>();
            image.color = new Color(.92f, .28f, .31f);
            image.raycastTarget = false;
            Place(image.rectTransform, 0, 0, 1, 1);

            var slider = root.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.fillRect = image.rectTransform;
            root.SetActive(false);
            return slider;
        }

        private static void SetIcon(Image icon, MonsterSO species)
        {
            icon.sprite = species != null ? (species.sprite != null ? species.sprite : species.icon) : null;
            icon.enabled = icon.sprite != null;
        }

        private static Image CreateIcon(RectTransform parent, string name,
            float left, float bottom, float right, float top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var icon = go.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Place(icon.rectTransform, left, bottom, right, top);
            return icon;
        }

        private static TMP_Text Text(Transform parent, string name, string value,
            float left, float bottom, float right, float top, int size)
        {
            var label = InventoryUiFactory.Text(parent, name, value, Vector2.zero, Vector2.zero, size);
            label.fontSizeMin = 12;
            label.alignment = TextAlignmentOptions.Center;
            Place(label.rectTransform, left, bottom, right, top);
            return label;
        }

        private static Button AddButton(Transform parent, string name, string label,
            float left, float bottom, float right, float top, UnityEngine.Events.UnityAction click)
        {
            var button = InventoryUiFactory.Button(parent, name, label, Vector2.zero, Vector2.zero);
            Place((RectTransform)button.transform, left, bottom, right, top);
            Caption(button).fontSizeMin = 12;
            button.onClick.AddListener(click);
            return button;
        }

        private static TMP_Text Caption(Button button) => button.GetComponentInChildren<TMP_Text>(true);

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
