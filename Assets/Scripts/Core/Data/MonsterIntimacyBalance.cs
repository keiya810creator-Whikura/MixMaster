using System;
using System.Globalization;
using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Per-stage order 1..6 starts at 40,50,60,70,80,90%.
    /// Intimacy Lv10 always reaches 100%. Kills must be scored by this
    /// individual, not shared party experience.
    /// </summary>
    public static class MonsterIntimacyBalance
    {
        public const int MaxLevel = 10;
        public const int KillsPerLevelStep = 10;

        public static int GetStageOrder(MonsterSO monster)
        {
            if (monster == null)
                return 1;

            // Monster CSV is arranged in fixed groups of six per stage.
            // A direct ID calculation remains valid even when catalog
            // ScriptableObjects are loaded in a different order.
            string id = monster.monsterId ?? string.Empty;
            int separator = id.LastIndexOf('_');
            string suffix = separator < 0 ? id : id.Substring(separator + 1);
            if (int.TryParse(suffix, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int number) && number > 0)
                return (number - 1) % 6 + 1;

            return 1;
        }

        public static int GetStartingPercent(MonsterSO monster)
        {
            return 30 + GetStageOrder(monster) * 10;
        }

        public static int GetRequiredKillsForNextLevel(int currentLevel)
        {
            return Mathf.Clamp(currentLevel, 1, MaxLevel - 1) *
                KillsPerLevelStep;
        }

        public static int GetLevelFromTotalKills(int kills)
        {
            int remaining = Math.Max(0, kills);
            int level = 1;
            while (level < MaxLevel)
            {
                int required = GetRequiredKillsForNextLevel(level);
                if (remaining < required)
                    break;
                remaining -= required;
                level++;
            }
            return level;
        }

        public static int GetKillsWithinLevel(int totalKills)
        {
            int remaining = Math.Max(0, totalKills);
            for (int level = 1; level < MaxLevel; level++)
            {
                int required = GetRequiredKillsForNextLevel(level);
                if (remaining < required)
                    return remaining;
                remaining -= required;
            }
            return 0;
        }

        public static float GetPowerPercent(MonsterSO monster, int intimacyLevel)
        {
            float start = GetStartingPercent(monster);
            float fraction = (Mathf.Clamp(intimacyLevel, 1, MaxLevel) - 1f) /
                (MaxLevel - 1f);
            return Mathf.Lerp(start, 100f, fraction);
        }

        public static float GetPowerMultiplier(MonsterSO monster, int intimacyLevel)
        {
            return GetPowerPercent(monster, intimacyLevel) / 100f;
        }
    }
}
