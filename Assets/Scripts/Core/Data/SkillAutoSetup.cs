using System;
using System.Collections.Generic;

namespace MixMaster.Core
{
    public enum SkillSetupProfile { Attack, Healing, Utility }

    public static class SkillAutoSetup
    {
        public static List<SkillSlot> Build(IReadOnlyList<SkillSO> learned, SkillSetupProfile profile)
        {
            var sorted = new List<SkillSO>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (learned != null)
                foreach (var skill in learned)
                    if (skill != null && skill.executable && !string.IsNullOrEmpty(skill.skillId) && seen.Add(skill.skillId))
                        sorted.Add(skill);
            sorted.Sort((a, b) =>
            {
                int group = Rank(b, profile).CompareTo(Rank(a, profile));
                if (group != 0) return group;
                int efficiency = Efficiency(b).CompareTo(Efficiency(a));
                return efficiency != 0 ? efficiency : StringComparer.Ordinal.Compare(a.skillId, b.skillId);
            });

            var slots = new List<SkillSlot>();
            for (int i = 0; i < SkillProgressionManager.SlotCount; i++)
            {
                if (i >= sorted.Count) { slots.Add(new SkillSlot()); continue; }
                var skill = sorted[i];
                bool healing = skill.category == SkillCategory.Active && skill.effect == SkillBaseEffect.Heal;
                bool self = skill.application == SkillApplication.Self || skill.targetType == SkillTargetType.Self;
                slots.Add(new SkillSlot
                {
                    skillId = skill.skillId,
                    trigger = healing ? (self ? SkillTrigger.SelfHpBelow : SkillTrigger.TargetHpBelow) : SkillTrigger.Ready,
                    threshold = healing ? 70f : 0f,
                    priority = SkillProgressionManager.SlotCount - i
                });
            }
            return slots;
        }

        private static int Rank(SkillSO skill, SkillSetupProfile profile)
        {
            bool passive = skill.category == SkillCategory.Passive;
            if (profile == SkillSetupProfile.Healing)
                return !passive && skill.effect == SkillBaseEffect.Heal ? 4 :
                    !passive && skill.effect == SkillBaseEffect.None ? 3 : passive ? 2 : 1;
            if (profile == SkillSetupProfile.Utility)
            {
                bool utility = false;
                if (skill.passives != null)
                    foreach (var effect in skill.passives)
                        if (effect != null && effect.type >= SkillPassiveType.MaterialDrop &&
                            effect.type != SkillPassiveType.Melee && effect.type != SkillPassiveType.Ranged &&
                            (effect.type < SkillPassiveType.FireResistance || effect.type > SkillPassiveType.AllResistance))
                            utility = true;
                return passive ? (utility ? 4 : 3) : skill.effect == SkillBaseEffect.None ? 2 : 1;
            }
            return !passive && skill.effect == SkillBaseEffect.Damage ? 4 :
                passive ? 3 : skill.effect == SkillBaseEffect.None ? 2 : 1;
        }

        private static double Efficiency(SkillSO skill)
        {
            if (skill.category == SkillCategory.Passive)
            {
                double total = 0;
                if (skill.passives != null) foreach (var effect in skill.passives)
                    if (effect != null) total += Math.Max(0, effect.amount);
                return total;
            }
            double power = Math.Max(0, skill.powerMultiplier) + Math.Log10(Math.Max(0, skill.flatPower) + 1d);
            return power / Math.Max(.2, skill.cooldown) / (1d + Math.Log10(Math.Max(0, skill.mpCost) + 1d));
        }
    }
}
