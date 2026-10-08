using System;
using UnityEngine;

namespace MixMaster.Core
{
    [Serializable]
    public class MonsterIndividualValues
    {
        [Range(0, 100)] public int hp = 50;
        [Range(0, 100)] public int mp = 50;
        [Range(0, 100)] public int attack = 50;
        [Range(0, 100)] public int magic = 50;
        [Range(0, 100)] public int defense = 50;
        [Range(0, 100)] public int magicDefense = 50;
        [Range(0, 100)] public int criticalRate = 50;
        [Range(0, 100)] public int criticalMultiplier = 50;
        [Range(0, 100)] public int moveSpeed = 50;
        [Range(0, 100)] public int attackSpeed = 50;
        [Range(0, 100)] public int attackRange = 50;

        public void ClampAll()
        {
            hp = Clamp(hp);
            mp = Clamp(mp);
            attack = Clamp(attack);
            magic = Clamp(magic);
            defense = Clamp(defense);
            magicDefense = Clamp(magicDefense);
            criticalRate = Clamp(criticalRate);
            criticalMultiplier = Clamp(criticalMultiplier);
            moveSpeed = Clamp(moveSpeed);
            attackSpeed = Clamp(attackSpeed);
            attackRange = Clamp(attackRange);
        }

        public static int Clamp(int value)
        {
            return Mathf.Clamp(value, 0, 100);
        }

        public static float ToMultiplier(int value)
        {
            // 0 => x0.5, 50 => x1.0, 100 => x1.5
            return 0.5f + Clamp(value) / 100f;
        }
    }

    [Serializable]
    public class StatPercentageModifiers
    {
        [Tooltip("0.20 = +20%, -0.10 = -10%")]
        public float maxHp;
        public float maxMp;
        public float attack;
        public float magic;
        public float defense;
        public float magicDefense;
        public float criticalRate;
        public float criticalMultiplier;
        public float moveSpeed;
        public float attackSpeed;
        public float attackRange;

        [Header("Additive Rates")]
        [Tooltip("Additive drop-rate bonus. 1.0 = +100%.")]
        public float dropRateBonus;

        [Tooltip("Added directly to final elemental resistance values.")]
        public ElementResistanceSet resistanceBonus = new ElementResistanceSet();

        public static StatPercentageModifiers CreateZero()
        {
            return new StatPercentageModifiers();
        }

        public void AddFrom(StatPercentageModifiers other)
        {
            if (other == null)
                return;

            maxHp += other.maxHp;
            maxMp += other.maxMp;
            attack += other.attack;
            magic += other.magic;
            defense += other.defense;
            magicDefense += other.magicDefense;
            criticalRate += other.criticalRate;
            criticalMultiplier += other.criticalMultiplier;
            moveSpeed += other.moveSpeed;
            attackSpeed += other.attackSpeed;
            attackRange += other.attackRange;
            dropRateBonus += other.dropRateBonus;

            if (resistanceBonus == null)
                resistanceBonus = new ElementResistanceSet();

            if (other.resistanceBonus == null)
                return;

            resistanceBonus.fire += other.resistanceBonus.fire;
            resistanceBonus.water += other.resistanceBonus.water;
            resistanceBonus.wind += other.resistanceBonus.wind;
            resistanceBonus.lightning += other.resistanceBonus.lightning;
            resistanceBonus.light += other.resistanceBonus.light;
            resistanceBonus.dark += other.resistanceBonus.dark;
        }
    }
}
