using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class AltarManager : MonoBehaviour
    {
        private readonly List<AltarProgressRecord> monsterRecords =
            new List<AltarProgressRecord>();

        private readonly List<MapAltarProgressRecord> mapRecords =
            new List<MapAltarProgressRecord>();

        public IReadOnlyList<AltarProgressRecord> Records => monsterRecords;
        public IReadOnlyList<AltarProgressRecord> MonsterRecords => monsterRecords;
        public IReadOnlyList<MapAltarProgressRecord> MapRecords => mapRecords;

        public event Action<AltarProgressRecord> AltarProgressChanged;
        public event Action<MapAltarProgressRecord> MapAltarProgressChanged;

        public void RestoreAltars(
            IReadOnlyList<AltarProgressRecord> savedMonsters,
            IReadOnlyList<MapAltarProgressRecord> savedMaps)
        {
            monsterRecords.Clear();
            mapRecords.Clear();

            if (savedMonsters != null)
            {
                for (int i = 0; i < savedMonsters.Count; i++)
                {
                    AltarProgressRecord record = savedMonsters[i];
                    if (record == null)
                        continue;

                    record.spawnEfficiencyLevel = Mathf.Clamp(
                        record.spawnEfficiencyLevel, 0,
                        AltarBalance.SpawnEfficiencyMaxLevel);
                    record.dropRateLevel = Mathf.Clamp(
                        record.dropRateLevel, 0,
                        AltarBalance.MonsterDropRateMaxLevel);
                    record.postMaxRespawnDelayScale =
                        Mathf.Clamp01(record.postMaxRespawnDelayScale);
                    record.postMaxMonsterDropRateScale =
                        Mathf.Clamp01(record.postMaxMonsterDropRateScale);

                    monsterRecords.Add(record);
                }
            }

            if (savedMaps != null)
            {
                for (int i = 0; i < savedMaps.Count; i++)
                {
                    MapAltarProgressRecord record = savedMaps[i];
                    if (record == null)
                        continue;

                    record.titledMonsterRateLevel =
                        Mathf.Clamp(record.titledMonsterRateLevel,
                            0, AltarBalance.TitleRateMaxLevel);
                    foreach (DungeonUpgradeType type in DungeonUpgradeBalance.All)
                    {
                        DungeonUpgradeBalance.SetLevel(record, type,
                            DungeonUpgradeBalance.Level(record, type));
                        if (record.dungeonAppliedLevelsConfigured)
                            DungeonUpgradeBalance.SetAppliedLevel(record, type,
                                DungeonUpgradeBalance.AppliedLevel(record, type));
                    }
                    mapRecords.Add(record);
                }
            }

            foreach (AltarProgressRecord record in monsterRecords)
                AltarProgressChanged?.Invoke(record);

            foreach (MapAltarProgressRecord record in mapRecords)
                MapAltarProgressChanged?.Invoke(record);
        }

        public AltarProgressRecord GetOrCreate(string mapId, string monsterId)
        {
            return GetOrCreateMonster(mapId, monsterId);
        }

        public AltarProgressRecord GetOrCreateMonster(string mapId, string monsterId)
        {
            mapId ??= string.Empty;
            monsterId ??= string.Empty;

            AltarProgressRecord record =
                monsterRecords.Find(x =>
                    x.mapId == mapId &&
                    x.monsterId == monsterId);

            if (record != null)
                return record;

            record = new AltarProgressRecord
            {
                mapId = mapId,
                monsterId = monsterId
            };

            monsterRecords.Add(record);
            return record;
        }

        public MapAltarProgressRecord GetOrCreateMap(string mapId)
        {
            mapId ??= string.Empty;

            MapAltarProgressRecord record =
                mapRecords.Find(x => x.mapId == mapId);

            if (record != null)
                return record;

            record = new MapAltarProgressRecord
            {
                mapId = mapId
            };

            mapRecords.Add(record);
            return record;
        }

        public void DonateMaterial(
            string mapId,
            string monsterId,
            long amount)
        {
            if (amount <= 0L)
                return;

            AltarProgressRecord record =
                GetOrCreateMonster(mapId, monsterId);

            record.donatedMaterial =
                LongMath.SaturatingAdd(
                    record.donatedMaterial,
                    amount);

            AltarProgressChanged?.Invoke(record);
        }

        public void DonateDungeonMaterial(string mapId, long amount)
        {
            if (amount <= 0L)
                return;

            MapAltarProgressRecord record =
                GetOrCreateMap(mapId);

            record.donatedDungeonMaterial =
                LongMath.SaturatingAdd(
                    record.donatedDungeonMaterial,
                    amount);

            MapAltarProgressChanged?.Invoke(record);
        }

        public void SetSpawnEfficiencyLevel(
            string mapId,
            string monsterId,
            int level)
        {
            AltarProgressRecord record =
                GetOrCreateMonster(mapId, monsterId);

            record.spawnEfficiencyLevel =
                Mathf.Clamp(
                    level,
                    0,
                    AltarBalance.SpawnEfficiencyMaxLevel);

            AltarProgressChanged?.Invoke(record);
        }

        public void SetDropRateLevel(
            string mapId,
            string monsterId,
            int level)
        {
            AltarProgressRecord record =
                GetOrCreateMonster(mapId, monsterId);

            record.dropRateLevel =
                Mathf.Clamp(
                    level,
                    0,
                    AltarBalance.MonsterDropRateMaxLevel);

            AltarProgressChanged?.Invoke(record);
        }

        /// <summary>
        /// Title rate is map-wide because dungeon material affects
        /// every monster on that map.
        /// </summary>
        public void SetMapTitledMonsterRateLevel(
            string mapId,
            int level)
        {
            MapAltarProgressRecord record =
                GetOrCreateMap(mapId);

            record.titledMonsterRateLevel =
                Mathf.Clamp(level, 0, AltarBalance.TitleRateMaxLevel);

            MapAltarProgressChanged?.Invoke(record);
        }

        public float GetMapTitleRate(string mapId)
        {
            MapAltarProgressRecord record = GetOrCreateMap(mapId);
            return AltarBalance.GetMapTitleRate(record.titledMonsterRateLevel);
        }

        /// <summary>
        /// Atomically checks availability and consumes the map's dungeon
        /// material before advancing its level. Does not upgrade at MAX.
        /// </summary>
        public bool TryUpgradeMapTitleRate(
            string mapId, MaterialSO dungeonMaterial,
            MaterialInventoryManager inventory)
        {
            if (string.IsNullOrWhiteSpace(mapId) ||
                dungeonMaterial == null || inventory == null ||
                string.IsNullOrWhiteSpace(dungeonMaterial.materialId))
                return false;

            MapAltarProgressRecord record = GetOrCreateMap(mapId);
            int level = Mathf.Clamp(record.titledMonsterRateLevel,
                0, AltarBalance.TitleRateMaxLevel);
            if (level >= AltarBalance.TitleRateMaxLevel)
                return false;

            long cost = AltarBalance.GetUpgradeCost(level);
            if (!inventory.TryConsume(dungeonMaterial, cost))
                return false;

            record.donatedDungeonMaterial = LongMath.SaturatingAdd(
                record.donatedDungeonMaterial, cost);
            record.titledMonsterRateLevel = level + 1;
            MapAltarProgressChanged?.Invoke(record);
            return true;
        }

        /// <summary>
        /// Spends the dungeon material belonging to exactly this map.
        /// Successful upgrades are persisted through existing mapAltars.
        /// </summary>
        public bool TryUpgradeDungeon(string mapId,
            DungeonUpgradeType type, MaterialSO dungeonMaterial,
            MaterialInventoryManager inventory)
        {
            if (string.IsNullOrWhiteSpace(mapId) ||
                dungeonMaterial == null || inventory == null ||
                string.IsNullOrWhiteSpace(dungeonMaterial.materialId) ||
                DungeonUpgradeBalance.MaxLevel(type) <= 0)
                return false;

            MapAltarProgressRecord record = GetOrCreateMap(mapId);
            int level = DungeonUpgradeBalance.Level(record, type);
            if (level >= DungeonUpgradeBalance.MaxLevel(type))
                return false;

            long cost = DungeonUpgradeBalance.Cost(type, level);
            if (!inventory.TryConsume(dungeonMaterial, cost))
                return false;

            record.donatedDungeonMaterial = LongMath.SaturatingAdd(
                record.donatedDungeonMaterial, cost);
            int previouslyApplied =
                DungeonUpgradeBalance.AppliedLevel(record, type);
            DungeonUpgradeBalance.SetLevel(record, type, level + 1);
            // When the player has configured an effective level and it was
            // previously MAX, automatically follow the newly unlocked Lv.
            // When intentionally lowered, preserve that choice.
            if (record.dungeonAppliedLevelsConfigured &&
                previouslyApplied == level)
                DungeonUpgradeBalance.SetAppliedLevel(record, type, level + 1);

            MapAltarProgressChanged?.Invoke(record);
            return true;
        }

        public int GetDungeonUpgradeLevel(string mapId,
            DungeonUpgradeType type)
        {
            return DungeonUpgradeBalance.Level(GetOrCreateMap(mapId), type);
        }

        public int GetDungeonAppliedLevel(string mapId,
            DungeonUpgradeType type)
        {
            return DungeonUpgradeBalance.AppliedLevel(
                GetOrCreateMap(mapId), type);
        }

        /// <summary>
        /// Changes only the applied Lv, not the purchased Lv or owned
        /// materials. All changes are saved via mapAltars in SaveManager.
        /// </summary>
        public bool SetDungeonAppliedLevel(string mapId,
            DungeonUpgradeType type, int targetLevel)
        {
            if (string.IsNullOrWhiteSpace(mapId) ||
                DungeonUpgradeBalance.MaxLevel(type) <= 0)
                return false;

            MapAltarProgressRecord record = GetOrCreateMap(mapId);
            int purchased = DungeonUpgradeBalance.Level(record, type);
            if (targetLevel < 0 || targetLevel > purchased)
                return false;
            if (DungeonUpgradeBalance.AppliedLevel(record, type) ==
                targetLevel)
                return false;

            DungeonUpgradeBalance.SetAppliedLevel(record, type,
                targetLevel);
            MapAltarProgressChanged?.Invoke(record);
            return true;
        }

        /// <summary>
        /// Compatibility wrapper for older call sites.
        /// monsterId is intentionally ignored by the new map-wide rule.
        /// </summary>
        public void SetTitledMonsterRateLevel(
            string mapId,
            string monsterId,
            int level)
        {
            SetMapTitledMonsterRateLevel(mapId, level);
        }

        public float GetMonsterBodyDropRate(
            string mapId,
            MonsterSO monster)
        {
            if (monster == null)
                return 0f;

            AltarProgressRecord record =
                GetOrCreateMonster(
                    mapId,
                    monster.monsterId);

            return AltarBalance.GetMonsterBodyDropRate(
                monster,
                record);
        }

        public void SetPostMaxRespawnControl(
            string mapId,
            string monsterId,
            bool enabled,
            float baseDelayScale)
        {
            AltarProgressRecord record =
                GetOrCreateMonster(mapId, monsterId);

            record.postMaxRespawnEnabled = enabled;
            record.postMaxRespawnDelayScale =
                Mathf.Clamp01(baseDelayScale);

            AltarProgressChanged?.Invoke(record);
        }

        public void SetPostMaxMonsterDropRateScale(
            string mapId,
            string monsterId,
            float scale)
        {
            AltarProgressRecord record =
                GetOrCreateMonster(mapId, monsterId);

            record.postMaxMonsterDropRateScale =
                Mathf.Clamp01(scale);

            AltarProgressChanged?.Invoke(record);
        }
    }
}
