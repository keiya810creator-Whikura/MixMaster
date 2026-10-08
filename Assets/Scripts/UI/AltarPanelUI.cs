using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MixMaster.Core;
using MixMaster.World;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class AltarPanelUI : MonoBehaviour
    {
        [Header("Map")]
        [Tooltip("空ならMapManager.CurrentMapId、さらに空なら現在Scene名を使います。")]
        [SerializeField] private string mapIdOverride;

        [Header("UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private AltarMonsterEntryUI entryPrefab;
        [SerializeField] private Button closeButton;

        [Header("Optional Manual Monsters")]
        [Tooltip("空ならScene内のEnemySpawnPointから自動収集します。")]
        [SerializeField] private List<MonsterSO> manualMonsters =
            new List<MonsterSO>();

        [Header("Upgrade Cost - Test Defaults")]
        [Tooltip("最終バランス確定前のテスト値です。")]
        [SerializeField] private long spawnUpgradeBaseCost = 1L;
        [SerializeField] private long spawnUpgradeCostStep = 1L;
        [SerializeField] private long dropUpgradeBaseCost = 1L;
        [SerializeField] private long dropUpgradeCostStep = 1L;

        private readonly List<AltarMonsterEntryUI> entries =
            new List<AltarMonsterEntryUI>();

        private AltarManager altarManager;
        private MaterialInventoryManager inventoryManager;
        private SpawnManager spawnManager;
        private MapManager mapManager;

        private string currentMapId = string.Empty;
        private bool subscribed;

        public string CurrentMapId => currentMapId;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }

        private void OnEnable()
        {
            CacheManagers();
            Subscribe();
            Rebuild();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            Unsubscribe();
        }

        public void Rebuild()
        {
            CacheManagers();

            currentMapId =
                ResolveCurrentMapId();

            if (titleText != null)
            {
                titleText.text =
                    string.IsNullOrWhiteSpace(currentMapId)
                        ? "祭壇"
                        : "祭壇  " + currentMapId;
            }

            ClearEntries();

            if (contentRoot == null)
            {
                Debug.LogWarning(
                    "[AltarPanelUI] Content Rootが未設定です。",
                    this);
                return;
            }

            if (entryPrefab == null)
            {
                Debug.LogWarning(
                    "[AltarPanelUI] Entry Prefabが未設定です。",
                    this);
                return;
            }

            List<MonsterSO> monsters =
                ResolveMonsters();

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterSO monster = monsters[i];

                if (monster == null)
                    continue;

                AltarMonsterEntryUI entry =
                    Instantiate(
                        entryPrefab,
                        contentRoot);

                entry.name =
                    "AltarEntry_" +
                    (!string.IsNullOrWhiteSpace(monster.monsterId)
                        ? monster.monsterId
                        : i.ToString());

                entry.Bind(
                    currentMapId,
                    monster,
                    altarManager,
                    inventoryManager,
                    spawnManager,
                    spawnUpgradeBaseCost,
                    spawnUpgradeCostStep,
                    dropUpgradeBaseCost,
                    dropUpgradeCostStep);

                entries.Add(entry);
            }
        }

        public void RefreshAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                    entries[i].Refresh();
            }
        }

        public void Close()
        {
            MapBuildingUIRouter router =
                MapBuildingUIRouter.Instance;

            if (router == null)
            {
                router =
                    FindFirstObjectByType<MapBuildingUIRouter>();
            }

            if (router != null)
            {
                router.CloseAll();
                return;
            }

            gameObject.SetActive(false);
        }

        public void SetEntryPrefab(
            AltarMonsterEntryUI prefab)
        {
            entryPrefab = prefab;
        }

        public void SetContentRoot(
            RectTransform root)
        {
            contentRoot = root;
        }

        public void SetTitleText(
            TMP_Text text)
        {
            titleText = text;
        }

        public void SetCloseButton(
            Button button)
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            closeButton = button;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
        }

        private void CacheManagers()
        {
            if (altarManager == null)
                altarManager = FindFirstObjectByType<AltarManager>();

            if (inventoryManager == null)
            {
                inventoryManager =
                    FindFirstObjectByType<MaterialInventoryManager>();
            }

            if (spawnManager == null)
                spawnManager = FindFirstObjectByType<SpawnManager>();

            if (mapManager == null)
                mapManager = FindFirstObjectByType<MapManager>();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            if (altarManager != null)
                altarManager.AltarProgressChanged += HandleAltarChanged;

            if (inventoryManager != null)
                inventoryManager.MaterialChanged += HandleMaterialChanged;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (altarManager != null)
                altarManager.AltarProgressChanged -= HandleAltarChanged;

            if (inventoryManager != null)
                inventoryManager.MaterialChanged -= HandleMaterialChanged;

            subscribed = false;
        }

        private void HandleAltarChanged(
            AltarProgressRecord record)
        {
            if (record == null ||
                !string.Equals(
                    record.mapId,
                    currentMapId,
                    StringComparison.Ordinal))
            {
                return;
            }

            RefreshAll();
        }

        private void HandleMaterialChanged(
            string materialId,
            long delta,
            long total)
        {
            RefreshAll();
        }

        private List<MonsterSO> ResolveMonsters()
        {
            List<MonsterSO> result =
                new List<MonsterSO>();

            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.Ordinal);

            if (manualMonsters != null &&
                manualMonsters.Count > 0)
            {
                for (int i = 0; i < manualMonsters.Count; i++)
                {
                    AddUniqueMonster(
                        result,
                        ids,
                        manualMonsters[i]);
                }

                SortMonsters(result);
                return result;
            }

            EnemySpawnPoint[] spawnPoints =
                FindObjectsByType<EnemySpawnPoint>(
                    FindObjectsSortMode.None);

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EnemySpawnPoint spawnPoint =
                    spawnPoints[i];

                if (spawnPoint == null)
                    continue;

                AddUniqueMonster(
                    result,
                    ids,
                    spawnPoint.MonsterDefinition);
            }

            SortMonsters(result);
            return result;
        }

        private static void AddUniqueMonster(
            List<MonsterSO> result,
            HashSet<string> ids,
            MonsterSO monster)
        {
            if (monster == null)
                return;

            string id =
                string.IsNullOrWhiteSpace(monster.monsterId)
                    ? monster.name
                    : monster.monsterId;

            if (!ids.Add(id))
                return;

            result.Add(monster);
        }

        private static void SortMonsters(
            List<MonsterSO> monsters)
        {
            monsters.Sort(
                (a, b) =>
                    string.Compare(
                        a != null ? a.monsterId : string.Empty,
                        b != null ? b.monsterId : string.Empty,
                        StringComparison.Ordinal));
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

        private string ResolveCurrentMapId()
        {
            if (!string.IsNullOrWhiteSpace(mapIdOverride))
                return mapIdOverride.Trim();

            if (mapManager != null &&
                !string.IsNullOrWhiteSpace(mapManager.CurrentMapId))
            {
                return mapManager.CurrentMapId;
            }

            return SceneManager
                .GetActiveScene()
                .name;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            spawnUpgradeBaseCost =
                Math.Max(1L, spawnUpgradeBaseCost);

            spawnUpgradeCostStep =
                Math.Max(0L, spawnUpgradeCostStep);

            dropUpgradeBaseCost =
                Math.Max(1L, dropUpgradeBaseCost);

            dropUpgradeCostStep =
                Math.Max(0L, dropUpgradeCostStep);
        }
#endif
    }
}
