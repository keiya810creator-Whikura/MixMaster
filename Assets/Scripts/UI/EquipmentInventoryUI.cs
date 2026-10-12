using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class EquipmentInventoryUI : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button closeButton;

        private EquipmentManager manager;
        private TMP_Dropdown sortDropdown;
        private TMP_Dropdown statFilter;
        private TMP_Dropdown rarityFilter;
        private Button directionButton;
        private TMP_Text directionLabel;
        private bool ascending;
        private bool subscribed;
        private string inspectedId;
        private OwnedMonsterRecord targetMonster;
        private PlayerManager playerManager;
        private EquipmentComparisonPanelUI comparisonPanel;
        private TMP_Text ownerHeading;
        private Button[] equipSlotButtons = new Button[3];
        private Button unequipButton;
        private TMP_Text[] slotButtonTexts = new TMP_Text[3];

        private readonly List<GameObject> activeRows = new List<GameObject>();

        private static readonly string[] SortChoices = BuildSortChoices();
        private static readonly string[] StatChoices = InventoryStatFilter.Labels;
        private static readonly string[] RarityChoices =
        {
            "全レア度", "ノーマル", "レア", "スーパーレア",
            "エピックレア", "レジェンダリーレア"
        };

        public void SetReferences(RectTransform content,
            TMP_Text count, TMP_Text detail, Button close)
        {
            contentRoot = content;
            countText = count;
            detailText = detail;
            closeButton = close;
        }

        private void Awake()
        {
            // Inventory may be opened from a monster panel on a different
            // Canvas. Give it a top-level sorting layer so it is never
            // hidden behind the previous screen.
            Canvas overlay = GetComponent<Canvas>();
            if (overlay == null)
                overlay = gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 500;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            EnsureControls();
            EnsureEquipmentActions();
            EnsureComparisonPanel();
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            BindManager();
            Rebuild();
        }

        private void OnDisable()
        {
            if (comparisonPanel != null)
                comparisonPanel.Close();
            if (subscribed && manager != null)
                manager.InventoryChanged -= Rebuild;
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            targetMonster = null;
            inspectedId = null;
            OpenPanel();
        }

        public void OpenForMonster(OwnedMonsterRecord monster)
        {
            if (monster == null) return;
            targetMonster = monster;
            inspectedId = null;
            OpenPanel();
        }

        private void OpenPanel()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            else
            {
                BindManager();
                Rebuild();
            }
            transform.SetAsLastSibling();
            RefreshActionControls();
        }

        public void Close()
        {
            if (comparisonPanel != null)
                comparisonPanel.Close();
            gameObject.SetActive(false);
        }

        public void SortBy(int selected)
        {
            if (sortDropdown != null)
                sortDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, SortChoices.Length - 1));
            Rebuild();
        }

        public void FilterByStat(int selected)
        {
            if (statFilter != null)
                statFilter.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, StatChoices.Length - 1));
            Rebuild();
        }

        public void FilterByRarity(int selected)
        {
            if (rarityFilter != null)
                rarityFilter.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, RarityChoices.Length - 1));
            Rebuild();
        }

        private void BindManager()
        {
            if (manager == null)
                manager = FindFirstObjectByType<EquipmentManager>();
            if (manager == null || subscribed)
                return;

            manager.InventoryChanged += Rebuild;
            subscribed = true;
        }

        public void Rebuild()
        {
            if (manager == null) BindManager();

            foreach (GameObject row in activeRows)
                if (row != null) Destroy(row);
            activeRows.Clear();

            if (manager == null || contentRoot == null)
                return;

            RefreshActionControls();
            IReadOnlyList<EquipmentRecord> source = manager.Inventory;
            var matches = new List<int>();
            int statIndex = statFilter != null ? statFilter.value : 0;
            int rarityIndex = rarityFilter != null ? rarityFilter.value : 0;
            int sortIndex = sortDropdown != null ? sortDropdown.value : 0;

            for (int i = 0; i < source.Count; i++)
            {
                EquipmentRecord record = source[i];
                if (record == null) continue;

                if (rarityIndex != 0 &&
                    (int)EquipmentRarityUtility.GetRarity(record.level) !=
                    rarityIndex - 1)
                    continue;

                if (statIndex != 0)
                {
                    EquipmentSO definition =
                        manager.GetEquipmentDefinition(record.equipmentId);
                    if (definition == null ||
                        Math.Abs(InventoryStatFilter.GetValue(
                            definition.modifiers, (InventoryStat)statIndex)) < 0.000001d)
                        continue;
                }

                matches.Add(i);
            }

            matches.Sort((a, b) =>
            {
                EquipmentRecord left = source[a], right = source[b];
                int compare;
                if (sortIndex == 0)
                    compare = b.CompareTo(a); // newest first
                else if (sortIndex == 1)
                    compare = right.level.CompareTo(left.level);
                else
                {
                    InventoryStat stat = (InventoryStat)(sortIndex - 1);
                    EquipmentSO first =
                        manager.GetEquipmentDefinition(left.equipmentId);
                    EquipmentSO second =
                        manager.GetEquipmentDefinition(right.equipmentId);

                    double x = first != null
                        ? InventoryStatFilter.GetValue(manager.GetScaledModifiers(left), stat) : 0d;
                    double y = second != null
                        ? InventoryStatFilter.GetValue(manager.GetScaledModifiers(right), stat) : 0d;
                    compare = y.CompareTo(x);
                }

                if (compare == 0)
                    compare = b.CompareTo(a);
                return ascending ? -compare : compare;
            });

            if (countText != null)
                countText.text = "装備 " + matches.Count.ToString("N0") +
                    " / " + source.Count.ToString("N0");

            foreach (int index in matches)
                BuildEntry(source[index], index);

            if (matches.Count == 0 && detailText != null)
                detailText.text = "条件に一致する装備がありません。";
        }

        private void BuildEntry(EquipmentRecord record, int index)
        {
            EquipmentSO definition =
                manager.GetEquipmentDefinition(record.equipmentId);
            GameObject row = new GameObject(
                "Equipment_" + record.uniqueId, typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(contentRoot, false);
            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = 120f;
            layout.minHeight = 120f;

            Image bg = row.GetComponent<Image>();
            bg.color = RarityColor(EquipmentRarityUtility.GetRarity(record.level));
            Button button = row.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => Inspect(record));

            if (definition != null && definition.icon != null)
            {
                GameObject icon = new GameObject(
                    "Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(row.transform, false);
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                InventoryUiFactory.TopLeft(iconRect,
                    new Vector2(10f, -17f), new Vector2(84f, 84f));
                Image iconImage = icon.GetComponent<Image>();
                iconImage.sprite = definition.icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            string name = definition != null &&
                !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName : record.equipmentId;

            var rarity = EquipmentRarityUtility.GetRarity(record.level);
            string label = name + "   Lv." + record.level +
                " 《" + EquipmentRarityUtility.Labels[(int)rarity] + "》" +
                "\n" + (definition != null
                    ? InventoryStatFilter.FormatModifiers(manager.GetScaledModifiers(record))
                    : "装備定義が見つかりません") +
                (manager.GetEquippedOwner(record.uniqueId).Length > 0 ? "  [装備中]" : "");
            TMP_Text text = InventoryUiFactory.Text(
                row.transform, "Summary", label,
                new Vector2(112f, -10f), new Vector2(790f, 104f), 23f);
            text.enableWordWrapping = true;

            activeRows.Add(row);
        }

        private void Inspect(EquipmentRecord record)
        {
            inspectedId = record.uniqueId;
            EquipmentSO definition =
                manager.GetEquipmentDefinition(record.equipmentId);
            string name = definition != null
                ? definition.displayName : record.equipmentId;
            var rarity = EquipmentRarityUtility.GetRarity(record.level);
            if (detailText != null)
            {
                string owner = manager.GetEquippedOwner(record.uniqueId);
                detailText.text = name + "　Lv." + record.level +
                    "　[" + EquipmentRarityUtility.Labels[(int)rarity] + "]" +
                    "\n" + (definition != null
                        ? InventoryStatFilter.FormatModifiers(
                            manager.GetScaledModifiers(record)) +
                            "\n" + definition.description
                        : "装備定義が未登録です。") +
                    (string.IsNullOrEmpty(owner) ? "" :
                        "\n装備者: " + (owner == "プレイヤー" ? owner : "仲間"));
            }
            RefreshActionControls();
        }

        private IReadOnlyList<string> CurrentEquipmentIds =>
            targetMonster == null
                ? manager.PlayerEquipmentUniqueIds
                : (IReadOnlyList<string>)(targetMonster.equippedItemUniqueIds
                    ?? new List<string>());

        private void EnsureEquipmentActions()
        {
            if (unequipButton != null) return;

            ownerHeading = InventoryUiFactory.Text(transform,
                "CurrentOwner", "装備先: プレイヤー",
                new Vector2(470f, -83f), new Vector2(440f, 36f), 23f);

            TMP_Text compareHint = InventoryUiFactory.Text(transform,
                "ComparisonHint",
                "装備を選択 → 1～3枠を押して比較 → 確定",
                new Vector2(25f, -225f), new Vector2(850f, 28f), 20f);
            compareHint.color = new Color32(184, 184, 184, 255);

            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                Button button = InventoryUiFactory.Button(transform,
                    "EquipSlot" + (i + 1),
                    (i + 1) + "枠に装備",
                    new Vector2(23f + i * 180f, 18f),
                    new Vector2(170f, 53f));
                RectTransform rt = button.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(23f + i * 180f, 13f);
                equipSlotButtons[i] = button;
                slotButtonTexts[i] = button.GetComponentInChildren<TMP_Text>();
                button.onClick.AddListener(() => PreviewEquip(slot));
            }

            unequipButton = InventoryUiFactory.Button(transform,
                "UnequipButton", "装備解除",
                new Vector2(566f, 18f), new Vector2(170f, 53f));
            RectTransform un = unequipButton.GetComponent<RectTransform>();
            un.anchorMin = Vector2.zero;
            un.anchorMax = Vector2.zero;
            un.pivot = Vector2.zero;
            un.anchoredPosition = new Vector2(566f, 13f);
            unequipButton.onClick.AddListener(PreviewUnequip);

            // Keep the selected-item text visible above the action buttons.
            if (detailText != null)
            {
                RectTransform rt = detailText.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(28f, 75f);
                rt.sizeDelta = new Vector2(-56f, 125f);
            }
        }

        private void RefreshActionControls()
        {
            if (manager == null) return;
            IReadOnlyList<string> ids = CurrentEquipmentIds;
            if (ownerHeading != null)
            {
                ownerHeading.text = targetMonster == null
                    ? "装備先: プレイヤー"
                    : "装備先: 仲間  " + targetMonster.monsterId;
            }

            EquipmentRecord selected = manager.FindEquipment(inspectedId);
            string owner = selected != null
                ? manager.GetEquippedOwner(selected.uniqueId) : "";
            string targetOwner = targetMonster == null
                ? "プレイヤー" : targetMonster.uniqueId;
            bool equipAllowed = selected != null &&
                (owner == "" || owner == targetOwner);

            for (int i = 0; i < equipSlotButtons.Length; i++)
            {
                if (equipSlotButtons[i] == null) continue;
                equipSlotButtons[i].interactable = equipAllowed;
                string itemName = i < ids.Count ?
                    GetDisplayName(manager.FindEquipment(ids[i])) : "空き";
                if (slotButtonTexts[i] != null)
                    slotButtonTexts[i].text = (i + 1) + "枠: " + itemName;
            }

            if (unequipButton != null)
                unequipButton.interactable =
                    selected != null && owner == targetOwner;
        }

        private string GetDisplayName(EquipmentRecord record)
        {
            if (record == null) return "空き";
            EquipmentSO def = manager.GetEquipmentDefinition(record.equipmentId);
            return def != null && !string.IsNullOrEmpty(def.displayName)
                ? def.displayName : record.equipmentId;
        }

        private void EnsureComparisonPanel()
        {
            if (comparisonPanel != null)
                return;

            comparisonPanel = GetComponentInChildren<EquipmentComparisonPanelUI>(
                true);
            if (comparisonPanel == null)
                comparisonPanel = EquipmentComparisonPanelUI.Create(transform);
        }

        // Called by each of the three equip-slot buttons.
        public void PreviewEquip(int slot)
        {
            OpenComparison(slot, false);
        }

        // Called by the unequip button. Removing equipment also shows
        // resulting final stats before actually changing the loadout.
        public void PreviewUnequip()
        {
            if (manager == null || string.IsNullOrWhiteSpace(inspectedId))
                return;
            int slot = -1;
            IReadOnlyList<string> equipped = CurrentEquipmentIds;
            if (equipped != null)
            {
                for (int i = 0; i < equipped.Count; i++)
                    if (equipped[i] == inspectedId)
                    {
                        slot = i;
                        break;
                    }
            }

            if (slot >= 0)
                OpenComparison(slot, true);
        }

        private void OpenComparison(int slot, bool unequip)
        {
            if (manager == null || string.IsNullOrWhiteSpace(inspectedId))
                return;

            EnsureComparisonPanel();
            if (playerManager == null)
                playerManager = FindFirstObjectByType<PlayerManager>();

            string id = inspectedId;
            OwnedMonsterRecord owner = targetMonster;
            if (!EquipmentComparisonCalculator.TryCalculate(
                    manager, playerManager, owner, id, slot, unequip,
                    out EquipmentComparisonCalculator.Comparison preview,
                    out string error))
            {
                if (detailText != null)
                    detailText.text = error;
                return;
            }

            comparisonPanel.Show(preview, () =>
            {
                // No real changes are made while comparison is displayed.
                // If an external event changed gear during the preview,
                // require the user to confirm the updated figures again.
                if (!EquipmentComparisonCalculator.TryCalculate(
                        manager, playerManager, owner, id, slot, unequip,
                        out EquipmentComparisonCalculator.Comparison latest,
                        out string latestError))
                {
                    if (detailText != null)
                        detailText.text = latestError;
                    return;
                }

                if (!SameComparison(preview, latest))
                {
                    comparisonPanel.Show(latest, () =>
                        ApplyComparisonIfValid(id, slot, owner, unequip, latest));
                    return;
                }

                ApplyComparisonIfValid(id, slot, owner, unequip, latest);
            });
        }

        private static bool SameComparison(
            EquipmentComparisonCalculator.Comparison a,
            EquipmentComparisonCalculator.Comparison b)
        {
            if (a.previousItem != b.previousItem ||
                a.newItem != b.newItem ||
                a.slot != b.slot || a.isUnequip != b.isUnequip)
                return false;

            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
            {
                InventoryStat stat = (InventoryStat)i;
                if (Math.Abs(InventoryStatFilter.GetValue(a.before, stat) -
                    InventoryStatFilter.GetValue(b.before, stat)) > 0.00001d ||
                    Math.Abs(InventoryStatFilter.GetValue(a.after, stat) -
                    InventoryStatFilter.GetValue(b.after, stat)) > 0.00001d)
                    return false;
            }
            return true;
        }

        private void ApplyComparisonIfValid(string id, int slot,
            OwnedMonsterRecord owner, bool unequip,
            EquipmentComparisonCalculator.Comparison confirmed)
        {
            if (!EquipmentComparisonCalculator.TryCalculate(
                    manager, playerManager, owner, id, slot, unequip,
                    out EquipmentComparisonCalculator.Comparison current,
                    out string error))
            {
                if (detailText != null) detailText.text = error;
                return;
            }

            if (!SameComparison(confirmed, current))
            {
                comparisonPanel.Show(current, () =>
                    ApplyComparisonIfValid(id, slot, owner, unequip, current));
                return;
            }

            bool success = unequip
                ? (owner == null
                    ? manager.UnequipFromPlayer(id)
                    : manager.UnequipFromMonster(owner, id))
                : (owner == null
                    ? manager.EquipToPlayer(id, slot)
                    : manager.EquipToMonster(owner, id, slot));

            if (!success)
            {
                if (detailText != null)
                    detailText.text = "装備を変更できません。装備状況を確認してください。";
            }
            else
            {
                EquipmentRecord selected = manager.FindEquipment(id);
                if (selected != null)
                    Inspect(selected);
            }

            RefreshActionControls();
        }

        private void EnsureControls()
        {
            Transform parent = transform.Find("FilterControls");
            if (parent == null)
            {
                GameObject area = new GameObject(
                    "FilterControls", typeof(RectTransform));
                area.transform.SetParent(transform, false);
                RectTransform rect = area.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -110f);
                rect.sizeDelta = new Vector2(0f, 122f);
                parent = rect;
            }

            var control = (RectTransform)parent;
            sortDropdown = InventoryUiFactory.Dropdown(control,
                "SortDropdown", new Vector2(20f, -6f),
                new Vector2(240f, 54f), SortChoices);
            statFilter = InventoryUiFactory.Dropdown(control,
                "StatFilter", new Vector2(270f, -6f),
                new Vector2(245f, 54f), StatChoices);
            rarityFilter = InventoryUiFactory.Dropdown(control,
                "RarityFilter", new Vector2(525f, -6f),
                new Vector2(240f, 54f), RarityChoices);
            directionButton = InventoryUiFactory.Button(control,
                "Direction", "降順", new Vector2(775f, -6f),
                new Vector2(135f, 54f));
            directionLabel = directionButton.GetComponentInChildren<TMP_Text>();

            InventoryUiFactory.Text(control, "SortHint", "並び替え",
                new Vector2(24f, -70f), new Vector2(230f, 28f), 18f);
            InventoryUiFactory.Text(control, "StatHint", "ステータス絞り込み",
                new Vector2(273f, -70f), new Vector2(270f, 28f), 18f);
            InventoryUiFactory.Text(control, "RarityHint", "レア度絞り込み",
                new Vector2(528f, -70f), new Vector2(240f, 28f), 18f);

            sortDropdown.onValueChanged.AddListener(SortBy);
            statFilter.onValueChanged.AddListener(FilterByStat);
            rarityFilter.onValueChanged.AddListener(FilterByRarity);
            directionButton.onClick.AddListener(() =>
            {
                ascending = !ascending;
                if (directionLabel != null)
                    directionLabel.text = ascending ? "昇順" : "降順";
                Rebuild();
            });
        }

        private static string[] BuildSortChoices()
        {
            var options = new List<string> { "入手順", "レベル" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                options.Add(InventoryStatFilter.Labels[i]);
            return options.ToArray();
        }

        private static Color RarityColor(EquipmentRarity rarity)
        {
            switch (rarity)
            {
                case EquipmentRarity.Rare:
                    return new Color(0.13f, 0.27f, 0.43f, 1f);
                case EquipmentRarity.SuperRare:
                    return new Color(0.20f, 0.29f, 0.52f, 1f);
                case EquipmentRarity.EpicRare:
                    return new Color(0.36f, 0.20f, 0.48f, 1f);
                case EquipmentRarity.LegendaryRare:
                    return new Color(0.47f, 0.31f, 0.11f, 1f);
                default:
                    return new Color(0.15f, 0.19f, 0.25f, 1f);
            }
        }
    }
}
