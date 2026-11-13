using System;

namespace MixMaster.Core
{
    public enum DisplayOption
    {
        MaterialLog, DamageText, HealingText, BuffText, DebuffText,
        SkillEffects, AttackRange
    }

    [Serializable]
    public sealed class DisplaySettingsData
    {
        // False is the visible default, including fields absent in old JSON.
        public bool hideMaterialLog;
        public bool hideDamageText;
        public bool hideHealingText;
        public bool hideBuffText;
        public bool hideDebuffText;
        public bool hideSkillEffects;
        public bool hideAttackRange;

        public bool IsDisabled(DisplayOption option)
        {
            switch (option)
            {
                case DisplayOption.MaterialLog: return hideMaterialLog;
                case DisplayOption.DamageText: return hideDamageText;
                case DisplayOption.HealingText: return hideHealingText;
                case DisplayOption.BuffText: return hideBuffText;
                case DisplayOption.DebuffText: return hideDebuffText;
                case DisplayOption.SkillEffects: return hideSkillEffects;
                case DisplayOption.AttackRange: return hideAttackRange;
                default: return false;
            }
        }

        internal bool SetDisabled(DisplayOption option, bool disabled)
        {
            if (IsDisabled(option) == disabled) return false;
            switch (option)
            {
                case DisplayOption.MaterialLog: hideMaterialLog = disabled; break;
                case DisplayOption.DamageText: hideDamageText = disabled; break;
                case DisplayOption.HealingText: hideHealingText = disabled; break;
                case DisplayOption.BuffText: hideBuffText = disabled; break;
                case DisplayOption.DebuffText: hideDebuffText = disabled; break;
                case DisplayOption.SkillEffects: hideSkillEffects = disabled; break;
                case DisplayOption.AttackRange: hideAttackRange = disabled; break;
                default: return false;
            }
            return true;
        }

        public DisplaySettingsData Copy() => (DisplaySettingsData)MemberwiseClone();
    }
}
