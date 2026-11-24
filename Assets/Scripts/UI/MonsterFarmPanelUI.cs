using System;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class MonsterFarmPanelUI : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            public Button button;
            public Button removeButton;
            public Image icon;
            public TMP_Text levels;
            [NonSerialized] public OwnedMonsterRecord record;
            public Button favoriteButton, lockButton;
            public TMP_Text favoriteLabel, lockLabel;
            [NonSerialized] public bool fitButtonsAtRuntime;
            [NonSerialized] public Vector2 layoutSize;
        }

        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private OwnedMonsterDetailUI detailPrefab;
        [SerializeField] private Slot[] slots;

        internal OwnedMonsterDetailUI DetailPrefab => detailPrefab;
        private MonsterManager monsters;
        private MonsterCatalogSO catalog;
        [SerializeField] private OwnedMonsterDetailUI detail;
        private bool dirty;
        [SerializeField] private Button[] presetButtons = new Button[MonsterManager.FarmPresetCount];
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeLabel;
        [SerializeField] private Button targetLevelButton, acknowledgeButton, trainingButton;
        private ProgressionPanelUI targetPanel;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < presetButtons.Length; i++)
            {
                int index = i;
                float left = .04f + i * .31f;
                if (presetButtons[i] == null) presetButtons[i] = AddFarmButton($"Farm{i + 1}", $"ファーム{i + 1}",
                    new Vector2(left, .076f), new Vector2(left + .30f, .123f));
                presetButtons[i].onClick.AddListener(() =>
                {
                    if (monsters != null && monsters.SetActiveFarm(index)) SaveFarm();
                });
            }
            if (upgradeButton == null) upgradeButton = AddFarmButton("FarmUpgrade", "", new Vector2(.04f, .012f),
                new Vector2(.96f, .068f));
            upgradeLabel = upgradeButton.GetComponentInChildren<TMP_Text>();
            upgradeButton.onClick.AddListener(() =>
            {
                if (monsters != null && monsters.TryUpgradeFarm()) SaveFarm();
            });
            bool createTargetControls = targetLevelButton == null;
            if (targetLevelButton == null)
                targetLevelButton = AddFarmButton("FarmTarget", "育成目標", new Vector2(.04f, .772f), new Vector2(.46f, .812f));
            if (acknowledgeButton == null)
                acknowledgeButton = AddFarmButton("AcknowledgeTargets", "通知を確認", new Vector2(.49f, .772f), new Vector2(.74f, .812f));
            if (trainingButton == null)
                trainingButton = AddFarmButton("Training", "訓練場", new Vector2(.76f, .772f), new Vector2(.96f, .812f));
            if (createTargetControls && slots.Length > 0 && slots[0].button.transform.parent is RectTransform slotArea)
                slotArea.anchorMax = new Vector2(slotArea.anchorMax.x, Mathf.Min(slotArea.anchorMax.y, .76f));
            targetLevelButton.onClick.AddListener(() =>
            {
                if (monsters == null) return;
                if (targetPanel == null) targetPanel = ProgressionPanelUI.Create(transform, "FarmTargetLevel");
                targetPanel.ShowNumber("ファームの育成目標Lv", monsters.FarmTargetLevel(monsters.ActiveFarmIndex),
                    1, ExperienceProgression.MaxLevel, level =>
                    {
                        monsters.SetFarmTargetLevel(monsters.ActiveFarmIndex, level);
                        SaveFarm();
                    });
            });
            acknowledgeButton.onClick.AddListener(() =>
            {
                if (monsters == null) return;
                monsters.AcknowledgeFarmTargets(monsters.ActiveFarmIndex);
                SaveFarm();
            });
            trainingButton.onClick.AddListener(() => RaidPanelUI.OpenTraining(transform));
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].button.onClick.AddListener(() => OpenDetail(index));
                Slot slot = slots[i];
                slot.removeButton.onClick.AddListener(() => RemoveFromFarm(index));
                slot.fitButtonsAtRuntime = slot.favoriteButton == null || slot.lockButton == null;
                if (slot.favoriteButton == null)
                    slot.favoriteButton = CreateProtectionButton(slot, "Favorite", "☆",
                        () => ToggleProtection(slot, false), out slot.favoriteLabel);
                else slot.favoriteButton.onClick.AddListener(() => ToggleProtection(slot, false));
                if (slot.lockButton == null)
                    slot.lockButton = CreateProtectionButton(slot, "Lock", "ロック",
                        () => ToggleProtection(slot, true), out slot.lockLabel);
                else slot.lockButton.onClick.AddListener(() => ToggleProtection(slot, true));
            }
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            monsters = FindFirstObjectByType<MonsterManager>();
            catalog = MonsterCatalogSO.Load();
            if (monsters != null)
            {
                monsters.FarmChanged += MarkDirty;
                monsters.MonsterProtectionChanged += HandleProtectionChanged;
                monsters.MonsterSold += HandleSale;
                monsters.MonstersBulkSold += MarkDirty;
                monsters.MonsterLeveledUp += HandleLevelUp;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (monsters != null)
            {
                monsters.FarmChanged -= MarkDirty;
                monsters.MonsterProtectionChanged -= HandleProtectionChanged;
                monsters.MonsterSold -= HandleSale;
                monsters.MonstersBulkSold -= MarkDirty;
                monsters.MonsterLeveledUp -= HandleLevelUp;
            }
            if (detail != null) detail.Hide();
            if (targetPanel != null) targetPanel.Close();
        }

        private void Close() => gameObject.SetActive(false);
        private void MarkDirty() => dirty = true;
        private void HandleSale(string _) => MarkDirty();

        private void HandleLevelUp(OwnedMonsterRecord record)
        {
            if (record.farmStartLevel > 0) MarkDirty();
        }

        private void LateUpdate()
        {
            if (dirty) Refresh();

            foreach (Slot slot in slots)
            {
                if (!slot.fitButtonsAtRuntime) continue;
                Vector2 size = ((RectTransform)slot.button.transform).rect.size;
                if (size == slot.layoutSize) continue;
                slot.layoutSize = size;
                float side = Mathf.Max(0f, Mathf.Min(size.x * .28f, size.y * .28f));
                PlaceButton(slot.favoriteButton, 1f / 6f, side);
                PlaceButton(slot.lockButton, .5f, side);
                PlaceButton(slot.removeButton, 5f / 6f, side);
            }
        }

        private Button AddFarmButton(string name, string caption, Vector2 min, Vector2 max)
        {
            var button = InventoryUiFactory.Button(transform, name, caption, Vector2.zero, Vector2.zero);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            button.GetComponentInChildren<TMP_Text>().font = countText.font;
            return button;
        }

        private void SaveFarm()
        {
            if (detail != null) detail.Hide();
            Refresh();
            FindFirstObjectByType<SaveManager>()?.SaveGame();
        }

        private void Refresh()
        {
            dirty = false;
            int count = monsters != null ? monsters.FarmMonsters.Count : 0;
            countText.text = monsters == null ? "" :
                $"稼働中 ファーム{monsters.ActiveFarmIndex + 1}　{count} / {MonsterManager.MaxFarmSize}体";
            targetLevelButton.GetComponentInChildren<TMP_Text>().text = monsters == null ? "育成目標" :
                $"目標Lv {monsters.FarmTargetLevel(monsters.ActiveFarmIndex)}（変更）";
            acknowledgeButton.interactable = monsters != null && monsters.FarmNotificationCount(monsters.ActiveFarmIndex) > 0;
            for (int i = 0; i < presetButtons.Length; i++)
            {
                presetButtons[i].interactable = monsters != null && i != monsters.ActiveFarmIndex;
                int members = monsters != null ? monsters.GetFarm(i).Count : 0;
                presetButtons[i].GetComponentInChildren<TMP_Text>().text = $"ファーム{i + 1}（{members}体）" +
                    (monsters != null && monsters.FarmNotificationCount(i) > 0 ? " ●" : "");
            }
            int cost = monsters != null ? monsters.FarmUpgradeCost : 0;
            upgradeButton.interactable = cost > 0 && monsters.RaidMaterials >= cost;
            upgradeLabel.text = monsters == null ? "" :
                $"ファーム Lv{monsters.FarmLevel}　経験値 {monsters.FarmExperienceRate}%\n" +
                (cost == 0 ? $"最大レベル　レイド勲晶 {monsters.RaidMaterials:N0}個" :
                    $"レベルアップ　レイド勲晶 {monsters.RaidMaterials:N0} / {cost:N0}");
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                OwnedMonsterRecord record = i < count ? monsters.FarmMonsters[i] : null;
                slot.record = record;
                RefreshProtection(slot);
                slot.removeButton.gameObject.SetActive(record != null);
                MonsterSO species = record != null && catalog != null
                    ? catalog.GetMonster(record.monsterId) : null;
                slot.icon.sprite = species != null
                    ? (species.icon != null ? species.icon : species.sprite) : null;
                slot.icon.enabled = slot.icon.sprite != null;
                slot.levels.text = record != null
                    ? $"{record.farmStartLevel}Lv → {record.level}Lv"
                    : "空き";
                slot.levels.color = monsters != null && monsters.HasFarmNotification(record)
                    ? new Color(1f, .86f, .3f) : Color.white;
                slot.button.interactable = record != null;
            }
        }

        private static Button CreateProtectionButton(Slot slot, string name, string label,
            Action click, out TMP_Text text)
        {
            Button button = InventoryUiFactory.Button(slot.button.transform, name, label,
                Vector2.zero, Vector2.zero);
            text = button.GetComponentInChildren<TMP_Text>();
            text.font = slot.levels.font;
            text.enableWordWrapping = false;
            text.fontSizeMin = 8f;
            button.onClick.AddListener(() => click());
            return button;
        }

        private static void PlaceButton(Button button, float horizontalAnchor, float side)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(horizontalAnchor, .98f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(side, side);
        }

        private static void RefreshProtection(Slot slot)
        {
            var record = slot.record;
            slot.favoriteButton.gameObject.SetActive(record != null);
            slot.lockButton.gameObject.SetActive(record != null);
            if (record == null) return;
            slot.favoriteLabel.text = record.isFavorite ? "★" : "☆";
            slot.favoriteLabel.color = record.isFavorite ? new Color(1f, .84f, .35f) : Color.white;
            slot.lockLabel.text = record.isLocked ? "ロック中" : "ロック";
            slot.lockLabel.color = record.isLocked ? new Color(.52f, .89f, 1f) : Color.white;
        }

        private void HandleProtectionChanged(OwnedMonsterRecord record)
        {
            foreach (Slot slot in slots)
                if (slot.record == record) RefreshProtection(slot);
        }

        private void ToggleProtection(Slot slot, bool locked)
        {
            var record = slot.record;
            if (record == null || record.farmStartLevel <= 0 || monsters == null) return;
            bool changed = locked ? monsters.SetLocked(record.uniqueId, !record.isLocked)
                : monsters.SetFavorite(record.uniqueId, !record.isFavorite);
            if (changed) FindFirstObjectByType<SaveManager>()?.SaveGame();
        }

        private void RemoveFromFarm(int index)
        {
            OwnedMonsterRecord record = slots[index].record;
            if (record == null || monsters == null ||
                !monsters.SetFarmRegistration(record.uniqueId, false))
                return;

            Refresh();
            FindFirstObjectByType<SaveManager>()?.SaveGame();
        }

        private void OpenDetail(int index)
        {
            if (dirty || monsters == null || index >= monsters.FarmMonsters.Count)
                return;
            if (detail == null) detail = GetComponentInChildren<OwnedMonsterDetailUI>(true);
            if (detail == null) detail = Instantiate(detailPrefab, transform, false);

            detail.transform.SetAsLastSibling();
            detail.Show(monsters.FarmMonsters[index], catalog);
        }
    }
}
