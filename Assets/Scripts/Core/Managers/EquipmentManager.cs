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
        public const int MaxEnhancedEquipmentLevel =
            MaxEquipmentLevel + EquipmentEnhancement.MaxEnhancements;

        private readonly List<EquipmentRecord> inventory = new List<EquipmentRecord>();
        private readonly List<string> playerEquipmentUniqueIds = new List<string>();
        private MonsterManager monsterManager;
        private bool enhancing;

        public IReadOnlyList<EquipmentRecord> Inventory => inventory;
        public IReadOnlyList<string> PlayerEquipmentUniqueIds => playerEquipmentUniqueIds;
        public event Action InventoryChanged;
        public event Action EquipmentChanged;

        public bool AutoSellEnabled { get; private set; }
        public int AutoSellKeepLevel { get; private set; } = 50;

        /// <summary>
        /// Changing the rule never sells items already in the inventory.
        /// </summary>
        public void SetAutoSellSettings(bool enabled, int minimumLevelToKeep)
        {
            AutoSellEnabled = enabled;
            AutoSellKeepLevel = Mathf.Clamp(minimumLevelToKeep,
                MinEquipmentLevel, MaxEquipmentLevel);
        }

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
                CreateEquipment("EQP_001", 50, 1L, false);
            if (!inventory.Exists(x => x != null && x.equipmentId == "EQP_002"))
                CreateEquipment("EQP_002", 50, 1L, false);
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

                    EquipmentEnhancement.Normalize(item);
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

        public EquipmentRecord CreateEquipment(string equipmentId, int level,
            long baseValue, bool allowAutoSell = true, string sourceMapId = null)
        {
            var item = new EquipmentRecord
            {
                uniqueId = Guid.NewGuid().ToString("N"),
                equipmentId = equipmentId ?? string.Empty,
                level = Mathf.Clamp(level, MinEquipmentLevel, MaxEquipmentLevel),
                originalLevel = Mathf.Clamp(level, MinEquipmentLevel, MaxEquipmentLevel),
                sourceMapId = sourceMapId,
                baseValue = Math.Max(0L, baseValue)
            };
            if (string.IsNullOrWhiteSpace(item.sourceMapId))
            {
                MapSO origin = ResolveEnhancementMap(item);
                if (origin != null) item.sourceMapId = origin.mapId;
            }
            inventory.Add(item);

            // Reuse the ordinary validated sale path, including gold credit.
            // Starter grants bypass this to avoid immediately selling a new
            // player's guaranteed equipment.
            if (allowAutoSell && AutoSellEnabled &&
                item.level < AutoSellKeepLevel &&
                TrySellEquipment(item.uniqueId, out _))
                return item;

            InventoryChanged?.Invoke();
            return item;
        }

        public long GetScaledValue(EquipmentRecord equipment)
        {
            if (equipment == null) return 0L;
            return LongMath.SaturatingMultiply(
                equipment.baseValue,
                EquipmentEnhancement.Level(equipment));
        }

        public MapSO ResolveEnhancementMap(EquipmentRecord record)
        {
            if (record == null) return null;
            MapCatalogSO catalog = MapCatalogSO.Load();
            if (catalog == null) return null;
            if (!string.IsNullOrWhiteSpace(record.sourceMapId))
                return catalog.GetMap(record.sourceMapId);
            MapSO found = catalog.FindEquipmentMap(record.equipmentId);
            if (found != null) return found;
            // The two guaranteed training items belong to the first field.
            return record.equipmentId == "EQP_001" || record.equipmentId == "EQP_002"
                ? catalog.GetMap("MAP_001") : null;
        }

        public bool CanEnhanceEquipment(string uniqueId,
            out EquipmentEnhancementCost cost, out string error)
        {
            EquipmentRecord record = FindEquipment(uniqueId);
            if (!EquipmentEnhancement.TryBuildCost(record,
                    ResolveEnhancementMap(record), out cost, out error))
                return false;
            GameManager game = GetComponent<GameManager>() ??
                FindFirstObjectByType<GameManager>();
            MaterialInventoryManager materials = GetComponent<MaterialInventoryManager>() ??
                FindFirstObjectByType<MaterialInventoryManager>();
            if (game == null || materials == null)
            { error = "所持金・素材管理を確認してください。"; return false; }
            if (game.Gold < cost.gold)
            { error = "ゴールドが不足しています。"; return false; }
            foreach (MaterialSO material in cost.materials)
                if (materials.GetAmount(material) < cost.amountPerMaterial)
                { error = "素材が不足しています。"; return false; }
            return true;
        }

        public bool TryEnhanceEquipment(string uniqueId, out string error)
        {
            error = string.Empty;
            if (enhancing) return false;
            if (!CanEnhanceEquipment(uniqueId, out EquipmentEnhancementCost cost, out error))
                return false;
            EquipmentRecord record = FindEquipment(uniqueId);
            GameManager game = GetComponent<GameManager>() ??
                FindFirstObjectByType<GameManager>();
            MaterialInventoryManager materials = GetComponent<MaterialInventoryManager>() ??
                FindFirstObjectByType<MaterialInventoryManager>();

            // No callbacks run while resources/level are being committed.
            // Guard the subsequent notifications against recursive purchases.
            enhancing = true;
            try
            {
                if (!game.TrySpendGold(cost.gold, false))
                { error = "ゴールドが不足しています。"; return false; }
                if (!materials.TryConsumeSet(cost.materials, cost.amountPerMaterial))
                {
                    game.AddGold(cost.gold);
                    error = "素材が不足しています。";
                    return false;
                }
                EquipmentEnhancement.Normalize(record);
                record.enhancementCount++;
                record.level = record.originalLevel + record.enhancementCount;
                record.sourceMapId = cost.map.mapId;

                game.NotifyGoldChanged();
                materials.NotifyConsumedSet(cost.materials, cost.amountPerMaterial);
                InventoryChanged?.Invoke();
                EquipmentChanged?.Invoke();
                return true;
            }
            finally { enhancing = false; }
        }

        public bool SetFavorite(string uniqueId, bool favorite)
        {
            EquipmentRecord record = FindEquipment(uniqueId);
            if (record == null) return false;
            if (record.isFavorite != favorite)
            {
                record.isFavorite = favorite;
                InventoryChanged?.Invoke();
            }
            return true;
        }

        public bool SetLocked(string uniqueId, bool locked)
        {
            EquipmentRecord record = FindEquipment(uniqueId);
            if (record == null) return false;
            if (record.isLocked != locked)
            {
                record.isLocked = locked;
                InventoryChanged?.Invoke();
            }
            return true;
        }

        public bool CanSell(EquipmentRecord record)
        {
            return record != null && !record.isLocked && !record.isFavorite &&
                string.IsNullOrEmpty(GetEquippedOwner(record.uniqueId));
        }

        /// <summary>
        /// Existing equipment baseValue x level is reused as its sale price.
        /// Does not remove or alter any equipped items.
        /// </summary>
        public long GetEquipmentSaleGold(EquipmentRecord record)
        {
            return record == null ? 0L : Math.Max(0L, GetScaledValue(record));
        }

        public bool TrySellEquipment(string uniqueId, out long earnedGold)
        {
            earnedGold = 0L;
            EquipmentRecord record = FindEquipment(uniqueId);
            if (!CanSell(record)) return false;

            GameManager game = GetComponent<GameManager>() ??
                FindFirstObjectByType<GameManager>();
            if (game == null) return false;

            earnedGold = GetEquipmentSaleGold(record);
            inventory.Remove(record);
            game.AddGold(earnedGold);
            InventoryChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// A snapshot of filtered unique IDs is passed in by the UI.
        /// Revalidate protection/equipment on the manager side so nothing
        /// protected can be sold between preview and confirmation.
        /// </summary>
        public int SellEquipmentBatch(IReadOnlyList<string> uniqueIds,
            out long earnedGold)
        {
            earnedGold = 0L;
            if (uniqueIds == null || uniqueIds.Count == 0) return 0;

            GameManager game = GetComponent<GameManager>() ??
                FindFirstObjectByType<GameManager>();
            if (game == null) return 0;

            var distinct = new HashSet<string>(StringComparer.Ordinal);
            var sold = new List<EquipmentRecord>();
            foreach (string id in uniqueIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !distinct.Add(id))
                    continue;
                EquipmentRecord record = FindEquipment(id);
                if (!CanSell(record)) continue;
                earnedGold = LongMath.SaturatingAdd(
                    earnedGold, GetEquipmentSaleGold(record));
                sold.Add(record);
            }

            if (sold.Count == 0) return 0;
            foreach (EquipmentRecord record in sold)
                inventory.Remove(record);
            game.AddGold(earnedGold);
            InventoryChanged?.Invoke(); // once rather than once per row
            return sold.Count;
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
            return GetScaledModifiers(record, EquipmentEnhancement.Level(record));
        }

        public StatPercentageModifiers GetScaledModifiers(EquipmentRecord record,
            int effectiveLevel)
        {
            EquipmentSO def = record != null
                ? GetEquipmentDefinition(record.equipmentId) : null;
            if (def == null || def.modifiers == null)
                return StatPercentageModifiers.CreateZero();

            float scale = Mathf.Clamp(effectiveLevel,
                MinEquipmentLevel, MaxEnhancedEquipmentLevel);
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

        internal bool ApplyPresetEquipment(IReadOnlyList<PartyPresetActor> actors)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var targets = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;
            foreach (PartyPresetActor actor in actors)
            {
                targets.Add(actor.ownerId);
                IReadOnlyList<string> current = actor.ownerId == "player"
                    ? playerEquipmentUniqueIds : Monsters.FindOwnedMonster(actor.ownerId).equippedItemUniqueIds;
                for (int i = 0; i < MaxEquipmentSlots; i++)
                {
                    string a = current != null && i < current.Count ? current[i] : string.Empty;
                    if ((a ?? string.Empty) != (actor.equipmentIds[i] ?? string.Empty)) changed = true;
                }
                foreach (string id in actor.equipmentIds)
                    if (!string.IsNullOrWhiteSpace(id)) taken.Add(id);
            }
            for (int i = 0; i < playerEquipmentUniqueIds.Count; i++)
                if (targets.Contains("player") || taken.Contains(playerEquipmentUniqueIds[i])) playerEquipmentUniqueIds[i] = string.Empty;
            if (Monsters != null) foreach (OwnedMonsterRecord record in Monsters.OwnedMonsters)
            {
                if (record?.equippedItemUniqueIds == null) continue;
                for (int i = 0; i < record.equippedItemUniqueIds.Count; i++)
                    if (targets.Contains(record.uniqueId) || taken.Contains(record.equippedItemUniqueIds[i]))
                    {
                        if (!targets.Contains(record.uniqueId) && !string.IsNullOrWhiteSpace(record.equippedItemUniqueIds[i])) changed = true;
                        record.equippedItemUniqueIds[i] = string.Empty;
                    }
            }
            foreach (PartyPresetActor actor in actors)
            {
                if (actor.ownerId == "player")
                {
                    playerEquipmentUniqueIds.Clear();
                    playerEquipmentUniqueIds.AddRange(actor.equipmentIds);
                }
                else Monsters.FindOwnedMonster(actor.ownerId).equippedItemUniqueIds = new List<string>(actor.equipmentIds);
            }
            return changed;
        }

        internal void NotifyPresetEquipmentChanged()
        {
            InventoryChanged?.Invoke();
            EquipmentChanged?.Invoke();
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
