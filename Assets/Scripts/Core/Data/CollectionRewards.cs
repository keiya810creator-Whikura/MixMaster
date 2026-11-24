using System;
using System.Collections.Generic;

namespace MixMaster.Core
{
    public enum CollectionGroup { Normal, Special, UltraSpecial, Raid, Equipment }

    public static class CollectionRewards
    {
        public static readonly int[] Milestones = { 10, 25, 50, 75, 100 };
        public static string Name(CollectionGroup group) => group switch
        {
            CollectionGroup.Normal => "通常モンスター",
            CollectionGroup.Special => "特殊配合",
            CollectionGroup.UltraSpecial => "超特殊配合",
            CollectionGroup.Raid => "レイド",
            _ => "装備"
        };
        public static string Key(CollectionGroup group, int percent) => ((int)group) + "/" + percent;

        public static bool Matches(MonsterSO species, CollectionGroup group)
        {
            if (species == null || string.IsNullOrEmpty(species.monsterId)) return false;
            string id = species.monsterId;
            int underscore = id.LastIndexOf('_');
            if (!int.TryParse(id.Substring(underscore + 1), out int number)) return false;
            return group switch
            {
                CollectionGroup.Normal => number >= 0 && number < 10000,
                CollectionGroup.Special => number >= 10000 && number < 20000,
                CollectionGroup.UltraSpecial => number >= 20000 && number < 30000,
                CollectionGroup.Raid => number >= 30000 && number < 30020,
                _ => false
            };
        }

        public static bool Reached(int owned, int total, int percent) =>
            total > 0 && (long)owned * 100 >= (long)total * percent;

        public static void Count(CollectionGroup group, MonsterCatalogSO monsters, EquipmentCatalogSO equipment,
            Func<string, bool> hasMonster, Func<string, bool> hasEquipment, out int owned, out int total)
        {
            owned = total = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (group == CollectionGroup.Equipment)
            {
                if (equipment?.Equipment == null) return;
                foreach (var definition in equipment.Equipment)
                    if (definition != null && !string.IsNullOrEmpty(definition.equipmentId) && seen.Add(definition.equipmentId))
                    { total++; if (hasEquipment(definition.equipmentId)) owned++; }
            }
            else
            {
                if (monsters?.Monsters == null) return;
                foreach (var definition in monsters.Monsters)
                    if (Matches(definition, group) && seen.Add(definition.monsterId))
                    { total++; if (hasMonster(definition.monsterId)) owned++; }
            }
        }
    }
}
