using System;

namespace MixMaster.Core
{
    public static class SkillEnhancementRules
    {
        public static double Multiplier(int rank) => Math.Pow(1.01d, Math.Max(0, rank));

        public static long NextCost(SkillSO skill, int rank) =>
            Math.Max(1, skill.skillPointCost) + (long)Math.Max(0, rank);

        public static bool CanEnhance(SkillSO skill)
        {
            if (skill == null || !skill.executable) return false;
            if (skill.category == SkillCategory.Passive)
                return skill.passives != null && Array.Exists(skill.passives, passive =>
                    passive != null && IsNumericPassive(passive.type) && IsNumeric(passive.amount));

            return (skill.effect != SkillBaseEffect.None &&
                    (skill.flatPower > 0 || (skill.powerSource != SkillPowerSource.None &&
                        skill.powerMultiplier > 0))) ||
                (skill.affixes != null && Array.Exists(skill.affixes, affix =>
                    affix != null && affix.type != SkillAffixType.None &&
                    (affix.duration > 0 || (affix.chance > 0 && affix.chance < 100) ||
                     (HasMagnitude(affix) && IsNumeric(affix.amount)))));
        }

        private static bool IsNumeric(double amount) =>
            amount != 0d && !double.IsNaN(amount) && !double.IsInfinity(amount);

        private static bool IsNumericPassive(SkillPassiveType type) =>
            type != SkillPassiveType.None && type != SkillPassiveType.Melee && type != SkillPassiveType.Ranged;

        private static bool IsDamageOverTime(SkillAffix affix) =>
            affix.type == SkillAffixType.Status &&
            (affix.ailment == SkillAilment.Poison || affix.ailment == SkillAilment.Burn);

        private static bool HasMagnitude(SkillAffix affix) => IsDamageOverTime(affix) ||
            ((affix.type == SkillAffixType.Buff || affix.type == SkillAffixType.Debuff) &&
                (affix.change == SkillChange.Percent || affix.change == SkillChange.Flat));

        private static double Scale(double amount, double multiplier, bool whole = false)
        {
            if (!IsNumeric(amount) || multiplier <= 1d) return amount;
            double magnitude = Math.Min(float.MaxValue, Math.Abs(amount) * multiplier);
            return Math.Sign(amount) * (whole ? Math.Ceiling(magnitude) : magnitude);
        }

        public static long WholeAmount(double amount, int rank) =>
            SkillMath.Amount(rank > 0 ? Math.Ceiling(amount) : amount);

        public static double PassiveAmount(SkillPassive passive, double multiplier) =>
            IsNumericPassive(passive.type)
                ? Scale(passive.amount, multiplier, passive.type is SkillPassiveType.WeaponCount or SkillPassiveType.TitleRolls)
                : passive.amount;

        public static long Power(SkillSO skill, long stat, float projectileScale, int rank)
        {
            float scale = Math.Max(0f, projectileScale);
            double baseAmount = (stat * (double)skill.powerMultiplier + skill.flatPower) * scale;
            if (rank <= 0 || !(baseAmount > 0d)) return SkillMath.Amount(baseAmount);

            double multiplier = Multiplier(rank);
            double enhanced = baseAmount * multiplier;
            if (enhanced >= long.MaxValue) return long.MaxValue;

            // The authored coefficient is a float. Remove its binary noise
            // before ceiling values such as 1,000 × 0.1 × 1.01 to 101.
            const double maxDecimal = (double)decimal.MaxValue;
            if (stat * (double)skill.powerMultiplier >= maxDecimal ||
                skill.powerMultiplier >= maxDecimal || scale >= maxDecimal || multiplier >= maxDecimal)
                return WholeAmount(enhanced, rank);

            decimal precise = (stat * (decimal)skill.powerMultiplier + skill.flatPower) *
                (decimal)scale * (decimal)multiplier;
            return precise >= long.MaxValue ? long.MaxValue : (long)Math.Max(1m, Math.Ceiling(precise));
        }

        public static SkillAffix Affix(SkillAffix source, double multiplier)
        {
            if (source == null || source.type == SkillAffixType.None || multiplier <= 1d) return source;
            return new SkillAffix
            {
                type = source.type,
                stat = source.stat,
                change = source.change,
                amount = HasMagnitude(source) ? Scale(source.amount, multiplier,
                    (IsDamageOverTime(source) && source.change != SkillChange.MaxHpPercent) ||
                    (source.change == SkillChange.Flat &&
                     source.stat is SkillStat.Attack or SkillStat.Magic or SkillStat.Defense or SkillStat.MagicDefense))
                    : source.amount,
                duration = (float)Scale(source.duration, multiplier),
                chance = (float)Math.Min(100d, Scale(source.chance, multiplier)),
                interval = source.interval,
                ailment = source.ailment,
                stacking = source.stacking,
                stackLimit = source.stackLimit
            };
        }
    }
}
