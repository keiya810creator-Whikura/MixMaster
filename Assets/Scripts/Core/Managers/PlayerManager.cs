using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class PlayerManager : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats = new CharacterStats();
        private CharacterStats effectiveStats;
        private EquipmentManager equipmentManager;
        private bool equipmentSubscribed;

        // Base growth values are saved; equipment is only applied at runtime.
        public CharacterStats BaseStats => stats;
        public event Action StatsChanged;

        [Header("Experience")]
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private long experience = 0L;

        public int Level => level;
        public long Experience => experience;
        public long ExperienceToNextLevel => ExperienceProgression.RequiredExperience(level);
        public event Action<long, long> ExperienceChanged;
        public event Action<int> LeveledUp;

        [Header("Combat")]
        [SerializeField] private bool autoAttackEnabled = true;

        public bool AutoAttackEnabled => autoAttackEnabled;
        public event Action<bool> AutoAttackSettingChanged;

        public void SetAutoAttackEnabled(bool enabled)
        {
            if (autoAttackEnabled == enabled)
                return;

            autoAttackEnabled = enabled;
            AutoAttackSettingChanged?.Invoke(enabled);
        }

        public CharacterStats Stats => effectiveStats ?? stats;
        public long CurrentHp { get; private set; }
        public long CurrentMp { get; private set; }
        public float DropRateBonus => Mathf.Max(0f, Stats.dropRateBonus);

        public event Action<long, long> HpChanged;
        public event Action<long, long> MpChanged;

        private void Awake()
        {
            CurrentHp = Math.Max(1L, stats.maxHp);
            CurrentMp = Math.Max(0L, stats.maxMp);
            effectiveStats = stats;
        }

        private void Start()
        {
            RecalculateEquipmentStats();
        }

        private void OnDestroy()
        {
            if (equipmentManager != null && equipmentSubscribed)
                equipmentManager.EquipmentChanged -= RecalculateEquipmentStats;
        }

        private void SubscribeEquipment()
        {
            if (equipmentManager == null)
                equipmentManager = GetComponent<EquipmentManager>() ??
                    FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager != null && !equipmentSubscribed)
            {
                equipmentManager.EquipmentChanged += RecalculateEquipmentStats;
                equipmentSubscribed = true;
            }
        }

        public void RecalculateEquipmentStats()
        {
            SubscribeEquipment();
            long oldMaxHp = Math.Max(1L, Stats.maxHp);
            long oldMaxMp = Math.Max(0L, Stats.maxMp);
            var mods = equipmentManager != null
                ? equipmentManager.GetEquippedModifiers(
                    equipmentManager.PlayerEquipmentUniqueIds) : null;

            // Neutral IV and Lv1: only equipment percentages are applied.
            effectiveStats = StatCalculator.CalculateStats(
                stats, 1, new MonsterIndividualValues(), null, mods);
            long newMaxHp = Math.Max(1L, effectiveStats.maxHp);
            long newMaxMp = Math.Max(0L, effectiveStats.maxMp);
            if (CurrentHp > 0L)
            {
                long missingHp = Math.Max(0L, oldMaxHp - CurrentHp);
                CurrentHp = Math.Max(1L,
                    newMaxHp - Math.Min(newMaxHp - 1L, missingHp));
            }
            long missingMp = Math.Max(0L, oldMaxMp - CurrentMp);
            CurrentMp = Math.Max(0L, newMaxMp - Math.Min(newMaxMp, missingMp));

            StatsChanged?.Invoke();
            HpChanged?.Invoke(CurrentHp, newMaxHp);
            MpChanged?.Invoke(CurrentMp, newMaxMp);
        }

        public void RestorePlayerState(
            CharacterStats restoredStats,
            long health,
            long mana,
            int restoredLevel = 1,
            long restoredExperience = 0L,
            bool restoredAutoAttackEnabled = true)
        {
            if (restoredStats != null)
                stats = restoredStats;
            effectiveStats = stats;
            RecalculateEquipmentStats();

            autoAttackEnabled = restoredAutoAttackEnabled;
            AutoAttackSettingChanged?.Invoke(autoAttackEnabled);

            level = Math.Max(1, Math.Min(ExperienceProgression.MaxLevel, restoredLevel));
            experience = Math.Max(0L, restoredExperience);
            if (level >= ExperienceProgression.MaxLevel)
                experience = 0L;

            CurrentHp = Math.Max(0L,
                Math.Min(Math.Max(1L, Stats.maxHp), health));
            CurrentMp = Math.Max(0L,
                Math.Min(Math.Max(0L, Stats.maxMp), mana));

            HpChanged?.Invoke(CurrentHp, Math.Max(1L, stats.maxHp));
            MpChanged?.Invoke(CurrentMp, Math.Max(0L, stats.maxMp));
            ExperienceChanged?.Invoke(experience, ExperienceToNextLevel);
        }

        public void AddExperience(long amount)
        {
            if (amount <= 0L || level >= ExperienceProgression.MaxLevel)
                return;

            int nextLevel = level;
            long nextExperience = experience;
            ExperienceProgression.ApplyExperience(
                ref nextLevel, ref nextExperience, amount, out int gainedLevels);

            experience = nextExperience;
            for (int i = 0; i < gainedLevels; i++)
            {
                level++;
                ApplyLevelUpStats();
                LeveledUp?.Invoke(level);
            }

            ExperienceChanged?.Invoke(experience, ExperienceToNextLevel);
        }

        private void ApplyLevelUpStats()
        {
            // Before the reincarnation system is implemented, use fixed
            // base-stat gains (NOT multiplicative/percentage growth).
            // Preserve all other stats, equipment, resistance, crit and speed.
            stats.maxHp = LongMath.SaturatingAdd(Math.Max(1L, stats.maxHp), 5L);
            stats.maxMp = LongMath.SaturatingAdd(Math.Max(0L, stats.maxMp), 1L);
            stats.attack = LongMath.SaturatingAdd(stats.attack, 1L);
            stats.magic = LongMath.SaturatingAdd(stats.magic, 1L);
            stats.defense = LongMath.SaturatingAdd(stats.defense, 1L);
            stats.magicDefense = LongMath.SaturatingAdd(stats.magicDefense, 1L);
            RecalculateEquipmentStats();
        }

        public void RestoreFullHp()
        {
            CurrentHp = Math.Max(1L, Stats.maxHp);
            HpChanged?.Invoke(CurrentHp, Stats.maxHp);
        }

        public void RestoreFullMp()
        {
            CurrentMp = Math.Max(0L, Stats.maxMp);
            MpChanged?.Invoke(CurrentMp, Stats.maxMp);
        }

        public void RestoreFullResources()
        {
            RestoreFullHp();
            RestoreFullMp();
        }

        public void TakeDamage(long amount)
        {
            if (amount <= 0) return;

            CurrentHp = Math.Max(0L, CurrentHp - amount);
            HpChanged?.Invoke(CurrentHp, Stats.maxHp);
        }

        public void Heal(long amount)
        {
            if (amount <= 0 || CurrentHp >= Stats.maxHp) return;

            CurrentHp = Math.Min(
                Stats.maxHp,
                LongMath.SaturatingAdd(CurrentHp, amount));

            HpChanged?.Invoke(CurrentHp, Stats.maxHp);
        }

        public bool TrySpendMp(long amount)
        {
            if (amount <= 0)
                return true;

            if (CurrentMp < amount)
                return false;

            CurrentMp -= amount;
            MpChanged?.Invoke(CurrentMp, Stats.maxMp);
            return true;
        }

        public void RecoverMp(long amount)
        {
            if (amount <= 0 || CurrentMp >= Stats.maxMp)
                return;

            CurrentMp = Math.Min(
                Stats.maxMp,
                LongMath.SaturatingAdd(CurrentMp, amount));

            MpChanged?.Invoke(CurrentMp, Stats.maxMp);
        }

        public void SetDropRateBonus(float bonus)
        {
            stats.dropRateBonus = Mathf.Max(0f, bonus);
            RecalculateEquipmentStats();
        }
    }
}
