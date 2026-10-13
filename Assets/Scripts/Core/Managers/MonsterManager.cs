using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class MonsterManager : MonoBehaviour
    {
        [SerializeField] private int maxPartySize = 3;
        [SerializeField] private PartyCombatOrder partyCombatOrder =
            PartyCombatOrder.Individual;

        private readonly List<OwnedMonsterRecord> ownedMonsters = new List<OwnedMonsterRecord>();
        private readonly List<string> partyMonsterUniqueIds = new List<string>();

        public IReadOnlyList<OwnedMonsterRecord> OwnedMonsters => ownedMonsters;
        public IReadOnlyList<string> PartyMonsterUniqueIds => partyMonsterUniqueIds;
        public int MaxPartySize => Mathf.Clamp(maxPartySize, 1, 3);
        public PartyCombatOrder CurrentPartyCombatOrder => partyCombatOrder;
        public event Action<PartyCombatOrder> PartyCombatOrderChanged;

        public void SetPartyCombatOrder(PartyCombatOrder order)
        {
            if (!Enum.IsDefined(typeof(PartyCombatOrder), order) ||
                partyCombatOrder == order)
                return;

            partyCombatOrder = order;
            PartyCombatOrderChanged?.Invoke(order);
        }

        // Called during load, including old saves with no order field.
        public void RestorePartyCombatOrder(PartyCombatOrder order)
        {
            if (!Enum.IsDefined(typeof(PartyCombatOrder), order))
                order = PartyCombatOrder.Individual;

            partyCombatOrder = order;
            PartyCombatOrderChanged?.Invoke(order);
        }

        public MonsterCombatBehavior GetEffectiveCombatBehavior(
            OwnedMonsterRecord record)
        {
            if (partyCombatOrder == PartyCombatOrder.Individual)
                return record != null
                    ? record.combatBehavior
                    : MonsterCombatBehavior.PlayerAttackRange;

            switch (partyCombatOrder)
            {
                case PartyCombatOrder.HostileEnemies:
                    return MonsterCombatBehavior.HostileEnemies;
                case PartyCombatOrder.AutoHunt:
                    return MonsterCombatBehavior.AutoHunt;
                case PartyCombatOrder.NoAttack:
                    return MonsterCombatBehavior.NoAttack;
                default:
                    return MonsterCombatBehavior.PlayerAttackRange;
            }
        }

        public event Action<OwnedMonsterRecord> MonsterObtained;
        public event Action<OwnedMonsterRecord> MonsterExperienceChanged;
        public event Action<OwnedMonsterRecord> MonsterLeveledUp;
        public event Action<OwnedMonsterRecord> MonsterBehaviorChanged;
        public event Action PartyChanged;
        public event Action<OwnedMonsterRecord> MonsterProtectionChanged;
        public event Action<string> MonsterSold;
        public event Action MonstersBulkSold;

        /// <summary>
        /// Selling is intentionally restricted for party, favorite and locked
        /// members. Breeding can reuse IsProtectedFromConsumption.
        /// </summary>
        public bool IsProtectedFromConsumption(OwnedMonsterRecord record)
        {
            return record == null || record.isLocked || record.isFavorite ||
                partyMonsterUniqueIds.Contains(record.uniqueId);
        }

        public bool SetFavorite(string uniqueId, bool favorite)
        {
            OwnedMonsterRecord record = FindOwnedMonster(uniqueId);
            if (record == null) return false;
            if (record.isFavorite != favorite)
            {
                record.isFavorite = favorite;
                MonsterProtectionChanged?.Invoke(record);
            }
            return true;
        }

        public bool SetLocked(string uniqueId, bool locked)
        {
            OwnedMonsterRecord record = FindOwnedMonster(uniqueId);
            if (record == null) return false;
            if (record.isLocked != locked)
            {
                record.isLocked = locked;
                MonsterProtectionChanged?.Invoke(record);
            }
            return true;
        }

        /// <summary>
        /// Gold = numeric suffix of monster ID (MON_025 -> 25) x level.
        /// A titled individual sells for twice the normal value.
        /// Saturating multiplication protects long overflow.
        /// </summary>
        public long GetMonsterSaleGold(OwnedMonsterRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.monsterId))
                return 0L;

            string id = record.monsterId.Trim();
            int underscore = id.LastIndexOf('_');
            string numeric = underscore >= 0
                ? id.Substring(underscore + 1) : id;
            if (!long.TryParse(numeric,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long number) || number <= 0L)
                return 0L;

            long gold = LongMath.SaturatingMultiply(
                number, Math.Max(1, record.level));
            if (!string.IsNullOrWhiteSpace(record.titleId))
                gold = LongMath.SaturatingMultiply(gold, 2L);
            return gold;
        }

        /// <summary>
        /// Sell only eligible monsters from a *snapshot* of filtered IDs.
        /// Party, favorite and locked records are always excluded.
        /// All changes are published once to avoid 500 UI rebuilds.
        /// </summary>
        public int SellMonstersBatch(IReadOnlyList<string> selectedIds,
            out long earnedGold)
        {
            earnedGold = 0L;
            if (selectedIds == null || selectedIds.Count == 0)
                return 0;

            GameManager game = GetComponent<GameManager>() ??
                FindFirstObjectByType<GameManager>();
            if (game == null)
                return 0;

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var toSell = new List<OwnedMonsterRecord>();
            foreach (string id in selectedIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                    continue;
                OwnedMonsterRecord record = FindOwnedMonster(id);
                if (IsProtectedFromConsumption(record))
                    continue;

                toSell.Add(record);
                earnedGold = LongMath.SaturatingAdd(
                    earnedGold, GetMonsterSaleGold(record));
            }

            if (toSell.Count == 0)
                return 0;

            // All equipped items remain owned in EquipmentManager.
            // Clearing removed monsters' slot IDs directly avoids repeated
            // EquipmentChanged events and 500 unnecessary UI rebuilds.
            foreach (OwnedMonsterRecord record in toSell)
            {
                record.equippedItemUniqueIds?.Clear();
                ownedMonsters.Remove(record);
            }

            game.AddGold(earnedGold);
            MonstersBulkSold?.Invoke();
            return toSell.Count;
        }

        public bool TrySellMonster(string uniqueId, out long goldGained)
        {
            goldGained = 0L;
            OwnedMonsterRecord record = FindOwnedMonster(uniqueId);
            if (IsProtectedFromConsumption(record)) return false;

            GameManager game = GetComponent<GameManager>() ??
                FindFirstObjectByType<GameManager>();
            if (game == null) return false;

            // Equipment never disappears when selling its owner.
            EquipmentManager equipment = GetComponent<EquipmentManager>() ??
                FindFirstObjectByType<EquipmentManager>();
            if (equipment != null)
                equipment.UnequipAllFromMonster(record);

            goldGained = GetMonsterSaleGold(record);
            ownedMonsters.Remove(record);
            game.AddGold(goldGained);
            MonsterSold?.Invoke(uniqueId);
            return true;
        }

        public OwnedMonsterRecord ObtainMonster(
            string monsterId,
            string titleId = "")
        {
            return ObtainMonster(
                monsterId,
                new MonsterIndividualValues(),
                titleId);
        }

        public OwnedMonsterRecord ObtainMonster(
            MonsterSO definition,
            MonsterIndividualValues individualValues,
            TitleSO title)
        {
            return ObtainMonster(
                definition != null
                    ? definition.monsterId
                    : string.Empty,
                individualValues,
                title != null
                    ? title.titleId
                    : string.Empty);
        }

        public OwnedMonsterRecord ObtainMonster(
            string monsterId,
            MonsterIndividualValues individualValues,
            string titleId = "")
        {
            var monster = new OwnedMonsterRecord
            {
                uniqueId = Guid.NewGuid().ToString("N"),
                monsterId = monsterId ?? string.Empty,
                level = 1,
                experience = 0,
                individualValues =
                    MonsterDropRoller.CloneIndividualValues(
                        individualValues),
                titleId = titleId ?? string.Empty
            };

            monster.individualValues.ClampAll();

            ownedMonsters.Add(monster);
            MonsterObtained?.Invoke(monster);
            return monster;
        }

        public void RestoreMonsters(
            IReadOnlyList<OwnedMonsterRecord> savedMonsters,
            IReadOnlyList<string> savedPartyIds)
        {
            ownedMonsters.Clear();
            partyMonsterUniqueIds.Clear();

            HashSet<string> uniqueIds =
                new HashSet<string>(StringComparer.Ordinal);

            if (savedMonsters != null)
            {
                for (int i = 0; i < savedMonsters.Count; i++)
                {
                    OwnedMonsterRecord record = savedMonsters[i];
                    if (record == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(record.uniqueId))
                        record.uniqueId = Guid.NewGuid().ToString("N");

                    if (!uniqueIds.Add(record.uniqueId))
                        continue;

                    if (record.individualValues == null)
                        record.individualValues = new MonsterIndividualValues();

                    record.individualValues.ClampAll();
                    if (!Enum.IsDefined(typeof(MonsterCombatBehavior), record.combatBehavior))
                        record.combatBehavior = MonsterCombatBehavior.PlayerAttackRange;
                    record.level = Mathf.Clamp(record.level, 1, ExperienceProgression.MaxLevel);
                    record.experience = Math.Max(0L, record.experience);
                    // Normalize experience from older saves without altering
                    // the stored monster's individual values or title.
                    int restoredLevel = record.level;
                    long restoredExp = record.experience;
                    ExperienceProgression.ApplyExperience(
                        ref restoredLevel, ref restoredExp, 0L,
                        out int restoredLevels, true);
                    record.level = restoredLevel;
                    record.experience = restoredExp;
                    if (restoredLevels > 0)
                        record.skillPoints = (int)Math.Min(
                            int.MaxValue,
                            (long)Math.Max(0, record.skillPoints) + restoredLevels);
                    record.learnedSkillIds ??= new List<string>();
                    record.equippedItemUniqueIds ??= new List<string>();
                    ownedMonsters.Add(record);
                }
            }

            if (savedPartyIds != null)
            {
                for (int i = 0; i < savedPartyIds.Count; i++)
                {
                    string id = savedPartyIds[i];
                    if (partyMonsterUniqueIds.Count >= MaxPartySize)
                        break;

                    if (!string.IsNullOrWhiteSpace(id) &&
                        !partyMonsterUniqueIds.Contains(id) &&
                        FindOwnedMonster(id) != null)
                    {
                        partyMonsterUniqueIds.Add(id);
                    }
                }
            }

            PartyChanged?.Invoke();
        }

        public bool AddToParty(string monsterUniqueId)
        {
            if (string.IsNullOrEmpty(monsterUniqueId)) return false;
            if (partyMonsterUniqueIds.Contains(monsterUniqueId)) return true;
            if (partyMonsterUniqueIds.Count >= MaxPartySize) return false;
            if (FindOwnedMonster(monsterUniqueId) == null) return false;

            partyMonsterUniqueIds.Add(monsterUniqueId);
            PartyChanged?.Invoke();
            return true;
        }

        public bool RemoveFromParty(string monsterUniqueId)
        {
            bool removed = partyMonsterUniqueIds.Remove(monsterUniqueId);
            if (removed) PartyChanged?.Invoke();
            return removed;
        }

        public OwnedMonsterRecord FindOwnedMonster(string monsterUniqueId)
        {
            return ownedMonsters.Find(x => x.uniqueId == monsterUniqueId);
        }

        public bool SetMonsterCombatBehavior(
            string monsterUniqueId, MonsterCombatBehavior behavior)
        {
            if (!Enum.IsDefined(typeof(MonsterCombatBehavior), behavior))
                return false;

            OwnedMonsterRecord record = FindOwnedMonster(monsterUniqueId);
            if (record == null)
                return false;

            if (record.combatBehavior != behavior)
            {
                record.combatBehavior = behavior;
                MonsterBehaviorChanged?.Invoke(record);
            }

            return true;
        }

        public void ClearParty()
        {
            if (partyMonsterUniqueIds.Count == 0)
                return;

            partyMonsterUniqueIds.Clear();
            PartyChanged?.Invoke();
        }

        public void AddExperience(string monsterUniqueId, long amount)
        {
            if (amount <= 0) return;

            var monster = FindOwnedMonster(monsterUniqueId);
            if (monster == null) return;

            int nextLevel = monster.level;
            long nextExp = monster.experience;

            ExperienceProgression.ApplyExperience(
                ref nextLevel, ref nextExp, amount, out int gainedLevels, true);

            monster.level = nextLevel;
            monster.experience = nextExp;

            if (gainedLevels > 0)
            {
                monster.skillPoints = (int)Math.Min(int.MaxValue,
                    (long)monster.skillPoints + gainedLevels);
                MonsterLeveledUp?.Invoke(monster);
            }

            MonsterExperienceChanged?.Invoke(monster);
        }
    }
}