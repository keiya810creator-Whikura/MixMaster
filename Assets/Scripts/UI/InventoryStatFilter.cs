using System;
using MixMaster.Core;
using UnityEngine;

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

        /// <summary>
        /// Only the first 11 stat selectors have individual-value fields.
        /// Resistances and material drop rate do not have IV data and must
        /// continue to use the fully calculated CharacterStats values.
        /// </summary>
        public static bool SupportsIndividualValues(InventoryStat stat)
        {
            return stat >= InventoryStat.MaxHp && stat <= InventoryStat.AttackRange;
        }

        public static double GetIndividualValue(
            MonsterIndividualValues iv, InventoryStat stat)
        {
            if (iv == null)
                return 0d;

            switch (stat)
            {
                case InventoryStat.MaxHp: return iv.hp;
                case InventoryStat.MaxMp: return iv.mp;
                case InventoryStat.Attack: return iv.attack;
                case InventoryStat.Magic: return iv.magic;
                case InventoryStat.Defense: return iv.defense;
                case InventoryStat.MagicDefense: return iv.magicDefense;
                case InventoryStat.CriticalRate: return iv.criticalRate;
                case InventoryStat.CriticalMultiplier: return iv.criticalMultiplier;
                case InventoryStat.MoveSpeed: return iv.moveSpeed;
                case InventoryStat.AttackSpeed: return iv.attackSpeed;
                case InventoryStat.AttackRange: return iv.attackRange;
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
        Normal, Rare, SuperRare, EpicRare, LegendaryRare, GodRare
    }

    public static class EquipmentRarityUtility
    {
        public static EquipmentRarity GetRarity(int level)
        {
            if (level >= EquipmentManager.MaxEnhancedEquipmentLevel)
                return EquipmentRarity.GodRare;
            if (level <= 50) return EquipmentRarity.Normal;
            if (level <= 70) return EquipmentRarity.Rare;
            if (level <= 85) return EquipmentRarity.SuperRare;
            if (level <= 95) return EquipmentRarity.EpicRare;
            return EquipmentRarity.LegendaryRare;
        }

        public static readonly string[] Labels = {
            "ノーマル", "レア", "スーパーレア", "エピックレア",
            "レジェンダリーレア", "ゴッドレア"
        };

        public static readonly string[] FilterLabels = BuildFilterLabels();

        private static string[] BuildFilterLabels()
        {
            var labels = new string[Labels.Length + 1];
            labels[0] = "全レア度";
            Array.Copy(Labels, 0, labels, 1, Labels.Length);
            return labels;
        }

        public static string FormatInventoryName(string name, EquipmentRecord record)
        {
            int level = EquipmentEnhancement.Level(record);
            return name + " Lv" + level + " 《" + Labels[(int)GetRarity(level)] +
                "》 +" + EquipmentEnhancement.Count(record);
        }

        public static Color BackgroundColor(EquipmentRarity rarity)
        {
            switch (rarity)
            {
                case EquipmentRarity.Rare:
                    return new Color(0.13f, 0.27f, 0.43f, 1f);
                case EquipmentRarity.SuperRare:
                    return new Color(0.20f, 0.29f, 0.52f, 1f);
                case EquipmentRarity.EpicRare:
                    return new Color(0.48f, 0.18f, 0.20f, 1f);
                case EquipmentRarity.LegendaryRare:
                    return new Color(0.36f, 0.20f, 0.48f, 1f);
                case EquipmentRarity.GodRare:
                    return new Color(0.47f, 0.31f, 0.11f, 1f);
                default:
                    return new Color(0.15f, 0.19f, 0.25f, 1f);
            }
        }
    }
}
