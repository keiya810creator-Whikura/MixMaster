using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    public enum MonsterAttackType
    {
        Melee,
        Ranged
    }

    [CreateAssetMenu(
        fileName = "Monster_",
        menuName = "MixMaster/Data/Monster")]
    public sealed class MonsterSO : ScriptableObject
    {
        [Header("Identity")]
        public string monsterId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;

        [Header("Prefabs")]
        public GameObject enemyPrefab;
        public GameObject partyPrefab;

        [Header("Base Stats")]
        public CharacterStats baseStats = new CharacterStats();

        [Header("Combat")]
        public MonsterAttackType attackType = MonsterAttackType.Melee;
        [Min(0.1f)] public float detectionRange = 4f;

        [Header("Ranged Attack")]
        public GameObject projectilePrefab;
        [Min(0.1f)] public float projectileSpeed = 8f;
        [Min(0.1f)] public float projectileLifetime = 3f;
        public bool projectileHoming = true;

        [Header("World Spawn")]
        [Min(0f)] public float baseRespawnInterval = 8f;

        [Header("Drops")]
        public MaterialSO uniqueMaterial;
        [Range(0f, 1f)] public float materialBaseDropRate = 0.1f;

        [Tooltip("Very low initial chance for the monster itself to be obtained.")]
        [Range(0f, 1f)] public float baseMonsterDropRate = 0.0001f;

        [Min(0)] public long experienceReward = 1;

        [Header("Titles")]
        [Tooltip("Titles naturally associated with this species. A map can build its title pool from every monster on the map.")]
        public List<TitleSO> titlePool = new List<TitleSO>();

        [Header("Skills")]
        [Tooltip("Skills this monster can learn. Rough target: about 10 per species.")]
        public List<SkillSO> availableSkills = new List<SkillSO>();

        [Header("Breeding")]
        public int breedingValue;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (baseStats == null)
                baseStats = new CharacterStats();

            baseStats.maxHp = System.Math.Max(1L, baseStats.maxHp);
            baseStats.maxMp = System.Math.Max(0L, baseStats.maxMp);
            baseStats.attack = System.Math.Max(0L, baseStats.attack);
            baseStats.magic = System.Math.Max(0L, baseStats.magic);
            baseStats.defense = System.Math.Max(0L, baseStats.defense);
            baseStats.magicDefense = System.Math.Max(0L, baseStats.magicDefense);
            baseStats.criticalRate = Mathf.Clamp01(baseStats.criticalRate);
            baseStats.criticalMultiplier = Mathf.Max(1f, baseStats.criticalMultiplier);
            baseStats.moveSpeed = Mathf.Max(0.1f, baseStats.moveSpeed);
            baseStats.attackSpeed = Mathf.Max(0.1f, baseStats.attackSpeed);
            baseStats.attackRange = Mathf.Max(0.1f, baseStats.attackRange);
            baseStats.dropRateBonus = Mathf.Max(0f, baseStats.dropRateBonus);

            if (baseStats.resistances == null)
                baseStats.resistances = new ElementResistanceSet();

            baseStats.resistances.ClampAll();

            detectionRange = Mathf.Max(0.1f, detectionRange);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            baseRespawnInterval = Mathf.Max(0f, baseRespawnInterval);
            materialBaseDropRate = Mathf.Clamp01(materialBaseDropRate);
            baseMonsterDropRate = Mathf.Clamp01(baseMonsterDropRate);
            experienceReward = System.Math.Max(0L, experienceReward);
        }
#endif
    }
}
