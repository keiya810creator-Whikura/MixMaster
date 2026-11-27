using System;
using System.Text;
using UnityEngine;

namespace MixMaster.Core
{
    public enum EquipmentOptionKind
    {
        Melee, MagicShot, Weakness, BossDamage,
        MaxHp, Fire, Water, Wind, Lightning, Light, Dark, BossGuard, HealingReceived,
        Material, DungeonMaterial, Experience, SkillHealing, SkillMp
    }

    [Serializable]
    public struct EquipmentDropOption
    {
        public EquipmentOptionKind kind;
        public int percent;
    }

    internal static class EquipmentDropOptions
    {
        private static readonly string[] Names =
        {
            "通常近接ダメージ", "通常魔弾ダメージ", "弱点ダメージ", "ボスへのダメージ",
            "最大HP", "火耐性", "水耐性", "風耐性", "雷耐性", "光耐性", "闇耐性",
            "ボスから受けるダメージ", "受けるHP回復量",
            "モンスター素材獲得量", "ダンジョン固有素材獲得量", "討伐経験値",
            "スキルHP回復量", "スキル消費MP"
        };

        internal static int KindCount => Names.Length;
        internal static string Name(EquipmentOptionKind kind) => Names[(int)kind];
        internal static bool ChangesCharacterStats(EquipmentOptionKind kind) =>
            kind is >= EquipmentOptionKind.MaxHp and <= EquipmentOptionKind.Dark;

        internal static bool IsReduction(EquipmentOptionKind kind) =>
            kind is EquipmentOptionKind.BossGuard or EquipmentOptionKind.SkillMp;

        internal static string FormatBonus(EquipmentOptionKind kind, int percent, bool asDifference = false)
        {
            int value = IsReduction(kind) ? -percent : percent;
            string unit = kind is >= EquipmentOptionKind.Fire and <= EquipmentOptionKind.Dark
                ? "ポイント" : asDifference ? "pt" : "%";
            return value.ToString("+0;-0;0") + unit;
        }

        internal static EquipmentDropOption[] Roll(EquipmentCategory category, bool danger)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            int count = danger
                ? (roll < 45 ? 1 : roll < 90 ? 2 : 3)
                : (roll < 20 ? 0 : roll < 75 ? 1 : roll < 97 ? 2 : 3);
            if (count == 0) return Array.Empty<EquipmentDropOption>();

            int first = category == EquipmentCategory.Armor ? (int)EquipmentOptionKind.MaxHp :
                category == EquipmentCategory.Accessory ? (int)EquipmentOptionKind.Material : 0;
            int end = category == EquipmentCategory.Weapon ? (int)EquipmentOptionKind.MaxHp :
                category == EquipmentCategory.Armor ? (int)EquipmentOptionKind.Material : Names.Length;
            var pool = new EquipmentOptionKind[end - first];
            for (int i = 0; i < pool.Length; i++) pool[i] = (EquipmentOptionKind)(first + i);

            var result = new EquipmentDropOption[count];
            for (int i = 0; i < count; i++)
            {
                int next = UnityEngine.Random.Range(i, pool.Length);
                (pool[i], pool[next]) = (pool[next], pool[i]);
                EquipmentOptionKind kind = pool[i];
                int maxPercent = kind is EquipmentOptionKind.Material or EquipmentOptionKind.DungeonMaterial ? 200 : 10;
                result[i] = new EquipmentDropOption { kind = kind, percent = UnityEngine.Random.Range(1, maxPercent + 1) };
            }
            return result;
        }

        internal static string Stars(int count) =>
            count > 0 ? " " + Highlight(new string('★', count)) : string.Empty;

        internal static string Highlight(string text) => "<color=#FFD65C>" + text + "</color>";

        internal static string Describe(EquipmentDropOption[] options)
        {
            if (options == null || options.Length == 0) return string.Empty;
            var text = new StringBuilder("\nオプション");
            foreach (var option in options)
            {
                if ((uint)option.kind >= (uint)Names.Length) continue;
                text.Append("　").Append(Name(option.kind)).Append(' ')
                    .Append(FormatBonus(option.kind, option.percent));
            }
            return text.ToString();
        }

        internal static double Bonus(string owner, EquipmentOptionKind kind) =>
            GameManager.Instance?.GetComponent<EquipmentManager>()?.OptionBonus(owner, kind) ?? 0d;

        internal static double PartyBonus(EquipmentOptionKind kind)
        {
            var root = GameManager.Instance;
            var equipment = root?.GetComponent<EquipmentManager>();
            if (equipment == null) return 0d;
            double total = equipment.OptionBonus("player", kind);
            var monsters = root.GetComponent<MonsterManager>();
            if (monsters != null)
                foreach (string owner in monsters.PartyMonsterUniqueIds)
                    total += equipment.OptionBonus(owner, kind);
            return total;
        }

        internal static string Owner(MaterialDropSourceInfo source) =>
            source?.sourceType == MaterialDropSourceType.Player ? "player" : source?.ownedMonsterUniqueId;

        internal static long Guard(long damage, string owner, bool boss)
        {
            double bonus = boss && damage > 0 ? Bonus(owner, EquipmentOptionKind.BossGuard) : 0d;
            return bonus > 0 ? Math.Max(1L, SkillMath.Amount(damage * (1d - bonus / 100d))) : damage;
        }

        internal static long IncreaseQuantity(long amount, double bonus)
        {
            if (amount <= 0 || bonus <= 0) return amount;
            double extra = amount * (bonus / 100d);
            long whole = SkillMath.Amount(extra);
            if (extra - whole > 0 && UnityEngine.Random.value < extra - whole)
                whole = LongMath.SaturatingAdd(whole, 1L);
            return LongMath.SaturatingAdd(amount, whole);
        }
    }
}
