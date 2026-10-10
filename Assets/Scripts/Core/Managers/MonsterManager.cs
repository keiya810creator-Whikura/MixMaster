using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class MonsterManager : MonoBehaviour
    {
        [SerializeField] private int maxPartySize = 3;

        private readonly List<OwnedMonsterRecord> ownedMonsters = new List<OwnedMonsterRecord>();
        private readonly List<string> partyMonsterUniqueIds = new List<string>();

        public IReadOnlyList<OwnedMonsterRecord> OwnedMonsters => ownedMonsters;
        public IReadOnlyList<string> PartyMonsterUniqueIds => partyMonsterUniqueIds;
        public int MaxPartySize => Mathf.Clamp(maxPartySize, 1, 3);

        public event Action<OwnedMonsterRecord> MonsterObtained;
        public event Action<OwnedMonsterRecord> MonsterExperienceChanged;
        public event Action<OwnedMonsterRecord> MonsterLeveledUp;
        public event Action PartyChanged;

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
                    record.level = Mathf.Clamp(record.level, 1, ExperienceProgression.MaxLevel);
                    record.experience = Math.Max(0L, record.experience);
                    // Normalize experience from older saves without altering
                    // the stored monster's individual values or title.
                    int restoredLevel = record.level;
                    long restoredExp = record.experience;
                    ExperienceProgression.ApplyExperience(
                        ref restoredLevel, ref restoredExp, 0L, out int restoredLevels);
                    record.level = restoredLevel;
                    record.experience = restoredExp;
                    if (restoredLevels > 0)
                        record.skillPoints = Math.Min(int.MaxValue,
                            (int)Math.Min((long)int.MaxValue,
                                (long)record.skillPoints + restoredLevels));
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

        public void AddExperience(string monsterUniqueId, long amount)
        {
            if (amount <= 0) return;

            var monster = FindOwnedMonster(monsterUniqueId);
            if (monster == null) return;

            int nextLevel = monster.level;
            long nextExp = monster.experience;

            ExperienceProgression.ApplyExperience(
                ref nextLevel, ref nextExp, amount, out int gainedLevels);

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