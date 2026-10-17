using System;
namespace MixMaster.Core
{
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
    }
}