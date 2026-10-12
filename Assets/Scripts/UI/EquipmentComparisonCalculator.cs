using System;
using System.Collections.Generic;
using MixMaster.Core;
using UnityEngine;

namespace MixMaster.UI
{
    /// <summary>
    /// Preview the exact same three-slot assignment as EquipmentManager,
    /// but never mutate inventory, equipped IDs, or character stats.
    /// </summary>
    public static class EquipmentComparisonCalculator
    {
        public sealed class Comparison
        {
            public CharacterStats before;
            public CharacterStats after;
            public string ownerName;
            public string previousItem;
            public string newItem;
            public string itemId;
            public int slot;
            public bool isUnequip;
        }

        public static bool TryCalculate(
            EquipmentManager equipment, PlayerManager player,
            OwnedMonsterRecord monster, string itemId, int slot,
            bool unequip, out Comparison result, out string error)
        {
            result = null;
            error = "";
            if (equipment == null || slot < 0 ||
                slot >= EquipmentManager.MaxEquipmentSlots)
            {
                error = "装備先の枠が無効です。";
                return false;
            }

            IReadOnlyList<string> source = monster == null
                ? equipment.PlayerEquipmentUniqueIds
                : (IReadOnlyList<string>)monster.equippedItemUniqueIds;
            var beforeIds = new List<string>(EquipmentManager.MaxEquipmentSlots);
            for (int i = 0; i < EquipmentManager.MaxEquipmentSlots; i++)
                beforeIds.Add(source != null && i < source.Count
                    ? source[i] : "");

            var afterIds = new List<string>(beforeIds);
            string previous = beforeIds[slot];

            if (unequip)
            {
                if (string.IsNullOrEmpty(itemId) ||
                    previous != itemId)
                {
                    error = "装備解除するアイテムがその枠にありません。";
                    return false;
                }
                afterIds[slot] = "";
            }
            else
            {
                EquipmentRecord selected = equipment.FindEquipment(itemId);
                if (selected == null)
                {
                    error = "装備が見つかりません。";
                    return false;
                }

                string owner = equipment.GetEquippedOwner(itemId);
                string expectedOwner = monster == null
                    ? "プレイヤー" : monster.uniqueId;
                if (!string.IsNullOrEmpty(owner) &&
                    owner != expectedOwner)
                {
                    error = "別のキャラクターが装備中です。先に解除してください。";
                    return false;
                }

                int existingSlot = beforeIds.IndexOf(itemId);
                if (existingSlot >= 0 && existingSlot != slot)
                    afterIds[existingSlot] = previous;
                afterIds[slot] = itemId;
            }

            CharacterStats current;
            CharacterStats projected;
            string ownerName;
            if (monster == null)
            {
                if (player == null || player.BaseStats == null)
                {
                    error = "プレイヤーステータスを取得できません。";
                    return false;
                }

                ownerName = "プレイヤー";
                current = StatCalculator.CalculateStats(
                    player.BaseStats, 1, new MonsterIndividualValues(),
                    null, equipment.GetEquippedModifiers(beforeIds));
                projected = StatCalculator.CalculateStats(
                    player.BaseStats, 1, new MonsterIndividualValues(),
                    null, equipment.GetEquippedModifiers(afterIds));
            }
            else
            {
                MonsterCatalogSO catalog = MonsterCatalogSO.Load();
                MonsterSO definition = catalog != null
                    ? catalog.GetMonster(monster.monsterId) : null;
                if (definition == null)
                {
                    error = "仲間モンスターの定義を取得できません。";
                    return false;
                }

                TitleSO title = catalog.GetTitle(monster.titleId);
                ownerName = string.IsNullOrWhiteSpace(definition.displayName)
                    ? monster.monsterId : definition.displayName;
                current = StatCalculator.CalculateMonsterStats(
                    definition, monster.level, monster.individualValues,
                    title, equipment.GetEquippedModifiers(beforeIds));
                projected = StatCalculator.CalculateMonsterStats(
                    definition, monster.level, monster.individualValues,
                    title, equipment.GetEquippedModifiers(afterIds));
            }

            result = new Comparison
            {
                before = current,
                after = projected,
                ownerName = ownerName,
                previousItem = DisplayName(equipment, previous),
                newItem = unequip ? "未装備" : DisplayName(equipment, itemId),
                itemId = itemId,
                slot = slot,
                isUnequip = unequip
            };
            return true;
        }

        private static string DisplayName(EquipmentManager equipment,
            string uniqueId)
        {
            EquipmentRecord record = equipment.FindEquipment(uniqueId);
            if (record == null) return "未装備";
            EquipmentSO definition =
                equipment.GetEquipmentDefinition(record.equipmentId);
            string name = definition != null &&
                !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName : record.equipmentId;
            return name + " Lv." + record.level;
        }
    }
}
