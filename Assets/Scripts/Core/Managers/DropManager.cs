using System;
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class DropManager : MonoBehaviour
    {
        /// <summary>
        /// Generic 0..1 probability roll.
        /// </summary>
        public bool Roll(float probability)
        {
            probability = Mathf.Clamp01(probability);
            return UnityEngine.Random.value < probability;
        }

        /// <summary>
        /// New project rule: drop-rate bonuses are additive.
        /// Example: base 0.10 + bonus 2.00 = final 2.10 (210%).
        /// </summary>
        public double ApplyRateBonus(double baseRate, double bonusRate)
        {
            return CalculateAdditiveRate(baseRate, bonusRate);
        }

        public double CalculateAdditiveRate(double baseRate, double bonusRate)
        {
            baseRate = Math.Max(0d, baseRate);
            bonusRate = Math.Max(0d, bonusRate);
            return baseRate + bonusRate;
        }

        /// <summary>
        /// Converts an uncapped rate into quantity.
        /// 2.10 => 2 guaranteed + 10% chance of one more.
        /// </summary>
        public long RollQuantityFromRate(double finalRate)
        {
            if (finalRate <= 0d)
                return 0L;

            if (finalRate >= long.MaxValue)
                return long.MaxValue;

            long guaranteed = (long)Math.Floor(finalRate);
            double remainder = finalRate - guaranteed;

            if (remainder > 0d && UnityEngine.Random.value < remainder)
                guaranteed = LongMath.SaturatingAdd(guaranteed, 1L);

            return guaranteed;
        }

        /// <summary>
        /// Material drop helper for the current specification.
        /// Base material rate comes from EnemySO.
        /// Killer drop bonus is added directly and may exceed 100%.
        /// </summary>
        public long RollMaterialQuantity(double baseMaterialRate, double killerDropRateBonus)
        {
            double finalRate = CalculateAdditiveRate(
                baseMaterialRate,
                killerDropRateBonus);

            return RollQuantityFromRate(finalRate);
        }

        public long ApplyQuantityMultiplier(long baseAmount, double multiplier)
        {
            if (baseAmount <= 0 || multiplier <= 0d)
                return 0L;

            double value = baseAmount * multiplier;

            if (value >= long.MaxValue)
                return long.MaxValue;

            return (long)value;
        }
    }
}
