using System;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Player;
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
        [Header("Protection Icons")]
        [SerializeField] private Sprite favoriteSprite;
        [SerializeField] private Sprite lockSprite;

        private EquipmentManager manager;
        private MonsterManager ownerMonsters;
        private MonsterCatalogSO ownerCatalog;
        private SpriteRenderer playerSpriteRenderer;
        private TMP_Dropdown sortDropdown;
        private TMP_Dropdown statFilter;
        private TMP_Dropdown rarityFilter;
        private Button bulkSellButton;
        private Button autoSellSettingsButton;
        private InventoryAutoSellDialog autoSellDialog;
        private Button favoriteButton;
        private Button lockButton;
        private Button sellButton;
        private Button enhanceButton;
        private EquipmentEnhancementPanelUI enhancementPanel;
        public bool EnhancementAllowed { get; private set; }
        public event Action Closed;
        private TMP_Text favoriteLabel;
        private TMP_Text lockLabel;
        private TMP_Text sellLabel;
        private InventoryBulkSaleDialog saleDialog;
        private readonly List<string> filteredIds = new List<string>();
        private const int PageSize = 30;
        private int pageIndex;
        private Button previousPageButton;
        private Button nextPageButton;
        private TMP_Text pageText;
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
        private Button unequipAllButton;
        private TMP_Text[] slotButtonTexts = new TMP_Text[3];

        private readonly List<GameObject> activeRows = new List<GameObject>();

        private static readonly string[] SortChoices = BuildSortChoices();
        private static readonly string[] StatChoices = InventoryStatFilter.Labels;
        private static readonly string[] RarityChoices = EquipmentRarityUtility.FilterLabels;

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
            EnsureSaleActions();
            EnsurePagingControls();
            EnsureComparisonPanel();
            saleDialog = InventoryBulkSaleDialog.Ensure(SafeAreaLayout.ContentRoot(transform));
            autoSellDialog = InventoryAutoSellDialog.Ensure(SafeAreaLayout.ContentRoot(transform));
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
            SetEnhancementAccess(false);
            if (comparisonPanel != null)
                comparisonPanel.Close();
            if (saleDialog != null)
                saleDialog.Close();
            if (autoSellDialog != null)
                autoSellDialog.Close();
            if (enhancementPanel != null)
                enhancementPanel.Close();
            if (subscribed && manager != null)
                manager.InventoryChanged -= Rebuild;
            subscribed = false;
            Closed?.Invoke();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            OpenInventory(false, null);
        }

        public void OpenForMonster(OwnedMonsterRecord monster)
        {
            if (monster == null) return;
            OpenInventory(false, monster);
        }

        public void OpenForBlacksmith()
        {
            OpenInventory(true, null);
        }

        private void OpenInventory(bool allowEnhancement, OwnedMonsterRecord monster)
        {
            // End the previous building session before changing its mode.
            if (gameObject.activeSelf && EnhancementAllowed != allowEnhancement)
                Close();
            SetEnhancementAccess(allowEnhancement);
            targetMonster = monster;
            inspectedId = null;
            OpenPanel();
        }

        private void SetEnhancementAccess(bool allowed)
        {
            EnhancementAllowed = allowed;
            if (!allowed && enhancementPanel != null) enhancementPanel.Close();
            if (enhanceButton != null)
            {
                enhanceButton.gameObject.SetActive(allowed);
                enhanceButton.interactable = allowed && manager != null &&
                    manager.FindEquipment(inspectedId) != null;
            }
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
            SetEnhancementAccess(false);
            if (comparisonPanel != null)
                comparisonPanel.Close();
            if (saleDialog != null)
                saleDialog.Close();
            if (autoSellDialog != null)
                autoSellDialog.Close();
            if (enhancementPanel != null)
                enhancementPanel.Close();
            gameObject.SetActive(false);
        }

        public void SortBy(int selected)
        {
            if (sortDropdown != null)
                sortDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, SortChoices.Length - 1));
            pageIndex = 0;
            Rebuild();
        }

        public void FilterByStat(int selected)
        {
            if (statFilter != null)
                statFilter.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0,
                        InventoryProtectionIcons.CombinedStatFilterLabels.Length - 1));
            pageIndex = 0;
            Rebuild();
        }

        public void FilterByRarity(int selected)
        {
            if (rarityFilter != null)
                rarityFilter.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, RarityChoices.Length - 1));
            pageIndex = 0;
            Rebuild();
        }

        private void BindManager()
        {
            if (manager == null)
                manager = FindFirstObjectByType<EquipmentManager>();
            if (ownerMonsters == null)
                ownerMonsters = FindFirstObjectByType<MonsterManager>();
            if (ownerCatalog == null)
                ownerCatalog = MonsterCatalogSO.Load();
            if (playerSpriteRenderer == null)
            {
                PlayerController player =
                    FindFirstObjectByType<PlayerController>();
                if (player != null)
                    playerSpriteRenderer = player.GetComponent<SpriteRenderer>();
            }
            if (manager == null || subscribed)
                return;

            manager.InventoryChanged += Rebuild;
            subscribed = true;
        }

        public void Rebuild()
        {
            if (manager == null) BindManager();

            // Preserve the 30 pooled row views between all rebuilds.
            filteredIds.Clear();

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
                if (record == null ||
                    !InventoryProtectionIcons.MatchesCombinedStatFilter(
                        statIndex, record.isFavorite, record.isLocked))
                    continue;

                if (rarityIndex != 0 &&
                    (int)EquipmentRarityUtility.GetRarity(record.level) !=
                    rarityIndex - 1)
                    continue;

                if (statIndex != 0 &&
                    statIndex < InventoryStatFilter.Labels.Length)
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
                else if (sortIndex == InventoryStatFilter.Labels.Length + 1)
                    compare = right.isFavorite.CompareTo(left.isFavorite);
                else if (sortIndex == InventoryStatFilter.Labels.Length + 2)
                    compare = right.isLocked.CompareTo(left.isLocked);
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
                filteredIds.Add(source[index].uniqueId);

            pageIndex = Mathf.Clamp(pageIndex, 0, Math.Max(0,
                (filteredIds.Count + PageSize - 1) / PageSize - 1));
            RenderPage();
            EquipmentRecord inspected = manager.FindEquipment(inspectedId);
            if (inspected != null) Inspect(inspected);
            else if (matches.Count == 0 && detailText != null)
                detailText.text = "条件に一致する装備がありません。";
        }

        private GameObject CreateRow()
        {
            GameObject row = new GameObject("EquipmentRow",
                typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(contentRoot, false);
            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = 120f;
            layout.minHeight = 120f;

            GameObject icon = new GameObject("Icon",
                typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(row.transform, false);
            InventoryUiFactory.TopLeft(icon.GetComponent<RectTransform>(),
                new Vector2(10f, -17f), new Vector2(84f, 84f));
            Image iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            TMP_Text summary = InventoryUiFactory.Text(
                row.transform, "Summary", "",
                new Vector2(112f, -10f), new Vector2(625f, 104f), 23f);
            // Leave a fixed-width portrait area at the right. Use anchors
            // so existing prefab viewport widths also lay out correctly.
            RectTransform summaryRect = summary.rectTransform;
            summaryRect.anchorMin = new Vector2(0f, 0f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.pivot = new Vector2(0f, 0.5f);
            summaryRect.offsetMin = new Vector2(112f, 8f);
            summaryRect.offsetMax = new Vector2(-147f, -8f);
            summary.enableWordWrapping = true;

            GameObject owner = new GameObject("EquippedOwner",
                typeof(RectTransform), typeof(Image));
            owner.transform.SetParent(row.transform, false);
            RectTransform ownerRect = owner.GetComponent<RectTransform>();
            ownerRect.anchorMin = new Vector2(1f, 0.5f);
            ownerRect.anchorMax = ownerRect.anchorMin;
            ownerRect.pivot = new Vector2(1f, 0.5f);
            ownerRect.anchoredPosition = new Vector2(-8f, 0f);
            ownerRect.sizeDelta = new Vector2(132f, 112f);
            Image ownerBackground = owner.GetComponent<Image>();
            ownerBackground.color = new Color(0.05f, 0.08f, 0.12f, 0.48f);
            ownerBackground.raycastTarget = false;

            GameObject portrait = new GameObject("Portrait",
                typeof(RectTransform), typeof(Image));
            portrait.transform.SetParent(owner.transform, false);
            RectTransform portraitRect =
                portrait.GetComponent<RectTransform>();
            InventoryUiFactory.TopLeft(portraitRect,
                new Vector2(33f, -3f), new Vector2(66f, 69f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            TMP_Text ownerLabel = InventoryUiFactory.Text(
                owner.transform, "OwnerLabel", "",
                new Vector2(4f, -76f), new Vector2(124f, 33f), 19f);
            ownerLabel.alignment = TextAlignmentOptions.Center;
            ownerLabel.enableWordWrapping = true;
            ownerLabel.raycastTarget = false;
            owner.SetActive(false);
            return row;
        }

        private void BindEntry(GameObject row, EquipmentRecord record)
        {
            row.name = "Equipment_" + record.uniqueId;
            EquipmentSO definition =
                manager.GetEquipmentDefinition(record.equipmentId);
            Image background = row.GetComponent<Image>();
            background.color = EquipmentRarityUtility.BackgroundColor(
                EquipmentRarityUtility.GetRarity(record.level));

            Button button = row.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Inspect(record));

            Image icon = row.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = definition != null ? definition.icon : null;
            icon.enabled = icon.sprite != null;

            string name = definition != null &&
                !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName : record.equipmentId;
            TMP_Text summary =
                row.transform.Find("Summary").GetComponent<TMP_Text>();
            string equippedOwner =
                manager.GetEquippedOwner(record.uniqueId);
            summary.text = EquipmentRarityUtility.FormatInventoryName(name, record) + "\n" +
                (definition != null
                    ? InventoryStatFilter.FormatModifiers(
                        manager.GetScaledModifiers(record))
                    : "装備定義が見つかりません") +
                (!string.IsNullOrEmpty(equippedOwner)
                    ? "  [装備中]" : "");

            BindOwnerPortrait(row, equippedOwner);
            InventoryProtectionIcons.Set(row.transform,
                record.isFavorite, record.isLocked,
                favoriteSprite, lockSprite,
                new Vector2(10f, -10f), new Vector2(53f, -10f));
        }

        /// <summary>
        /// A row is pooled and rebound as the player flips pages. Always
        /// clear the old owner icon/label before showing a new equipment
        /// record, otherwise a previous item's owner can linger.
        /// </summary>
        private void BindOwnerPortrait(GameObject row, string ownerUniqueId)
        {
            Transform owner = row.transform.Find("EquippedOwner");
            if (owner == null) return;

            bool equipped = !string.IsNullOrWhiteSpace(ownerUniqueId);
            owner.gameObject.SetActive(equipped);
            if (!equipped) return;

            Image portrait = owner.Find("Portrait").GetComponent<Image>();
            TMP_Text label = owner.Find("OwnerLabel").GetComponent<TMP_Text>();
            Sprite image = null;
            string caption;

            if (ownerUniqueId == "プレイヤー")
            {
                if (playerSpriteRenderer == null)
                {
                    PlayerController player =
                        FindFirstObjectByType<PlayerController>();
                    if (player != null)
                        playerSpriteRenderer =
                            player.GetComponent<SpriteRenderer>();
                }
                image = playerSpriteRenderer != null
                    ? playerSpriteRenderer.sprite : null;
                caption = "主人公";
            }
            else
            {
                if (ownerMonsters == null)
                    ownerMonsters = FindFirstObjectByType<MonsterManager>();
                if (ownerCatalog == null)
                    ownerCatalog = MonsterCatalogSO.Load();

                OwnedMonsterRecord monster = ownerMonsters != null
                    ? ownerMonsters.FindOwnedMonster(ownerUniqueId) : null;
                MonsterSO definition = monster != null &&
                    ownerCatalog != null
                    ? ownerCatalog.GetMonster(monster.monsterId) : null;

                image = definition != null
                    ? (definition.icon != null
                        ? definition.icon : definition.sprite) : null;

                string species = definition != null &&
                    !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : monster != null ? monster.monsterId : "仲間";
                // A species portrait alone cannot distinguish two owned
                // individuals of that species. Include an ID suffix.
                string tail = ownerUniqueId.Length > 4
                    ? ownerUniqueId.Substring(ownerUniqueId.Length - 4)
                    : ownerUniqueId;
                caption = species + "\nLv." +
                    (monster != null ? monster.level.ToString() : "?") +
                    " #" + tail;
            }

            portrait.sprite = image;
            portrait.enabled = image != null;
            label.text = caption;
        }

        private void RenderPage()
        {
            if (manager == null || contentRoot == null)
                return;

            int start = pageIndex * PageSize;
            int visible = Mathf.Min(PageSize,
                Mathf.Max(0, filteredIds.Count - start));

            // Maximum 30 entry objects even when thousands are owned.
            while (activeRows.Count < visible)
                activeRows.Add(CreateRow());

            for (int i = 0; i < activeRows.Count; i++)
            {
                GameObject row = activeRows[i];
                bool active = i < visible;
                if (row.activeSelf != active)
                    row.SetActive(active);
                if (!active) continue;

                EquipmentRecord record = manager.FindEquipment(
                    filteredIds[start + i]);
                if (record != null) BindEntry(row, record);
            }

            if (countText != null)
                countText.text = "装備 " + filteredIds.Count.ToString("N0") +
                    " / " + manager.Inventory.Count.ToString("N0");
            RefreshActionControls();
            RefreshBulkSellButton();
            RefreshSaleActions();
            RefreshPageControls();

            ScrollRect scroll =
                contentRoot.GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        public void NextPage()
        {
            if ((pageIndex + 1) * PageSize >= filteredIds.Count)
                return;
            pageIndex++;
            RenderPage();
        }

        public void PreviousPage()
        {
            if (pageIndex <= 0) return;
            pageIndex--;
            RenderPage();
        }

        private void Inspect(EquipmentRecord record)
        {
            inspectedId = record.uniqueId;
            EquipmentSO definition =
                manager.GetEquipmentDefinition(record.equipmentId);
            string name = definition != null
                ? definition.displayName : record.equipmentId;
            if (detailText != null)
            {
                string owner = manager.GetEquippedOwner(record.uniqueId);
                detailText.text = EquipmentRarityUtility.FormatInventoryName(name, record) +
                    "\n" + (definition != null
                        ? InventoryStatFilter.FormatModifiers(
                            manager.GetScaledModifiers(record)) +
                            "\n" + definition.description
                        : "装備定義が未登録です。") +
                    (string.IsNullOrEmpty(owner) ? "" :
                        "\n装備者: " + GetOwnerDisplayName(owner));
            }
            RefreshActionControls();
            RefreshSaleActions();
        }

        private string GetOwnerDisplayName(string ownerUniqueId)
        {
            if (ownerUniqueId == "プレイヤー") return "主人公";
            if (ownerMonsters == null)
                ownerMonsters = FindFirstObjectByType<MonsterManager>();
            if (ownerCatalog == null)
                ownerCatalog = MonsterCatalogSO.Load();

            OwnedMonsterRecord record = ownerMonsters != null
                ? ownerMonsters.FindOwnedMonster(ownerUniqueId) : null;
            MonsterSO monster = record != null && ownerCatalog != null
                ? ownerCatalog.GetMonster(record.monsterId) : null;
            string name = monster != null &&
                !string.IsNullOrWhiteSpace(monster.displayName)
                ? monster.displayName
                : record != null ? record.monsterId : "仲間";
            string suffix = ownerUniqueId.Length > 4
                ? ownerUniqueId.Substring(ownerUniqueId.Length - 4)
                : ownerUniqueId;
            return name + (record != null
                ? " Lv." + record.level : "") + " #" + suffix;
        }

        private IReadOnlyList<string> CurrentEquipmentIds =>
            targetMonster == null
                ? manager.PlayerEquipmentUniqueIds
                : (IReadOnlyList<string>)(targetMonster.equippedItemUniqueIds
                    ?? new List<string>());

        private void EnsureEquipmentActions()
        {
            if (unequipButton != null) return;

            ownerHeading = InventoryUiFactory.Text(SafeAreaLayout.ContentRoot(transform),
                "CurrentOwner", "装備先: プレイヤー",
                new Vector2(470f, -83f), new Vector2(440f, 36f), 23f);

            TMP_Text compareHint = InventoryUiFactory.Text(SafeAreaLayout.ContentRoot(transform),
                "ComparisonHint",
                "装備：比較して確定 ／ 解除：すぐに反映",
                new Vector2(25f, -308f), new Vector2(850f, 28f), 20f);
            compareHint.color = new Color32(184, 184, 184, 255);

            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                Button button = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
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

            unequipButton = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
                "UnequipButton", "装備解除",
                new Vector2(566f, 18f), new Vector2(170f, 53f));
            RectTransform un = unequipButton.GetComponent<RectTransform>();
            un.anchorMin = Vector2.zero;
            un.anchorMax = Vector2.zero;
            un.pivot = Vector2.zero;
            un.anchoredPosition = new Vector2(566f, 13f);
            unequipButton.onClick.AddListener(UnequipSelected);

            unequipAllButton = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
                "UnequipAllButton", "装備全解除",
                new Vector2(745f, 18f), new Vector2(170f, 53f));
            RectTransform allRect = unequipAllButton.GetComponent<RectTransform>();
            allRect.anchorMin = Vector2.zero;
            allRect.anchorMax = Vector2.zero;
            allRect.pivot = Vector2.zero;
            allRect.anchoredPosition = new Vector2(745f, 13f);
            unequipAllButton.GetComponent<Image>().color =
                new Color(0.43f, 0.22f, 0.23f, 1f);
            unequipAllButton.onClick.AddListener(UnequipAll);

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

            if (unequipAllButton != null)
            {
                bool hasEquippedItem = false;
                if (ids != null)
                {
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(ids[i]))
                        {
                            hasEquippedItem = true;
                            break;
                        }
                    }
                }
                unequipAllButton.interactable = hasEquippedItem;
            }
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
                comparisonPanel = EquipmentComparisonPanelUI.Create(SafeAreaLayout.ContentRoot(transform));
        }

        // Called by each of the three equip-slot buttons.
        public void PreviewEquip(int slot)
        {
            OpenComparison(slot, false);
        }

        // Unequip actions apply immediately; only equipping requires a
        // before/after comparison and an explicit confirmation.
        public void UnequipSelected()
        {
            if (manager == null || string.IsNullOrWhiteSpace(inspectedId))
                return;

            string id = inspectedId;
            bool removed = targetMonster == null
                ? manager.UnequipFromPlayer(id)
                : manager.UnequipFromMonster(targetMonster, id);

            if (removed)
            {
                EquipmentRecord selected = manager.FindEquipment(id);
                if (selected != null)
                    Inspect(selected);
                if (detailText != null)
                    detailText.text += "\n装備を解除しました。";
            }

            RefreshActionControls();
        }

        public void UnequipAll()
        {
            if (manager == null)
                return;

            bool removed = targetMonster == null
                ? manager.UnequipAllFromPlayer()
                : manager.UnequipAllFromMonster(targetMonster);

            if (removed)
            {
                // Do not clear the actual owned equipment inventory or
                // the current selection; only the owner's equipped slots.
                EquipmentRecord selected = manager.FindEquipment(inspectedId);
                if (selected != null)
                    Inspect(selected);
                if (detailText != null)
                    detailText.text = "装備3枠をすべて解除しました。" +
                        "\n装備は所持品に残っています。";
            }

            RefreshActionControls();
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

        private void EnsureSaleActions()
        {
            if (favoriteButton != null) return;

            favoriteButton = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
                "FavoriteEquipment", "☆ お気に入り",
                new Vector2(23f, 0f), new Vector2(205f, 54f));
            lockButton = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
                "LockEquipment", "ロックする",
                new Vector2(238f, 0f), new Vector2(175f, 54f));
            sellButton = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
                "SellEquipment", "売却",
                new Vector2(423f, 0f), new Vector2(235f, 54f));
            enhanceButton = InventoryUiFactory.Button(SafeAreaLayout.ContentRoot(transform),
                "EnhanceEquipment", "強化",
                new Vector2(668f, 0f), new Vector2(247f, 54f));

            var controls = new[] { favoriteButton, lockButton, sellButton, enhanceButton };
            float[] positions = { 23f, 238f, 423f, 668f };
            for (int i = 0; i < controls.Length; i++)
            {
                RectTransform rect = controls[i].GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(positions[i], 215f);
            }
            favoriteLabel = favoriteButton.GetComponentInChildren<TMP_Text>();
            lockLabel = lockButton.GetComponentInChildren<TMP_Text>();
            sellLabel = sellButton.GetComponentInChildren<TMP_Text>();
            sellButton.GetComponent<Image>().color =
                new Color(0.49f, 0.24f, 0.22f, 1f);

            favoriteButton.onClick.AddListener(() =>
            {
                EquipmentRecord selected = manager != null
                    ? manager.FindEquipment(inspectedId) : null;
                if (selected == null) return;
                manager.SetFavorite(selected.uniqueId, !selected.isFavorite);
                Inspect(selected);
            });
            lockButton.onClick.AddListener(() =>
            {
                EquipmentRecord selected = manager != null
                    ? manager.FindEquipment(inspectedId) : null;
                if (selected == null) return;
                manager.SetLocked(selected.uniqueId, !selected.isLocked);
                Inspect(selected);
            });
            sellButton.onClick.AddListener(ConfirmSingleSale);
            enhanceButton.GetComponent<Image>().color = new Color(0.18f, 0.43f, 0.35f, 1f);
            enhanceButton.onClick.AddListener(() =>
            {
                if (!EnhancementAllowed || !isActiveAndEnabled || manager == null ||
                    manager.FindEquipment(inspectedId) == null) return;
                if (enhancementPanel == null)
                    enhancementPanel = EquipmentEnhancementPanelUI.Ensure(SafeAreaLayout.ContentRoot(transform));
                enhancementPanel.Open(manager, inspectedId);
            });
            SetEnhancementAccess(EnhancementAllowed);

            // Keep paging/detail actions clear of the longer filtering bar.
            ScrollRect scroll = contentRoot != null
                ? contentRoot.GetComponentInParent<ScrollRect>(true) : null;
            if (scroll != null)
            {
                RectTransform rect = scroll.GetComponent<RectTransform>();
                rect.offsetMax = new Vector2(rect.offsetMax.x,
                    Mathf.Min(rect.offsetMax.y, -325f));
                rect.offsetMin = new Vector2(rect.offsetMin.x,
                    Mathf.Max(rect.offsetMin.y, 300f));
            }
        }

        private void RefreshSaleActions()
        {
            EquipmentRecord selected = manager != null
                ? manager.FindEquipment(inspectedId) : null;
            if (favoriteButton == null) return;

            favoriteButton.interactable = selected != null;
            lockButton.interactable = selected != null;
            if (enhanceButton != null)
            {
                enhanceButton.gameObject.SetActive(EnhancementAllowed);
                enhanceButton.interactable = EnhancementAllowed && selected != null;
            }
            bool canSell = manager != null && manager.CanSell(selected);
            sellButton.interactable = canSell;

            if (favoriteLabel != null)
                favoriteLabel.text = selected != null && selected.isFavorite
                    ? "★ お気に入り" : "☆ お気に入り";
            if (lockLabel != null)
                lockLabel.text = selected != null && selected.isLocked
                    ? "ロック解除" : "ロックする";
            if (sellLabel != null)
                sellLabel.text = selected != null && manager != null
                    ? "売却 " + manager.GetEquipmentSaleGold(selected).ToString("N0") + " G"
                    : "売却";
        }

        private void RefreshBulkSellButton()
        {
            if (bulkSellButton == null || manager == null) return;
            bool any = false;
            foreach (string id in filteredIds)
            {
                if (!manager.CanSell(manager.FindEquipment(id))) continue;
                any = true;
                break;
            }
            bulkSellButton.interactable = any;
        }

        private void ConfirmSingleSale()
        {
            if (manager == null || string.IsNullOrEmpty(inspectedId)) return;
            EquipmentRecord selected = manager.FindEquipment(inspectedId);
            if (!manager.CanSell(selected)) return;

            string id = selected.uniqueId;
            string name = GetDisplayName(selected);
            long gold = manager.GetEquipmentSaleGold(selected);
            saleDialog.Show(
                name + " を売却しますか？\n\n" +
                "獲得: " + gold.ToString("N0") + " G\n" +
                "※装備は消失し、元に戻せません。",
                () =>
                {
                    if (!manager.TrySellEquipment(id, out long earned)) return;
                    if (inspectedId == id) inspectedId = null;
                    if (detailText != null)
                        detailText.text = "売却しました: " + earned.ToString("N0") + " G";
                    RefreshActionControls();
                });
        }

        private void ConfirmBulkSale()
        {
            if (manager == null) return;
            var ids = new List<string>();
            long gold = 0L;
            foreach (string id in filteredIds)
            {
                EquipmentRecord record = manager.FindEquipment(id);
                if (!manager.CanSell(record)) continue;
                ids.Add(id);
                gold = LongMath.SaturatingAdd(
                    gold, manager.GetEquipmentSaleGold(record));
            }
            if (ids.Count == 0) return;

            saleDialog.Show(
                "絞り込んだ装備を一括売却しますか？\n\n" +
                "対象: " + ids.Count.ToString("N0") +
                "個 ／ 合計: " + gold.ToString("N0") + " G\n\n" +
                "ロック・お気に入り・装備中は除外済みです。" +
                "\nこの操作は元に戻せません。",
                () =>
                {
                    if (manager == null) return;
                    int sold = manager.SellEquipmentBatch(ids,
                        out long earned);
                    if (sold > 0 && manager.FindEquipment(inspectedId) == null)
                    {
                        inspectedId = null;
                        if (detailText != null)
                            detailText.text = "一括売却: " +
                                sold.ToString("N0") + "個  " +
                                earned.ToString("N0") + " G";
                    }
                });
        }

        private void EnsurePagingControls()
        {
            if (previousPageButton != null) return;

            GameObject barObject = new GameObject(
                "EquipmentPageControls", typeof(RectTransform));
            barObject.transform.SetParent(SafeAreaLayout.ContentRoot(transform), false);
            RectTransform bar = barObject.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 275f);
            bar.sizeDelta = new Vector2(0f, 60f);

            previousPageButton = InventoryUiFactory.Button(bar,
                "PreviousPage", "◀ 前へ",
                new Vector2(24f, -3f), new Vector2(185f, 54f));
            nextPageButton = InventoryUiFactory.Button(bar,
                "NextPage", "次へ ▶",
                Vector2.zero, new Vector2(185f, 54f));
            RectTransform nextRect = nextPageButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(1f, 1f);
            nextRect.anchorMax = Vector2.one;
            nextRect.pivot = Vector2.one;
            nextRect.anchoredPosition = new Vector2(-24f, -3f);
            nextRect.sizeDelta = new Vector2(185f, 54f);

            pageText = InventoryUiFactory.Text(bar, "PageNumber",
                "1 / 1", Vector2.zero, new Vector2(330f, 53f), 25f);
            RectTransform labelRect = pageText.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = labelRect.anchorMin;
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -3f);
            pageText.alignment = TextAlignmentOptions.Center;

            previousPageButton.onClick.AddListener(PreviousPage);
            nextPageButton.onClick.AddListener(NextPage);
            ScrollRect scroll = contentRoot != null
                ? contentRoot.GetComponentInParent<ScrollRect>(true) : null;
            if (scroll != null)
            {
                RectTransform rect = scroll.GetComponent<RectTransform>();
                rect.offsetMin = new Vector2(rect.offsetMin.x,
                    Mathf.Max(rect.offsetMin.y, 355f));
            }
        }

        private void RefreshPageControls()
        {
            int pages = Math.Max(1,
                (filteredIds.Count + PageSize - 1) / PageSize);
            if (pageText != null)
                pageText.text = (pageIndex + 1) + " / " + pages +
                    " (30個/ページ)";
            if (previousPageButton != null)
                previousPageButton.interactable = pageIndex > 0;
            if (nextPageButton != null)
                nextPageButton.interactable = pageIndex + 1 < pages;
        }

        private void EnsureControls()
        {
            // Serialized TMP_Dropdowns come from the prefab, never rebuilt
            // as GameObjects when the inventory is opened.
            Transform controls = SafeAreaLayout.ContentRoot(transform).Find("FilterControls");
            if (controls == null)
            {
                Debug.LogError(
                    "[EquipmentInventoryUI] FilterControlsがPrefabにありません。" +
                    " MixMaster/UI/インベントリフィルターをPrefabに反映 を実行してください。",
                    this);
                return;
            }

            sortDropdown = controls.Find("SortDropdown")?.GetComponent<TMP_Dropdown>();
            statFilter = controls.Find("StatFilter")?.GetComponent<TMP_Dropdown>();
            rarityFilter = controls.Find("RarityFilter")?.GetComponent<TMP_Dropdown>();
            directionButton = controls.Find("Direction")?.GetComponent<Button>();
            bulkSellButton =
                controls.Find("BulkSellEquipment")?.GetComponent<Button>();
            autoSellSettingsButton =
                controls.Find("AutoSellEquipment")?.GetComponent<Button>();
            if (sortDropdown == null || statFilter == null ||
                rarityFilter == null || directionButton == null ||
                bulkSellButton == null || autoSellSettingsButton == null)
            {
                Debug.LogError(
                    "[EquipmentInventoryUI] Prefab内の絞り込みUIが不足しています。" +
                    " UnityのPrefab更新メニューを実行してください。", this);
                return;
            }

            directionLabel = directionButton.GetComponentInChildren<TMP_Text>();
            if (rarityFilter.options.Count != RarityChoices.Length)
            {
                int selected = rarityFilter.value;
                rarityFilter.ClearOptions();
                rarityFilter.AddOptions(new List<string>(RarityChoices));
                rarityFilter.SetValueWithoutNotify(Mathf.Clamp(selected, 0,
                    RarityChoices.Length - 1));
            }
            if (statFilter.options.Count !=
                InventoryProtectionIcons.CombinedStatFilterLabels.Length)
            {
                int selected = statFilter.value;
                statFilter.ClearOptions();
                statFilter.AddOptions(new List<string>(
                    InventoryProtectionIcons.CombinedStatFilterLabels));
                statFilter.SetValueWithoutNotify(Mathf.Clamp(selected, 0,
                    statFilter.options.Count - 1));
            }

            sortDropdown.onValueChanged.AddListener(SortBy);
            statFilter.onValueChanged.AddListener(FilterByStat);
            rarityFilter.onValueChanged.AddListener(FilterByRarity);
            bulkSellButton.onClick.AddListener(ConfirmBulkSale);
            autoSellSettingsButton.onClick.AddListener(() =>
                autoSellDialog?.OpenEquipment());
            directionButton.onClick.AddListener(() =>
            {
                ascending = !ascending;
                if (directionLabel != null)
                    directionLabel.text = ascending ? "昇順" : "降順";
                pageIndex = 0;
                Rebuild();
            });
        }

        private static string[] BuildSortChoices()
        {
            var options = new List<string> { "入手順", "レベル" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                options.Add(InventoryStatFilter.Labels[i]);
            options.Add("お気に入り優先");
            options.Add("ロック優先");
            return options.ToArray();
        }

    }
}
