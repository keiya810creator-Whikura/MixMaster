using UnityEngine;

namespace MixMaster.Core
{
    public static class AltarBalance
    {
        public const int SpawnEfficiencyMaxLevel = 100;
        public const int MonsterDropRateMaxLevel = 100;
        public const float MaxMonsterBodyDropRate = 0.10f;

        public const long UpgradeCostPerLevel = 10L;

        /// <summary>
        /// Cost to raise from currentLevel to currentLevel + 1.
        /// Lv0 -> Lv1 = 10, Lv1 -> Lv2 = 20, ... Lv99 -> Lv100 = 1000.
        /// </summary>
        public static long GetUpgradeCost(int currentLevel)
        {
            int safeLevel =
                Mathf.Max(0, currentLevel);

            return (safeLevel + 1L) *
                   UpgradeCostPerLevel;
        }

        public static float GetMonsterBodyDropRate(
            MonsterSO monster,
            AltarProgressRecord record)
        {
            if (monster == null)
                return 0f;

            float baseRate =
                Mathf.Clamp(
                    monster.baseMonsterDropRate,
                    0f,
                    MaxMonsterBodyDropRate);

            if (record == null)
                return baseRate;

            int level =
                Mathf.Clamp(
                    record.dropRateLevel,
                    0,
                    MonsterDropRateMaxLevel);

            if (level >= MonsterDropRateMaxLevel)
            {
                return Mathf.Clamp01(
                    MaxMonsterBodyDropRate *
                    Mathf.Clamp01(
                        record.postMaxMonsterDropRateScale));
            }

            float t =
                MonsterDropRateMaxLevel <= 0
                    ? 0f
                    : level /
                      (float)MonsterDropRateMaxLevel;

            return Mathf.Lerp(
                baseRate,
                MaxMonsterBodyDropRate,
                t);
        }
    }
}
