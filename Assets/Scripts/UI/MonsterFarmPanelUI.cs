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
            [NonSerialized] public Button favoriteButton, lockButton;
            [NonSerialized] public TMP_Text favoriteLabel, lockLabel;
        }

        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private OwnedMonsterDetailUI detailPrefab;
        [SerializeField] private Slot[] slots;

        private MonsterManager monsters;
        private MonsterCatalogSO catalog;
        private OwnedMonsterDetailUI detail;
        private bool dirty;

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].button.onClick.AddListener(() => OpenDetail(index));
                Slot slot = slots[i];
                slot.removeButton.onClick.AddListener(() => RemoveFromFarm(index));
                slot.favoriteButton = CreateProtectionButton(slot, "Favorite", "☆", .02f, .30f,
                    () => ToggleProtection(slot, false), out slot.favoriteLabel);
                slot.lockButton = CreateProtectionButton(slot, "Lock", "ロック", .32f, .60f,
                    () => ToggleProtection(slot, true), out slot.lockLabel);
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
        }

        private void Refresh()
        {
            dirty = false;
            int count = monsters != null ? monsters.FarmMonsters.Count : 0;
            countText.text = $"{count} / {MonsterManager.MaxFarmSize}体";
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
                    ? $"{record.farmStartLevel}Lv → {record.level}Lv" +
                      (record.level >= BreedingManager.MinimumParentLevel
                          ? $"\n<color=#9AF4A3>Lv.{BreedingManager.MinimumParentLevel}到達</color>" : "")
                    : "空き";
                slot.button.interactable = record != null;
            }
        }

        private static Button CreateProtectionButton(Slot slot, string name, string label,
            float left, float right, Action click, out TMP_Text text)
        {
            Button button = InventoryUiFactory.Button(slot.button.transform, name, label,
                Vector2.zero, Vector2.zero);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, .66f);
            rect.anchorMax = new Vector2(right, .98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            text = button.GetComponentInChildren<TMP_Text>();
            text.font = slot.levels.font;
            button.onClick.AddListener(() => click());
            return button;
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
            if (detail == null)
                detail = Instantiate(detailPrefab, transform, false);

            detail.transform.SetAsLastSibling();
            detail.Show(monsters.FarmMonsters[index], catalog);
        }
    }
}
