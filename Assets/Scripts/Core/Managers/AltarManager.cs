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
                Mathf.Max(0, level);

            MapAltarProgressChanged?.Invoke(record);
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
