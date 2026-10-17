using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Core;
using MixMaster.Player;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OwnedMonsterListUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private OwnedMonsterEntryUI entryPrefab;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private Button closeButton;
        [SerializeField] private Toggle displayModeToggle;
        [SerializeField] private TMP_Text displayModeText;
        [SerializeField] private OwnedMonsterDetailUI detailPanel;
        [Header("Protection Icons")]
        [SerializeField] private Sprite favoriteSprite;
        [SerializeField] private Sprite lockSprite;

        [Header("Input")]
        [SerializeField] private bool lockPlayerMovementWhileOpen = true;
        [SerializeField] private bool disableJoystickWhileOpen = true;

        private const int PageSize = 30;
        private readonly List<OwnedMonsterEntryUI> entries =
            new List<OwnedMonsterEntryUI>(PageSize);
        private readonly List<int> filteredIndices = new List<int>();
        private readonly Dictionary<string, CharacterStats> statCache =
            new Dictionary<string, CharacterStats>(StringComparer.Ordinal);
        private int pageIndex;
        private Button previousPageButton;
        private Button nextPageButton;
        private TMP_Text pageNumberText;
        private TMP_Text goldText;
        private Button bulkSellButton;
        private Button autoSellSettingsButton;
        private InventoryBulkSaleDialog bulkSaleDialog;
        private InventoryAutoSellDialog autoSellDialog;
        private GameManager gameManager;

        private MonsterManager monsterManager;
        private MonsterCatalogSO catalog;
        private PlayerController player;
        private FloatingJoystick joystick;
        private bool subscribed;
        private EquipmentManager equipmentManager;
        private bool showIndividualValues;

        private TMP_Dropdown sortDropdown;
        private TMP_Dropdown statFilterDropdown;
        private TMP_Dropdown thresholdDropdown;
        private Button sortDirectionButton;
        private TMP_Text directionText;
        private bool ascending;

        private static readonly string[] SortOptions = CreateSortOptions();
        private static readonly string[] ThresholdOptions =
        {
            "上位25%", "上位50%", "上位75%", "0以外"
        };

        private void Awake()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(
                    Close);

                closeButton.onClick.AddListener(
                    Close);
            }

            if (displayModeToggle != null)
            {
                displayModeToggle.onValueChanged
                    .RemoveListener(
                        HandleDisplayModeChanged);

                displayModeToggle.onValueChanged
                    .AddListener(
                        HandleDisplayModeChanged);

                showIndividualValues =
                    displayModeToggle.isOn;
            }

            RefreshDisplayModeText();
            EnsureSortingControls();
            EnsurePagingControls();
            EnsureBulkSaleDialog();
            autoSellDialog = InventoryAutoSellDialog.Ensure(transform);
        }

        private void OnEnable()
        {
            if (panelRoot == null ||
                panelRoot == gameObject ||
                panelRoot.activeSelf)
            {
                HandlePanelOpened();
            }
        }

        private void OnDisable()
        {
            if (autoSellDialog != null)
                autoSellDialog.Close();
            if (panelRoot == null ||
                panelRoot == gameObject)
            {
                HandlePanelClosed();
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            if (displayModeToggle != null)
            {
                displayModeToggle.onValueChanged
                    .RemoveListener(
                        HandleDisplayModeChanged);
            }

            Unsubscribe();
        }

        public void Open()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            if (!panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }
            else
            {
                HandlePanelOpened();
            }
        }

        public void Close()
        {
            if (panelRoot == null)
                panelRoot = gameObject;

            HandlePanelClosed();
            if (autoSellDialog != null)
                autoSellDialog.Close();

            if (panelRoot.activeSelf)
                panelRoot.SetActive(false);
        }

        public void Rebuild()
        {
            CacheReferences();
            filteredIndices.Clear();

            if (contentRoot == null || entryPrefab == null ||
                monsterManager == null)
            {
                HidePooledEntries();
                RefreshCount(0);
                RefreshPageControls();
                return;
            }

            IReadOnlyList<OwnedMonsterRecord> owned = monsterManager.OwnedMonsters;
            int sortValue = sortDropdown != null ? sortDropdown.value : 0;
            int filterValue = statFilterDropdown != null ? statFilterDropdown.value : 0;
            int thresholdValue = thresholdDropdown != null ? thresholdDropdown.value : 0;


            for (int i = 0; i < owned.Count; i++)
            {
                OwnedMonsterRecord record = owned[i];
                if (record != null &&
                    InventoryProtectionIcons.MatchesCombinedStatFilter(
                        filterValue, record.isFavorite, record.isLocked))
                    filteredIndices.Add(i);
            }

            // Lazy: with acquisition or level ordering and no numeric
            // filters, calculate stats only for the visible page.
            if (filterValue > 0 &&
                filterValue < InventoryStatFilter.Labels.Length &&
                filteredIndices.Count > 0)
            {
                InventoryStat selected = (InventoryStat)filterValue;
                if (thresholdValue < 3)
                {
                    var scores = new List<double>(filteredIndices.Count);
                    foreach (int index in filteredIndices)
                        scores.Add(GetValueForSorting(owned[index], selected));

                    scores.Sort((x, y) => y.CompareTo(x));
                    float fraction = thresholdValue == 0 ? 0.25f :
                        thresholdValue == 1 ? 0.5f : 0.75f;
                    int cutoff = Mathf.Clamp(
                        Mathf.CeilToInt(scores.Count * fraction) - 1,
                        0, scores.Count - 1);
                    double minimum = scores[cutoff];
                    filteredIndices.RemoveAll(index =>
                        GetValueForSorting(owned[index], selected) < minimum);
                }
                else
                    filteredIndices.RemoveAll(index =>
                        Math.Abs(GetValueForSorting(owned[index], selected)) < 0.000001d);
            }

            filteredIndices.Sort((a, b) =>
            {
                int cmp;
                if (sortValue == 0)
                    cmp = b.CompareTo(a);
                else if (sortValue == 1)
                    cmp = owned[b].level.CompareTo(owned[a].level);
                else if (sortValue == InventoryStatFilter.Labels.Length + 1)
                    cmp = owned[b].isFavorite.CompareTo(owned[a].isFavorite);
                else if (sortValue == InventoryStatFilter.Labels.Length + 2)
                    cmp = owned[b].isLocked.CompareTo(owned[a].isLocked);
                else
                {
                    InventoryStat stat = (InventoryStat)(sortValue - 1);
                    cmp = GetValueForSorting(owned[b], stat).CompareTo(
                        GetValueForSorting(owned[a], stat));
                }

                if (cmp == 0) cmp = b.CompareTo(a);
                return ascending ? -cmp : cmp;
            });

            int pages = Math.Max(1, (filteredIndices.Count + PageSize - 1) / PageSize);
            pageIndex = Mathf.Clamp(pageIndex, 0, pages - 1);
            RenderPage();
        }

        private double GetValueForSorting(
            OwnedMonsterRecord record, InventoryStat stat)
        {
            if (showIndividualValues &&
                InventoryStatFilter.SupportsIndividualValues(stat))
                return InventoryStatFilter.GetIndividualValue(
                    record != null ? record.individualValues : null, stat);

            return InventoryStatFilter.GetValue(GetCachedStats(record), stat);
        }

        private CharacterStats GetCachedStats(OwnedMonsterRecord record)
        {
            if (record == null) return null;
            string key = record.uniqueId ?? string.Empty;
            if (!statCache.TryGetValue(key, out CharacterStats stats))
            {
                stats = CalculateStats(record);
                statCache[key] = stats;
            }
            return stats;
        }

        private void RenderPage()
        {
            if (monsterManager == null || entryPrefab == null || contentRoot == null)
                return;
            IReadOnlyList<OwnedMonsterRecord> owned = monsterManager.OwnedMonsters;
            int start = pageIndex * PageSize;
            int visible = Mathf.Min(PageSize, Mathf.Max(0,
                filteredIndices.Count - start));

            // No more than 30 instantiated entry prefabs, even with 5000
            // owned monsters. Rebind these existing views every page.
            while (entries.Count < visible)
            {
                OwnedMonsterEntryUI entry = Instantiate(entryPrefab, contentRoot);
                entry.SetSelectionHandler(OpenDetail);
                entries.Add(entry);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                OwnedMonsterEntryUI entry = entries[i];
                if (entry == null) continue;
                bool show = i < visible;
                if (entry.gameObject.activeSelf != show)
                    entry.gameObject.SetActive(show);
                if (!show) continue;

                OwnedMonsterRecord record = owned[filteredIndices[start + i]];
                entry.name = "OwnedMonster_" + record.uniqueId;
                entry.Bind(record, catalog, showIndividualValues,
                    monsterManager.PartyMonsterUniqueIds,
                    showIndividualValues ? null : GetCachedStats(record),
                    favoriteSprite, lockSprite,
                     sortDropdown != null ? sortDropdown.value : 0);
            }

            RefreshCount(filteredIndices.Count);
            RefreshPageControls();
            RefreshBulkSaleButton();
            ScrollRect scroll = contentRoot.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void HidePooledEntries()
        {
            foreach (OwnedMonsterEntryUI entry in entries)
                if (entry != null)
                    entry.gameObject.SetActive(false);
        }

        public void RefreshAll()
        {
            CacheReferences();
            if (monsterManager == null) return;
            foreach (OwnedMonsterEntryUI entry in entries)
            {
                if (entry == null || !entry.gameObject.activeSelf ||
                    entry.Record == null) continue;
                entry.Bind(entry.Record, catalog, showIndividualValues,
                    monsterManager.PartyMonsterUniqueIds,
                    showIndividualValues ? null : GetCachedStats(entry.Record),
                    favoriteSprite, lockSprite,
                     sortDropdown != null ? sortDropdown.value : 0);
            }
            RefreshCount(filteredIndices.Count);
            RefreshGoldText();
            RefreshBulkSaleButton();
        }

        public void NextPage()
        {
            if ((pageIndex + 1) * PageSize >= filteredIndices.Count) return;
            pageIndex++;
            RenderPage();
        }

        public void PreviousPage()
        {
            if (pageIndex <= 0) return;
            pageIndex--;
            RenderPage();
        }

        private void RequestFirstPage()
        {
            pageIndex = 0;
            Rebuild();
        }

        private void HandlePanelOpened()
        {
            CacheReferences();
            Subscribe();

            if (lockPlayerMovementWhileOpen &&
                player != null)
            {
                player.SetCombatMovementLocked(true);
            }

            if (disableJoystickWhileOpen &&
                joystick != null)
            {
                joystick.SetInteractionEnabled(false);
            }

            // Combat can progress while the list is closed.
            statCache.Clear();
            Rebuild();
        }

        private void HandlePanelClosed()
        {
            if (detailPanel != null)
                detailPanel.Hide();

            if (lockPlayerMovementWhileOpen &&
                player != null)
            {
                player.SetCombatMovementLocked(false);
            }

            if (disableJoystickWhileOpen &&
                joystick != null)
            {
                joystick.SetInteractionEnabled(true);
            }

            Unsubscribe();
        }

        private void CacheReferences()
        {
            if (monsterManager == null)
            {
                monsterManager =
                    FindFirstObjectByType<MonsterManager>();
            }

            if (catalog == null)
                catalog = MonsterCatalogSO.Load();

            if (player == null)
            {
                player =
                    FindFirstObjectByType<PlayerController>();
            }

            if (joystick == null)
            {
                joystick =
                    FindFirstObjectByType<FloatingJoystick>();
            }
            if (equipmentManager == null)
                equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            if (monsterManager == null)
                return;

            if (equipmentManager == null)
                equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager != null)
                equipmentManager.EquipmentChanged += HandleEquipmentChanged;

            monsterManager.MonsterObtained +=
                HandleMonsterObtained;
            monsterManager.MonsterProtectionChanged += HandleProtectionChanged;
            monsterManager.MonsterSold += HandleMonsterSold;
            monsterManager.MonstersBulkSold += HandleMonstersBulkSold;
            if (gameManager != null)
            {
                gameManager.GoldChanged -= HandleGoldChanged;
                gameManager.GoldChanged += HandleGoldChanged;
            }
            monsterManager.MonsterExperienceChanged +=
                HandleMonsterExperienceChanged;
            monsterManager.MonsterLeveledUp += HandleMonsterLevelChanged;
            monsterManager.MonsterIntimacyLeveledUp += HandleMonsterLevelChanged;
            monsterManager.PartyChanged += RefreshAll;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed ||
                monsterManager == null)
            {
                return;
            }

            if (equipmentManager != null)
                equipmentManager.EquipmentChanged -= HandleEquipmentChanged;

            monsterManager.MonsterObtained -=
                HandleMonsterObtained;
            monsterManager.MonsterProtectionChanged -= HandleProtectionChanged;
            monsterManager.MonsterSold -= HandleMonsterSold;
            monsterManager.MonstersBulkSold -= HandleMonstersBulkSold;
            if (gameManager != null)
                gameManager.GoldChanged -= HandleGoldChanged;
            monsterManager.MonsterExperienceChanged -=
                HandleMonsterExperienceChanged;
            monsterManager.MonsterLeveledUp -= HandleMonsterLevelChanged;
            monsterManager.MonsterIntimacyLeveledUp -= HandleMonsterLevelChanged;
            monsterManager.PartyChanged -= RefreshAll;

            subscribed = false;
        }

        public void OpenDetail(OwnedMonsterRecord record)
        {
            if (record == null)
                return;

            CacheReferences();

            if (detailPanel == null)
            {
                detailPanel = GetComponentInChildren<OwnedMonsterDetailUI>(true);
            }

            if (detailPanel == null)
            {
                Debug.LogWarning(
                    "[OwnedMonsterListUI] 詳細パネルが未接続です。" +
                    "『MixMaster/UI/仲間詳細Prefabを作成・一覧に接続』を実行してください。",
                    this);
                return;
            }

            detailPanel.Show(record, catalog, showIndividualValues);
        }

        private void HandleDisplayModeChanged(
            bool showIvs)
        {
            showIndividualValues = showIvs;

            for (int i = 0; i < entries.Count; i++)
            {
                OwnedMonsterEntryUI entry =
                    entries[i];

                if (entry != null)
                {
                    entry.SetDisplayMode(
                        showIndividualValues);
                }
            }

            RefreshDisplayModeText();

            // Re-evaluate rankings and thresholds for the new display mode.
            pageIndex = 0;
            Rebuild();
        }

        private void RefreshDisplayModeText()
        {
            if (displayModeText == null)
                return;

            displayModeText.text =
                showIndividualValues
                    ? "表示: 個体値"
                    : "表示: ステータス";
        }

        private void HandleMonsterObtained(
            OwnedMonsterRecord record)
        {
            RequestFirstPage();
        }

        private void HandleProtectionChanged(OwnedMonsterRecord record)
        {
            Rebuild();
        }

        private void HandleMonsterSold(string uniqueId)
        {
            statCache.Remove(uniqueId);
            if (detailPanel != null && detailPanel.SelectedRecord != null &&
                detailPanel.SelectedRecord.uniqueId == uniqueId)
                detailPanel.Hide();
            Rebuild();
        }

        private void HandleMonstersBulkSold()
        {
            statCache.Clear();
            if (detailPanel != null && detailPanel.SelectedRecord != null &&
                monsterManager.FindOwnedMonster(
                    detailPanel.SelectedRecord.uniqueId) == null)
                detailPanel.Hide();
            Rebuild();
        }

        private void HandleGoldChanged(long amount)
        {
            RefreshGoldText();
        }

        private void HandleEquipmentChanged()
        {
            statCache.Clear();
            Rebuild();
        }

        private void HandleMonsterLevelChanged(OwnedMonsterRecord record)
        {
            if (record != null) statCache.Remove(record.uniqueId);
            if ((sortDropdown != null && sortDropdown.value != 0) ||
                (statFilterDropdown != null && statFilterDropdown.value > 0 &&
                 statFilterDropdown.value < InventoryStatFilter.Labels.Length))
                Rebuild();
            else RefreshAll();
        }

        private void HandleMonsterExperienceChanged(OwnedMonsterRecord record)
        {
            if (record == null)
                return;

            // Only the affected entry needs recalculation; avoid rebuilding
            // a large monster collection on every battle EXP gain.
            for (int i = 0; i < entries.Count; i++)
            {
                OwnedMonsterEntryUI entry = entries[i];
                if (entry == null || entry.Record == null ||
                    entry.Record.uniqueId != record.uniqueId)
                    continue;

                entry.Bind(record, catalog, showIndividualValues,
                    monsterManager.PartyMonsterUniqueIds,
                    showIndividualValues ? null : GetCachedStats(record),
                    favoriteSprite, lockSprite,
                     sortDropdown != null ? sortDropdown.value : 0);
                break;
            }
        }

        private void RefreshCount(int displayed = -1)
        {
            int count =
                monsterManager != null
                    ? monsterManager.OwnedMonsters.Count
                    : 0;

            if (countText != null)
            {
                countText.text = displayed >= 0
                    ? "仲間 " + displayed.ToString("N0") + " / " +
                      count.ToString("N0") + "体"
                    : "仲間 " + count.ToString("N0") + "体";
            }

            if (emptyState != null)
            {
                emptyState.SetActive(
                    displayed >= 0 ? displayed == 0 : count <= 0);
            }
        }

        private void ClearEntries()
        {
            HidePooledEntries();
        }

        private CharacterStats CalculateStats(OwnedMonsterRecord record)
        {
            MonsterSO monster = catalog != null
                ? catalog.GetMonster(record.monsterId) : null;
            TitleSO title = catalog != null
                ? catalog.GetTitle(record.titleId) : null;
            return StatCalculator.CalculateOwnedMonsterStats(
                monster, record, title,
                equipmentManager != null ? equipmentManager.GetEquippedModifiers(
                    record.equippedItemUniqueIds) : null);
        }

        private static string[] CreateSortOptions()
        {
            var choices = new List<string> { "入手順", "レベル" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                choices.Add(InventoryStatFilter.Labels[i]);
            choices.Add("お気に入り優先");
            choices.Add("ロック優先");
            return choices.ToArray();
        }

        private void EnsureSortingControls()
        {
            // Dropdowns are authored into the panel prefab by the Editor
            // baker. Never instantiate a TMP_Dropdown during gameplay.
            Transform controls = transform.Find("InventoryFilters");
            if (controls == null)
            {
                Debug.LogError(
                    "[OwnedMonsterListUI] InventoryFiltersがPrefabにありません。" +
                    " MixMaster/UI/インベントリフィルターをPrefabに反映 を実行してください。",
                    this);
                return;
            }

            sortDropdown = controls.Find("SortDropdown")?.GetComponent<TMP_Dropdown>();
            statFilterDropdown =
                controls.Find("StatFilter")?.GetComponent<TMP_Dropdown>();
            thresholdDropdown =
                controls.Find("ThresholdFilter")?.GetComponent<TMP_Dropdown>();
            sortDirectionButton =
                controls.Find("SortDirection")?.GetComponent<Button>();
            bulkSellButton =
                controls.Find("SellFilteredMonsters")?.GetComponent<Button>();
            autoSellSettingsButton =
                controls.Find("AutoSellMonsters")?.GetComponent<Button>();
            if (sortDropdown == null || statFilterDropdown == null ||
                thresholdDropdown == null || sortDirectionButton == null ||
                bulkSellButton == null || autoSellSettingsButton == null)
            {
                Debug.LogError(
                    "[OwnedMonsterListUI] Prefabのフィルター項目が不足しています。" +
                    " UnityのPrefab更新メニューを実行してください。", this);
                return;
            }

            directionText = sortDirectionButton.GetComponentInChildren<TMP_Text>();
            if (statFilterDropdown.options.Count !=
                InventoryProtectionIcons.CombinedStatFilterLabels.Length)
            {
                // Existing scene overrides from old versions retain their
                // serialized dropdown; only correct its option labels.
                int selected = statFilterDropdown.value;
                statFilterDropdown.ClearOptions();
                statFilterDropdown.AddOptions(new List<string>(
                    InventoryProtectionIcons.CombinedStatFilterLabels));
                statFilterDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0,
                        statFilterDropdown.options.Count - 1));
            }

            sortDropdown.onValueChanged.AddListener(_ => RequestFirstPage());
            statFilterDropdown.onValueChanged.AddListener(_ =>
            {
                thresholdDropdown.interactable = statFilterDropdown.value > 0 &&
                    statFilterDropdown.value < InventoryStatFilter.Labels.Length;
                RequestFirstPage();
            });
            thresholdDropdown.onValueChanged.AddListener(_ => RequestFirstPage());
            bulkSellButton.onClick.AddListener(ConfirmFilteredMonsterSale);
            autoSellSettingsButton.onClick.AddListener(() =>
                autoSellDialog?.OpenMonsters());
            sortDirectionButton.onClick.AddListener(() =>
            {
                ascending = !ascending;
                if (directionText != null)
                    directionText.text = ascending ? "昇順" : "降順";
                RequestFirstPage();
            });
            thresholdDropdown.interactable = statFilterDropdown.value > 0 &&
                statFilterDropdown.value < InventoryStatFilter.Labels.Length;
        }

        private void EnsureBulkSaleDialog()
        {
            if (bulkSaleDialog == null)
                bulkSaleDialog = InventoryBulkSaleDialog.Ensure(transform);
        }

        private void GetEligibleFilteredMonsters(List<string> destination,
            out long totalGold)
        {
            destination.Clear();
            totalGold = 0L;
            if (monsterManager == null) return;
            IReadOnlyList<OwnedMonsterRecord> owned = monsterManager.OwnedMonsters;
            foreach (int index in filteredIndices)
            {
                if (index < 0 || index >= owned.Count) continue;
                OwnedMonsterRecord record = owned[index];
                if (monsterManager.IsProtectedFromConsumption(record))
                    continue;

                destination.Add(record.uniqueId);
                totalGold = LongMath.SaturatingAdd(
                    totalGold, monsterManager.GetMonsterSaleGold(record));
            }
        }

        private void RefreshBulkSaleButton()
        {
            if (bulkSellButton == null || monsterManager == null)
                return;

            bool found = false;
            IReadOnlyList<OwnedMonsterRecord> owned = monsterManager.OwnedMonsters;
            foreach (int index in filteredIndices)
            {
                if (index >= 0 && index < owned.Count &&
                    !monsterManager.IsProtectedFromConsumption(owned[index]))
                {
                    found = true;
                    break;
                }
            }
            bulkSellButton.interactable = found;
        }

        private void ConfirmFilteredMonsterSale()
        {
            CacheReferences();
            if (monsterManager == null) return;
            var selection = new List<string>();
            GetEligibleFilteredMonsters(selection, out long gold);
            if (selection.Count == 0) return;
            EnsureBulkSaleDialog();
            bulkSaleDialog.Show(
                "フィルター結果を一括売却しますか？\n\n" +
                "対象: " + selection.Count.ToString("N0") + "体" +
                "　／　合計: " + gold.ToString("N0") + " G\n\n" +
                "編成中・お気に入り・ロック中は除外しています。" +
                "\nこの操作は元に戻せません。",
                () =>
                {
                    if (monsterManager == null) return;
                    // IDs are frozen at preview time, and the manager
                    // rechecks all protection flags at commit time.
                    int sold = monsterManager.SellMonstersBatch(selection,
                        out long received);
                    if (sold == 0) Rebuild();
                });
        }

        private void EnsurePagingControls()
        {
            if (previousPageButton != null) return;
            GameObject area = new GameObject(
                "MonsterPageControls", typeof(RectTransform));
            area.transform.SetParent(transform, false);
            RectTransform bar = area.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 12f);
            bar.sizeDelta = new Vector2(0f, 92f);

            previousPageButton = InventoryUiFactory.Button(
                bar, "PreviousPage", "◀ 前へ",
                new Vector2(24f, -9f), new Vector2(180f, 53f));
            nextPageButton = InventoryUiFactory.Button(
                bar, "NextPage", "次へ ▶",
                Vector2.zero, new Vector2(180f, 53f));
            RectTransform next = nextPageButton.GetComponent<RectTransform>();
            next.anchorMin = new Vector2(1f, 1f);
            next.anchorMax = new Vector2(1f, 1f);
            next.pivot = new Vector2(1f, 1f);
            next.anchoredPosition = new Vector2(-24f, -9f);

            pageNumberText = InventoryUiFactory.Text(
                bar, "PageNumber", "1 / 1",
                Vector2.zero, new Vector2(265f, 48f), 26f);
            RectTransform pageRect = pageNumberText.rectTransform;
            pageRect.anchorMin = new Vector2(0.5f, 1f);
            pageRect.anchorMax = new Vector2(0.5f, 1f);
            pageRect.pivot = new Vector2(0.5f, 1f);
            pageRect.anchoredPosition = new Vector2(0f, -10f);
            pageNumberText.alignment = TextAlignmentOptions.Center;

            goldText = InventoryUiFactory.Text(
                bar, "GoldDisplay", "Gold: 0 G",
                Vector2.zero, new Vector2(420f, 29f), 19f);
            RectTransform goldRect = goldText.rectTransform;
            goldRect.anchorMin = new Vector2(0.5f, 0f);
            goldRect.anchorMax = new Vector2(0.5f, 0f);
            goldRect.pivot = new Vector2(0.5f, 0f);
            goldRect.anchoredPosition = Vector2.zero;
            goldText.alignment = TextAlignmentOptions.Center;
            goldText.color = new Color(1f, 0.83f, 0.46f);
            previousPageButton.onClick.AddListener(PreviousPage);
            nextPageButton.onClick.AddListener(NextPage);

            RectTransform scroll =
                transform.Find("ScrollView") as RectTransform;
            if (scroll != null)
            {
                Vector2 min = scroll.offsetMin;
                min.y = Mathf.Max(min.y, 110f);
                scroll.offsetMin = min;
            }
            RefreshPageControls();
        }

        private void RefreshPageControls()
        {
            int pages = Math.Max(1,
                (filteredIndices.Count + PageSize - 1) / PageSize);
            if (pageNumberText != null)
                pageNumberText.text = (pageIndex + 1) + " / " + pages +
                    "  (30体/ページ)";
            if (previousPageButton != null)
                previousPageButton.interactable = pageIndex > 0;
            if (nextPageButton != null)
                nextPageButton.interactable = pageIndex + 1 < pages;
            RefreshGoldText();
        }

        private void RefreshGoldText()
        {
            if (goldText == null) return;
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();
            goldText.text = "Gold: " +
                (gameManager != null ? gameManager.Gold.ToString("N0") : "0")
                + " G";
        }

        public void SortBy(int index)
        {
            if (sortDropdown != null)
                sortDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(index, 0, SortOptions.Length - 1));
            RequestFirstPage();
        }

        public void FilterByStat(int index)
        {
            if (statFilterDropdown != null)
                statFilterDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(index, 0,
                        InventoryProtectionIcons.CombinedStatFilterLabels.Length - 1));
            RequestFirstPage();
        }

        public void SetPanelRoot(GameObject value) => panelRoot = value;
        public void SetDetailPanel(OwnedMonsterDetailUI value) => detailPanel = value;
        public void SetContentRoot(RectTransform value) => contentRoot = value;
        public void SetEntryPrefab(OwnedMonsterEntryUI value) => entryPrefab = value;
        public void SetCountText(TMP_Text value) => countText = value;
        public void SetEmptyState(GameObject value) => emptyState = value;
        public void SetDisplayModeText(TMP_Text value)
        {
            displayModeText = value;
            RefreshDisplayModeText();
        }

        public void SetDisplayModeToggle(Toggle value)
        {
            if (displayModeToggle != null)
            {
                displayModeToggle.onValueChanged
                    .RemoveListener(
                        HandleDisplayModeChanged);
            }

            displayModeToggle = value;

            if (displayModeToggle != null)
            {
                displayModeToggle.onValueChanged
                    .RemoveListener(
                        HandleDisplayModeChanged);

                displayModeToggle.onValueChanged
                    .AddListener(
                        HandleDisplayModeChanged);

                showIndividualValues =
                    displayModeToggle.isOn;
            }

            RefreshDisplayModeText();
        }

        public void SetCloseButton(Button value)
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            closeButton = value;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }
    }
}
