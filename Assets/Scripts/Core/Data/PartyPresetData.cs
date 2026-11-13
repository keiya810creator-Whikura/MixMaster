using System;
using System.Collections.Generic;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class PartyPresetActor
    {
        public string ownerId;
        public string monsterId;
        public string displayName;
        public MonsterCombatBehavior combatBehavior;
        public List<string> equipmentIds = new List<string>();
        public List<string> equipmentNames = new List<string>();
        public List<SkillSlot> skills = new List<SkillSlot>();
    }

    [Serializable]
    public sealed class PartyPresetRecord
    {
        public int slot;
        public string id;
        public string name;
        public string savedUtc;
        public PartyCombatOrder combatOrder;
        public PartyPresetActor player;
        public List<PartyPresetActor> members = new List<PartyPresetActor>();
    }

    public static class PartyPresetData
    {
        public const int MaxPresets = 20;
        public const int MaxNameLength = 40;
        public static string DefaultName(int slot) => "プリセット" + (slot + 1).ToString("00");

        public static string CleanName(string name)
        {
            if (name == null) return string.Empty;
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (char.IsControl(chars[i])) chars[i] = ' ';
            string result = new string(chars).Trim();
            if (result.Length <= MaxNameLength) return result;
            int length = char.IsHighSurrogate(result[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;
            return result.Substring(0, length).TrimEnd();
        }

        public static SkillSlot CopySkill(SkillSlot source) => source == null ? null : new SkillSlot
        {
            skillId = source.skillId, trigger = source.trigger, threshold = source.threshold,
            andCondition = source.andCondition, andThreshold = source.andThreshold, priority = source.priority
        };

        public static PartyPresetActor CopyActor(PartyPresetActor source)
        {
            if (source == null) return null;
            var result = new PartyPresetActor
            {
                ownerId = source.ownerId, monsterId = source.monsterId, displayName = source.displayName,
                combatBehavior = source.combatBehavior,
                equipmentIds = source.equipmentIds == null ? null : new List<string>(source.equipmentIds),
                equipmentNames = source.equipmentNames == null ? null : new List<string>(source.equipmentNames),
                skills = source.skills == null ? null : new List<SkillSlot>()
            };
            if (source.skills != null) foreach (SkillSlot slot in source.skills) result.skills.Add(CopySkill(slot));
            return result;
        }

        public static PartyPresetRecord Copy(PartyPresetRecord source)
        {
            if (source == null) return null;
            var result = new PartyPresetRecord
            {
                slot = source.slot, id = source.id, name = source.name, savedUtc = source.savedUtc,
                combatOrder = source.combatOrder, player = CopyActor(source.player),
                members = source.members == null ? null : new List<PartyPresetActor>()
            };
            if (source.members != null) foreach (PartyPresetActor actor in source.members) result.members.Add(CopyActor(actor));
            return result;
        }

        public static List<PartyPresetActor> Actors(PartyPresetRecord record)
        {
            var result = new List<PartyPresetActor>();
            if (record == null) return result;
            if (record.player != null) result.Add(record.player);
            if (record.members != null) result.AddRange(record.members);
            return result;
        }
    }
}
