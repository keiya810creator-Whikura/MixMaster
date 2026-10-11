using System;
using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Uses the imported per-level EXP table when available.
    /// Without an imported table, the legacy 10 * level^2 formula remains.
    /// Experience is progress within the current level.
    /// </summary>
    public static class ExperienceProgression
    {
        public const int MaxLevel = 9999;
        private const string ExperienceTableResourcePath = "MixMaster/ExperienceTable";

        private static ExperienceTableSO cachedTable;
        private static bool attemptedLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            cachedTable = null;
            attemptedLoad = false;
        }

        private static ExperienceTableSO LoadTable()
        {
            if (!attemptedLoad)
            {
                cachedTable = Resources.Load<ExperienceTableSO>(
                    ExperienceTableResourcePath);
                attemptedLoad = true;
            }

            return cachedTable;
        }

        // Backward-compatible default: player EXP.
        public static long RequiredExperience(int level)
        {
            return RequiredExperience(level, false);
        }

        public static long RequiredExperience(int level, bool forMonster)
        {
            if (level >= MaxLevel)
                return 0L;

            int safeLevel = Math.Max(1, level);
            ExperienceTableSO table = LoadTable();

            if (table != null)
            {
                long imported = table.GetRequiredExperience(safeLevel, forMonster);
                if (imported > 0L)
                    return imported;
            }

            long longLevel = safeLevel;
            return LongMath.SaturatingMultiply(
                10L, LongMath.SaturatingMultiply(longLevel, longLevel));
        }

        public static void ApplyExperience(
            ref int level,
            ref long experience,
            long addedExperience,
            out int gainedLevels)
        {
            ApplyExperience(
                ref level, ref experience, addedExperience, out gainedLevels, false);
        }

        public static void ApplyExperience(
            ref int level,
            ref long experience,
            long addedExperience,
            out int gainedLevels,
            bool forMonster)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            experience = Math.Max(0L, experience);
            gainedLevels = 0;

            if (addedExperience > 0L)
                experience = LongMath.SaturatingAdd(experience, addedExperience);

            while (level < MaxLevel)
            {
                long required = RequiredExperience(level, forMonster);
                if (required <= 0L || experience < required)
                    break;

                experience -= required;
                level++;
                gainedLevels++;
            }

            if (level >= MaxLevel)
                experience = 0L;
        }
    }
}
