using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MixMaster.Core;

namespace MixMaster.UI
{
    public enum EncyclopediaKind { Monster, Equipment }

    /// <summary>Read-only definitions: browsing never applies growth or equipment.</summary>
    public static class EncyclopediaContent
    {
        public static string Name(string displayName, string id) =>
            string.IsNullOrWhiteSpace(displayName) ? id ?? "未登録" : displayName;

        public static List<MonsterSO> Monsters(MonsterCatalogSO catalog) =>
            Definitions(catalog != null ? catalog.Monsters : null, x => x.monsterId);
        public static List<EquipmentSO> Equipment(EquipmentCatalogSO catalog) =>
            Definitions(catalog != null ? catalog.Equipment : null, x => x.equipmentId);

        private static List<T> Definitions<T>(IReadOnlyList<T> source, Func<T, string> id)
            where T : UnityEngine.Object
        {
            var result = new List<T>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (source != null)
                for (int i = 0; i < source.Count; i++)
                    if (source[i] != null && !string.IsNullOrWhiteSpace(id(source[i])) && seen.Add(id(source[i])))
                        result.Add(source[i]);
            result.Sort((a, b) => CompareIds(id(a), id(b)));
            return result;
        }

        public static int CompareIds(string a, string b)
        {
            a = a ?? string.Empty; b = b ?? string.Empty;
            int sa = a.LastIndexOf('_'), sb = b.LastIndexOf('_');
            string pa = sa < 0 ? string.Empty : a.Substring(0, sa + 1);
            string pb = sb < 0 ? string.Empty : b.Substring(0, sb + 1);
            if (string.Equals(pa, pb, StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(a.Substring(sa + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long na) &&
                long.TryParse(b.Substring(sb + 1), NumberStyles.None, CultureInfo.InvariantCulture, out long nb))
            {
                int order = na.CompareTo(nb);
                if (order != 0) return order;
            }
            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        public static string Habitats(MonsterSO monster, MapCatalogSO maps) =>
            MapNames(maps, map => ContainsMonster(map, monster), false);
        public static string EquipmentDungeons(EquipmentSO equipment, MapCatalogSO maps) =>
            MapNames(maps, map => ContainsEquipment(map, equipment), true);

        private static string MapNames(MapCatalogSO catalog, Func<MapSO, bool> matches, bool dungeon)
        {
            var text = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog != null && catalog.Maps != null)
                foreach (MapSO map in catalog.Maps)
                {
                    if (map == null || !matches(map)) continue;
                    string name = Name(map.displayName, map.mapId);
                    string key = string.IsNullOrWhiteSpace(map.mapId) ? name : map.mapId;
                    if (!seen.Add(key)) continue;
                    if (text.Length > 0) text.Append('\n');
                    text.Append(name);
                    if (dungeon && !name.EndsWith("ダンジョン", StringComparison.Ordinal)) text.Append(" ダンジョン");
                }
            return text.Length > 0 ? text.ToString() : "未登録";
        }

        public static bool ContainsMonster(MapSO map, MonsterSO monster)
        {
            if (map == null || monster == null) return false;
            bool Same(MonsterSO candidate) => candidate != null &&
                (candidate == monster || (!string.IsNullOrWhiteSpace(monster.monsterId) &&
                string.Equals(candidate.monsterId, monster.monsterId, StringComparison.OrdinalIgnoreCase)));
            if (Same(map.bossMonster)) return true;
            if (map.normalMonsters != null) foreach (MonsterSO candidate in map.normalMonsters) if (Same(candidate)) return true;
            if (map.monsters != null) foreach (MonsterSO candidate in map.monsters) if (Same(candidate)) return true;
            return false;
        }

        public static bool ContainsEquipment(MapSO map, EquipmentSO equipment)
        {
            if (map == null || equipment == null || map.dungeonEquipment == null) return false;
            foreach (EquipmentSO candidate in map.dungeonEquipment)
                if (candidate != null && (candidate == equipment ||
                    (!string.IsNullOrWhiteSpace(equipment.equipmentId) && string.Equals(
                        candidate.equipmentId, equipment.equipmentId, StringComparison.OrdinalIgnoreCase)))) return true;
            return false;
        }

        public static List<TitleSO> Titles(MonsterSO monster) =>
            Definitions(monster != null ? monster.titlePool : null, x => x.titleId);

        public static List<SkillSO> Skills(MonsterSO monster)
        {
            var all = new List<SkillSO>();
            if (monster != null)
            {
                if (monster.availableSkills != null) all.AddRange(monster.availableSkills);
                if (monster.enemySkills != null) all.AddRange(monster.enemySkills);
            }
            return Definitions(all, x => x.skillId);
        }

        public static string BaseStats(CharacterStats stats)
        {
            if (stats == null) return "基礎値未登録";
            string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
            string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            ElementResistanceSet r = stats.resistances ?? new ElementResistanceSet();
            return "HP " + N(stats.maxHp) + "　MP " + N(stats.maxMp) +
                "\n攻撃 " + N(stats.attack) + "　魔力 " + N(stats.magic) +
                "\n防御 " + N(stats.defense) + "　魔防 " + N(stats.magicDefense) +
                "\nクリティカル率 " + F(stats.criticalRate * 100f) + "%　倍率 ×" + F(stats.criticalMultiplier) +
                "\n移動速度 " + F(stats.moveSpeed) + "　攻撃速度 " + F(stats.attackSpeed) +
                "\n攻撃範囲 " + F(stats.attackRange) + "　属性 " + Element(stats.element) +
                "\n素材ドロップ率補正 " + Signed(stats.dropRateBonus * 100d, "%") +
                "\n火耐性 " + F(r.fire * 100f) + "%　水耐性 " + F(r.water * 100f) + "%" +
                "\n風耐性 " + F(r.wind * 100f) + "%　雷耐性 " + F(r.lightning * 100f) + "%" +
                "\n光耐性 " + F(r.light * 100f) + "%　闇耐性 " + F(r.dark * 100f) + "%";
        }

        public static string Modifiers(StatPercentageModifiers stats) =>
            InventoryStatFilter.FormatModifiers(stats).Replace("  ", "\n");

        public static string TitleDetails(TitleSO title) => title == null ? "称号未登録" :
            (title.description ?? string.Empty) + "\n\nステータス補正\n" + Modifiers(title.modifiers);

        public static string SkillDetails(SkillSO skill)
        {
            if (skill == null) return "スキル未登録";
            var text = new StringBuilder(skill.description ?? string.Empty);
            text.Append("\n\n").Append(skill.category == SkillCategory.Passive ? "パッシブスキル" : "アクティブスキル")
                .Append("\n必要レベル ").Append(skill.requiredLevel).Append("　習得ポイント ").Append(skill.skillPointCost);
            if (skill.category == SkillCategory.Passive)
            {
                text.Append("\n枠にセットしている間のみ発動");
                if (skill.passives != null) foreach (SkillPassive passive in skill.passives)
                {
                    if (passive == null || passive.type == SkillPassiveType.None) continue;
                    text.Append('\n').Append(PassiveName(passive.type));
                    if (passive.type != SkillPassiveType.Melee && passive.type != SkillPassiveType.Ranged)
                        text.Append(' ').Append(Signed(passive.amount,
                            passive.type == SkillPassiveType.WeaponCount || passive.type == SkillPassiveType.TitleRolls ? "" : "%"));
                }
            }
            else
            {
                text.Append("\n消費MP ").Append(skill.mpCost).Append("　再使用 ").Append(Number(skill.cooldown)).Append("秒")
                    .Append("\n詠唱 ").Append(Number(skill.castTime)).Append("秒　共通待機 ").Append(Number(skill.globalCooldown)).Append("秒")
                    .Append("\n対象 ").Append(Label((int)skill.targetType, "自身", "敵単体", "自身の周囲", "対象の周囲", "味方単体"))
                    .Append("\n射程 ").Append(Number(skill.range)).Append("　効果半径 ").Append(Number(skill.radius))
                    .Append("\n属性 ").Append(skill.useOwnerElement ? "使用者の属性" : Element(skill.overrideElement))
                    .Append("\n基本効果 ").Append(Label((int)skill.effect, "ダメージ", "HP回復", "付与効果のみ"))
                    .Append("\n威力 ").Append(Label((int)skill.powerSource, "固定値", "攻撃", "魔力", "最大HP"))
                    .Append(" ×").Append(Number(skill.powerMultiplier)).Append(" ＋ ").Append(skill.flatPower)
                    .Append("\nクリティカル ").Append(skill.canCritical ? "有効" : "無効");
                if (skill.effectTiming == SkillEffectTiming.Interval)
                    text.Append("\n効果間隔 ").Append(Number(skill.effectInterval)).Append("秒");
            }
            if (skill.affixes != null) foreach (SkillAffix affix in skill.affixes)
            {
                if (affix == null || affix.type == SkillAffixType.None) continue;
                text.Append("\n\n追加効果：").Append(Label((int)affix.type, "なし", "バフ", "デバフ", "状態異常"));
                if (affix.type == SkillAffixType.Status)
                    text.Append(' ').Append(Label((int)affix.ailment, "なし", "毒", "火傷", "凍結", "麻痺", "睡眠", "沈黙", "暗闇", "混乱"));
                else text.Append(' ').Append(Label((int)affix.stat, "なし", "攻撃", "魔力", "防御", "魔防", "移動速度", "攻撃速度", "攻撃範囲"));
                text.Append(' ').Append(Number(affix.amount));
                if (affix.change == SkillChange.Percent || affix.change == SkillChange.MaxHpPercent) text.Append('%');
                text.Append("\n持続 ").Append(Number(affix.duration)).Append("秒　成功率 ").Append(Number(affix.chance)).Append('%');
                if (affix.interval > 0f) text.Append("　間隔 ").Append(Number(affix.interval)).Append("秒");
                text.Append("\n重複 ").Append(Label((int)affix.stacking, "時間を更新", "重ねがけ", "追加を無視"));
                if (affix.stacking == SkillStacking.Stack) text.Append("　上限 ").Append(affix.stackLimit);
            }
            return text.ToString().Trim();
        }

        private static string Element(ElementType value) => Label((int)value, "無", "火", "水", "風", "雷", "光", "闇");
        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Signed(double value, string unit) => (value >= 0d ? "+" : "") + Number(value) + unit;
        private static string Label(int index, params string[] labels) => index >= 0 && index < labels.Length ? labels[index] : "未登録";
        private static string PassiveName(SkillPassiveType type) => Label((int)type,
            "なし", "最大HP", "最大MP", "攻撃", "魔力", "防御", "魔防", "移動速度", "攻撃速度", "攻撃範囲",
            "クリティカル率", "クリティカル倍率", "素材ドロップ率", "装備ドロップ率", "火属性耐性", "水属性耐性",
            "風属性耐性", "雷属性耐性", "光属性耐性", "闇属性耐性", "全属性耐性", "近接攻撃タイプになる",
            "遠距離攻撃タイプになる", "配合成功率", "武器取得数", "称号抽選回数", "敵リスポーン速度");
    }
}
