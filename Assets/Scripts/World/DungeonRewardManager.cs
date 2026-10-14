using System;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.UI;
using UnityEngine;

namespace MixMaster.World
{
    public sealed class DungeonEquipmentAward
    {
        public EquipmentSO definition;
        public int level;
        public bool wasAutoSold;
    }

    public sealed class DungeonRewardReport
    {
        public string mapName;
        public MaterialSO dungeonMaterial;
        public long actualMaterialAmount;
        public int defeatedMidBosses;
        public readonly List<DungeonEquipmentAward> equipment =
            new List<DungeonEquipmentAward>();
    }

    /// <summary>
    /// Session-bound reward ledger. Roll normal-monster equipment on death,
    /// but grant rewards only once when the final boss chest is absorbed.
    /// Abandoning the dungeon discards unclaimed drops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonRewardManager : MonoBehaviour
    {
        [Header("Rare equipment drop (normal dungeon monsters only)")]
        [SerializeField, Range(0f, 1f)] private float normalEquipmentDropChance =
            0.0005f;

        [Header("Equipment reward level")]
        [SerializeField, Range(1, 100)] private int minimumEquipmentLevel = 40;
        [SerializeField, Range(1, 100)] private int maximumEquipmentLevel = 75;
        [SerializeField, Min(0)] private long equipmentBaseValue = 1L;

        [Header("Boss chest guaranteed rewards")]
        [SerializeField, Min(0)] private int dungeonMaterialPerClear = 10;
        [SerializeField, Min(0)] private int extraMaterialPerMidBoss = 2;
        [SerializeField, Min(1)] private int baseBossEquipmentCount = 1;

        [Header("Chest appearance")]
        [Tooltip("Leave empty to use a simple runtime-generated chest sprite.")]
        [SerializeField] private Sprite chestSprite;
        [SerializeField] private GameObject chestPrefab;

        private readonly List<EquipmentSO> pendingEquipment =
            new List<EquipmentSO>();

        private sealed class GroundEquipmentDrop
        {
            public EquipmentSO definition;
            public DungeonRewardChestPickup pickup;
        }

        private readonly List<GroundEquipmentDrop> groundDrops =
            new List<GroundEquipmentDrop>();

        private DungeonManager dungeon;
        private DungeonEnemySpawner spawner;
        private DungeonMapGenerator mapGenerator;
        private EquipmentManager equipment;
        private MaterialInventoryManager materials;
        private DungeonRewardChestPickup activeChest;
        private DungeonResultPanelUI results;
        private bool chestAbsorbed;
        private bool sessionActive;
        private MapSO sourceMap;

        private void OnEnable()
        {
            dungeon = GetComponent<DungeonManager>() ??
                FindFirstObjectByType<DungeonManager>();
            spawner = GetComponent<DungeonEnemySpawner>() ??
                FindFirstObjectByType<DungeonEnemySpawner>();
            mapGenerator = GetComponent<DungeonMapGenerator>() ??
                FindFirstObjectByType<DungeonMapGenerator>();
            equipment = GetComponent<EquipmentManager>() ??
                FindFirstObjectByType<EquipmentManager>();
            materials = GetComponent<MaterialInventoryManager>() ??
                FindFirstObjectByType<MaterialInventoryManager>();

            if (dungeon != null)
            {
                dungeon.DungeonStarted += HandleDungeonStart;
                dungeon.FinalBossDefeated += HandleFinalBossDefeated;
                dungeon.DungeonEnded += HandleDungeonEnded;
            }
            if (spawner != null)
                spawner.NormalEnemyDefeated += HandleNormalEnemyDefeated;
        }

        private void OnDisable()
        {
            if (dungeon != null)
            {
                dungeon.DungeonStarted -= HandleDungeonStart;
                dungeon.FinalBossDefeated -= HandleFinalBossDefeated;
                dungeon.DungeonEnded -= HandleDungeonEnded;
            }
            if (spawner != null)
                spawner.NormalEnemyDefeated -= HandleNormalEnemyDefeated;
            Cleanup();
        }

        private void HandleDungeonStart(
            string mapId, DungeonGenerationSettings settings)
        {
            Cleanup();
            sourceMap = mapGenerator != null ? mapGenerator.SourceMap : null;
            sessionActive = true;
            chestAbsorbed = false;
        }

        private void HandleNormalEnemyDefeated(
            MonsterSO monster, Vector3 worldPosition)
        {
            if (!sessionActive || chestAbsorbed || dungeon == null ||
                !dungeon.IsDungeonActive)
                return;

            if (UnityEngine.Random.value >= normalEquipmentDropChance)
                return;

            EquipmentSO reward = RollEquipmentDefinition();
            if (reward == null) return;

            // Normal equipment also appears on the ground and is absorbed.
            // The rolled item is stored in the dungeon ledger until clear.
            var obj = new GameObject(
                "DungeonRareEquipment_" + reward.equipmentId);
            obj.transform.position = worldPosition;
            var pickup = obj.AddComponent<DungeonRewardChestPickup>();
            var player = FindFirstObjectByType<PlayerController>();
            var tracked = new GroundEquipmentDrop
            {
                definition = reward,
                pickup = pickup
            };
            groundDrops.Add(tracked);
            pickup.Initialize(reward.icon,
                player != null ? player.transform : null, () =>
                {
                    if (sessionActive && !chestAbsorbed)
                        pendingEquipment.Add(reward);
                    groundDrops.Remove(tracked);
                });
            pickup.SetTint(new Color(0.5f, 0.96f, 1f));
            Debug.Log("[DungeonReward] 雑魚から超レア装備がドロップ: " +
                reward.displayName, this);
        }

        private void HandleFinalBossDefeated(Vector3 position)
        {
            if (!sessionActive || chestAbsorbed ||
                activeChest != null || dungeon == null ||
                !dungeon.IsDungeonActive)
                return;

            // The source map reference is stable until the dungeon ends.
            if (sourceMap == null && mapGenerator != null)
                sourceMap = mapGenerator.SourceMap;

            var player = FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("[DungeonReward] Playerが見つかりません。", this);
                // Stay in session; the chest will seek the player if found
                // on a subsequent frame.
            }

            GameObject chest;
            if (chestPrefab != null)
                chest = Instantiate(chestPrefab, position, Quaternion.identity);
            else
                chest = new GameObject("DungeonFinalBossChest");
            chest.name = "DungeonFinalBossChest";
            chest.transform.position = position;

            // Do not parent it to the dungeon Grid: parent scale must not
            // change the visual or absorption speed.
            activeChest = chest.GetComponent<DungeonRewardChestPickup>();
            if (activeChest == null)
                activeChest = chest.AddComponent<DungeonRewardChestPickup>();

            activeChest.Initialize(chestSprite,
                player != null ? player.transform : null, ClaimAndShowResult);
        }

