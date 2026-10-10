using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class PlayerManager : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats = new CharacterStats();

        [Header("Experience")]
        [SerializeField, Min(1)] private int level = 1;
        [SerializeField, Min(0)] private long experience = 0L;

        public int Level => level;
        public long Experience => experience;
        public long ExperienceToNextLevel => ExperienceProgression.RequiredExperience(level);
        public event Action<long, long> ExperienceChanged;
        public event Action<int> LeveledUp;

        public CharacterStats Stats => stats;
        public long CurrentHp { get; private set; }
        public long CurrentMp { get; private set; }
        public float DropRateBonus => Mathf.Max(0f, stats.dropRateBonus);

        public event Action<long, long> HpChanged;
        public event Action<long, long> MpChanged;

        private void Awake()
        {
            CurrentHp = Math.Max(1L, stats.maxHp);
            CurrentMp = Math.Max(0L, stats.maxMp);
        }

        public void RestorePlayerState(
            CharacterStats restoredStats,
            long health,
            long mana,
            int restoredLevel = 1,
            long restoredExperience = 0L)
        {
            if (restoredStats != null)
                stats = restoredStats;

            level = Math.Max(1, Math.Min(ExperienceProgression.MaxLevel, restoredLevel));
            experience = Math.Max(0L, restoredExperience);
            if (level >= ExperienceProgression.MaxLevel)
                experience = 0L;

            CurrentHp = Math.Max(0L,
                Math.Min(Math.Max(1L, stats.maxHp), health));
            CurrentMp = Math.Max(0L,
                Math.Min(Math.Max(0L, stats.maxMp), mana));

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
            long previousMaxHp = Math.Max(1L, stats.maxHp);
            long previousMaxMp = Math.Max(0L, stats.maxMp);

            // Grow combat attributes; do not alter movement, criticals,
            // range, equipment, resistances, or drop rate.
            stats.maxHp = IncreaseByTenPercent(previousMaxHp);
            stats.maxMp = previousMaxMp > 0L
                ? IncreaseByTenPercent(previousMaxMp) : 0L;
            stats.attack = IncreaseByTenPercent(stats.attack);
            stats.magic = IncreaseByTenPercent(stats.magic);
            stats.defense = IncreaseByTenPercent(stats.defense);
            stats.magicDefense = IncreaseByTenPercent(stats.magicDefense);

            // Preserve missing HP / MP instead of fully healing every level.
            if (CurrentHp > 0L)
            {
                CurrentHp = Math.Min(stats.maxHp,
                    LongMath.SaturatingAdd(CurrentHp,
                        stats.maxHp - previousMaxHp));
            }
            CurrentMp = Math.Min(stats.maxMp,
                LongMath.SaturatingAdd(CurrentMp, stats.maxMp - previousMaxMp));

            HpChanged?.Invoke(CurrentHp, stats.maxHp);
            MpChanged?.Invoke(CurrentMp, stats.maxMp);
        }

        private static long IncreaseByTenPercent(long value)
        {
            if (value <= 0L)
                return 0L;

            double grown = Math.Ceiling(value * 1.1d);
            return grown >= long.MaxValue ? long.MaxValue : (long)grown;
        }

        public void RestoreFullHp()
        {
            CurrentHp = Math.Max(1L, stats.maxHp);
            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public void RestoreFullMp()
        {
            CurrentMp = Math.Max(0L, stats.maxMp);
            MpChanged?.Invoke(CurrentMp, stats.maxMp);
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
            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public void Heal(long amount)
        {
            if (amount <= 0 || CurrentHp >= stats.maxHp) return;

            CurrentHp = Math.Min(
                stats.maxHp,
                LongMath.SaturatingAdd(CurrentHp, amount));

            HpChanged?.Invoke(CurrentHp, stats.maxHp);
        }

        public bool TrySpendMp(long amount)
        {
            if (amount <= 0)
                return true;

            if (CurrentMp < amount)
                return false;

            CurrentMp -= amount;
            MpChanged?.Invoke(CurrentMp, stats.maxMp);
            return true;
        }

        public void RecoverMp(long amount)
        {
            if (amount <= 0 || CurrentMp >= stats.maxMp)
                return;

            CurrentMp = Math.Min(
                stats.maxMp,
                LongMath.SaturatingAdd(CurrentMp, amount));

            MpChanged?.Invoke(CurrentMp, stats.maxMp);
        }

        public void SetDropRateBonus(float bonus)
        {
            stats.dropRateBonus = Mathf.Max(0f, bonus);
        }
    }
}
