using System;

namespace MixMaster.Core
{
    /// <summary>
    /// Shared experience curve for player and owned monsters.
    /// Experience is progress within the current level, not lifetime total.
    /// A level N to N+1 costs 10 * N * N EXP.
    /// </summary>
    public static class ExperienceProgression
    {
        public const int MaxLevel = 9999;

        public static long RequiredExperience(int level)
        {
            if (level >= MaxLevel)
                return 0L;

            long safeLevel = Math.Max(1, level);
            return LongMath.SaturatingMultiply(
                10L, LongMath.SaturatingMultiply(safeLevel, safeLevel));
        }

        public static void ApplyExperience(
            ref int level,
            ref long experience,
            long addedExperience,
            out int gainedLevels)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            experience = Math.Max(0L, experience);
            gainedLevels = 0;

            if (addedExperience > 0L)
                experience = LongMath.SaturatingAdd(experience, addedExperience);

            while (level < MaxLevel)
            {
                long required = RequiredExperience(level);
                if (experience < required || required <= 0L)
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
