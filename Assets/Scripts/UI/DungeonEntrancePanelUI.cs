using System;
using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Dungeon entrance: map-wide title-rate upgrade, challenge and close.
    /// All controls are authored in the scene prefab and reused by the router.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEntrancePanelUI : MonoBehaviour
    {
        [Header("Optional map override")]
        [SerializeField] private MapSO mapOverride;
        [SerializeField] private string mapIdOverride;

        [Header("Dungeon generation settings")]
        [Tooltip("Enabled: use the DungeonManager default generation settings " +
            "configured in the Manager Settings Prefab Inspector.")]
        [SerializeField] private bool useManagerDungeonSettings = true;
        [SerializeField] private DungeonGenerationSettings generationSettings =
            new DungeonGenerationSettings();

        [Header("UI references")]
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private Image materialIcon;
        [SerializeField] private TMP_Text materialNameText;
        [SerializeField] private TMP_Text materialCountText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text rateText;
        [SerializeField] private TMP_Text upgradeCostText;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button closeButton;

        private AltarManager altar;
        private MaterialInventoryManager materials;
        private MapManager maps;
        private MapSO resolvedMap;
        private string currentMapId = string.Empty;
        private bool listening;
        private bool buttonsWired;

        [Serializable]
        private sealed class DungeonUpgradeRow
        {
            public DungeonUpgradeType type;
            public TMP_Text heading;
            public TMP_Text effect;
            public TMP_Text cost;
            public TMP_Text applied;
            public Button upgradeButton;
            public Button minButton;
            public Button minusButton;
            public Button plusButton;
            public Button maxButton;
        }

        [SerializeField] private DungeonUpgradeRow[] dungeonUpgradeRows = Array.Empty<DungeonUpgradeRow>();

        private void Awake()
        {
            WireButtons();
        }

        private void WireButtons()
        {
            if (buttonsWired || upgradeButton == null ||
                challengeButton == null || closeButton == null)
                return;
            HoldToUpgradeButton.Attach(
                upgradeButton, TryUpgradeTitleRate, SaveUpgradeProgress);
            challengeButton.onClick.AddListener(Challenge);
            closeButton.onClick.AddListener(Close);
            foreach (DungeonUpgradeRow row in dungeonUpgradeRows)
            {
                if (row == null) continue;
                DungeonUpgradeType type = row.type;
                HoldToUpgradeButton.Attach(row.upgradeButton,
                    () => TryUpgradeDungeon(type), SaveUpgradeProgress);
                row.minButton.onClick.AddListener(() => SetDungeonAppliedLevel(type, 0));
                row.minusButton.onClick.AddListener(() => AdjustDungeonAppliedLevel(type, -1));
                row.plusButton.onClick.AddListener(() => AdjustDungeonAppliedLevel(type, 1));
                row.maxButton.onClick.AddListener(() => SetDungeonAppliedLevel(type,
                    altar != null ? altar.GetDungeonUpgradeLevel(currentMapId, type) : 0));
            }
            buttonsWired = true;
        }

        private void OnEnable()
        {
            WireButtons();
            CacheManagers();
            ResolveMap();
            Subscribe();
            Refresh();
            transform.SetAsLastSibling();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            // HoldToUpgradeButton unregisters its click handler in OnDestroy.
            if (challengeButton != null)
                challengeButton.onClick.RemoveListener(Challenge);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void SetMapDefinition(MapSO definition)
        {
            mapOverride = definition;
            if (isActiveAndEnabled)
            {
                ResolveMap();
                Refresh();
            }
        }

        public void Refresh()
        {
            CacheManagers();
            if (string.IsNullOrEmpty(currentMapId))
                ResolveMap();

            if (mapNameText != null)
                mapNameText.text = resolvedMap != null &&
                    !string.IsNullOrWhiteSpace(resolvedMap.displayName)
                    ? resolvedMap.displayName + " のダンジョン"
                    : "ダンジョン  " + currentMapId;

            MaterialSO dungeonMaterial =
                resolvedMap != null ? resolvedMap.dungeonMaterial : null;
            long owned = materials != null && dungeonMaterial != null
                ? materials.GetAmount(dungeonMaterial) : 0L;

            if (materialIcon != null)
            {
                materialIcon.sprite =
                    dungeonMaterial != null ? dungeonMaterial.icon : null;
                materialIcon.enabled = materialIcon.sprite != null;
            }
            if (materialNameText != null)
                materialNameText.text = dungeonMaterial != null
                    ? (!string.IsNullOrWhiteSpace(dungeonMaterial.displayName)
                        ? dungeonMaterial.displayName : dungeonMaterial.materialId)
                    : "ダンジョン素材が未設定です";
            if (materialCountText != null)
                materialCountText.text = "所持数  " + owned.ToString("N0");

            int level = altar != null &&
                !string.IsNullOrWhiteSpace(currentMapId)
                ? altar.GetOrCreateMap(currentMapId).titledMonsterRateLevel : 0;
            level = Mathf.Clamp(level, 0, AltarBalance.TitleRateMaxLevel);
            bool maxed = level >= AltarBalance.TitleRateMaxLevel;
            long cost = maxed ? 0L : AltarBalance.GetUpgradeCost(level);

            if (levelText != null)
                levelText.text = "称号付与率強化  Lv " + level + " / " +
                    AltarBalance.TitleRateMaxLevel;
            if (rateText != null)
            {
                float current = AltarBalance.GetMapTitleRate(level) * 100f;
                rateText.text = maxed
                    ? "称号付与率  " + current.ToString("0.00") + "%  MAX"
                    : "称号付与率  " + current.ToString("0.00") +
                      "%  →  " +
                      (AltarBalance.GetMapTitleRate(level + 1) * 100f)
                      .ToString("0.00") + "%";
            }
            if (upgradeCostText != null)
                upgradeCostText.text = maxed ? "強化完了" :
                    "必要ダンジョン素材  ×" + cost.ToString("N0");

            if (upgradeButton != null)
                upgradeButton.interactable =
                    altar != null && materials != null &&
                    dungeonMaterial != null &&
                    !maxed && owned >= cost &&
                    !string.IsNullOrWhiteSpace(currentMapId);
            if (challengeButton != null)
            {
                DungeonManager dungeon =
                    FindFirstObjectByType<DungeonManager>();
                challengeButton.interactable =
                    !string.IsNullOrWhiteSpace(currentMapId) &&
                    dungeon != null && !dungeon.IsDungeonActive;
            }
            if (noticeText != null)
                noticeText.text = dungeonMaterial == null
                    ? "このマップのMapSOとダンジョン素材を設定してください。"
                    : "強化はこのマップ専用です。他のマップには影響しません。";

            foreach (DungeonUpgradeRow row in dungeonUpgradeRows)
            {
                if (row == null) continue;
                DungeonUpgradeType type = row.type;
                int upgradeLevel = altar != null &&
                    !string.IsNullOrWhiteSpace(currentMapId)
                    ? altar.GetDungeonUpgradeLevel(currentMapId, type) : 0;
                int cap = DungeonUpgradeBalance.MaxLevel(type);
                bool finished = upgradeLevel >= cap;
                long needed = finished ? 0L :
                    DungeonUpgradeBalance.Cost(type, upgradeLevel);
                int appliedLevel = altar != null &&
                    !string.IsNullOrWhiteSpace(currentMapId)
                    ? altar.GetDungeonAppliedLevel(currentMapId, type) : 0;
                row.heading.text = DungeonUpgradeBalance.Name(type) +
                    "  購入Lv " + upgradeLevel + " / " + cap;
                row.applied.text = "適用 Lv " + appliedLevel +
                    " / " + upgradeLevel;
                row.minButton.interactable = appliedLevel > 0;
                row.minusButton.interactable = appliedLevel > 0;
                row.plusButton.interactable = appliedLevel < upgradeLevel;
                row.maxButton.interactable = appliedLevel < upgradeLevel;
                row.effect.text = finished
                    ? DungeonUpgradeBalance.EffectText(type, upgradeLevel) +
                        "  MAX"
                    : DungeonUpgradeBalance.EffectText(type, upgradeLevel) +
                        " → " +
                        DungeonUpgradeBalance.EffectText(type, upgradeLevel + 1);
                row.cost.text = finished ? "最大強化済み" :
                    "素材 ×" + needed.ToString("N0") +
                    "   （所持 " + owned.ToString("N0") + "）";
                row.upgradeButton.interactable =
                    altar != null && materials != null &&
                    dungeonMaterial != null && !finished &&
                    owned >= needed &&
                    !string.IsNullOrWhiteSpace(currentMapId);
            }
        }

        private void SetDungeonAppliedLevel(
            DungeonUpgradeType type, int requestedLevel)
        {
            if (altar == null || string.IsNullOrWhiteSpace(currentMapId))
                return;

            if (altar.SetDungeonAppliedLevel(
                currentMapId, type, requestedLevel))
            {
                SaveManager save = FindFirstObjectByType<SaveManager>();
                save?.SaveGame();
            }
            Refresh();
        }

        private void AdjustDungeonAppliedLevel(
            DungeonUpgradeType type, int delta)
        {
            if (altar == null) return;
            int current = altar.GetDungeonAppliedLevel(currentMapId, type);
            SetDungeonAppliedLevel(type, current + delta);
        }

        private bool TryUpgradeDungeon(DungeonUpgradeType type)
        {
            if (altar == null || materials == null || resolvedMap == null ||
                resolvedMap.dungeonMaterial == null) return false;

            bool success = altar.TryUpgradeDungeon(
                currentMapId, type, resolvedMap.dungeonMaterial, materials);
            Refresh();
            return success;
        }

        private bool TryUpgradeTitleRate()
        {
            if (altar == null || materials == null || resolvedMap == null ||
                resolvedMap.dungeonMaterial == null)
                return false;

            bool success = altar.TryUpgradeMapTitleRate(
                currentMapId, resolvedMap.dungeonMaterial, materials);
            Refresh();
            return success;
        }

        private void SaveUpgradeProgress()
        {
            SaveManager save = FindFirstObjectByType<SaveManager>();
            save?.SaveGame();
        }

        private void Challenge()
        {
            DungeonManager dungeon = FindFirstObjectByType<DungeonManager>();
            if (dungeon == null || string.IsNullOrWhiteSpace(currentMapId))
                return;

            // Release the entrance locks before generating the dungeon.
            string entranceMap = currentMapId;

            // Persist the safe field position before switching to the
            // transient dungeon map. In-dungeon autosaves also record this
            // position, so restarting never strands the player in empty space.
            SaveManager save = FindFirstObjectByType<SaveManager>();
            if (save != null)
                save.SaveGame();

            Close();

            // Copy the Inspector settings. DungeonManager fills in a random
            // seed when it is zero; never mutate the serialized template.
            DungeonGenerationSettings source =
                useManagerDungeonSettings
                    ? dungeon.GetDefaultSettings()
                    : (generationSettings ?? new DungeonGenerationSettings());
            var requested = new DungeonGenerationSettings
            {
                roomCount = source.roomCount,
                branchCount = source.branchCount,
                sideBossCount = source.sideBossCount,
                seed = source.seed
            };
            dungeon.BeginDungeon(requested, entranceMap);
        }

        public void Close()
        {
            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router == null)
                router = FindFirstObjectByType<MapBuildingUIRouter>();
            if (router != null && router.IsOpen)
                router.CloseAll();
            else
                gameObject.SetActive(false);
        }

        private void CacheManagers()
        {
            if (altar == null) altar = FindFirstObjectByType<AltarManager>();
            if (materials == null)
                materials = FindFirstObjectByType<MaterialInventoryManager>();
            if (maps == null) maps = FindFirstObjectByType<MapManager>();
        }

        private void ResolveMap()
        {
            currentMapId = !string.IsNullOrWhiteSpace(mapIdOverride)
                ? mapIdOverride.Trim()
                : maps != null && !string.IsNullOrWhiteSpace(maps.CurrentMapId)
                    ? maps.CurrentMapId
                    : SceneManager.GetActiveScene().name;

            resolvedMap = mapOverride;
            if (resolvedMap == null)
            {
                TilemapMapRoot[] roots =
                    FindObjectsByType<TilemapMapRoot>(
                        FindObjectsSortMode.None);
                for (int i = 0; i < roots.Length; i++)
                {
                    MapSO candidate = roots[i] != null
                        ? roots[i].MapDefinition : null;
                    if (candidate == null) continue;
                    if (string.Equals(candidate.mapId, currentMapId,
                        StringComparison.Ordinal))
                    {
                        resolvedMap = candidate;
                        break;
                    }
                }
                if (resolvedMap == null && roots.Length == 1 &&
                    roots[0] != null)
                    resolvedMap = roots[0].MapDefinition;
            }

            if (resolvedMap != null &&
                string.IsNullOrWhiteSpace(mapIdOverride) &&
                !string.IsNullOrWhiteSpace(resolvedMap.mapId))
                currentMapId = resolvedMap.mapId;
        }

        private void Subscribe()
        {
            if (listening) return;
            if (altar != null)
                altar.MapAltarProgressChanged += OnMapUpgrade;
            if (materials != null)
                materials.MaterialChanged += OnMaterialChanged;
            listening = true;
        }

        private void Unsubscribe()
        {
            if (!listening) return;
            if (altar != null)
                altar.MapAltarProgressChanged -= OnMapUpgrade;
            if (materials != null)
                materials.MaterialChanged -= OnMaterialChanged;
            listening = false;
        }

        private void OnMapUpgrade(MapAltarProgressRecord record)
        {
            if (record != null && record.mapId == currentMapId)
                Refresh();
        }

        private void OnMaterialChanged(string materialId,
            long delta, long total)
        {
            if (resolvedMap != null && resolvedMap.dungeonMaterial != null &&
                resolvedMap.dungeonMaterial.materialId == materialId)
                Refresh();
        }

    }
}
