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
        private MonsterManager monsterManager;

        public IReadOnlyList<EquipmentRecord> Inventory => inventory;
        public IReadOnlyList<string> PlayerEquipmentUniqueIds => playerEquipmentUniqueIds;
        public event Action InventoryChanged;
        public event Action EquipmentChanged;

        private MonsterManager Monsters
        {
            get
            {
                if (monsterManager == null)
                    monsterManager = GetComponent<MonsterManager>() ??
                        FindFirstObjectByType<MonsterManager>();
                return monsterManager;
            }
        }

        public void GrantStarterEquipmentIfMissing()
        {
            if (!inventory.Exists(x => x != null && x.equipmentId == "EQP_001"))
                CreateEquipment("EQP_001", 50, 1L);
            if (!inventory.Exists(x => x != null && x.equipmentId == "EQP_002"))
                CreateEquipment("EQP_002", 50, 1L);
        }

        public void RestoreEquipment(
            IReadOnlyList<EquipmentRecord> savedInventory,
            IReadOnlyList<string> savedPlayerEquippedIds)
        {
            inventory.Clear();
            playerEquipmentUniqueIds.Clear();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);

            if (savedInventory != null)
                foreach (EquipmentRecord item in savedInventory)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.uniqueId) ||
                        !uniqueIds.Add(item.uniqueId)) continue;

                    item.level = Mathf.Clamp(item.level,
                        MinEquipmentLevel, MaxEquipmentLevel);
                    item.baseValue = Math.Max(0L, item.baseValue);
                    inventory.Add(item);
                }

            if (savedPlayerEquippedIds != null)
                foreach (string id in savedPlayerEquippedIds)
                {
                    if (playerEquipmentUniqueIds.Count >= MaxEquipmentSlots) break;
                    bool valid = !string.IsNullOrWhiteSpace(id) &&
                        !playerEquipmentUniqueIds.Contains(id) &&
                        FindEquipment(id) != null;
                    // Empty strings preserve the original slot positions.
                    playerEquipmentUniqueIds.Add(valid ? id : string.Empty);
                }

            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
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
            InventoryChanged?.Invoke();
            return item;
        }

        public long GetScaledValue(EquipmentRecord equipment)
        {
            if (equipment == null) return 0L;
            return LongMath.SaturatingMultiply(
                equipment.baseValue,
                Mathf.Clamp(equipment.level, MinEquipmentLevel, MaxEquipmentLevel));
        }

        public EquipmentRecord FindEquipment(string uniqueId)
        {
            return inventory.Find(x => x.uniqueId == uniqueId);
        }

        public EquipmentSO GetEquipmentDefinition(string equipmentId)
        {
            EquipmentCatalogSO catalog = EquipmentCatalogSO.Load();
            return catalog != null ? catalog.GetEquipment(equipmentId) : null;
        }

        /// <summary>
        /// CSV values define the boost at Lv1. Each stat/elemental resistance
        /// and drop rate scales linearly with the instance's equipment level.
        /// Original SO modifiers are never mutated.
        /// </summary>
        public StatPercentageModifiers GetScaledModifiers(EquipmentRecord record)
        {
            EquipmentSO def = record != null
                ? GetEquipmentDefinition(record.equipmentId) : null;
            if (def == null || def.modifiers == null)
                return StatPercentageModifiers.CreateZero();

            float scale = Mathf.Clamp(record.level,
                MinEquipmentLevel, MaxEquipmentLevel);
            StatPercentageModifiers src = def.modifiers;
            ElementResistanceSet r = src.resistanceBonus;
            return new StatPercentageModifiers
            {
                maxHp = src.maxHp * scale,
                maxMp = src.maxMp * scale,
                attack = src.attack * scale,
                magic = src.magic * scale,
                defense = src.defense * scale,
                magicDefense = src.magicDefense * scale,
                criticalRate = src.criticalRate * scale,
                criticalMultiplier = src.criticalMultiplier * scale,
                moveSpeed = src.moveSpeed * scale,
                attackSpeed = src.attackSpeed * scale,
                attackRange = src.attackRange * scale,
                dropRateBonus = src.dropRateBonus * scale,
                resistanceBonus = new ElementResistanceSet
                {
                    fire = r != null ? r.fire * scale : 0f,
                    water = r != null ? r.water * scale : 0f,
                    wind = r != null ? r.wind * scale : 0f,
                    lightning = r != null ? r.lightning * scale : 0f,
                    light = r != null ? r.light * scale : 0f,
                    dark = r != null ? r.dark * scale : 0f
                }
            };
        }

        public List<StatPercentageModifiers> GetEquippedModifiers(
            IReadOnlyList<string> equippedIds)
        {
            var results = new List<StatPercentageModifiers>(MaxEquipmentSlots);
            if (equippedIds == null) return results;
            foreach (string id in equippedIds)
            {
                EquipmentRecord record = FindEquipment(id);
                if (record != null)
                    results.Add(GetScaledModifiers(record));
            }
            return results;
        }

        public string GetEquippedOwner(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId)) return "";
            if (playerEquipmentUniqueIds.Contains(uniqueId)) return "プレイヤー";

            MonsterManager monsters = Monsters;
            if (monsters == null) return "";
            foreach (OwnedMonsterRecord monster in monsters.OwnedMonsters)
                if (monster?.equippedItemUniqueIds != null &&
                    monster.equippedItemUniqueIds.Contains(uniqueId))
                    return monster.uniqueId;
            return "";
        }

        // Assign an item to a chosen slot. A replaced item remains in inventory.
        // One physical equipment instance cannot be worn by multiple owners.
        public bool EquipToPlayer(string uniqueId, int slot) =>
            EquipInternal(null, uniqueId, slot);

        public bool EquipToPlayer(string uniqueId) =>
            EquipToPlayer(uniqueId, FirstEmptySlot(playerEquipmentUniqueIds));

        public bool EquipToMonster(OwnedMonsterRecord monster, string uniqueId, int slot) =>
            monster != null && EquipInternal(monster, uniqueId, slot);

        public bool EquipToMonster(OwnedMonsterRecord monster, string uniqueId) =>
            monster != null && EquipToMonster(monster, uniqueId,
                FirstEmptySlot(monster.equippedItemUniqueIds));

        private static int FirstEmptySlot(IReadOnlyList<string> slots)
        {
            if (slots == null) return 0;
            for (int i = 0; i < MaxEquipmentSlots; i++)
                if (i >= slots.Count || string.IsNullOrWhiteSpace(slots[i]))
                    return i;
            return -1;
        }

        private bool EquipInternal(OwnedMonsterRecord monster, string uniqueId, int slot)
        {
            if (FindEquipment(uniqueId) == null ||
                slot < 0 || slot >= MaxEquipmentSlots)
                return false;

            List<string> slots = monster == null
                ? playerEquipmentUniqueIds : monster.equippedItemUniqueIds;
            if (slots == null)
            {
                slots = new List<string>(MaxEquipmentSlots);
                monster.equippedItemUniqueIds = slots;
            }

            string owner = GetEquippedOwner(uniqueId);
            string intendedOwner = monster == null ? "プレイヤー" : monster.uniqueId;
            if (!string.IsNullOrEmpty(owner) && owner != intendedOwner)
                return false;

            while (slots.Count < MaxEquipmentSlots)
                slots.Add(string.Empty);

            if (slots[slot] == uniqueId)
                return true;

            int oldIndex = slots.IndexOf(uniqueId);
            if (oldIndex >= 0)
                slots[oldIndex] = slots[slot]; // Swap slots if already worn.

            slots[slot] = uniqueId;
            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool UnequipFromPlayer(string uniqueId)
        {
            int index = playerEquipmentUniqueIds.IndexOf(uniqueId);
            if (index < 0) return false;
            playerEquipmentUniqueIds[index] = string.Empty;
            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool UnequipFromMonster(OwnedMonsterRecord monster, string uniqueId)
        {
            if (monster == null || monster.equippedItemUniqueIds == null)
                return false;
            int index = monster.equippedItemUniqueIds.IndexOf(uniqueId);
            if (index < 0) return false;
            monster.equippedItemUniqueIds[index] = string.Empty;
            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool UnequipCurrentOwner(string uniqueId)
        {
            if (UnequipFromPlayer(uniqueId)) return true;
            MonsterManager monsters = Monsters;
            if (monsters == null) return false;
            foreach (OwnedMonsterRecord record in monsters.OwnedMonsters)
                if (UnequipFromMonster(record, uniqueId)) return true;
            return false;
        }

        /// <summary>
        /// Remove all three equipped items from the player, preserving their
        /// ownership in inventory and preserving fixed slot positions.
        /// One notification pair ensures stats/UI are recalculated only once.
        /// </summary>
        public bool UnequipAllFromPlayer()
        {
            if (!ClearEquippedSlots(playerEquipmentUniqueIds))
                return false;

            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Remove all equipment from only this owned monster. Other party
        /// members and player equipment are not affected.
        /// </summary>
        public bool UnequipAllFromMonster(OwnedMonsterRecord monster)
        {
            if (monster == null ||
                !ClearEquippedSlots(monster.equippedItemUniqueIds))
                return false;

            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
            return true;
        }

        private static bool ClearEquippedSlots(List<string> slots)
        {
            if (slots == null) return false;

            bool changed = false;
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(slots[i]))
                    continue;

                slots[i] = string.Empty;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Repair obsolete saves where an item could be worn simultaneously
        /// by player and multiple monsters. Player assignment wins.
        /// </summary>
        public void NormalizeMonsterEquipment()
        {
            MonsterManager monsters = Monsters;
            if (monsters == null) return;

            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < playerEquipmentUniqueIds.Count; i++)
            {
                string id = playerEquipmentUniqueIds[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (FindEquipment(id) == null || !taken.Add(id))
                    playerEquipmentUniqueIds[i] = string.Empty;
            }

            foreach (OwnedMonsterRecord monster in monsters.OwnedMonsters)
            {
                if (monster == null) continue;
                monster.equippedItemUniqueIds ??= new List<string>();
                List<string> slots = monster.equippedItemUniqueIds;
                for (int i = 0; i < slots.Count; i++)
                {
                    string id = slots[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (i >= MaxEquipmentSlots ||
                        FindEquipment(id) == null || !taken.Add(id))
                        slots[i] = string.Empty;
                }
                if (slots.Count > MaxEquipmentSlots)
                    slots.RemoveRange(MaxEquipmentSlots,
                        slots.Count - MaxEquipmentSlots);
            }

            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
        }
    }
}
