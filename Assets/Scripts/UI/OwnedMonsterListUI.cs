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

            for (int i = owned.Count - 1;
                 i >= 0;
                 i--)
            {
                OwnedMonsterRecord record =
                    owned[i];

                if (record == null)
                    continue;

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
                    showIndividualValues);
                entry.SetSelectionHandler(OpenDetail);

                entries.Add(entry);
            }

            RefreshCount();
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
                        showIndividualValues);
                }
            }

            RefreshCount();
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

        private void RefreshCount()
        {
            int count =
                monsterManager != null
                    ? monsterManager.OwnedMonsters.Count
                    : 0;

            if (countText != null)
            {
                countText.text =
                    "仲間 " +
                    count.ToString("N0") +
                    "体";
            }

            if (emptyState != null)
            {
                emptyState.SetActive(
                    count <= 0);
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