        private EquipmentSO RollEquipmentDefinition()
        {
            if (sourceMap == null && mapGenerator != null)
                sourceMap = mapGenerator.SourceMap;

            List<EquipmentSO> pool =
                sourceMap != null ? sourceMap.dungeonEquipment : null;
            if (pool == null || pool.Count == 0)
                return null;

            int validCount = 0;
            foreach (EquipmentSO item in pool)
                if (item != null && !string.IsNullOrWhiteSpace(item.equipmentId))
                    validCount++;

            if (validCount == 0) return null;
            int index = UnityEngine.Random.Range(0, validCount);
            foreach (EquipmentSO item in pool)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.equipmentId))
                    continue;
                if (index-- == 0) return item;
            }
            return null;
        }

        private void ClaimAndShowResult()
        {
            // Called at most once even if Update/physics delivers repeated
            // chest pickup callbacks.
            if (chestAbsorbed || !sessionActive || dungeon == null ||
                !dungeon.IsDungeonActive)
                return;

            chestAbsorbed = true;
            activeChest = null;

            // A rare monster drop that has not reached the player yet
            // is still credited at successful final-boss chest collection.
            foreach (GroundEquipmentDrop drop in groundDrops)
            {
                if (drop.definition != null)
                    pendingEquipment.Add(drop.definition);
                if (drop.pickup != null)
                    Destroy(drop.pickup.gameObject);
            }
            groundDrops.Clear();
            if (sourceMap == null && mapGenerator != null)
                sourceMap = mapGenerator.SourceMap;

            var report = new DungeonRewardReport
            {
                mapName = sourceMap != null &&
                    !string.IsNullOrWhiteSpace(sourceMap.displayName)
                    ? sourceMap.displayName : dungeon.EntranceMapId,
                dungeonMaterial = sourceMap != null
                    ? sourceMap.dungeonMaterial : null,
                defeatedMidBosses = dungeon.DefeatedSideBosses
            };

            if (materials != null && report.dungeonMaterial != null)
            {
                long materialTotal = LongMath.SaturatingAdd(
                    Math.Max(0L, dungeonMaterialPerClear),
                    LongMath.SaturatingMultiply(
                        Math.Max(0L, extraMaterialPerMidBoss),
                        Math.Max(0, dungeon.DefeatedSideBosses)));

                report.actualMaterialAmount = materials.AddMaterial(
                    report.dungeonMaterial, materialTotal);
            }

            int guaranteed = dungeon.GetFinalBossEquipmentCount(
                baseBossEquipmentCount);
            for (int i = 0; i < guaranteed; i++)
            {
                EquipmentSO definition = RollEquipmentDefinition();
                if (definition != null)
                    pendingEquipment.Add(definition);
            }

            if (equipment != null)
            {
                foreach (EquipmentSO definition in pendingEquipment)
                {
                    int min = Mathf.Clamp(minimumEquipmentLevel, 1, 100);
                    int max = Mathf.Clamp(maximumEquipmentLevel, min, 100);
                    int level = UnityEngine.Random.Range(min, max + 1);

                    EquipmentRecord created =
                        equipment.CreateEquipment(
                            definition.equipmentId, level,
                            equipmentBaseValue, true);
                    if (created == null) continue;

                    report.equipment.Add(new DungeonEquipmentAward
                    {
                        definition = definition,
                        level = created.level,
                        wasAutoSold = equipment.FindEquipment(
                            created.uniqueId) == null
                    });
                }
            }

            pendingEquipment.Clear();
            SaveManager save = GetComponent<SaveManager>() ??
                FindFirstObjectByType<SaveManager>();
            if (save != null && !save.SaveGame())
                Debug.LogWarning(
                    "[DungeonReward] 報酬は所持品に反映されましたが、" +
                    "セーブの確認ができませんでした。", this);

            mapGenerator?.SetTemporaryReturnVisible(false);
            var player = FindFirstObjectByType<PlayerController>();
            player?.SetCombatMovementLocked(true);
            var joystick = FindFirstObjectByType<FloatingJoystick>();
            joystick?.SetInteractionEnabled(false);

            results = DungeonResultPanelUI.Show(report, ReturnFromResult);
        }

        private void ReturnFromResult()
        {
            if (!chestAbsorbed) return;

            // Dismiss UI first, then release movement controls and restore
            // the field through the existing dungeon manager.
            if (results != null)
            {
                results.Close();
                results = null;
            }
            var player = FindFirstObjectByType<PlayerController>();
            player?.SetCombatMovementLocked(false);
            var joystick = FindFirstObjectByType<FloatingJoystick>();
            joystick?.SetInteractionEnabled(true);

            if (mapGenerator != null)
                mapGenerator.ReturnToEntrance();
            else
                dungeon?.EndDungeon();

            SaveManager save = GetComponent<SaveManager>() ??
                FindFirstObjectByType<SaveManager>();
            save?.SaveGame();
        }

        private void HandleDungeonEnded()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            sessionActive = false;
            chestAbsorbed = false;
            pendingEquipment.Clear();
            foreach (GroundEquipmentDrop drop in groundDrops)
                if (drop.pickup != null)
                    Destroy(drop.pickup.gameObject);
            groundDrops.Clear();
            sourceMap = null;
            if (activeChest != null)
                Destroy(activeChest.gameObject);
            activeChest = null;
            if (results != null)
            {
                results.Close();
                results = null;
            }
            var player = FindFirstObjectByType<PlayerController>();
            player?.SetCombatMovementLocked(false);
            var joystick = FindFirstObjectByType<FloatingJoystick>();
            joystick?.SetInteractionEnabled(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimumEquipmentLevel = Mathf.Clamp(minimumEquipmentLevel, 1, 100);
            maximumEquipmentLevel = Mathf.Clamp(maximumEquipmentLevel,
                minimumEquipmentLevel, 100);
            equipmentBaseValue = Math.Max(0L, equipmentBaseValue);
            dungeonMaterialPerClear = Mathf.Max(0, dungeonMaterialPerClear);
            extraMaterialPerMidBoss = Mathf.Max(0, extraMaterialPerMidBoss);
            baseBossEquipmentCount = Mathf.Max(1, baseBossEquipmentCount);
        }
#endif
    }
}
