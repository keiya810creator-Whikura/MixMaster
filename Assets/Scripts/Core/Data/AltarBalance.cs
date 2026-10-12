using UnityEngine;

namespace MixMaster.Core
{
    public static class AltarBalance
    {
        public const int SpawnEfficiencyMaxLevel = 100;
        public const int MonsterDropRateMaxLevel = 100;
        public const float MaxMonsterBodyDropRate = 0.10f;

        // Per-level monster acquisition chance increase is 1/10 of the
        // previous balance. Keep the original 10% absolute design cap.
        public const float MonsterDropRateUpgradeScale = 0.10f;

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

            // Reaching Lv100 now adds one tenth of the original
            // (MaxMonsterBodyDropRate - baseRate) improvement.
            // The previous Lv100 special case used to jump directly to 10%;
            // do not allow that jump after reducing the upgrade gain.
            float maxLeveledRate = Mathf.Lerp(
                baseRate,
                MaxMonsterBodyDropRate,
                MonsterDropRateUpgradeScale);

            if (level >= MonsterDropRateMaxLevel)
            {
                return Mathf.Clamp01(
                    maxLeveledRate *
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
                maxLeveledRate,
                t);
        }
    }
}
