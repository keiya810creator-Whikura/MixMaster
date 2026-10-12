using System;
using System.Text;
using MixMaster.Core;
using MixMaster.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Read-only player status page. The equipment button opens a working
    /// equipment overview and exposes an event for a future equip/change UI.
    /// Reincarnation is intentionally handled by world altar/town objects,
    /// not from the player's status screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStatusMenuUI : MonoBehaviour
    {
        [Header("Status")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text resourcesText;
        [SerializeField] private TMP_Text coreStatsText;
        [SerializeField] private TMP_Text combatStatsText;
        [SerializeField] private TMP_Text resistancesText;
        [SerializeField] private TMP_Text equipmentPreviewText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Sprite portraitOverride;

        [Header("Navigation")]
        [SerializeField] private GameObject statusPage;
        [SerializeField] private GameObject equipmentPage;
        [SerializeField] private TMP_Text equipmentPageText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button equipmentCloseButton;
        [SerializeField] private bool showEquipmentOverviewOnClick = true;
        [SerializeField] private EquipmentInventoryUI equipmentInventory;

        public void SetEquipmentInventory(EquipmentInventoryUI inventory)
        {
            equipmentInventory = inventory;
        }

        [Header("Future Equipment UI")]
        [SerializeField] private UnityEvent onEquipmentMenuRequested =
            new UnityEvent();

        private PlayerManager player;
        private EquipmentManager equipment;
        private SpriteRenderer playerRenderer;
        private bool subscribed;
        private float nextRefreshTime;

        public UnityEvent EquipmentMenuRequested => onEquipmentMenuRequested;

        public void SetReferences(
            TMP_Text level, TMP_Text resources, TMP_Text core,
            TMP_Text combat, TMP_Text resistances, TMP_Text gearSummary,
            Image portrait, GameObject mainPage, GameObject gearPage,
            TMP_Text gearPageText, Button close, Button equip,
            Button back, Button closeGear)
        {
            levelText = level;
            resourcesText = resources;
            coreStatsText = core;
            combatStatsText = combat;
            resistancesText = resistances;
            equipmentPreviewText = gearSummary;
            portraitImage = portrait;
            statusPage = mainPage;
            equipmentPage = gearPage;
            equipmentPageText = gearPageText;
            closeButton = close;
            equipmentButton = equip;
            backButton = back;
            equipmentCloseButton = closeGear;
        }

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (equipmentButton != null)
                equipmentButton.onClick.AddListener(OpenEquipment);
            if (backButton != null)
                backButton.onClick.AddListener(BackToStatus);
            if (equipmentCloseButton != null)
                equipmentCloseButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            Subscribe();
            BackToStatus();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (equipmentButton != null)
                equipmentButton.onClick.RemoveListener(OpenEquipment);
            if (backButton != null)
                backButton.onClick.RemoveListener(BackToStatus);
            if (equipmentCloseButton != null)
                equipmentCloseButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + 0.3f;
            if (player == null)
                Subscribe();

            // EquipmentManager currently has no changed event. Poll only
            // while the menu is visible to keep the overview up-to-date.
            Refresh();
        }

        public void Open()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Subscribe();
            BackToStatus();
        }

        public void Close()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void OpenEquipment()
        {
            // Prefer the new searchable inventory. Keep the old overview
            // as a fallback for scenes that have not installed the prefab.
            if (equipmentInventory == null)
                equipmentInventory = FindFirstObjectByType<EquipmentInventoryUI>(
                    FindObjectsInactive.Include);

            if (equipmentInventory != null)
            {
                equipmentInventory.Open();
                onEquipmentMenuRequested?.Invoke();
                return;
            }

            if (showEquipmentOverviewOnClick)
            {
                if (statusPage != null) statusPage.SetActive(false);
                if (equipmentPage != null) equipmentPage.SetActive(true);
                RefreshEquipment();
            }

            // A future full equipment/inventory screen can subscribe here.
            onEquipmentMenuRequested?.Invoke();
        }

        public void BackToStatus()
        {
            if (equipmentPage != null) equipmentPage.SetActive(false);
            if (statusPage != null) statusPage.SetActive(true);
            Refresh();
        }

        private void Subscribe()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerManager>();
            if (equipment == null)
                equipment = FindFirstObjectByType<EquipmentManager>();
            if (playerRenderer == null)
            {
                PlayerController controller =
                    FindFirstObjectByType<PlayerController>();
                if (controller != null)
                    playerRenderer =
                        controller.GetComponentInChildren<SpriteRenderer>();
            }

            if (player == null || subscribed)
                return;

            player.HpChanged += HandleResourceChange;
            player.MpChanged += HandleResourceChange;
            player.ExperienceChanged += HandleResourceChange;
            player.LeveledUp += HandleLevelUp;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (subscribed && player != null)
            {
                player.HpChanged -= HandleResourceChange;
                player.MpChanged -= HandleResourceChange;
                player.ExperienceChanged -= HandleResourceChange;
                player.LeveledUp -= HandleLevelUp;
            }

            subscribed = false;
        }

        private void HandleResourceChange(long current, long max)
        {
            Refresh();
        }

        private void HandleLevelUp(int newLevel)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (player == null)
            {
                SetText(levelText, "プレイヤー");
                SetText(resourcesText, "プレイヤーデータを取得中...");
                SetText(coreStatsText, string.Empty);
                SetText(combatStatsText, string.Empty);
                SetText(resistancesText, string.Empty);
                RefreshEquipment();
                return;
            }

            CharacterStats s = player.Stats;
            if (s == null)
                return;

            SetText(levelText, "プレイヤー　Lv." + player.Level.ToString("N0"));

            long required = player.ExperienceToNextLevel;
            string exp = required > 0L
                ? player.Experience.ToString("N0") + " / " +
                  required.ToString("N0")
                : "MAX";

            SetText(resourcesText,
                "HP      " + player.CurrentHp.ToString("N0") +
                " / " + s.maxHp.ToString("N0") +
                "\nMP      " + player.CurrentMp.ToString("N0") +
                " / " + s.maxMp.ToString("N0") +
                "\nEXP     " + exp);

            SetText(coreStatsText,
                "最大HP       " + s.maxHp.ToString("N0") +
                "\n最大MP       " + s.maxMp.ToString("N0") +
                "\n攻撃力       " + s.attack.ToString("N0") +
                "\n魔力           " + s.magic.ToString("N0") +
                "\n防御力       " + s.defense.ToString("N0") +
                "\n魔法防御   " + s.magicDefense.ToString("N0"));

            SetText(combatStatsText,
                "クリティカル率         " + FormatPercent(s.criticalRate) +
                "\nクリティカル倍率     x" +
                s.criticalMultiplier.ToString("0.##") +
                "\n移動速度                 " + s.moveSpeed.ToString("0.##") +
                "\n攻撃速度                 " + s.attackSpeed.ToString("0.##") +
                "\n攻撃範囲                 " + s.attackRange.ToString("0.##") +
                "\nドロップ率補正         +" +
                (s.dropRateBonus * 100f).ToString("0.##") + "%");

            ElementResistanceSet res = s.resistances;
            SetText(resistancesText, res == null
                ? "属性耐性データなし"
                : "火  " + FormatPercent(res.fire) +
                  "      水  " + FormatPercent(res.water) +
                  "\n風  " + FormatPercent(res.wind) +
                  "      雷  " + FormatPercent(res.lightning) +
                  "\n光  " + FormatPercent(res.light) +
                  "      闇  " + FormatPercent(res.dark));

            RefreshPortrait();
            RefreshEquipment();
        }

        private void RefreshPortrait()
        {
            if (portraitImage == null)
                return;

            if (playerRenderer == null)
            {
                PlayerController controller =
                    FindFirstObjectByType<PlayerController>();
                if (controller != null)
                    playerRenderer =
                        controller.GetComponentInChildren<SpriteRenderer>();
            }

            Sprite sprite = portraitOverride != null
                ? portraitOverride
                : playerRenderer != null ? playerRenderer.sprite : null;
            if (portraitImage.sprite != sprite)
                portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
        }

        private void RefreshEquipment()
        {
            if (equipment == null)
                equipment = FindFirstObjectByType<EquipmentManager>();

            var info = new StringBuilder();
            if (equipment == null)
            {
                info.Append("装備データを取得中...");
            }
            else
            {
                int count = equipment.PlayerEquipmentUniqueIds.Count;
                for (int i = 0; i < EquipmentManager.MaxEquipmentSlots; i++)
                {
                    info.Append("装備 ").Append(i + 1).Append("   ");
                    if (i < count)
                    {
                        EquipmentRecord item = equipment.FindEquipment(
                            equipment.PlayerEquipmentUniqueIds[i]);
                        if (item != null)
                        {
                            EquipmentSO definition =
                                equipment.GetEquipmentDefinition(item.equipmentId);
                            string equipmentName = definition != null &&
                                !string.IsNullOrWhiteSpace(definition.displayName)
                                    ? definition.displayName
                                    : string.IsNullOrEmpty(item.equipmentId)
                                        ? "不明な装備" : item.equipmentId;

                            info.Append(equipmentName);
                            info.Append("  Lv.").Append(item.level);
                            info.Append("  ")
                                .Append(InventoryStatFilter.FormatModifiers(
                                    equipment.GetScaledModifiers(item)));
                        }
                        else info.Append("空き");
                    }
                    else info.Append("空き");

                    if (i + 1 < EquipmentManager.MaxEquipmentSlots)
                        info.Append('\n');
                }
            }

            SetText(equipmentPreviewText, info.ToString());

            SetText(equipmentPageText,
                info.ToString() +
                "\n\n所持装備数   " +
                (equipment != null
                    ? equipment.Inventory.Count.ToString("N0") : "-") +
                "\n\n装備の変更・強化は今後ここに追加します。" +
                "\n装備ボタンは将来の装備メニューへの接続に対応しています。");
        }

        private static string FormatPercent(float value)
        {
            return (value * 100f).ToString("0.##") + "%";
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value)
                label.text = value;
        }
    }
}
