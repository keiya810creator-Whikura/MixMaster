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
        private sealed class FighterView
        {
            public Image icon;
            public TMP_Text hp;

            public void Render(RaidBattle.Fighter fighter, float time)
            {
                icon.gameObject.SetActive(fighter != null);
                hp.gameObject.SetActive(fighter != null);
                if (fighter == null) return;
                icon.sprite = fighter.Species.sprite != null ? fighter.Species.sprite : fighter.Species.icon;
                icon.enabled = icon.sprite != null;
                icon.color = fighter.Hp > 0 ? Color.white : new Color(.5f, .5f, .5f, .45f);
                float attackTime = time - fighter.LastAttack;
                icon.rectTransform.localScale = Vector3.one * (attackTime < .16f ? 1.12f : 1f);
                hp.text = fighter.Hp > 0 ? $"{fighter.Hp:N0}" : "戦闘不能";
            }
        }

        private MapBuildingUIRouter router;
        private RaidReception reception;
        private MonsterManager monsters;
        private MonsterCatalogSO catalog;
        private RaidBattle battle;
        private int farmIndex;
        private int tier;
        private float stepRemainder;
        private TMP_Text status;
        private TMP_Text bossName;
        private Button start;
        private Button close;
        private Button previous;
        private Button next;
        private readonly Button[] farms = new Button[MonsterManager.FarmPresetCount];
        private readonly FighterView[] allies = new FighterView[MonsterManager.MaxFarmSize];
        private FighterView boss;

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
            battle = null;
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
            boss = CreateFighter(layout, "Boss", .25f, .51f, .75f, .79f);

            for (int i = 0; i < allies.Length; i++)
            {
                float left = .045f + (i % 5) * .185f;
                float bottom = .224f + (3 - i / 5) * .069f;
                allies[i] = CreateFighter(layout, $"Ally{i + 1}", left, bottom, left + .17f, bottom + .067f);
            }

            status = Text(layout, "Status", "", .04f, .145f, .96f, .222f, 36);
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
            boss.Render(species != null ? new RaidBattle.Fighter(species, species.baseStats) : null, 0);
            var farm = monsters?.GetFarm(farmIndex);
            int ready = 0;
            for (int i = 0; i < allies.Length; i++)
            {
                MonsterSO ally = farm != null && i < farm.Count && catalog != null
                    ? catalog.GetMonster(farm[i].monsterId) : null;
                allies[i].Render(ally != null ? new RaidBattle.Fighter(ally, ally.baseStats) : null, 0);
                // The reception only previews members, without showing battle HP.
                allies[i].hp.gameObject.SetActive(false);
                if (ally != null) ready++;
            }
            boss.hp.gameObject.SetActive(false);

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
            start.interactable = !dungeon && species != null && tier <= unlocked && ready > 0 &&
                FindFirstObjectByType<SaveManager>() != null;
            status.text = dungeon ? "ダンジョン探索中は挑戦できません" :
                tier > unlocked ? "前の難易度をクリアすると解放" :
                species == null ? "受付の Inspector にボスを設定してください" :
                ready == 0 ? "出撃するファームにモンスターを登録してください" :
                $"勝利報酬　レイド勲晶 ×{reception.GetReward(tier):N0}";
            Caption(close).text = "閉じる";
        }

        private void StartBattle()
        {
            if (battle != null)
            {
                if (!battle.Finished) return;
                battle = null;
                Caption(start).text = "挑戦";
                RefreshReception();
                return;
            }
            RefreshReception();
            if (!start.interactable) return;

            battle = new RaidBattle(reception.GetBoss(tier, catalog), reception.GetBossLevel(tier),
                monsters.GetFarm(farmIndex), catalog, FindFirstObjectByType<EquipmentManager>());
            stepRemainder = 0;
            start.interactable = previous.interactable = next.interactable = false;
            foreach (Button button in farms) button.interactable = false;
            Caption(close).text = "撤退";
            RenderBattle();
        }

        private void Update()
        {
            if (reception == null || !reception.isActiveAndEnabled)
            {
                router.CloseAll();
                return;
            }
            if (battle == null || battle.Finished) return;
            const float step = .05f;
            stepRemainder += Time.unscaledDeltaTime;
            bool ticked = false;
            while (stepRemainder >= step && !battle.Finished)
            {
                stepRemainder -= step;
                battle.Step(step);
                ticked = true;
            }
            if (!ticked) return;

            RenderBattle();
            if (!battle.Finished) return;
            Caption(close).text = "閉じる";
            Caption(start).text = "受付に戻る";
            start.interactable = true;
            if (battle.Won)
            {
                int reward = reception.GetReward(tier);
                monsters.CompleteRaid(tier, reward);
                bool saved = FindFirstObjectByType<SaveManager>()?.SaveGame() == true;
                status.text = $"勝利！　レイド勲晶 ×{reward:N0}" + (saved ? "" : "\nセーブに失敗しました");
            }
            else
                status.text = battle.Elapsed >= RaidBattle.TimeLimit ? "時間切れ" : "全滅";
        }

        private void OnDisable()
        {
            battle = null;
            if (start != null) Caption(start).text = "挑戦";
        }

        private void RenderBattle()
        {
            float time = battle.Elapsed;
            boss.Render(battle.Boss, time);
            for (int i = 0; i < allies.Length; i++)
                allies[i].Render(i < battle.Allies.Count ? battle.Allies[i] : null, time);
            status.text = $"残り {Mathf.CeilToInt(Mathf.Max(0, RaidBattle.TimeLimit - time))} 秒";
        }

        private static FighterView CreateFighter(RectTransform parent, string name,
            float left, float bottom, float right, float top)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var icon = go.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            float hpTop = bottom + (top - bottom) * .25f;
            Place(icon.rectTransform, left, hpTop, right, top);
            return new FighterView { icon = icon, hp = Text(parent, name + "HP", "", left, bottom, right, hpTop, 23) };
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
