using System;
namespace MixMaster.Core
{
    // Per-hit presentation flags. They must never be shared/mutated across AoE targets.
    public readonly struct DamageFeedback
    {
        public readonly bool IsCritical;
        public readonly bool IsWeakness;
        public DamageFeedback(bool critical, bool weakness)
        { IsCritical = critical; IsWeakness = weakness; }
    }

    public static class DamageCalculator
    {
        public static long Calculate(long attackPower, long defense)
        {
            if (attackPower <= 0L) return 0L;
            double atk = attackPower;
            double def = Math.Max(0L, defense);
            double result = Math.Floor(atk * (atk / (atk + def)));
            return result < 1d ? 1L : result >= long.MaxValue ? long.MaxValue : (long)result;
        }

        public static float Resistance(ElementResistanceSet resistances, ElementType element)
        {
            float value = resistances != null ? resistances.Get(element) : 0f;
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        public static long ApplyResistance(long damage, float resistance)
        {
            if (damage <= 0) return 0;
            if (float.IsNaN(resistance) || float.IsInfinity(resistance)) resistance = 0f;
            double multiplier = 1d - resistance;
            if (multiplier <= 0d) return 0;
            if (multiplier > long.MaxValue / (double)damage) return long.MaxValue;
            // Decimal preserves long precision and avoids 100 * (1 - 0.8f)
            // becoming 19 solely through the float's binary representation.
            return Scale(damage, 1m - (decimal)resistance);
        }

        // Only the final HP receiver calls this, once per accepted hit/tick.
        public static long Randomize(long damage) => damage <= 0 ? 0 :
            ApplyVariance(damage, UnityEngine.Random.Range(0.8f, 1.2f));

        public static long ApplyVariance(long damage, double factor) =>
            Scale(damage, (decimal)(double.IsNaN(factor) ? 1d : Math.Max(0.8d, Math.Min(1.2d, factor))));

        private static long Scale(long damage, decimal multiplier)
        {
            if (damage <= 0 || multiplier <= 0m) return 0;
            decimal result = damage * multiplier;
            if (result >= long.MaxValue) return long.MaxValue;
            return Math.Max(1L, (long)decimal.Floor(result));
        }
    }
}
