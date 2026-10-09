using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class EquipmentManager : MonoBehaviour
    {
        public const int MaxEquipmentSlots = 3;
        public const int MinEquipmentLevel = 1;
        public const int MaxEquipmentLevel = 100;

        private readonly List<EquipmentRecord> inventory = new List<EquipmentRecord>();
        private readonly List<string> playerEquipmentUniqueIds = new List<string>();

        public IReadOnlyList<EquipmentRecord> Inventory => inventory;
        public IReadOnlyList<string> PlayerEquipmentUniqueIds => playerEquipmentUniqueIds;

        public void RestoreEquipment(
            IReadOnlyList<EquipmentRecord> savedInventory,
            IReadOnlyList<string> savedPlayerEquippedIds)
        {
            inventory.Clear();
            playerEquipmentUniqueIds.Clear();

            if (savedInventory != null)
            {
                for (int i = 0; i < savedInventory.Count; i++)
                {
                    EquipmentRecord item = savedInventory[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.uniqueId))
                        continue;

                    item.level = Mathf.Clamp(
                        item.level, MinEquipmentLevel, MaxEquipmentLevel);
                    item.baseValue = Math.Max(0L, item.baseValue);
                    inventory.Add(item);
                }
            }

            if (savedPlayerEquippedIds != null)
            {
                for (int i = 0; i < savedPlayerEquippedIds.Count; i++)
                {
                    string id = savedPlayerEquippedIds[i];
                    if (playerEquipmentUniqueIds.Count >= MaxEquipmentSlots)
                        break;

                    if (!string.IsNullOrWhiteSpace(id) &&
                        !playerEquipmentUniqueIds.Contains(id) &&
                        FindEquipment(id) != null)
                        playerEquipmentUniqueIds.Add(id);
                }
            }
        }

        public EquipmentRecord CreateEquipment(string equipmentId, int level, long baseValue)
        {
            var item = new EquipmentRecord
            {
                uniqueId = Guid.NewGuid().ToString("N"),
                equipmentId = equipmentId ?? string.Empty,
                level = Mathf.Clamp(level, MinEquipmentLevel, MaxEquipmentLevel),
                baseValue = Math.Max(0L, baseValue)
            };

            inventory.Add(item);
            return item;
        }

        public long GetScaledValue(EquipmentRecord equipment)
        {
            if (equipment == null) return 0L;
            int level = Mathf.Clamp(equipment.level, MinEquipmentLevel, MaxEquipmentLevel);

            // Requirement: base value 1 => Lv1: 1, Lv100: 100.
            return LongMath.SaturatingMultiply(equipment.baseValue, level);
        }

        public bool EquipToPlayer(string equipmentUniqueId)
        {
            if (FindEquipment(equipmentUniqueId) == null) return false;
            if (playerEquipmentUniqueIds.Contains(equipmentUniqueId)) return true;
            if (playerEquipmentUniqueIds.Count >= MaxEquipmentSlots) return false;

            playerEquipmentUniqueIds.Add(equipmentUniqueId);
            return true;
        }

        public bool UnequipFromPlayer(string equipmentUniqueId)
        {
            return playerEquipmentUniqueIds.Remove(equipmentUniqueId);
        }

        public bool EquipToMonster(OwnedMonsterRecord monster, string equipmentUniqueId)
        {
            if (monster == null || FindEquipment(equipmentUniqueId) == null) return false;

            if (monster.equippedItemUniqueIds == null)
                monster.equippedItemUniqueIds = new List<string>(MaxEquipmentSlots);

            if (monster.equippedItemUniqueIds.Contains(equipmentUniqueId)) return true;
            if (monster.equippedItemUniqueIds.Count >= MaxEquipmentSlots) return false;

            monster.equippedItemUniqueIds.Add(equipmentUniqueId);
            return true;
        }

        public EquipmentRecord FindEquipment(string uniqueId)
        {
            return inventory.Find(x => x.uniqueId == uniqueId);
        }
    }
}