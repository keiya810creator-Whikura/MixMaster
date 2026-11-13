using System;
using UnityEngine;
namespace MixMaster.Core
{
    public enum SkillSpawnPosition { Owner, Target }
    public enum SkillFollowTarget { None, Owner, Target }
    public enum SkillEffectTiming { Hit, Spawn, Interval }
    public enum SkillPassiveType { None, MaxHp, MaxMp, Attack, Magic, Defense, MagicDefense, MoveSpeed, AttackSpeed, AttackRange, CriticalRate, CriticalMultiplier, MaterialDrop, EquipmentDrop, FireResistance, WaterResistance, WindResistance, LightningResistance, LightResistance, DarkResistance, AllResistance, Melee, Ranged, BreedingChance, WeaponCount, TitleRolls, RespawnSpeed }
    [Serializable] public sealed class SkillPassive { public SkillPassiveType type; public double amount; }
    public static class SkillPassiveRules
    {
        // Rates use percentage points in the sheet, e.g. 10 means +10%.
        public static CharacterStats Apply(CharacterStats source, Func<SkillPassiveType,double> value)
        {
            var s=ReincarnationRules.CopyStats(source);
            long Scale(long n,SkillPassiveType t)=>SkillMath.Amount(n*(1+value(t)/100));
            float ScaleFloat(float n,SkillPassiveType t)=>(float)Math.Min(float.MaxValue,n*(1+value(t)/100));
            s.maxHp=Math.Max(1,Scale(s.maxHp,SkillPassiveType.MaxHp));s.maxMp=Scale(s.maxMp,SkillPassiveType.MaxMp);
            s.attack=Scale(s.attack,SkillPassiveType.Attack);s.magic=Scale(s.magic,SkillPassiveType.Magic);
            s.defense=Scale(s.defense,SkillPassiveType.Defense);s.magicDefense=Scale(s.magicDefense,SkillPassiveType.MagicDefense);
            s.moveSpeed=ScaleFloat(s.moveSpeed,SkillPassiveType.MoveSpeed);s.attackSpeed=ScaleFloat(s.attackSpeed,SkillPassiveType.AttackSpeed);s.attackRange=ScaleFloat(s.attackRange,SkillPassiveType.AttackRange);
            s.criticalRate=Mathf.Clamp01(s.criticalRate+(float)(value(SkillPassiveType.CriticalRate)/100));
            s.criticalMultiplier=ScaleFloat(s.criticalMultiplier,SkillPassiveType.CriticalMultiplier);
            s.dropRateBonus+=(float)(value(SkillPassiveType.MaterialDrop)/100);
            s.resistances ??= new ElementResistanceSet();
            float Resist(float n,SkillPassiveType t)=>ElementResistanceSet.Clamp(n+(float)((value(t)+value(SkillPassiveType.AllResistance))/100));
            s.resistances.fire=Resist(s.resistances.fire,SkillPassiveType.FireResistance);s.resistances.water=Resist(s.resistances.water,SkillPassiveType.WaterResistance);
            s.resistances.wind=Resist(s.resistances.wind,SkillPassiveType.WindResistance);s.resistances.lightning=Resist(s.resistances.lightning,SkillPassiveType.LightningResistance);
            s.resistances.light=Resist(s.resistances.light,SkillPassiveType.LightResistance);s.resistances.dark=Resist(s.resistances.dark,SkillPassiveType.DarkResistance);
            return s;
        }
    }
}
