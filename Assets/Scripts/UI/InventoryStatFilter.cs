using System;
using MixMaster.Core;

namespace MixMaster.UI
{
    // Shared status selectors for equipment and owned-monster inventories.
    public enum InventoryStat
    {
        All = 0,
        MaxHp, MaxMp, Attack, Magic, Defense, MagicDefense,
        CriticalRate, CriticalMultiplier, MoveSpeed, AttackSpeed, AttackRange,
        FireResistance, WaterResistance, WindResistance,
        LightningResistance, LightResistance, DarkResistance, DropRate
    }

    public static class InventoryStatFilter
    {
        public static readonly string[] Labels =
        {
            "すべて", "最大HP", "最大MP", "攻撃力", "魔力", "防御力",
            "魔法防御", "クリ率", "クリ倍率", "移動速度",
            "攻撃速度", "攻撃範囲", "火耐性", "水耐性",
            "風耐性", "雷耐性", "光耐性", "闇耐性", "素材ドロ率"
        };

        public static double GetValue(CharacterStats s, InventoryStat stat)
        {
            if (s == null) return 0d;
            ElementResistanceSet r = s.resistances;
            switch (stat)
            {
                case InventoryStat.MaxHp: return s.maxHp;
                case InventoryStat.MaxMp: return s.maxMp;
                case InventoryStat.Attack: return s.attack;
                case InventoryStat.Magic: return s.magic;
                case InventoryStat.Defense: return s.defense;
                case InventoryStat.MagicDefense: return s.magicDefense;
                case InventoryStat.CriticalRate: return s.criticalRate;
                case InventoryStat.CriticalMultiplier: return s.criticalMultiplier;
                case InventoryStat.MoveSpeed: return s.moveSpeed;
                case InventoryStat.AttackSpeed: return s.attackSpeed;
                case InventoryStat.AttackRange: return s.attackRange;
                case InventoryStat.FireResistance: return r != null ? r.fire : 0;
                case InventoryStat.WaterResistance: return r != null ? r.water : 0;
                case InventoryStat.WindResistance: return r != null ? r.wind : 0;
                case InventoryStat.LightningResistance: return r != null ? r.lightning : 0;
                case InventoryStat.LightResistance: return r != null ? r.light : 0;
                case InventoryStat.DarkResistance: return r != null ? r.dark : 0;
                case InventoryStat.DropRate: return s.dropRateBonus;
                default: return 0d;
            }
        }

        public static double GetValue(StatPercentageModifiers s, InventoryStat stat)
        {
            if (s == null) return 0d;
            ElementResistanceSet r = s.resistanceBonus;
            switch (stat)
            {
                case InventoryStat.MaxHp: return s.maxHp;
                case InventoryStat.MaxMp: return s.maxMp;
                case InventoryStat.Attack: return s.attack;
                case InventoryStat.Magic: return s.magic;
                case InventoryStat.Defense: return s.defense;
                case InventoryStat.MagicDefense: return s.magicDefense;
                case InventoryStat.CriticalRate: return s.criticalRate;
                case InventoryStat.CriticalMultiplier: return s.criticalMultiplier;
                case InventoryStat.MoveSpeed: return s.moveSpeed;
                case InventoryStat.AttackSpeed: return s.attackSpeed;
                case InventoryStat.AttackRange: return s.attackRange;
                case InventoryStat.FireResistance: return r != null ? r.fire : 0;
                case InventoryStat.WaterResistance: return r != null ? r.water : 0;
                case InventoryStat.WindResistance: return r != null ? r.wind : 0;
                case InventoryStat.LightningResistance: return r != null ? r.lightning : 0;
                case InventoryStat.LightResistance: return r != null ? r.light : 0;
                case InventoryStat.DarkResistance: return r != null ? r.dark : 0;
                case InventoryStat.DropRate: return s.dropRateBonus;
                default: return 0d;
            }
        }

        public static string FormatModifiers(StatPercentageModifiers s)
        {
            if (s == null) return "ステータス補正なし";

            var text = new System.Text.StringBuilder();
            for (int i = 1; i < Labels.Length; i++)
            {
                var stat = (InventoryStat)i;
                double value = GetValue(s, stat);
                if (Math.Abs(value) < 0.000001d) continue;
                if (text.Length > 0) text.Append("  ");
                text.Append(Labels[i]).Append(' ');
                if (value > 0) text.Append('+');
                text.Append((value * 100d).ToString("0.##")).Append('%');
            }

            return text.Length > 0 ? text.ToString() : "ステータス補正なし";
        }
    }

    public enum EquipmentRarity
    {
        Normal, Rare, SuperRare, EpicRare, LegendaryRare
    }

    public static class EquipmentRarityUtility
    {
        public static EquipmentRarity GetRarity(int level)
        {
            if (level <= 50) return EquipmentRarity.Normal;
            if (level <= 70) return EquipmentRarity.Rare;
            if (level <= 85) return EquipmentRarity.SuperRare;
            if (level <= 95) return EquipmentRarity.EpicRare;
            return EquipmentRarity.LegendaryRare;
        }

        public static readonly string[] Labels = {
            "ノーマル", "レア", "スーパーレア", "エピックレア",
            "レジェンダリーレア"
        };
    }
}
