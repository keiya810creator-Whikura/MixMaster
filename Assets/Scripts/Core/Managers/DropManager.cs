using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class DropManager : MonoBehaviour
    {
        /// <summary>
        /// Generic roll helper. Pass the already-calculated final probability in the 0..1 range.
        /// </summary>
        public bool Roll(float probability)
        {
            probability = Mathf.Clamp01(probability);
            return Random.value < probability;
        }

        /// <summary>
        /// Applies a percentage bonus without forcing a hard 100% cap.
        /// Useful for quantity-type drops where values above 1.0 can later represent multiple rolls.
        /// </summary>
        public double ApplyRateBonus(double baseRate, double bonusRate)
        {
            if (baseRate <= 0d) return 0d;
            if (bonusRate < 0d) bonusRate = 0d;
            return baseRate * (1d + bonusRate);
        }

        public long ApplyQuantityMultiplier(long baseAmount, double multiplier)
        {
            if (baseAmount <= 0 || multiplier <= 0d) return 0;

            double value = baseAmount * multiplier;
            if (value >= long.MaxValue) return long.MaxValue;

            return (long)value;
        }
    }
}