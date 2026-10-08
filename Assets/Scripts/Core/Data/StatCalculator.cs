using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    public static class StatCalculator
    {
        public static CharacterStats CalculateMonsterStats(
            MonsterSO monster,
            int level,
            MonsterIndividualValues individualValues,
            TitleSO title = null,
            IReadOnlyList<StatPercentageModifiers> equipmentModifiers = null)
        {
            if (monster == null)
                return new CharacterStats();

            return CalculateStats(
                monster.baseStats,
                level,
                individualValues,
                title != null ? title.modifiers : null,
                equipmentModifiers);
        }

        public static CharacterStats CalculateStats(
            CharacterStats baseStats,
            int level,
            MonsterIndividualValues individualValues,
            StatPercentageModifiers titleModifiers = null,
            IReadOnlyList<StatPercentageModifiers> equipmentModifiers = null)
        {
            baseStats ??= new CharacterStats();
            individualValues ??= new MonsterIndividualValues();

            individualValues.ClampAll();

            StatPercentageModifiers equipment =
                AggregateModifiers(equipmentModifiers);

            CharacterStats result = new CharacterStats();

            int safeLevel = Mathf.Max(1, level);

            result.maxHp = CalculateGrowthLong(
                baseStats.maxHp,
                safeLevel,
                individualValues.hp,
                GetPercent(titleModifiers, x => x.maxHp),
                equipment.maxHp,
                minimum: 1L);

            result.maxMp = CalculateGrowthLong(
                baseStats.maxMp,
                safeLevel,
                individualValues.mp,
                GetPercent(titleModifiers, x => x.maxMp),
                equipment.maxMp,
                minimum: 0L);

            result.attack = CalculateGrowthLong(
                baseStats.attack,
                safeLevel,
                individualValues.attack,
                GetPercent(titleModifiers, x => x.attack),
                equipment.attack,
                minimum: 0L);

            result.magic = CalculateGrowthLong(
                baseStats.magic,
                safeLevel,
                individualValues.magic,
                GetPercent(titleModifiers, x => x.magic),
                equipment.magic,
                minimum: 0L);

            result.defense = CalculateGrowthLong(
                baseStats.defense,
                safeLevel,
                individualValues.defense,
                GetPercent(titleModifiers, x => x.defense),
                equipment.defense,
                minimum: 0L);

            result.magicDefense = CalculateGrowthLong(
                baseStats.magicDefense,
                safeLevel,
                individualValues.magicDefense,
                GetPercent(titleModifiers, x => x.magicDefense),
                equipment.magicDefense,
                minimum: 0L);

            result.criticalRate = Mathf.Clamp01(
                CalculateFloat(
                    baseStats.criticalRate,
                    individualValues.criticalRate,
                    GetPercent(titleModifiers, x => x.criticalRate),
                    equipment.criticalRate));

            result.criticalMultiplier = Mathf.Max(
                1f,
                CalculateFloat(
                    baseStats.criticalMultiplier,
                    individualValues.criticalMultiplier,
                    GetPercent(titleModifiers, x => x.criticalMultiplier),
                    equipment.criticalMultiplier));

            result.moveSpeed = Mathf.Max(
                0.1f,
                CalculateFloat(
                    baseStats.moveSpeed,
                    individualValues.moveSpeed,
                    GetPercent(titleModifiers, x => x.moveSpeed),
                    equipment.moveSpeed));

            result.attackSpeed = Mathf.Max(
                0.1f,
                CalculateFloat(
                    baseStats.attackSpeed,
                    individualValues.attackSpeed,
                    GetPercent(titleModifiers, x => x.attackSpeed),
                    equipment.attackSpeed));

            result.attackRange = Mathf.Max(
                0.1f,
                CalculateFloat(
                    baseStats.attackRange,
                    individualValues.attackRange,
                    GetPercent(titleModifiers, x => x.attackRange),
                    equipment.attackRange));

            result.element = baseStats.element;

            result.resistances = CalculateResistances(
                baseStats.resistances,
                titleModifiers != null
                    ? titleModifiers.resistanceBonus
                    : null,
                equipment.resistanceBonus);

            float titleDropBonus =
                titleModifiers != null
                    ? titleModifiers.dropRateBonus
                    : 0f;

            result.dropRateBonus = Mathf.Max(
                0f,
                baseStats.dropRateBonus +
                titleDropBonus +
                equipment.dropRateBonus);

            return result;
        }

        public static float GetIndividualValueMultiplier(int individualValue)
        {
            return MonsterIndividualValues.ToMultiplier(individualValue);
        }

        private static long CalculateGrowthLong(
            long baseValue,
            int level,
            int individualValue,
            float titlePercent,
            float equipmentPercent,
            long minimum)
        {
            if (baseValue <= 0L)
                return minimum;

            long leveled =
                LongMath.SaturatingMultiply(
                    baseValue,
                    Mathf.Max(1, level));

            double multiplier =
                MonsterIndividualValues.ToMultiplier(individualValue) *
                PercentToMultiplier(titlePercent) *
                PercentToMultiplier(equipmentPercent);

            long value = ScaleLong(leveled, multiplier);
            return Math.Max(minimum, value);
        }

        private static float CalculateFloat(
            float baseValue,
            int individualValue,
            float titlePercent,
            float equipmentPercent)
        {
            float multiplier =
                MonsterIndividualValues.ToMultiplier(individualValue) *
                PercentToMultiplier(titlePercent) *
                PercentToMultiplier(equipmentPercent);

            return baseValue * multiplier;
        }

        private static float PercentToMultiplier(float percent)
        {
            return Mathf.Max(0f, 1f + percent);
        }

        private static long ScaleLong(long value, double multiplier)
        {
            if (value <= 0L || multiplier <= 0d)
                return 0L;

            double result = value * multiplier;

            if (result >= long.MaxValue)
                return long.MaxValue;

            return (long)Math.Floor(result);
        }

        private static ElementResistanceSet CalculateResistances(
            ElementResistanceSet baseResistances,
            ElementResistanceSet titleBonus,
            ElementResistanceSet equipmentBonus)
        {
            baseResistances ??= new ElementResistanceSet();
            titleBonus ??= new ElementResistanceSet();
            equipmentBonus ??= new ElementResistanceSet();

            ElementResistanceSet result = new ElementResistanceSet
            {
                fire =
                    baseResistances.fire +
                    titleBonus.fire +
                    equipmentBonus.fire,

                water =
                    baseResistances.water +
                    titleBonus.water +
                    equipmentBonus.water,

                wind =
                    baseResistances.wind +
                    titleBonus.wind +
                    equipmentBonus.wind,

                earth =
                    baseResistances.earth +
                    titleBonus.earth +
                    equipmentBonus.earth,

                light =
                    baseResistances.light +
                    titleBonus.light +
                    equipmentBonus.light,

                dark =
                    baseResistances.dark +
                    titleBonus.dark +
                    equipmentBonus.dark
            };

            result.ClampAll();
            return result;
        }

        private static StatPercentageModifiers AggregateModifiers(
            IReadOnlyList<StatPercentageModifiers> modifiers)
        {
            StatPercentageModifiers result =
                new StatPercentageModifiers();

            if (modifiers == null)
                return result;

            for (int i = 0; i < modifiers.Count; i++)
                result.AddFrom(modifiers[i]);

            return result;
        }

        private static float GetPercent(
            StatPercentageModifiers modifiers,
            Func<StatPercentageModifiers, float> selector)
        {
            if (modifiers == null || selector == null)
                return 0f;

            return selector(modifiers);
        }
    }
}
