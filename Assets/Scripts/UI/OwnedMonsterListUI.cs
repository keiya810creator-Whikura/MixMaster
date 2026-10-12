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

        [Header("Input")]
        [SerializeField] private bool lockPlayerMovementWhileOpen = true;
        [SerializeField] private bool disableJoystickWhileOpen = true;

        private readonly List<OwnedMonsterEntryUI> entries =
            new List<OwnedMonsterEntryUI>();

        private MonsterManager monsterManager;
        private MonsterCatalogSO catalog;
        private PlayerController player;
        private FloatingJoystick joystick;
        private bool subscribed;
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

            if (panelRoot.activeSelf)
                panelRoot.SetActive(false);
        }

        public void Rebuild()
        {
            CacheReferences();

            ClearEntries();

            if (contentRoot == null ||
                entryPrefab == null ||
                monsterManager == null)
            {
                RefreshCount();
                return;
            }

            IReadOnlyList<OwnedMonsterRecord> owned =
                monsterManager.OwnedMonsters;

            var statsById = new Dictionary<string, CharacterStats>();
            var indices = new List<int>();
            for (int i = 0; i < owned.Count; i++)
            {
                OwnedMonsterRecord record = owned[i];
                if (record == null) continue;
                indices.Add(i);
                statsById[record.uniqueId] = CalculateStats(record);
            }

            int sortValue = sortDropdown != null ? sortDropdown.value : 0;
            int filterValue = statFilterDropdown != null
                ? statFilterDropdown.value : 0;
            int thresholdValue = thresholdDropdown != null
                ? thresholdDropdown.value : 0;

            // Choose a relative threshold among all owned monsters. This is
            // meaningful for raw HP/Attack/etc., which are usually nonzero
            // on every monster (unlike equipment modifiers).
            if (filterValue > 0 && indices.Count > 0)
            {
                var orderedScores = new List<double>();
                foreach (int index in indices)
                {
                    CharacterStats stats = statsById[owned[index].uniqueId];
                    orderedScores.Add(InventoryStatFilter.GetValue(
                        stats, (InventoryStat)filterValue));
                }

                if (thresholdValue < 3)
                {
                    orderedScores.Sort((a, b) => b.CompareTo(a));
                    float fraction = thresholdValue == 0 ? 0.25f :
                        thresholdValue == 1 ? 0.50f : 0.75f;
                    int cutoff = Mathf.Clamp(
                        Mathf.CeilToInt(orderedScores.Count * fraction) - 1,
                        0, orderedScores.Count - 1);
                    double minimum = orderedScores[cutoff];
                    indices.RemoveAll(index =>
                        InventoryStatFilter.GetValue(
                            statsById[owned[index].uniqueId],
                            (InventoryStat)filterValue) < minimum);
                }
                else
                {
                    indices.RemoveAll(index =>
                        Math.Abs(InventoryStatFilter.GetValue(
                            statsById[owned[index].uniqueId],
                            (InventoryStat)filterValue)) < 0.000001d);
                }
            }

            indices.Sort((a, b) =>
            {
                int cmp;
                if (sortValue == 0)
                    cmp = b.CompareTo(a);
                else if (sortValue == 1)
                    cmp = owned[b].level.CompareTo(owned[a].level);
                else
                {
                    InventoryStat metric = (InventoryStat)(sortValue - 1);
                    double x = InventoryStatFilter.GetValue(
                        statsById[owned[a].uniqueId], metric);
                    double y = InventoryStatFilter.GetValue(
                        statsById[owned[b].uniqueId], metric);
                    cmp = y.CompareTo(x);
                }

                if (cmp == 0) cmp = b.CompareTo(a);
                return ascending ? -cmp : cmp;
            });

            foreach (int index in indices)
            {
                OwnedMonsterRecord record = owned[index];

                OwnedMonsterEntryUI entry =
                    Instantiate(
                        entryPrefab,
                        contentRoot);

                entry.name =
                    "OwnedMonster_" +
                    record.uniqueId;

                entry.Bind(
                    record,
                    catalog,
                    showIndividualValues,
                    monsterManager.PartyMonsterUniqueIds);
                entry.SetSelectionHandler(OpenDetail);

                entries.Add(entry);
            }

            RefreshCount(indices.Count);
        }

        public void RefreshAll()
        {
            CacheReferences();

            for (int i = 0; i < entries.Count; i++)
            {
                OwnedMonsterEntryUI entry =
                    entries[i];

                if (entry != null)
                {
                    entry.Bind(
                        entry.Record,
                        catalog,
                        showIndividualValues,
                        monsterManager.PartyMonsterUniqueIds);
                }
            }

            RefreshCount(entries.Count);
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
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            if (monsterManager == null)
                return;

            monsterManager.MonsterObtained +=
                HandleMonsterObtained;
            monsterManager.MonsterExperienceChanged +=
                HandleMonsterExperienceChanged;
            monsterManager.MonsterLeveledUp += HandleMonsterLevelChanged;
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

            monsterManager.MonsterObtained -=
                HandleMonsterObtained;
            monsterManager.MonsterExperienceChanged -=
                HandleMonsterExperienceChanged;
            monsterManager.MonsterLeveledUp -= HandleMonsterLevelChanged;
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
            Rebuild();
        }

        private void HandleMonsterLevelChanged(OwnedMonsterRecord record)
        {
            if (sortDropdown != null && sortDropdown.value != 0 ||
                statFilterDropdown != null && statFilterDropdown.value != 0)
            {
                Rebuild();
            }
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
                    monsterManager.PartyMonsterUniqueIds);
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
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                    Destroy(entries[i].gameObject);
            }

            entries.Clear();
        }

        private CharacterStats CalculateStats(OwnedMonsterRecord record)
        {
            MonsterSO monster = catalog != null
                ? catalog.GetMonster(record.monsterId) : null;
            TitleSO title = catalog != null
                ? catalog.GetTitle(record.titleId) : null;
            return StatCalculator.CalculateMonsterStats(
                monster, Mathf.Max(1, record.level),
                record.individualValues, title);
        }

        private static string[] CreateSortOptions()
        {
            var choices = new List<string> { "入手順", "レベル" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                choices.Add(InventoryStatFilter.Labels[i]);
            return choices.ToArray();
        }

        private void EnsureSortingControls()
        {
            if (sortDropdown != null)
                return;

            Transform existing = transform.Find("InventoryFilters");
            RectTransform controls;
            if (existing != null)
                controls = existing as RectTransform;
            else
            {
                GameObject go = new GameObject(
                    "InventoryFilters", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                controls = go.GetComponent<RectTransform>();
                controls.anchorMin = new Vector2(0f, 1f);
                controls.anchorMax = new Vector2(1f, 1f);
                controls.pivot = new Vector2(0.5f, 1f);
                controls.anchoredPosition = new Vector2(0f, -95f);
                controls.sizeDelta = new Vector2(0f, 85f);
            }

            sortDropdown = InventoryUiFactory.Dropdown(
                controls, "SortDropdown", new Vector2(32f, -8f),
                new Vector2(225f, 56f), SortOptions);
            statFilterDropdown = InventoryUiFactory.Dropdown(
                controls, "StatFilter", new Vector2(267f, -8f),
                new Vector2(260f, 56f), InventoryStatFilter.Labels);
            thresholdDropdown = InventoryUiFactory.Dropdown(
                controls, "ThresholdFilter", new Vector2(537f, -8f),
                new Vector2(205f, 56f), ThresholdOptions);
            sortDirectionButton = InventoryUiFactory.Button(
                controls, "SortDirection", "降順",
                new Vector2(753f, -8f), new Vector2(130f, 56f));
            directionText =
                sortDirectionButton.GetComponentInChildren<TMP_Text>();

            sortDropdown.onValueChanged.AddListener(_ => Rebuild());
            statFilterDropdown.onValueChanged.AddListener(_ => Rebuild());
            thresholdDropdown.onValueChanged.AddListener(_ => Rebuild());
            sortDirectionButton.onClick.AddListener(() =>
            {
                ascending = !ascending;
                if (directionText != null)
                    directionText.text = ascending ? "昇順" : "降順";
                Rebuild();
            });

            Transform scroll = transform.Find("ScrollView");
            RectTransform scrollRect = scroll as RectTransform;
            if (scrollRect != null)
            {
                Vector2 offset = scrollRect.offsetMax;
                offset.y = Mathf.Min(offset.y, -186f);
                scrollRect.offsetMax = offset;
            }
        }

        public void SortBy(int index)
        {
            if (sortDropdown != null)
                sortDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(index, 0, SortOptions.Length - 1));
            Rebuild();
        }

        public void FilterByStat(int index)
        {
            if (statFilterDropdown != null)
                statFilterDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(index, 0, InventoryStatFilter.Labels.Length - 1));
            Rebuild();
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
