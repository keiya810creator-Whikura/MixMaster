using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Core;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class AltarMonsterEntryUI : MonoBehaviour
    {
        [Header("Monster")]
        [SerializeField] private Image monsterIcon;
        [SerializeField] private TMP_Text monsterNameText;

        [Header("Material")]
        [SerializeField] private Image materialIcon;
        [SerializeField] private TMP_Text materialNameText;
        [SerializeField] private TMP_Text materialCountText;

        [Header("Spawn Upgrade")]
        [SerializeField] private TMP_Text spawnLevelText;
        [SerializeField] private TMP_Text spawnValueText;
        [SerializeField] private TMP_Text spawnCostText;
        [SerializeField] private Button spawnUpgradeButton;

        [Header("Monster Drop Upgrade")]
        [SerializeField] private TMP_Text dropLevelText;
        [SerializeField] private TMP_Text dropValueText;
        [SerializeField] private TMP_Text dropCostText;
        [SerializeField] private Button dropUpgradeButton;

        private string mapId;
        private MonsterSO monster;
        private AltarManager altarManager;
        private MaterialInventoryManager inventoryManager;
        private SpawnManager spawnManager;

        private long spawnBaseCost;
        private long spawnCostStep;
        private long dropBaseCost;
        private long dropCostStep;

        private void Awake()
        {
            if (spawnUpgradeButton != null)
            {
                spawnUpgradeButton.onClick.RemoveListener(
                    UpgradeSpawn);

                spawnUpgradeButton.onClick.AddListener(
                    UpgradeSpawn);
            }

            if (dropUpgradeButton != null)
            {
                dropUpgradeButton.onClick.RemoveListener(
                    UpgradeDrop);

                dropUpgradeButton.onClick.AddListener(
                    UpgradeDrop);
            }
        }

        private void OnDestroy()
        {
            if (spawnUpgradeButton != null)
            {
                spawnUpgradeButton.onClick.RemoveListener(
                    UpgradeSpawn);
            }

            if (dropUpgradeButton != null)
            {
                dropUpgradeButton.onClick.RemoveListener(
                    UpgradeDrop);
            }
        }

        public void Bind(
            string currentMapId,
            MonsterSO definition,
            AltarManager altar,
            MaterialInventoryManager inventory,
            SpawnManager spawn,
            long newSpawnBaseCost,
            long newSpawnCostStep,
            long newDropBaseCost,
            long newDropCostStep)
        {
            mapId =
                currentMapId ?? string.Empty;

            monster = definition;
            altarManager = altar;
            inventoryManager = inventory;
            spawnManager = spawn;

            spawnBaseCost =
                Math.Max(1L, newSpawnBaseCost);

            spawnCostStep =
                Math.Max(0L, newSpawnCostStep);

            dropBaseCost =
                Math.Max(1L, newDropBaseCost);

            dropCostStep =
                Math.Max(0L, newDropCostStep);

            Refresh();
        }

        public void Refresh()
        {
            if (monster == null ||
                altarManager == null ||
                inventoryManager == null)
            {
                SetButtonsInteractable(false, false);
                return;
            }

            MaterialSO material =
                monster.uniqueMaterial;

            AltarProgressRecord record =
                altarManager.GetOrCreateMonster(
                    mapId,
                    monster.monsterId);

            long owned =
                material != null
                    ? inventoryManager.GetAmount(material)
                    : 0L;

            RefreshIdentity(
                material,
                owned);

            RefreshSpawn(
                record,
                material,
                owned);

            RefreshMonsterDrop(
                record,
                material,
                owned);
        }

        private void RefreshIdentity(
            MaterialSO material,
            long owned)
        {
            if (monsterIcon != null)
            {
                monsterIcon.sprite =
                    monster.icon != null
                        ? monster.icon
                        : monster.sprite;

                monsterIcon.enabled =
                    monsterIcon.sprite != null;
            }

            if (monsterNameText != null)
            {
                monsterNameText.text =
                    !string.IsNullOrWhiteSpace(monster.displayName)
                        ? monster.displayName
                        : monster.monsterId;
            }

            if (materialIcon != null)
            {
                materialIcon.sprite =
                    material != null
                        ? material.icon
                        : null;

                materialIcon.enabled =
                    materialIcon.sprite != null;
            }

            if (materialNameText != null)
            {
                materialNameText.text =
                    material == null
                        ? "素材未設定"
                        : (!string.IsNullOrWhiteSpace(material.displayName)
                            ? material.displayName
                            : material.materialId);
            }

            if (materialCountText != null)
            {
                materialCountText.text =
                    owned.ToString("N0") +
                    " / " +
                    MaterialInventoryManager
                        .MaxMaterialAmount
                        .ToString("N0");
            }
        }

        private void RefreshSpawn(
            AltarProgressRecord record,
            MaterialSO material,
            long owned)
        {
            int level =
                Mathf.Clamp(
                    record.spawnEfficiencyLevel,
                    0,
                    AltarBalance.SpawnEfficiencyMaxLevel);

            bool isMax =
                level >=
                AltarBalance.SpawnEfficiencyMaxLevel;

            if (spawnLevelText != null)
            {
                spawnLevelText.text =
                    "Lv " +
                    level +
                    " / " +
                    AltarBalance.SpawnEfficiencyMaxLevel;
            }

            if (spawnValueText != null)
            {
                float delay =
                    monster.baseRespawnInterval;

                bool canRespawn = true;

                if (spawnManager != null)
                {
                    canRespawn =
                        spawnManager.TryGetRespawnDelay(
                            monster.baseRespawnInterval,
                            level,
                            AltarBalance.SpawnEfficiencyMaxLevel,
                            record.postMaxRespawnEnabled,
                            record.postMaxRespawnDelayScale,
                            out delay);
                }

                if (!canRespawn)
                {
                    spawnValueText.text =
                        "出現停止";
                }
                else if (delay <= 0.0001f)
                {
                    spawnValueText.text =
                        "リスポーン: 即時";
                }
                else
                {
                    spawnValueText.text =
                        "リスポーン: " +
                        delay.ToString("0.##") +
                        "秒";
                }
            }

            long cost =
                GetCost(
                    spawnBaseCost,
                    spawnCostStep,
                    level);

            if (spawnCostText != null)
            {
                spawnCostText.text =
                    isMax
                        ? "MAX"
                        : "必要素材 ×" +
                          cost.ToString("N0");
            }

            if (spawnUpgradeButton != null)
            {
                spawnUpgradeButton.interactable =
                    !isMax &&
                    material != null &&
                    owned >= cost;
            }
        }

        private void RefreshMonsterDrop(
            AltarProgressRecord record,
            MaterialSO material,
            long owned)
        {
            int level =
                Mathf.Clamp(
                    record.dropRateLevel,
                    0,
                    AltarBalance.MonsterDropRateMaxLevel);

            bool isMax =
                level >=
                AltarBalance.MonsterDropRateMaxLevel;

            if (dropLevelText != null)
            {
                dropLevelText.text =
                    "Lv " +
                    level +
                    " / " +
                    AltarBalance.MonsterDropRateMaxLevel;
            }

            if (dropValueText != null)
            {
                float rate =
                    altarManager.GetMonsterBodyDropRate(
                        mapId,
                        monster);

                dropValueText.text =
                    "モンスター獲得率: " +
                    (rate * 100f).ToString("0.###") +
                    "%";
            }

            long cost =
                GetCost(
                    dropBaseCost,
                    dropCostStep,
                    level);

            if (dropCostText != null)
            {
                dropCostText.text =
                    isMax
                        ? "MAX"
                        : "必要素材 ×" +
                          cost.ToString("N0");
            }

            if (dropUpgradeButton != null)
            {
                dropUpgradeButton.interactable =
                    !isMax &&
                    material != null &&
                    owned >= cost;
            }
        }

        private void UpgradeSpawn()
        {
            if (!CanUpgrade(
                    true,
                    out AltarProgressRecord record,
                    out MaterialSO material,
                    out long cost))
            {
                return;
            }

            if (!inventoryManager.TryConsume(
                    material,
                    cost))
            {
                return;
            }

            altarManager.DonateMaterial(
                mapId,
                monster.monsterId,
                cost);

            altarManager.SetSpawnEfficiencyLevel(
                mapId,
                monster.monsterId,
                record.spawnEfficiencyLevel + 1);

            Refresh();
        }

        private void UpgradeDrop()
        {
            if (!CanUpgrade(
                    false,
                    out AltarProgressRecord record,
                    out MaterialSO material,
                    out long cost))
            {
                return;
            }

            if (!inventoryManager.TryConsume(
                    material,
                    cost))
            {
                return;
            }

            altarManager.DonateMaterial(
                mapId,
                monster.monsterId,
                cost);

            altarManager.SetDropRateLevel(
                mapId,
                monster.monsterId,
                record.dropRateLevel + 1);

            Refresh();
        }

        private bool CanUpgrade(
            bool spawn,
            out AltarProgressRecord record,
            out MaterialSO material,
            out long cost)
        {
            record = null;
            material = null;
            cost = 0L;

            if (monster == null ||
                altarManager == null ||
                inventoryManager == null)
            {
                return false;
            }

            material =
                monster.uniqueMaterial;

            if (material == null)
                return false;

            record =
                altarManager.GetOrCreateMonster(
                    mapId,
                    monster.monsterId);

            int level =
                spawn
                    ? record.spawnEfficiencyLevel
                    : record.dropRateLevel;

            int maxLevel =
                spawn
                    ? AltarBalance.SpawnEfficiencyMaxLevel
                    : AltarBalance.MonsterDropRateMaxLevel;

            if (level >= maxLevel)
                return false;

            cost =
                spawn
                    ? GetCost(
                        spawnBaseCost,
                        spawnCostStep,
                        level)
                    : GetCost(
                        dropBaseCost,
                        dropCostStep,
                        level);

            return inventoryManager.GetAmount(material) >= cost;
        }

        private static long GetCost(
            long baseCost,
            long step,
            int currentLevel)
        {
            long levelCost =
                LongMath.SaturatingMultiply(
                    Math.Max(0L, step),
                    Math.Max(0, currentLevel));

            return Math.Max(
                1L,
                LongMath.SaturatingAdd(
                    Math.Max(1L, baseCost),
                    levelCost));
        }

        private void SetButtonsInteractable(
            bool spawn,
            bool drop)
        {
            if (spawnUpgradeButton != null)
                spawnUpgradeButton.interactable = spawn;

            if (dropUpgradeButton != null)
                dropUpgradeButton.interactable = drop;
        }

        public void SetMonsterIcon(Image value) => monsterIcon = value;
        public void SetMonsterNameText(TMP_Text value) => monsterNameText = value;
        public void SetMaterialIcon(Image value) => materialIcon = value;
        public void SetMaterialNameText(TMP_Text value) => materialNameText = value;
        public void SetMaterialCountText(TMP_Text value) => materialCountText = value;
        public void SetSpawnLevelText(TMP_Text value) => spawnLevelText = value;
        public void SetSpawnValueText(TMP_Text value) => spawnValueText = value;
        public void SetSpawnCostText(TMP_Text value) => spawnCostText = value;
        public void SetSpawnUpgradeButton(Button value) => spawnUpgradeButton = value;
        public void SetDropLevelText(TMP_Text value) => dropLevelText = value;
        public void SetDropValueText(TMP_Text value) => dropValueText = value;
        public void SetDropCostText(TMP_Text value) => dropCostText = value;
        public void SetDropUpgradeButton(Button value) => dropUpgradeButton = value;
    }
}
