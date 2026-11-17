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
        private GameObject preview;
        private RawImage battlefield;
        private bool resultShown;
        private int farmIndex;
        private int tier;
        private TMP_Text status;
        private TMP_Text bossName;
        private Slider bossHp;
        private Button start;
        private Button close;
        private Button previous;
        private Button next;
        private readonly Button[] farms = new Button[MonsterManager.FarmPresetCount];
        private readonly Image[] allies = new Image[MonsterManager.MaxFarmSize];
        private Image boss;

        internal static RaidPanelUI Create(Transform parent)
        {
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
            Text(layout, "Title", "ファームレイド", .08f, .87f, .92f, .94f, 48);
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

            status = Text(layout, "Status", "", .04f, .145f, .96f, .222f, 36);
            bossHp = BuildBossHpSlider(layout);
            for (int i = 0; i < farms.Length; i++)
            {
                int index = i;
                float left = .045f + i * .305f;
                farms[i] = AddButton(layout, $"Farm{i + 1}", "", left, .092f, left + .292f, .144f,
                    () => { farmIndex = index; RefreshReception(); });
            }
            start = AddButton(layout, "Start", "挑戦", .045f, .032f, .495f, .087f, StartBattle);
            close = AddButton(layout, "Close", "閉じる", .505f, .032f, .955f, .087f, () => router.CloseAll());
        }

        private void RefreshReception()
        {
            var species = reception.GetBoss(tier, catalog);
            bossName.text = $"難易度 {tier + 1}　{(species != null ? species.displayName : "ボス未登録")}";
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
            next.interactable = tier + 1 < MonsterManager.RaidTierCount;
            for (int i = 0; i < farms.Length; i++)
            {
                int count = monsters != null ? monsters.GetFarm(i).Count : 0;
                Caption(farms[i]).text = $"ファーム{i + 1}（{count}体）";
                farms[i].interactable = monsters != null && i != farmIndex;
            }

            bool dungeon = FindFirstObjectByType<DungeonManager>()?.IsDungeonActive == true;
            // MonsterRuntimeSetup supplies the enemy components at spawn time.
            bool bossReady = species != null && species.enemyPrefab != null;
            start.interactable = !dungeon && bossReady && tier <= unlocked && ready > 0 &&
                FindFirstObjectByType<SaveManager>() != null;
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
            if (battle != null)
            {
                if (!resultShown) return;
                CloseBattle();
                RefreshReception();
                return;
            }

            RefreshReception();
            if (!start.interactable) return;
            battle = RaidBattlefield.Create(reception.GetBoss(tier, catalog), reception.GetBossLevel(tier),
                reception.ArenaFloor, monsters.GetFarm(farmIndex), catalog, FindFirstObjectByType<EquipmentManager>());
            if (battle == null)
            {
                status.text = "レイドフィールドを開けませんでした";
                return;
            }

            resultShown = false;
            battlefield.texture = battle.View;
            battlefield.transform.parent.gameObject.SetActive(true);
            preview.SetActive(false);
            Place(status.rectTransform, .04f, .181f, .96f, .222f);
            UpdateBossHp();
            bossHp.gameObject.SetActive(true);
            start.interactable = previous.interactable = next.interactable = false;
            foreach (Button button in farms) button.interactable = false;
            Caption(close).text = "撤退";
        }

        private void Update()
        {
            if (reception == null || !reception.isActiveAndEnabled)
            {
                router.CloseAll();
                return;
            }
            if (battle == null || resultShown) return;
            UpdateBossHp();
            if (battle.Result == RaidBattlefield.Outcome.Fighting)
            {
                status.text = $"残り {Mathf.CeilToInt(battle.Remaining)} 秒　味方 {battle.LivingAllies}/{battle.AllyCount}体";
                return;
            }

            resultShown = true;
            battle.Freeze();
            Caption(close).text = "閉じる";
            Caption(start).text = "受付に戻る";
            start.interactable = true;
            if (battle.Result == RaidBattlefield.Outcome.Victory)
            {
                int reward = reception.GetReward(tier);
                monsters.CompleteRaid(tier, reward);
                bool saved = FindFirstObjectByType<SaveManager>()?.SaveGame() == true;
                status.text = $"勝利！　レイド勲晶 ×{reward:N0}" + (saved ? "" : "\nセーブに失敗しました");
            }
            else
                status.text = battle.Result == RaidBattlefield.Outcome.Timeout ? "時間切れ" : "全滅";
        }

        private void OnDisable() => CloseBattle();

        private void CloseBattle()
        {
            if (battlefield != null)
            {
                battlefield.texture = null;
                battlefield.transform.parent.gameObject.SetActive(false);
            }
            if (battle != null) battle.Close();
            battle = null;
            resultShown = false;
            if (preview != null) preview.SetActive(true);
            if (bossHp != null)
            {
                bossHp.gameObject.SetActive(false);
                Place(status.rectTransform, .04f, .145f, .96f, .222f);
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

        private static TMP_Text Caption(Button button) => button.GetComponentInChildren<TMP_Text>();

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
