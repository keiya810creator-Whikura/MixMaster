using UnityEngine;

namespace MixMaster.Core
{
    public static class AltarBalance
    {
        public const int SpawnEfficiencyMaxLevel = 10;
        public const int MonsterDropRateMaxLevel = 10;
        public const float MaxMonsterBodyDropRate = 0.10f;

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
