using System;
using System.Linq;
using System.Text;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OwnedMonsterDetailUI : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private Image monsterIcon;
        [SerializeField] private TMP_Text monsterNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text intimacyText;
        [SerializeField] private TMP_Text titleDescriptionText;

        [Header("Statistics")]
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Toggle individualValuesToggle;
        [SerializeField] private TMP_Text displayModeText;
        [SerializeField] private TMP_Text resistanceText;
        [SerializeField] private TMP_Text dropBonusText;

        [Header("Training / Equipment")]
        [SerializeField] private TMP_Text skillPointsText;
        [SerializeField] private TMP_Text learnedSkillsText;
        [SerializeField] private TMP_Text equipmentText;
        [SerializeField] private Button equipmentInventoryButton;
        [SerializeField] private Button closeButton;

        [Header("Party")]
        [SerializeField] private Button partyButton;
        [SerializeField] private TMP_Text partyButtonText;
        [SerializeField] private TMP_Text partyStatusText;

        [Header("Behavior")]
        [SerializeField] private TMP_Dropdown behaviorDropdown;

        private MonsterManager monsterManager;
        private Button favoriteButton;
        private Button lockButton;
        private Button sellButton;
        private TMP_Text favoriteButtonText;
        private TMP_Text lockButtonText;
        private TMP_Text saleStatusText;
        private GameObject sellConfirmOverlay;
        private TMP_Text saleConfirmText;
        private Button confirmSellButton;
        private bool partySubscribed;

        private OwnedMonsterRecord selectedRecord;
        private MonsterCatalogSO catalog;
        private bool showIndividualValues;
        private bool readOnlyMode;
        private Color defaultPartyButtonColor = Color.white;

        public OwnedMonsterRecord SelectedRecord => selectedRecord;

        // A breeding shop can reuse this exact detail prefab without allowing
        // party changes, sales, locking or other mutations while previewing.
        public void SetReadOnlyMode(bool enabled)
        {
            readOnlyMode = enabled;
            if (equipmentInventoryButton != null)
                equipmentInventoryButton.gameObject.SetActive(!enabled);
            if (partyButton != null)
                partyButton.gameObject.SetActive(!enabled);
            if (behaviorDropdown != null)
                behaviorDropdown.gameObject.SetActive(!enabled);
            if (favoriteButton != null)
                favoriteButton.transform.parent.gameObject.SetActive(!enabled);
            if (sellConfirmOverlay != null && enabled)
                sellConfirmOverlay.SetActive(false);
            RefreshPartyControls();
            RefreshProtectionControls();
        }

        private void Awake()
        {
            EnsureBehaviorDropdown();
            EnsureEquipmentButton();
            EnsureProtectionControls();
            if (equipmentInventoryButton != null)
                equipmentInventoryButton.onClick.AddListener(OpenEquipmentInventory);
            if (behaviorDropdown != null)
                behaviorDropdown.onValueChanged.AddListener(HandleBehaviorChanged);

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (partyButton != null)
            {
                if (partyButton.targetGraphic is Image background)
                    defaultPartyButtonColor = background.color;
                partyButton.onClick.AddListener(ToggleParty);
            }

            if (individualValuesToggle != null)
            {
                individualValuesToggle.onValueChanged.AddListener(
                    HandleModeChanged);
            }
        }

        private void OnEnable()
        {
            SubscribeParty();
            SubscribeEquipment();
        }

        private void OnDisable()
        {
            UnsubscribeParty();
            UnsubscribeEquipment();
        }

        private void OnDestroy()
        {
            UnsubscribeParty();
            UnsubscribeEquipment();
            if (equipmentInventoryButton != null)
                equipmentInventoryButton.onClick.RemoveListener(OpenEquipmentInventory);
            if (behaviorDropdown != null)
                behaviorDropdown.onValueChanged.RemoveListener(HandleBehaviorChanged);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Hide);

            if (partyButton != null)
                partyButton.onClick.RemoveListener(ToggleParty);

            if (individualValuesToggle != null)
            {
                individualValuesToggle.onValueChanged.RemoveListener(
                    HandleModeChanged);
            }
        }

        public void Show(
            OwnedMonsterRecord record,
            MonsterCatalogSO monsterCatalog,
            bool showIvsInitially = false)
        {
            if (record == null)
                return;

            selectedRecord = record;
            SubscribeParty();
            catalog = monsterCatalog != null
                ? monsterCatalog
                : MonsterCatalogSO.Load();

            showIndividualValues = showIvsInitially;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            SubscribeEquipment();

            if (individualValuesToggle != null)
            {
                individualValuesToggle.SetIsOnWithoutNotify(
                    showIndividualValues);
            }

            Refresh();
        }

        public void Hide()
        {
            if (sellConfirmOverlay != null)
                sellConfirmOverlay.SetActive(false);
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (selectedRecord == null)
                return;

            MonsterSO monster = catalog != null
                ? catalog.GetMonster(selectedRecord.monsterId)
                : null;

            TitleSO title = catalog != null
                ? catalog.GetTitle(selectedRecord.titleId)
                : null;

            string monsterName = monster != null &&
                !string.IsNullOrWhiteSpace(monster.displayName)
                ? monster.displayName
                : selectedRecord.monsterId;

            string titleName = title != null
                ? (!string.IsNullOrWhiteSpace(title.displayName)
                    ? title.displayName
                    : title.titleId)
                : string.Empty;

            SetText(monsterNameText,
                (title != null ? "《" + titleName + "》" : "") +
                monsterName);

            if (monsterIcon != null)
            {
                monsterIcon.sprite = monster != null
                    ? (monster.icon != null ? monster.icon : monster.sprite)
                    : null;
                monsterIcon.enabled = monsterIcon.sprite != null;
                monsterIcon.raycastTarget = false;
            }

            SetText(levelText,
                "Lv." + Mathf.Max(1, selectedRecord.level));

            string elementName = monster != null
                ? GetElementName(monster.baseStats != null
                    ? monster.baseStats.element
                    : ElementType.None)
                : "不明";

            string attackTypeName = monster == null
                ? "不明"
                : (monster.attackType == MonsterAttackType.Ranged
                    ? "遠距離型"
                    : "近距離型");

            // Keep the existing top-row Element TMP reference.
            // This avoids breaking previously generated/customized prefabs.
            SetText(elementText,
                elementName + "属性｜" + attackTypeName);

            long requiredExp =
                ExperienceProgression.RequiredExperience(selectedRecord.level, true);
            SetText(experienceText,
                requiredExp > 0L
                    ? "EXP: " + selectedRecord.experience.ToString("N0") +
                      " / " + requiredExp.ToString("N0")
                    : "EXP: MAX");

            int affectionLevel = MonsterIntimacyBalance.GetLevelFromTotalKills(
                selectedRecord.intimacyKills);
            float affectionPercent = MonsterIntimacyBalance.GetPowerPercent(
                monster, affectionLevel);
            int neededKills = affectionLevel < MonsterIntimacyBalance.MaxLevel
                ? MonsterIntimacyBalance.GetRequiredKillsForNextLevel(affectionLevel)
                : 0;
            int currentKills = MonsterIntimacyBalance.GetKillsWithinLevel(
                selectedRecord.intimacyKills);
            SetText(intimacyText,
                "親密度 Lv." + affectionLevel + "/10（能力" +
                affectionPercent.ToString("0.#") + "%／初期" +
                MonsterIntimacyBalance.GetStartingPercent(monster) +
                "%） " + (affectionLevel >= MonsterIntimacyBalance.MaxLevel
                    ? "MAX" : "討伐 " + currentKills + "/" + neededKills));

            SetText(titleDescriptionText, title != null
                ? (!string.IsNullOrWhiteSpace(title.description)
                    ? "称号効果: " + title.description
                    : "称号: " + titleName)
                : "称号: なし");

            CharacterStats stats = monster != null
                ? StatCalculator.CalculateOwnedMonsterStats(
                    monster, selectedRecord, title,
                    GetEquippedModifiers())
                : null;

            // Drop-rate bonus is a shared additive stat (not an
            // equipment-specific drop chance). Show it in both display modes.
            string statLines = showIndividualValues
                ? FormatIndividualValues(selectedRecord.individualValues)
                : FormatStats(stats);

            SetText(statsText,
                statLines + FormatDropRateBonus(stats));

            SetText(displayModeText, showIndividualValues
                ? "表示: 個体値"
                : "表示: ステータス");

            SetText(resistanceText, stats != null
                ? FormatResistances(stats.resistances)
                : "モンスターデータなし");

            // Legacy separately positioned field is kept for old
            // prefab references but no longer duplicates the bonus.
            SetText(dropBonusText, string.Empty);

            SetText(skillPointsText,
                "スキルポイント: " + selectedRecord.skillPoints +
                "  (使用済み: " + selectedRecord.spentSkillPoints + ")");

            SetText(learnedSkillsText,
                FormatLearnedSkills(monster, selectedRecord));

            SetText(equipmentText,
                FormatEquipment(selectedRecord));

            RefreshPartyControls();
            RefreshBehaviorDropdown();
            RefreshProtectionControls();
        }

        private EquipmentManager equipmentManager;

        private System.Collections.Generic.List<StatPercentageModifiers>
            GetEquippedModifiers()
        {
            if (equipmentManager == null)
                equipmentManager = FindFirstObjectByType<EquipmentManager>();
            return equipmentManager != null && selectedRecord != null
                ? equipmentManager.GetEquippedModifiers(
                    selectedRecord.equippedItemUniqueIds) : null;
        }

        private void SubscribeEquipment()
        {
            if (equipmentManager == null)
                equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager != null)
            {
                equipmentManager.EquipmentChanged -= HandleEquipmentChanged;
                equipmentManager.EquipmentChanged += HandleEquipmentChanged;
            }
        }

        private void UnsubscribeEquipment()
        {
            if (equipmentManager != null)
                equipmentManager.EquipmentChanged -= HandleEquipmentChanged;
        }

        private void HandleEquipmentChanged()
        {
            if (isActiveAndEnabled && selectedRecord != null)
                Refresh();
        }

        private void OpenEquipmentInventory()
        {
            if (readOnlyMode || selectedRecord == null) return;
            EquipmentInventoryUI inventory =
                FindFirstObjectByType<EquipmentInventoryUI>(
                    FindObjectsInactive.Include);
            if (inventory != null)
                inventory.OpenForMonster(selectedRecord);
            else
                Debug.LogWarning(
                    "[OwnedMonsterDetailUI] 装備インベントリを設置してください。" +
                    "「MixMaster/UI/装備インベントリを作成・接続」を実行します。", this);
        }

        private void EnsureEquipmentButton()
        {
            if (equipmentInventoryButton != null) return;
            Transform area = transform.Find(
                "ScrollView/Viewport/Content/Equipment");
            if (area == null) return;

            Transform existing = area.Find("OpenEquipmentInventory");
            if (existing != null)
            {
                equipmentInventoryButton = existing.GetComponent<Button>();
                return;
            }

            equipmentInventoryButton = InventoryUiFactory.Button(
                area, "OpenEquipmentInventory", "装備変更",
                new Vector2(680f, -10f), new Vector2(180f, 48f));
        }

        private void HandleBehaviorChanged(int selectedIndex)
        {
            if (readOnlyMode || selectedRecord == null)
                return;

            SubscribeParty();
            if (monsterManager == null)
                return;

            MonsterCombatBehavior behavior;
            switch (selectedIndex)
            {
                case 0: behavior = MonsterCombatBehavior.HostileEnemies; break;
                case 2: behavior = MonsterCombatBehavior.AutoHunt; break;
                case 3: behavior = MonsterCombatBehavior.NoAttack; break;
                default: behavior = MonsterCombatBehavior.PlayerAttackRange; break;
            }
            monsterManager.SetMonsterCombatBehavior(selectedRecord.uniqueId, behavior);
        }

        private void RefreshBehaviorDropdown()
        {
            if (behaviorDropdown == null)
                return;

            int selectedIndex = 1; // Default: player attack range.
            if (selectedRecord != null)
            {
                switch (selectedRecord.combatBehavior)
                {
                    case MonsterCombatBehavior.HostileEnemies: selectedIndex = 0; break;
                    case MonsterCombatBehavior.AutoHunt: selectedIndex = 2; break;
                    case MonsterCombatBehavior.NoAttack: selectedIndex = 3; break;
                }
            }
            behaviorDropdown.SetValueWithoutNotify(selectedIndex);
        }

        private void EnsureBehaviorDropdown()
        {
            if (behaviorDropdown == null)
            {
                behaviorDropdown = GetComponentInChildren<TMP_Dropdown>(true);
            }

            if (behaviorDropdown == null)
                behaviorDropdown = CreateBehaviorDropdown();

            if (behaviorDropdown == null)
                return;

            // Also repair old/prefab-assigned dropdowns, not only runtime
            // generated ones. All four options should remain visible without
            // a scrollbar.
            if (behaviorDropdown.template != null)
            {
                ScrollRect existingScroll =
                    behaviorDropdown.template.GetComponent<ScrollRect>();
                if (existingScroll != null)
                {
                    existingScroll.vertical = false;
                    existingScroll.horizontal = false;
                    existingScroll.inertia = false;
                    existingScroll.scrollSensitivity = 0f;
                    existingScroll.movementType = ScrollRect.MovementType.Clamped;
                }
            }

            behaviorDropdown.ClearOptions();
            behaviorDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "敵対した敵を攻撃",
                "プレイヤーの攻撃範囲内の敵を攻撃",
                "自動狩りモード（マップ全体）",
                "攻撃しない"
            });
            // Default displayed option is the player attack-range behavior.
            behaviorDropdown.SetValueWithoutNotify(1);
        }

        private TMP_Dropdown CreateBehaviorDropdown()
        {
            Transform content = transform.Find("ScrollView/Viewport/Content");
            if (content == null)
            {
                Debug.LogWarning(
                    "[OwnedMonsterDetailUI] 行動選択を生成するスクロールContentがありません。", this);
                return null;
            }

            GameObject sectionObject = new GameObject(
                "CombatBehaviorSection",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sectionObject.transform.SetParent(content, false);
            RectTransform section = sectionObject.GetComponent<RectTransform>();
            section.sizeDelta = new Vector2(900f, 150f);
            LayoutElement layout = sectionObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 150f;
            layout.minHeight = 150f;
            sectionObject.GetComponent<Image>().color =
                new Color(0.095f, 0.13f, 0.19f, 0.97f);
            sectionObject.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI header = CreateBehaviorText(
                section, "Header", "仲間の行動",
                new Vector2(16f, -10f), new Vector2(750f, 40f), 28f);

            GameObject dropdownObject = new GameObject(
                "CombatBehaviorDropdown",
                typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            dropdownObject.transform.SetParent(section, false);
            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            SetTopLeft(rect, new Vector2(16f, -60f), new Vector2(840f, 60f));
            dropdownObject.GetComponent<Image>().color =
                new Color(0.20f, 0.26f, 0.35f, 1f);
            TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();

            TextMeshProUGUI caption = CreateBehaviorText(
                rect, "Label", string.Empty,
                new Vector2(18f, -9f), new Vector2(790f, 45f), 24f);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText = caption;

            // TMP_Dropdown needs a fully configured template, with ScrollRect,
            // masked viewport and an item Toggle to clone for every option.
            GameObject templateObject = new GameObject(
                "Template", typeof(RectTransform),
                typeof(Image), typeof(ScrollRect));
            templateObject.transform.SetParent(rect, false);
            RectTransform template = templateObject.GetComponent<RectTransform>();
            template.anchorMin = Vector2.zero;
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = Vector2.zero;
            template.sizeDelta = new Vector2(0f, 224f);
            templateObject.GetComponent<Image>().color =
                new Color(0.12f, 0.16f, 0.22f, 1f);

            GameObject viewportObject = new GameObject(
                "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(template, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            StretchBehaviorRect(viewport);
            viewportObject.GetComponent<Image>().color = Color.white;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject(
                "Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            RectTransform itemContent = contentObject.GetComponent<RectTransform>();
            itemContent.anchorMin = new Vector2(0f, 1f);
            itemContent.anchorMax = Vector2.one;
            itemContent.pivot = new Vector2(0.5f, 1f);
            itemContent.sizeDelta = new Vector2(0f, 220f);
            itemContent.anchoredPosition = Vector2.zero;

            GameObject itemObject = new GameObject(
                "Item", typeof(RectTransform),
                typeof(Image), typeof(Toggle));
            itemObject.transform.SetParent(itemContent, false);
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = new Vector2(0f, -27.5f);
            itemRect.sizeDelta = new Vector2(0f, 55f);
            Image itemImage = itemObject.GetComponent<Image>();
            itemImage.color = new Color(0.18f, 0.22f, 0.31f, 1f);
            Toggle toggle = itemObject.GetComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            toggle.isOn = true;

            TextMeshProUGUI itemText = CreateBehaviorText(
                itemRect, "Item Label", "行動",
                new Vector2(18f, -6f), new Vector2(790f, 44f), 23f);
            itemText.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.itemText = itemText;

            ScrollRect scroller = templateObject.GetComponent<ScrollRect>();
            scroller.viewport = viewport;
            scroller.content = itemContent;
            scroller.horizontal = false;
            // All four options fit entirely in the viewport (4 x 55px).
            // Disable nested dragging to avoid scrolling beyond the last
            // option or passing accidental drag input into the detail panel.
            scroller.vertical = false;
            scroller.inertia = false;
            scroller.scrollSensitivity = 0f;
            scroller.movementType = ScrollRect.MovementType.Clamped;

            dropdown.template = template;
            templateObject.SetActive(false);
            return dropdown;
        }

        private static TextMeshProUGUI CreateBehaviorText(
            Transform parent, string name, string value,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetTopLeft(go.GetComponent<RectTransform>(), position, size);
            TextMeshProUGUI result = go.GetComponent<TextMeshProUGUI>();
            result.text = value;
            result.fontSize = fontSize;
            result.color = Color.white;
            result.raycastTarget = false;
            return result;
        }

        private static void SetTopLeft(
            RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void StretchBehaviorRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SubscribeParty()
        {
            if (monsterManager == null)
                monsterManager = FindFirstObjectByType<MonsterManager>();

            if (monsterManager == null || partySubscribed)
                return;

            monsterManager.PartyChanged += HandlePartyChanged;
            monsterManager.MonsterProtectionChanged += HandleProtectionChanged;
            monsterManager.MonsterSold += HandleMonsterSold;
            monsterManager.MonsterExperienceChanged += HandleExperienceChanged;
            monsterManager.MonsterIntimacyLeveledUp += HandleExperienceChanged;
            monsterManager.MonsterIntimacyChanged += HandleExperienceChanged;
            partySubscribed = true;
        }

        private void UnsubscribeParty()
        {
            if (!partySubscribed)
                return;

            if (monsterManager != null)
            {
                monsterManager.PartyChanged -= HandlePartyChanged;
                monsterManager.MonsterProtectionChanged -= HandleProtectionChanged;
                monsterManager.MonsterSold -= HandleMonsterSold;
                monsterManager.MonsterExperienceChanged -= HandleExperienceChanged;
                monsterManager.MonsterIntimacyLeveledUp -= HandleExperienceChanged;
                monsterManager.MonsterIntimacyChanged -= HandleExperienceChanged;
            }

            partySubscribed = false;
        }

        private void HandlePartyChanged()
        {
            RefreshPartyControls();
            RefreshProtectionControls();
        }

        private void HandleProtectionChanged(OwnedMonsterRecord record)
        {
            if (selectedRecord != null && record != null &&
                selectedRecord.uniqueId == record.uniqueId)
                RefreshProtectionControls();
        }

        private void HandleMonsterSold(string uniqueId)
        {
            if (selectedRecord != null &&
                selectedRecord.uniqueId == uniqueId)
            {
                selectedRecord = null;
                Hide();
            }
        }

        private void HandleExperienceChanged(OwnedMonsterRecord record)
        {
            if (selectedRecord != null && record != null &&
                selectedRecord.uniqueId == record.uniqueId)
                Refresh();
        }

        private void EnsureProtectionControls()
        {
            if (favoriteButton != null) return;
            Transform content = transform.Find("ScrollView/Viewport/Content");
            if (content == null) return;

            GameObject section = new GameObject(
                "MonsterProtectionAndSale", typeof(RectTransform),
                typeof(Image), typeof(LayoutElement));
            section.transform.SetParent(content, false);
            LayoutElement layout = section.GetComponent<LayoutElement>();
            layout.preferredHeight = 175f;
            layout.minHeight = 175f;
            Image image = section.GetComponent<Image>();
            image.color = new Color(0.12f, 0.17f, 0.23f, 1f);
            image.raycastTarget = false;

            favoriteButton = InventoryUiFactory.Button(section.transform,
                "FavoriteButton", "☆ お気に入り",
                new Vector2(15f, -20f), new Vector2(265f, 60f));
            lockButton = InventoryUiFactory.Button(section.transform,
                "LockButton", "ロック",
                new Vector2(294f, -20f), new Vector2(250f, 60f));
            sellButton = InventoryUiFactory.Button(section.transform,
                "SellMonsterButton", "売却",
                new Vector2(557f, -20f), new Vector2(265f, 60f));
            sellButton.GetComponent<Image>().color =
                new Color(0.47f, 0.25f, 0.22f, 1f);
            favoriteButtonText = favoriteButton.GetComponentInChildren<TMP_Text>();
            lockButtonText = lockButton.GetComponentInChildren<TMP_Text>();

            saleStatusText = InventoryUiFactory.Text(
                section.transform, "SaleStatus",
                "売却額: -", new Vector2(18f, -96f),
                new Vector2(805f, 60f), 23f);

            favoriteButton.onClick.AddListener(ToggleFavorite);
            lockButton.onClick.AddListener(ToggleLock);
            sellButton.onClick.AddListener(OpenSellConfirmation);

            CreateSellConfirmation();
        }

        private void CreateSellConfirmation()
        {
            if (sellConfirmOverlay != null) return;
            GameObject overlay = new GameObject(
                "MonsterSellConfirmation", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(transform, false);
            InventoryUiFactory.Stretch(overlay.GetComponent<RectTransform>());
            Image cover = overlay.GetComponent<Image>();
            cover.color = new Color(0.015f, 0.026f, 0.042f, 0.96f);
            cover.raycastTarget = true;

            GameObject box = new GameObject(
                "ConfirmBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(overlay.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.08f, 0.32f);
            boxRect.anchorMax = new Vector2(0.92f, 0.69f);
            boxRect.offsetMin = Vector2.zero;
            boxRect.offsetMax = Vector2.zero;
            box.GetComponent<Image>().color =
                new Color(0.12f, 0.18f, 0.26f, 1f);

            saleConfirmText = InventoryUiFactory.Text(
                box.transform, "ConfirmMessage",
                "このモンスターを売却しますか？",
                new Vector2(26f, -32f), new Vector2(740f, 135f), 30f);
            saleConfirmText.enableWordWrapping = true;

            Button cancel = InventoryUiFactory.Button(
                box.transform, "Cancel", "キャンセル",
                new Vector2(30f, -200f), new Vector2(320f, 70f));
            confirmSellButton = InventoryUiFactory.Button(
                box.transform, "ConfirmSell", "売却する",
                new Vector2(385f, -200f), new Vector2(320f, 70f));
            confirmSellButton.GetComponent<Image>().color =
                new Color(0.52f, 0.22f, 0.21f, 1f);

            cancel.onClick.AddListener(() => overlay.SetActive(false));
            confirmSellButton.onClick.AddListener(ConfirmSale);
            sellConfirmOverlay = overlay;
            overlay.SetActive(false);
        }

        private void RefreshProtectionControls()
        {
            if (favoriteButton == null || selectedRecord == null) return;
            bool protectedFromSale = monsterManager == null ||
                monsterManager.IsProtectedFromConsumption(selectedRecord);
            if (favoriteButtonText != null)
                favoriteButtonText.text = selectedRecord.isFavorite
                    ? "★ お気に入り" : "☆ お気に入り";
            if (lockButtonText != null)
                lockButtonText.text = selectedRecord.isLocked
                    ? "🔒 ロック中" : "ロックする";

            if (saleStatusText != null)
                saleStatusText.text = monsterManager == null
                    ? "売却データを取得できません"
                    : "売却額: " +
                        monsterManager.GetMonsterSaleGold(selectedRecord)
                            .ToString("N0") + " G" +
                        (protectedFromSale
                            ? "　(編成中・ロック・お気に入りは売却不可)"
                            : "");
            if (sellButton != null)
                sellButton.interactable = !readOnlyMode && !protectedFromSale;
        }

        private void ToggleFavorite()
        {
            if (readOnlyMode || monsterManager == null || selectedRecord == null) return;
            monsterManager.SetFavorite(
                selectedRecord.uniqueId, !selectedRecord.isFavorite);
            RefreshProtectionControls();
        }

        private void ToggleLock()
        {
            if (readOnlyMode || monsterManager == null || selectedRecord == null) return;
            monsterManager.SetLocked(
                selectedRecord.uniqueId, !selectedRecord.isLocked);
            RefreshProtectionControls();
        }

        private void OpenSellConfirmation()
        {
            if (readOnlyMode || monsterManager == null || selectedRecord == null ||
                monsterManager.IsProtectedFromConsumption(selectedRecord) ||
                sellConfirmOverlay == null) return;

            MonsterSO monster = catalog != null
                ? catalog.GetMonster(selectedRecord.monsterId) : null;
            string name = monster != null &&
                !string.IsNullOrWhiteSpace(monster.displayName)
                ? monster.displayName : selectedRecord.monsterId;
            if (saleConfirmText != null)
                saleConfirmText.text = name + " (Lv." +
                    selectedRecord.level + ") を売却しますか？\n" +
                    "獲得: " + monsterManager.GetMonsterSaleGold(
                        selectedRecord).ToString("N0") + " G\n" +
                    "※売却した仲間は戻せません。装備品は残ります。";
            sellConfirmOverlay.SetActive(true);
            sellConfirmOverlay.transform.SetAsLastSibling();
        }

        private void ConfirmSale()
        {
            if (readOnlyMode || monsterManager == null || selectedRecord == null) return;
            string id = selectedRecord.uniqueId;
            if (!monsterManager.TrySellMonster(id, out long goldEarned))
            {
                if (saleConfirmText != null)
                    saleConfirmText.text =
                        "売却できません。編成・ロック・お気に入りを確認してください。";
                RefreshProtectionControls();
                return;
            }

            if (sellConfirmOverlay != null)
                sellConfirmOverlay.SetActive(false);
            // MonsterSold also hides this panel and rebuilds the inventory.
            selectedRecord = null;
            Hide();
        }

        private void ToggleParty()
        {
            if (readOnlyMode || selectedRecord == null)
                return;

            SubscribeParty();

            if (monsterManager == null)
                return;

            bool alreadyMember =
                monsterManager.PartyMonsterUniqueIds.Contains(
                    selectedRecord.uniqueId);

            if (alreadyMember)
                monsterManager.RemoveFromParty(selectedRecord.uniqueId);
            else
                monsterManager.AddToParty(selectedRecord.uniqueId);

            RefreshPartyControls();
        }

        private void RefreshPartyControls()
        {
            if (monsterManager == null)
            {
                if (partyButton != null)
                    partyButton.interactable = false;

                SetText(partyStatusText, "編成情報を取得できません");
                SetText(partyButtonText, "編成する");
                return;
            }

            int current = monsterManager.PartyMonsterUniqueIds.Count;
            int max = monsterManager.MaxPartySize;
            bool selected = selectedRecord != null &&
                monsterManager.PartyMonsterUniqueIds.Contains(
                    selectedRecord.uniqueId);

            SetText(partyStatusText, "パーティ " + current + "/" + max);
            SetText(partyButtonText, selected ? "編成から外す" : "パーティに編成");

            if (partyButton != null)
            {
                partyButton.interactable = !readOnlyMode && (selected || current < max);
                if (partyButton.targetGraphic is Image background)
                {
                    Color color = defaultPartyButtonColor;
                    if (selected)
                    {
                        int order = -1;
                        for (int i = 0; i < monsterManager.PartyMonsterUniqueIds.Count; i++)
                        {
                            if (monsterManager.PartyMonsterUniqueIds[i] == selectedRecord.uniqueId)
                            {
                                order = i;
                                break;
                            }
                        }

                        if (order >= 0 && order < 3)
                        {
                            MaterialDropSourceType source = order == 0
                                ? MaterialDropSourceType.Party1
                                : order == 1
                                    ? MaterialDropSourceType.Party2
                                    : MaterialDropSourceType.Party3;
                            color = MaterialDropLogUI.GetSourceColor(source);
                        }
                    }

                    background.color = color;
                }
            }
        }

        private void HandleModeChanged(bool showIvs)
        {
            showIndividualValues = showIvs;
            Refresh();
        }

        public static string FormatIndividualValues(
            MonsterIndividualValues iv)
        {
            if (iv == null)
                return "個体値データなし";

            return "HP " + FormatIv(iv.hp) + "    MP " + FormatIv(iv.mp) +
                   "\n攻撃 " + FormatIv(iv.attack) + "    魔力 " + FormatIv(iv.magic) +
                   "\n防御 " + FormatIv(iv.defense) + "    魔防 " + FormatIv(iv.magicDefense) +
                   "\nクリ率 " + FormatIv(iv.criticalRate) +
                   "    クリ倍率 " + FormatIv(iv.criticalMultiplier) +
                   "\n移動速度 " + FormatIv(iv.moveSpeed) +
                   "    攻撃速度 " + FormatIv(iv.attackSpeed) +
                   "\n攻撃範囲 " + FormatIv(iv.attackRange);
        }

        private static string FormatStats(CharacterStats stats)
        {
            if (stats == null)
                return "モンスターデータなし";

            return "HP " + stats.maxHp.ToString("N0") +
                   "    MP " + stats.maxMp.ToString("N0") +
                   "\n攻撃 " + stats.attack.ToString("N0") +
                   "    魔力 " + stats.magic.ToString("N0") +
                   "\n防御 " + stats.defense.ToString("N0") +
                   "    魔防 " + stats.magicDefense.ToString("N0") +
                   "\nクリ率 " + (stats.criticalRate * 100f).ToString("0.##") + "%" +
                   "    クリ倍率 x" + stats.criticalMultiplier.ToString("0.##") +
                   "\n移動速度 " + stats.moveSpeed.ToString("0.##") +
                   "    攻撃速度 " + stats.attackSpeed.ToString("0.##") +
                   "\n攻撃範囲 " + stats.attackRange.ToString("0.##");
        }

        private static string FormatDropRateBonus(CharacterStats stats)
        {
            if (stats == null)
                return "    ドロップ率補正 -";

            // Shared additive percentage; equipment-specific drop rate
            // itself is not implemented in the current data model.
            return "    ドロップ率補正 +" +
                   (stats.dropRateBonus * 100f).ToString("0.##") + "%";
        }

        private static string FormatIv(int value)
        {
            int iv = MonsterIndividualValues.Clamp(value);

            return iv == 100
                ? "<color=#FFD700><b>100</b></color>"
                : iv.ToString();
        }

        private static string FormatResistances(ElementResistanceSet set)
        {
            if (set == null)
                return "耐性データなし";

            return "火 " + FormatResistance(set.fire) +
                   "    水 " + FormatResistance(set.water) +
                   "\n風 " + FormatResistance(set.wind) +
                   "    雷 " + FormatResistance(set.lightning) +
                   "\n光 " + FormatResistance(set.light) +
                   "    闇 " + FormatResistance(set.dark);
        }

        private static string FormatResistance(float value)
        {
            float percent = ElementResistanceSet.Clamp(value) * 100f;
            return (percent >= 0f ? "+" : "") +
                   percent.ToString("0.##") + "%";
        }

        private static string FormatLearnedSkills(
            MonsterSO monster,
            OwnedMonsterRecord record)
        {
            if (record.learnedSkillIds == null ||
                record.learnedSkillIds.Count == 0)
            {
                return "習得スキル: なし";
            }

            StringBuilder builder = new StringBuilder("習得スキル:");

            for (int i = 0; i < record.learnedSkillIds.Count; i++)
            {
                string id = record.learnedSkillIds[i];
                string name = id;

                if (monster != null && monster.availableSkills != null)
                {
                    for (int j = 0; j < monster.availableSkills.Count; j++)
                    {
                        SkillSO skill = monster.availableSkills[j];

                        if (skill != null &&
                            string.Equals(skill.skillId, id,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            name = string.IsNullOrWhiteSpace(skill.displayName)
                                ? skill.skillId
                                : skill.displayName;
                            break;
                        }
                    }
                }

                builder.Append("\n・").Append(name);
            }

            return builder.ToString();
        }

        private static string FormatEquipment(OwnedMonsterRecord record)
        {
            StringBuilder result = new StringBuilder();
            var equipmentManager =
                UnityEngine.Object.FindFirstObjectByType<EquipmentManager>();

            for (int i = 0; i < EquipmentManager.MaxEquipmentSlots; i++)
            {
                if (i > 0)
                    result.Append("\n");

                result.Append("装備").Append(i + 1).Append(": ");

                if (record.equippedItemUniqueIds == null ||
                    i >= record.equippedItemUniqueIds.Count)
                {
                    result.Append("未装備");
                    continue;
                }

                string uniqueId = record.equippedItemUniqueIds[i];
                if (string.IsNullOrWhiteSpace(uniqueId))
                {
                    result.Append("未装備");
                    continue;
                }

                EquipmentRecord item = equipmentManager != null
                    ? equipmentManager.FindEquipment(uniqueId)
                    : null;
                EquipmentSO definition = item != null && equipmentManager != null
                    ? equipmentManager.GetEquipmentDefinition(item.equipmentId)
                    : null;
                string name = definition != null &&
                    !string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.displayName
                        : item != null ? item.equipmentId : "不明な装備";
                result.Append(name);
                if (item != null) result.Append(" Lv.").Append(item.level);
            }

            return result.ToString();
        }

        private static string GetElementName(ElementType type)
        {
            switch (type)
            {
                case ElementType.Fire: return "火";
                case ElementType.Water: return "水";
                case ElementType.Wind: return "風";
                case ElementType.Lightning: return "雷";
                case ElementType.Light: return "光";
                case ElementType.Dark: return "闇";
                default: return "無";
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        public void SetMonsterIcon(Image value) => monsterIcon = value;
        public void SetMonsterNameText(TMP_Text value) => monsterNameText = value;
        public void SetLevelText(TMP_Text value) => levelText = value;
        public void SetElementText(TMP_Text value) => elementText = value;
        public void SetExperienceText(TMP_Text value) => experienceText = value;
        public void SetIntimacyText(TMP_Text value) => intimacyText = value;
        public void SetTitleDescriptionText(TMP_Text value) => titleDescriptionText = value;
        public void SetStatsText(TMP_Text value) => statsText = value;
        public void SetResistanceText(TMP_Text value) => resistanceText = value;
        public void SetDropBonusText(TMP_Text value) => dropBonusText = value;
        public void SetSkillPointsText(TMP_Text value) => skillPointsText = value;
        public void SetLearnedSkillsText(TMP_Text value) => learnedSkillsText = value;
        public void SetEquipmentText(TMP_Text value) => equipmentText = value;
        public void SetDisplayModeText(TMP_Text value) => displayModeText = value;
        public void SetIndividualValuesToggle(Toggle value) => individualValuesToggle = value;
        public void SetCloseButton(Button value) => closeButton = value;
        public void SetPartyButton(Button value) => partyButton = value;
        public void SetPartyButtonText(TMP_Text value) => partyButtonText = value;
        public void SetPartyStatusText(TMP_Text value) => partyStatusText = value;
        public void SetBehaviorDropdown(TMP_Dropdown value) => behaviorDropdown = value;
    }
}
