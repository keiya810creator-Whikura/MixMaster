using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MixMaster.Core
{
    public sealed class EquipmentEnhancementCost
    {
        public MapSO map;
        public long gold;
        public long amountPerMaterial;
        public readonly List<MaterialSO> materials = new List<MaterialSO>(6);
    }

    public static class EquipmentEnhancement
    {
        public const int MaxEnhancements = 50;
        public const int RequiredMaterialTypes = 6;

        public static int OriginalLevel(EquipmentRecord record)
        {
            if (record == null) return EquipmentManager.MinEquipmentLevel;
            long original = record.originalLevel > 0 ? record.originalLevel :
                (long)record.level - Mathf.Clamp(record.enhancementCount, 0, MaxEnhancements);
            return (int)Math.Max(EquipmentManager.MinEquipmentLevel,
                Math.Min(EquipmentManager.MaxEquipmentLevel, original));
        }

        public static int Count(EquipmentRecord record) => record == null ? 0 :
            Mathf.Clamp(record.enhancementCount, 0, MaxEnhancements);

        public static int Level(EquipmentRecord record) =>
            OriginalLevel(record) + Count(record);

        public static void Normalize(EquipmentRecord record)
        {
            if (record == null) return;
            record.originalLevel = OriginalLevel(record);
            record.enhancementCount = Count(record);
            record.level = record.originalLevel + record.enhancementCount;
        }

        /// <summary>
        /// Recurrence, not base * Pow(1.2, n): round UP at every purchase.
        /// Integer arithmetic avoids float errors at exact multiples of five.
        /// </summary>
        public static long GoldCost(long bossIdNumber, int completedEnhancements)
        {
            if (bossIdNumber <= 0 || completedEnhancements < 0 ||
                completedEnhancements >= MaxEnhancements) return 0L;
            long cost = bossIdNumber;
            for (int i = 0; i < completedEnhancements; i++)
            {
                // ceil(cost * 1.2) = cost + ceil(cost / 5).
                long increase = cost / 5L + (cost % 5L == 0 ? 0L : 1L);
                if (cost > long.MaxValue - increase) return long.MaxValue;
                cost += increase;
            }
            return cost;
        }

        public static long MaterialCost(long gold) => gold <= 0 ? 0L :
            gold / RequiredMaterialTypes +
            (gold % RequiredMaterialTypes == 0 ? 0L : 1L);

        public static bool TryBuildCost(EquipmentRecord record, MapSO map,
            out EquipmentEnhancementCost cost, out string error)
        {
            cost = null;
            error = string.Empty;
            if (record == null) { error = "装備が見つかりません。"; return false; }
            if (Count(record) >= MaxEnhancements)
            { error = "最大強化（＋50）に到達しました。"; return false; }
            if (map == null || map.bossMonster == null)
            { error = "装備の出身マップとボスを設定してください。"; return false; }

            string id = map.bossMonster.monsterId ?? string.Empty;
            int separator = id.LastIndexOf('_');
            if (!long.TryParse(id.Substring(separator + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out long number) || number <= 0)
            { error = "ボスのEnemy IDから費用を取得できません。"; return false; }

            var result = new EquipmentEnhancementCost { map = map };
            var monsters = new List<MonsterSO>();
            AddMonsters(monsters, map.normalMonsters);
            AddMonsters(monsters, map.monsters);
            if (!monsters.Contains(map.bossMonster)) monsters.Add(map.bossMonster);
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MonsterSO monster in monsters)
            {
                MaterialSO material = monster.uniqueMaterial;
                if (material == null || string.IsNullOrWhiteSpace(material.materialId))
                { error = "マップの全モンスターに固有素材を設定してください。"; return false; }
                if (unique.Add(material.materialId)) result.materials.Add(material);
            }
            if (result.materials.Count != RequiredMaterialTypes)
            { error = "マップに異なるモンスター素材6種を設定してください。"; return false; }

            // The requested MAP1 cost is 6 G, independent of enemy Lv/titles.
            result.gold = GoldCost(number, Count(record));
            result.amountPerMaterial = MaterialCost(result.gold);
            cost = result;
            return true;
        }

        private static void AddMonsters(List<MonsterSO> target, List<MonsterSO> source)
        {
            if (source == null) return;
            foreach (MonsterSO monster in source)
                if (monster != null && !target.Contains(monster)) target.Add(monster);
        }
    }
}
