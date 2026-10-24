using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    public enum SkillBaseEffect { Damage, Heal, None }
    public enum SkillApplication { SelectedTarget, Self, EnemiesInArea, AlliesInArea }
    public enum SkillAffixType { None, Buff, Debuff, Status }
    public enum SkillStat { None, Attack, Magic, Defense, MagicDefense, MoveSpeed, AttackSpeed, AttackRange }
    public enum SkillChange { None, Percent, Flat, FixedDamage, MaxHpPercent }
    public enum SkillAilment { None, Poison, Burn, Freeze, Paralysis, Sleep, Silence, Blind, Confusion }
    public enum SkillStacking { Refresh, Stack, Ignore }
    public enum SkillTrigger { Manual, Ready, SelfHpBelow, SelfMpAbove, TargetHpBelow, EnemyCount, NormalHits, DamageTaken, Interval, Kill }

    [Serializable]
    public sealed class SkillAffix
    {
        public SkillAffixType type;
        public SkillStat stat;
        public SkillChange change;
        public double amount;
        public float duration, chance = 100f, interval;
        public SkillAilment ailment;
        public SkillStacking stacking;
        public int stackLimit = 1;
    }
    [Serializable]
    public sealed class SkillSlot
    {
        public string skillId;
        public SkillTrigger trigger;
        public float threshold = 50f;
        public SkillTrigger andCondition;
        public float andThreshold = 50f;
        public int priority;
    }
    [Serializable]
    public sealed class SkillLoadout
    {
        public string ownerId;
        [NonSerialized] public int revision;
        public List<SkillSlot> slots = new List<SkillSlot>();
    }
    [Serializable]
    public sealed class SkillSaveData
    {
        public List<string> playerLearned = new List<string>();
        public List<SkillLoadout> loadouts = new List<SkillLoadout>();
    }
    public static class SkillMath
    {
        public static long Amount(double n) => double.IsNaN(n) || n <= 0 ? 0 : n >= long.MaxValue ? long.MaxValue : (long)Math.Floor(n);
        public static double Change(double original, double percent, double flat, double minimum) => Math.Max(minimum, original * Math.Max(0, 1 + percent / 100) + flat);
    }
}
