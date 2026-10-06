using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class AltarManager : MonoBehaviour
    {
        private readonly List<AltarProgressRecord> records = new List<AltarProgressRecord>();

        public IReadOnlyList<AltarProgressRecord> Records => records;

        public event Action<AltarProgressRecord> AltarProgressChanged;

        public AltarProgressRecord GetOrCreate(string mapId, string monsterId)
        {
            var record = records.Find(x => x.mapId == mapId && x.monsterId == monsterId);
            if (record != null) return record;

            record = new AltarProgressRecord
            {
                mapId = mapId ?? string.Empty,
                monsterId = monsterId ?? string.Empty
            };

            records.Add(record);
            return record;
        }

        public void DonateMaterial(string mapId, string monsterId, long amount)
        {
            if (amount <= 0) return;

            var record = GetOrCreate(mapId, monsterId);
            record.donatedMaterial = LongMath.SaturatingAdd(record.donatedMaterial, amount);
            AltarProgressChanged?.Invoke(record);
        }

        public void SetSpawnEfficiencyLevel(string mapId, string monsterId, int level)
        {
            var record = GetOrCreate(mapId, monsterId);
            record.spawnEfficiencyLevel = Mathf.Max(0, level);
            AltarProgressChanged?.Invoke(record);
        }

        public void SetDropRateLevel(string mapId, string monsterId, int level)
        {
            var record = GetOrCreate(mapId, monsterId);
            record.dropRateLevel = Mathf.Max(0, level);
            AltarProgressChanged?.Invoke(record);
        }

        public void SetTitledMonsterRateLevel(string mapId, string monsterId, int level)
        {
            var record = GetOrCreate(mapId, monsterId);
            record.titledMonsterRateLevel = Mathf.Max(0, level);
            AltarProgressChanged?.Invoke(record);
        }
    }
}