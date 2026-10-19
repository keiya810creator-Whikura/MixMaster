using System;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class ReincarnationBookRecord
    {
        // HUMAN is always available; monster books use MonsterSO.monsterId.
        public string bookId;
        public bool isFavorite;
    }

    public enum ReincarnationBookSort
    {
        Acquisition,
        Name,
        Hp,
        Mp,
        Attack,
        Magic,
        Defense,
        MagicDefense
    }

    public static class ReincarnationRules
    {
        public const string HumanBookId = "HUMAN";
        public const int RequiredIntimacyLevel = 5;

        // Used points become available again when skills are reset.
        public static int TotalSkillPoints(int availableA, int spentA,
            int availableB = 0, int spentB = 0)
        {
            long total = (long)Math.Max(0, availableA) + Math.Max(0, spentA) +
                Math.Max(0, availableB) + Math.Max(0, spentB);
            return (int)Math.Min(int.MaxValue, total);
        }

        public static CharacterStats CopyStats(CharacterStats source)
        {
            source = source ?? new CharacterStats();
            ElementResistanceSet r = source.resistances ?? new ElementResistanceSet();
            return new CharacterStats
            {
                maxHp = Math.Max(1L, source.maxHp), maxMp = Math.Max(0L, source.maxMp),
                attack = Math.Max(0L, source.attack), magic = Math.Max(0L, source.magic),
                defense = Math.Max(0L, source.defense), magicDefense = Math.Max(0L, source.magicDefense),
                criticalRate = source.criticalRate, criticalMultiplier = source.criticalMultiplier,
                moveSpeed = source.moveSpeed, attackSpeed = source.attackSpeed,
                attackRange = source.attackRange, element = source.element,
                dropRateBonus = source.dropRateBonus,
                resistances = new ElementResistanceSet
                {
                    fire = r.fire, water = r.water, wind = r.wind,
                    lightning = r.lightning, light = r.light, dark = r.dark
                }
            };
        }
    }
}
