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

        public const int IndividualValueCount = 11;

        public bool AutoSellEnabled { get; private set; }
        public bool AutoSellUntitled { get; private set; }
        public int AutoSellIvThreshold { get; private set; } = 90;
        public int AutoSellRequiredStats { get; private set; } = 1;

        /// <summary>
        /// Rule changes affect future acquisitions only. A missing title can
        /// optionally override the IV requirement and trigger an immediate sale.
        /// </summary>
        public void SetAutoSellSettings(bool enabled, bool sellUntitled,
            int ivThreshold, int requiredStats)
        {
            AutoSellEnabled = enabled;
            AutoSellUntitled = sellUntitled;
            AutoSellIvThreshold = Mathf.Clamp(ivThreshold, 0, 100);
            AutoSellRequiredStats = Mathf.Clamp(requiredStats, 1,
                IndividualValueCount);
        }

        private bool ShouldAutoSell(OwnedMonsterRecord monster)
        {
            if (!AutoSellEnabled || monster == null)
                return false;

            if (AutoSellUntitled &&
                string.IsNullOrWhiteSpace(monster.titleId))
                return true;

            MonsterIndividualValues iv = monster.individualValues;
            if (iv == null) return false; // Preserve unknown data.
            int threshold = AutoSellIvThreshold;
            int count = 0;
            if (iv.hp >= threshold) count++;
            if (iv.mp >= threshold) count++;
            if (iv.attack >= threshold) count++;
            if (iv.magic >= threshold) count++;
            if (iv.defense >= threshold) count++;
            if (iv.magicDefense >= threshold) count++;
            if (iv.criticalRate >= threshold) count++;
            if (iv.criticalMultiplier >= threshold) count++;
            if (iv.moveSpeed >= threshold) count++;
            if (iv.attackSpeed >= threshold) count++;
            if (iv.attackRange >= threshold) count++;
            return count < AutoSellRequiredStats;
        }

        private readonly HashSet<string> acquiredMonsterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public event Action AcquisitionHistoryChanged;

        public bool HasAcquiredMonster(string monsterId) => acquiredMonsterIds.Contains(monsterId);

        public List<string> CaptureAcquiredMonsters()
        {
            var ids = new List<string>(acquiredMonsterIds);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        private readonly List<OwnedMonsterRecord> ownedMonsters = new List<OwnedMonsterRecord>();
        private readonly Dictionary<string, OwnedMonsterRecord> ownedById =
            new Dictionary<string, OwnedMonsterRecord>(StringComparer.Ordinal);
        private readonly List<string> partyMonsterUniqueIds = new List<string>();

        public const int MaxFarmSize = 20;
        public const int FarmPresetCount = 3;
        private static readonly int[] FarmRates = { 10, 15, 20, 25, 30, 40, 50 };
        private static readonly int[] FarmCosts = { 5, 10, 20, 35, 55, 80 };

        private readonly List<OwnedMonsterRecord>[] farms =
        {
            new List<OwnedMonsterRecord>(MaxFarmSize),
            new List<OwnedMonsterRecord>(MaxFarmSize),
            new List<OwnedMonsterRecord>(MaxFarmSize)
        };

        private bool farmTargetSavePending;
        private float farmTargetSaveAt;
        private void Update()
        {
            if (!farmTargetSavePending || Time.unscaledTime < farmTargetSaveAt) return;
            farmTargetSavePending = GetComponent<SaveManager>()?.SaveGame() != true;
            farmTargetSaveAt = Time.unscaledTime + 1f;
        }

        private readonly int[] farmTargetLevels = { BreedingManager.MinimumParentLevel, BreedingManager.MinimumParentLevel, BreedingManager.MinimumParentLevel };
        public int FarmTargetLevel(int index) => farmTargetLevels[Mathf.Clamp(index, 0, FarmPresetCount - 1)];
        public List<int> CaptureFarmTargets() => new List<int>(farmTargetLevels);

        public bool HasFarmNotification(OwnedMonsterRecord record) => record != null && record.farmStartLevel > 0 &&
            record.level >= FarmTargetLevel(record.farmPresetIndex) &&
            record.acknowledgedFarmTarget != FarmTargetLevel(record.farmPresetIndex);

        public int FarmNotificationCount(int index = -1)
        {
            int count = 0;
            for (int i = 0; i < farms.Length; i++)
            {
                if (index >= 0 && i != index) continue;
                foreach (var record in farms[i]) if (HasFarmNotification(record)) count++;
            }
            return count;
        }

        public bool SetFarmTargetLevel(int index, int level)
        {
            if (index < 0 || index >= FarmPresetCount || level < 1 || level > ExperienceProgression.MaxLevel) return false;
            if (farmTargetLevels[index] == level) return true;
            farmTargetLevels[index] = level;
            farmTargetSavePending = true;
            foreach (var record in farms[index]) record.acknowledgedFarmTarget = 0;
            FarmChanged?.Invoke();
            return true;
        }

        public void AcknowledgeFarmTargets(int index)
        {
            if (index < 0 || index >= FarmPresetCount) return;
            foreach (var record in farms[index])
                if (record.level >= FarmTargetLevel(index)) record.acknowledgedFarmTarget = FarmTargetLevel(index);
            farmTargetSavePending = true;
            FarmChanged?.Invoke();
        }

        public int ActiveFarmIndex { get; private set; }
        public int FarmLevel { get; private set; } = 1;
        public long RaidMaterials { get; private set; }
        public int ClearedRaidTiers { get; private set; }
        public const int RaidTierCount = 10;
        public int FarmExperienceRate => Math.Min(100, FarmRates[FarmLevel - 1] +
            (int)Math.Min(100d, SkillProgressionManager.PartyBonus(SkillPassiveType.FarmExperience)));
        public int FarmUpgradeCost => FarmLevel <= FarmCosts.Length ? FarmCosts[FarmLevel - 1] : 0;
        public IReadOnlyList<OwnedMonsterRecord> FarmMonsters => farms[ActiveFarmIndex];
        public IReadOnlyList<OwnedMonsterRecord> GetFarm(int index) => farms[index];
        public event Action FarmChanged;

        internal void RestoreFarmProgress(GameSaveData data)
        {
            farmTargetSavePending = false;
            ActiveFarmIndex = Mathf.Clamp(data.activeFarmIndex, 0, FarmPresetCount - 1);
            for (int i = 0; i < FarmPresetCount; i++)
                farmTargetLevels[i] = data.farmTargetLevels != null && i < data.farmTargetLevels.Count
                    ? Mathf.Clamp(data.farmTargetLevels[i], 1, ExperienceProgression.MaxLevel) : BreedingManager.MinimumParentLevel;
            FarmLevel = Mathf.Clamp(data.farmLevel, 1, FarmRates.Length);
            RaidMaterials = Math.Max(0L, data.raidMaterials);
            ClearedRaidTiers = Mathf.Clamp(data.clearedRaidTiers, 0, RaidTierCount);
        }

        public bool SetActiveFarm(int index)
        {
            if (index < 0 || index >= FarmPresetCount || index == ActiveFarmIndex) return false;
            ActiveFarmIndex = index;
            FarmChanged?.Invoke();
            return true;
        }

        public bool TryUpgradeFarm()
        {
            int cost = FarmUpgradeCost;
            if (cost == 0 || RaidMaterials < cost) return false;
            RaidMaterials -= cost;
            FarmLevel++;
            FarmChanged?.Invoke();
            return true;
        }

        internal void CompleteRaid(int tier, int reward)
        {
            if (tier < 0 || tier >= RaidTierCount || tier > ClearedRaidTiers || reward <= 0) return;
            RaidMaterials = LongMath.SaturatingAdd(RaidMaterials, reward);
            ClearedRaidTiers = Math.Max(ClearedRaidTiers, tier + 1);
            FarmChanged?.Invoke();
        }

        public bool SetFarmRegistration(string uniqueId, bool registered)
        {
            OwnedMonsterRecord monster = FindOwnedMonster(uniqueId);
            if (monster == null || registered == (monster.farmStartLevel > 0))
                return false;

            if (registered)
            {
                var farm = farms[ActiveFarmIndex];
                if (farm.Count >= MaxFarmSize) return false;
                monster.farmPresetIndex = ActiveFarmIndex;
                monster.farmStartLevel = monster.level;
                farm.Add(monster);
            }
            else
            {
                farms[monster.farmPresetIndex].Remove(monster);
                monster.farmStartLevel = 0;
            }
            monster.farmExperienceRemainder = 0;
            monster.acknowledgedFarmTarget = 0;
            FarmChanged?.Invoke();
            return true;
        }

        public long AwardFarmExperience(long amount)
        {
            if (amount <= 0) return 0L;

            long awarded = 0L;
            int rate = FarmExperienceRate;
            var farm = farms[ActiveFarmIndex];
            for (int i = farm.Count - 1; i >= 0; i--)
            {
                OwnedMonsterRecord monster = farm[i];
                if (monster.level >= ExperienceProgression.MaxLevel ||
                    partyMonsterUniqueIds.Contains(monster.uniqueId))
                    continue;

                int remainder = (int)(amount % 100) * rate + monster.farmExperienceRemainder;
                long share = (amount / 100) * rate + remainder / 100;
                monster.farmExperienceRemainder = remainder % 100;
                if (share > 0)
                {
                    AddExperience(monster.uniqueId, share);
                    awarded = Math.Max(awarded, share);
                }
            }
            return awarded;
        }

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
                case PartyCombatOrder.OwnAttackRange:
                    return MonsterCombatBehavior.OwnAttackRange;
                default:
                    return MonsterCombatBehavior.PlayerAttackRange;
            }
        }

        public event Action<OwnedMonsterRecord> MonsterObtained;
        public event Action<OwnedMonsterRecord> MonsterExperienceChanged;
        public event Action<OwnedMonsterRecord> MonsterLeveledUp;
        public event Action<OwnedMonsterRecord> MonsterIntimacyChanged;
        public event Action<OwnedMonsterRecord> MonsterIntimacyLeveledUp;
        public event Action<OwnedMonsterRecord> MonsterBehaviorChanged;
        public event Action<OwnedMonsterRecord> MonsterAttackRangeDisplayChanged;
        public event Action PartyChanged;
        internal bool IsApplyingPartyPreset { get; private set; }

        internal void ApplyPresetParty(PartyPresetRecord preset)
        {
            partyMonsterUniqueIds.Clear();
            foreach (PartyPresetActor actor in preset.members)
            {
                partyMonsterUniqueIds.Add(actor.ownerId);
                FindOwnedMonster(actor.ownerId).combatBehavior = actor.combatBehavior;
            }
            partyCombatOrder = preset.combatOrder;
        }

        internal void NotifyPartyPresetChanged()
        {
            // All configuration has already committed when observers run.
            // Let the field spawner preserve matching live/dead members.
            IsApplyingPartyPreset = true;
            try
            {
                PartyCombatOrderChanged?.Invoke(partyCombatOrder);
                foreach (string id in partyMonsterUniqueIds) MonsterBehaviorChanged?.Invoke(FindOwnedMonster(id));
                PartyChanged?.Invoke();
            }
            finally { IsApplyingPartyPreset = false; }
        }
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

        public event Action<OwnedMonsterRecord> MonsterRenamed;

        public bool SetNickname(string uniqueId, string nickname)
        {
            var record = FindOwnedMonster(uniqueId);
            if (record == null) return false;
            string value = OwnedMonsterRecord.NormalizeNickname(nickname);
            if (value == (record.nickname ?? string.Empty)) return false;
            record.nickname = value;
            MonsterRenamed?.Invoke(record);
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
                RemoveOwnedMonster(record);
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
            RemoveOwnedMonster(record);
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
            string titleId = "",
            bool bypassAutoSell = false,
            int initialSkillPoints = 0) =>
            ObtainMonster(monsterId, individualValues, titleId, bypassAutoSell, initialSkillPoints, null);

        private OwnedMonsterRecord ObtainMonster(
            string monsterId, MonsterIndividualValues individualValues, string titleId,
            bool bypassAutoSell, int initialSkillPoints, JournalMonster[] ancestors)
        {
            var monster = new OwnedMonsterRecord
            {
                uniqueId = Guid.NewGuid().ToString("N"),
                monsterId = monsterId ?? string.Empty,
                level = 1,
                experience = 0,
                skillPoints = Math.Max(0, initialSkillPoints),
                ancestors = ancestors,
                individualValues =
                    MonsterDropRoller.CloneIndividualValues(
                        individualValues),
                titleId = titleId ?? string.Empty
            };

            monster.individualValues.ClampAll();

            AddOwnedMonster(monster);
            bool firstAcquisition = !string.IsNullOrWhiteSpace(monster.monsterId) &&
                acquiredMonsterIds.Add(monster.monsterId);

            // Newly obtained monsters are not in a party, but use the
            // existing validated sale operation to preserve the same rules
            // for lock/favorite/party safety and gold accounting.
            bool sold = !bypassAutoSell && ShouldAutoSell(monster) &&
                TrySellMonster(monster.uniqueId, out _);
            if (!sold)
                (GetComponent<SkillProgressionManager>() ?? SkillProgressionManager.Instance)?.GrantInnateSkills(monster);
            if (firstAcquisition) AcquisitionHistoryChanged?.Invoke();
            if (sold) return null;

            GameAudio.Play(SoundCue.RareReward);
            MonsterObtained?.Invoke(monster);
            return monster;
        }

        /// <summary>
        /// Consumes both parents even on failed breeding (default). Equipment
        /// is returned to inventory. Future purchases may pass true for
        /// preserveParentsOnFailure without changing the failure roll.
        /// Success inherits each of the 11 IVs independently from either
        /// parent, and randomly inherits either parent's title (including none).
        /// A child without an inherited title has a 10% chance to roll its own.
        /// </summary>
        public bool TryCompleteBreedingAttempt(
            string parentUniqueIdA, string parentUniqueIdB, MonsterSO childSpecies,
            bool succeeded, bool preserveParentsOnFailure,
            out OwnedMonsterRecord child)
        {
            child = null;
            if (childSpecies == null ||
                string.IsNullOrWhiteSpace(childSpecies.monsterId) ||
                string.IsNullOrWhiteSpace(parentUniqueIdA) ||
                string.IsNullOrWhiteSpace(parentUniqueIdB) ||
                string.Equals(parentUniqueIdA, parentUniqueIdB, StringComparison.Ordinal))
                return false;

            OwnedMonsterRecord parentA = FindOwnedMonster(parentUniqueIdA);
            OwnedMonsterRecord parentB = FindOwnedMonster(parentUniqueIdB);
            if (IsProtectedFromConsumption(parentA) ||
                IsProtectedFromConsumption(parentB) ||
                parentA.level < BreedingManager.MinimumParentLevel ||
                parentB.level < BreedingManager.MinimumParentLevel ||
                BreedingManager.IsRaidBoss(parentA.monsterId) ||
                BreedingManager.IsRaidBoss(parentB.monsterId))
                return false;

            if (BreedingManager.IsRaidBoss(childSpecies.monsterId) &&
                (!BreedingManager.HasRaidLineage(parentA, parentB, out string bossId) ||
                 !string.Equals(childSpecies.monsterId, bossId, StringComparison.OrdinalIgnoreCase)))
                return false;

            var skills=GetComponent<SkillProgressionManager>() ?? SkillProgressionManager.Instance;
            var inheritedSkills=succeeded&&skills!=null?skills.InheritanceCandidates(parentUniqueIdA,parentUniqueIdB):null;
            MonsterIndividualValues inheritedIvs = null;
            string inheritedTitleId = string.Empty;
            int inheritedSkillPoints = ReincarnationRules.TotalSkillPoints(
                parentA.skillPoints, parentA.spentSkillPoints,
                parentB.skillPoints, parentB.spentSkillPoints);
            if (succeeded)
            {
                MonsterIndividualValues ivA = parentA.individualValues ??
                    new MonsterIndividualValues();
                MonsterIndividualValues ivB = parentB.individualValues ??
                    new MonsterIndividualValues();
                inheritedIvs = InheritIndividualValues(ivA, ivB);
                inheritedTitleId = UnityEngine.Random.Range(0, 2) == 0
                    ? (parentA.titleId ?? string.Empty)
                    : (parentB.titleId ?? string.Empty);
                if (string.IsNullOrWhiteSpace(inheritedTitleId))
                    inheritedTitleId = MonsterDropRoller.RollTitle(
                        childSpecies.titlePool, 0.10f)?.titleId ?? string.Empty;
            }

            JournalMonster[] ancestors = null;
            if (succeeded)
            {
                ancestors = new[]
                {
                    GameJournalManager.Snapshot(parentA, null), GameJournalManager.Snapshot(parentB, null),
                    parentA.GetAncestor(0), parentA.GetAncestor(1),
                    parentB.GetAncestor(0), parentB.GetAncestor(1)
                };
            }

            if (succeeded || !preserveParentsOnFailure)
            {
                EquipmentManager equipment = GetComponent<EquipmentManager>() ??
                    FindFirstObjectByType<EquipmentManager>();
                if (equipment != null)
                {
                    equipment.UnequipAllFromMonster(parentA);
                    equipment.UnequipAllFromMonster(parentB);
                }

                RemoveOwnedMonster(parentA);
                RemoveOwnedMonster(parentB);
            }

            if (succeeded)
            {
                child = ObtainMonster(childSpecies.monsterId,
                    inheritedIvs, inheritedTitleId, true, inheritedSkillPoints, ancestors);
                if(inheritedSkills!=null)skills.Inherit(child,inheritedSkills);
            }

            // Reuse the existing single-rebuild inventory notification.
            if (succeeded || !preserveParentsOnFailure)
                MonstersBulkSold?.Invoke();
            return true;
        }

        /// <summary>
        /// Legacy guaranteed-success breeding entry point.
        /// </summary>
        public bool TryCompleteBreeding(
            string parentUniqueIdA, string parentUniqueIdB, MonsterSO childSpecies,
            out OwnedMonsterRecord child)
        {
            return TryCompleteBreedingAttempt(
                parentUniqueIdA, parentUniqueIdB, childSpecies,
                true, false, out child);
        }

        private static MonsterIndividualValues InheritIndividualValues(
            MonsterIndividualValues a, MonsterIndividualValues b)
        {
            return new MonsterIndividualValues
            {
                hp = UnityEngine.Random.Range(0, 2) == 0 ? a.hp : b.hp,
                mp = UnityEngine.Random.Range(0, 2) == 0 ? a.mp : b.mp,
                attack = UnityEngine.Random.Range(0, 2) == 0 ? a.attack : b.attack,
                magic = UnityEngine.Random.Range(0, 2) == 0 ? a.magic : b.magic,
                defense = UnityEngine.Random.Range(0, 2) == 0 ? a.defense : b.defense,
                magicDefense = UnityEngine.Random.Range(0, 2) == 0
                    ? a.magicDefense : b.magicDefense,
                criticalRate = UnityEngine.Random.Range(0, 2) == 0
                    ? a.criticalRate : b.criticalRate,
                criticalMultiplier = UnityEngine.Random.Range(0, 2) == 0
                    ? a.criticalMultiplier : b.criticalMultiplier,
                moveSpeed = UnityEngine.Random.Range(0, 2) == 0
                    ? a.moveSpeed : b.moveSpeed,
                attackSpeed = UnityEngine.Random.Range(0, 2) == 0
                    ? a.attackSpeed : b.attackSpeed,
                attackRange = UnityEngine.Random.Range(0, 2) == 0
                    ? a.attackRange : b.attackRange
            };
        }

        public void RestoreMonsters(
            IReadOnlyList<OwnedMonsterRecord> savedMonsters,
            IReadOnlyList<string> savedPartyIds,
            IReadOnlyList<string> savedAcquiredMonsterIds = null)
        {
            acquiredMonsterIds.Clear();
            if (savedAcquiredMonsterIds != null)
                foreach (string id in savedAcquiredMonsterIds)
                    if (!string.IsNullOrWhiteSpace(id)) acquiredMonsterIds.Add(id);
            foreach (var farm in farms) farm.Clear();
            ownedMonsters.Clear();
            ownedById.Clear();
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

                    record.nickname = OwnedMonsterRecord.NormalizeNickname(record.nickname);
                    record.individualValues.ClampAll();
                    record.intimacyKills = Math.Max(0, record.intimacyKills);
                    record.skillPoints = Math.Max(0, record.skillPoints);
                    record.spentSkillPoints = Math.Max(0, record.spentSkillPoints);
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
                    record.inheritedSkillIds = SkillProgressionManager.CopyInheritedSkills(record.inheritedSkillIds);
                    record.equippedItemUniqueIds ??= new List<string>();
                    record.farmPresetIndex = Mathf.Clamp(record.farmPresetIndex, 0, FarmPresetCount - 1);
                    var farm = farms[record.farmPresetIndex];
                    if (record.farmStartLevel > 0 && farm.Count < MaxFarmSize)
                    {
                        record.farmStartLevel = Math.Min(record.farmStartLevel, record.level);
                        farm.Add(record);
                        record.farmExperienceRemainder = Mathf.Clamp(
                            record.farmExperienceRemainder + (record.farmHalfExperiencePending ? 50 : 0), 0, 99);
                    }
                    else
                    {
                        record.farmStartLevel = 0;
                        record.farmExperienceRemainder = 0;
                    }
                    record.farmHalfExperiencePending = false;
                    AddOwnedMonster(record);
                    if (!string.IsNullOrWhiteSpace(record.monsterId)) acquiredMonsterIds.Add(record.monsterId);
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
            FarmChanged?.Invoke();
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
            if (string.IsNullOrEmpty(monsterUniqueId)) return null;
            return ownedById.TryGetValue(monsterUniqueId, out var record) ? record : null;
        }

        private void AddOwnedMonster(OwnedMonsterRecord record)
        {
            ownedMonsters.Add(record);
            ownedById.Add(record.uniqueId, record);
        }

        private void RemoveOwnedMonster(OwnedMonsterRecord record)
        {
            ownedMonsters.Remove(record);
            ownedById.Remove(record.uniqueId);
            if (record.farmStartLevel > 0 && farms[record.farmPresetIndex].Remove(record))
            {
                record.farmStartLevel = 0;
                record.farmExperienceRemainder = 0;
                FarmChanged?.Invoke();
            }
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

        public bool SetMonsterAttackRangeVisible(string monsterUniqueId, bool visible)
        {
            OwnedMonsterRecord record = FindOwnedMonster(monsterUniqueId);
            if (record == null) return false;
            if (record.hideAttackRange == visible)
            {
                record.hideAttackRange = !visible;
                MonsterAttackRangeDisplayChanged?.Invoke(record);
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

        /// <summary>
        /// Only credited to the owned monster that delivered a killing blow.
        /// Separate from shared normal EXP and capped at intimacy Lv.10.
        /// </summary>
        public bool AddPersonalKill(string uniqueId)
        {
            OwnedMonsterRecord monster = FindOwnedMonster(uniqueId);
            if (monster == null ||
                MonsterIntimacyBalance.GetLevelFromTotalKills(
                    monster.intimacyKills) >= MonsterIntimacyBalance.MaxLevel)
                return false;

            int oldLevel = MonsterIntimacyBalance.GetLevelFromTotalKills(
                monster.intimacyKills);
            monster.intimacyKills = Math.Min(int.MaxValue,
                monster.intimacyKills + 1);
            int newLevel = MonsterIntimacyBalance.GetLevelFromTotalKills(
                monster.intimacyKills);
            if (newLevel > oldLevel)
                MonsterIntimacyLeveledUp?.Invoke(monster);

            MonsterIntimacyChanged?.Invoke(monster);
            return true;
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
                GameAudio.Play(SoundCue.LevelUp);
                MonsterLeveledUp?.Invoke(monster);
            }

            MonsterExperienceChanged?.Invoke(monster);
        }
    }
}
