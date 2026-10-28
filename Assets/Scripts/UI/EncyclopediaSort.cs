using System;
using System.Collections.Generic;
using System.Globalization;
using MixMaster.Core;

namespace MixMaster.UI
{
    /// <summary>Sort local definition lists without changing their catalogs.</summary>
    public static class EncyclopediaSort
    {
        public static readonly string[] Choices = BuildChoices();
        private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("ja-JP"), true);

        private static string[] BuildChoices()
        {
            var labels = new List<string> { "ID順", "名前" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++) labels.Add(InventoryStatFilter.Labels[i]);
            return labels.ToArray();
        }

        public static int Normalize(int option) => option >= 0 && option < Choices.Length ? option : 0;

        public static void Monsters(List<MonsterSO> definitions, int option, bool ascending)
        {
            if (definitions == null) return;
            option = Normalize(option);
            definitions.Sort((a, b) =>
            {
                if (a == null || b == null) return a == null ? (b == null ? 0 : 1) : -1;
                int order = option == 0 ? EncyclopediaContent.CompareIds(a.monsterId, b.monsterId)
                    : option == 1 ? Names.Compare(EncyclopediaContent.Name(a.displayName, a.monsterId), EncyclopediaContent.Name(b.displayName, b.monsterId))
                    : CompareStats(a.baseStats, b.baseStats, (InventoryStat)(option - 1));
                return Ordered(order, ascending, a.monsterId, b.monsterId);
            });
        }

        public static void Equipment(List<EquipmentSO> definitions, int option, bool ascending)
        {
            if (definitions == null) return;
            option = Normalize(option);
            definitions.Sort((a, b) =>
            {
                if (a == null || b == null) return a == null ? (b == null ? 0 : 1) : -1;
                int order = option == 0 ? EncyclopediaContent.CompareIds(a.equipmentId, b.equipmentId)
                    : option == 1 ? Names.Compare(EncyclopediaContent.Name(a.displayName, a.equipmentId), EncyclopediaContent.Name(b.displayName, b.equipmentId))
                    : InventoryStatFilter.GetValue(a.modifiers, (InventoryStat)(option - 1))
                        .CompareTo(InventoryStatFilter.GetValue(b.modifiers, (InventoryStat)(option - 1)));
                return Ordered(order, ascending, a.equipmentId, b.equipmentId);
            });
        }

        private static int Ordered(int order, bool ascending, string a, string b) => order == 0
            ? EncyclopediaContent.CompareIds(a, b) : ascending ? Math.Sign(order) : -Math.Sign(order);

        private static int CompareStats(CharacterStats a, CharacterStats b, InventoryStat stat)
        {
            // Keep full long precision for the six core stats, even above 2^53.
            if (stat >= InventoryStat.MaxHp && stat <= InventoryStat.MagicDefense)
                return CoreValue(a, stat).CompareTo(CoreValue(b, stat));
            return InventoryStatFilter.GetValue(a, stat).CompareTo(InventoryStatFilter.GetValue(b, stat));
        }

        private static long CoreValue(CharacterStats stats, InventoryStat stat)
        {
            if (stats == null) return 0;
            switch (stat)
            {
                case InventoryStat.MaxHp: return stats.maxHp;
                case InventoryStat.MaxMp: return stats.maxMp;
                case InventoryStat.Attack: return stats.attack;
                case InventoryStat.Magic: return stats.magic;
                case InventoryStat.Defense: return stats.defense;
                case InventoryStat.MagicDefense: return stats.magicDefense;
                default: return 0;
            }
        }
    }
}
