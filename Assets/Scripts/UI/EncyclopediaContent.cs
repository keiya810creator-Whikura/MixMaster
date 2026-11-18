using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MixMaster.Combat;
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

        public static string SkillDetails(SkillSO skill, ElementType? ownerElement = null)
        {
            if (skill == null) return "スキル未登録";

            ElementType? element = skill.useOwnerElement ? ownerElement : skill.overrideElement;
            var text = new StringBuilder();
            if (skill.category == SkillCategory.Passive)
            {
                text.Append("パッシブ　枠にセットしている間に有効");
            }
            else
            {
                text.Append("アクティブ　");
                if (!skill.useOwnerElement)
                    text.Append(Attribute(skill.overrideElement)).Append("（固定）");
                else if (element.HasValue)
                    text.Append(Attribute(element.Value)).Append("（使用者の属性）");
                else
                    text.Append("使用者の属性で発動");
            }

            if (!string.IsNullOrWhiteSpace(skill.description))
                text.Append('\n').Append(skill.description.Trim());

            if (skill.category == SkillCategory.Passive)
                AppendPassives(text, skill.passives);
            else
            {
                AppendActiveEffects(text, skill, element);
                AppendAffixes(text, skill.affixes);
                AppendActivation(text, skill);
            }

            text.Append("\n\n<b>習得条件</b>\n必要Lv ").Append(skill.requiredLevel)
                .Append("　必要SP ").Append(skill.skillPointCost.ToString("N0", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        private static void AppendActiveEffects(StringBuilder text, SkillSO skill, ElementType? element)
        {
            bool area = skill.application == SkillApplication.EnemiesInArea || skill.application == SkillApplication.AlliesInArea;
            string targets = skill.application switch
            {
                SkillApplication.Self => "使用者",
                SkillApplication.EnemiesInArea => "範囲内の敵",
                SkillApplication.AlliesInArea => "範囲内の味方",
                _ => skill.projectile && skill.effectTiming == SkillEffectTiming.Hit ? "命中した相手" : "選んだ対象"
            };

            text.Append("\n\n<b>効果</b>\n").Append(targets).Append(
                Label((int)skill.effect, "にダメージ", "のHPを回復", "に追加効果を付与"));
            if (area)
            {
                text.Append("\n効果半径 ").Append(Number(skill.radius)).Append("　");
                text.Append(skill.hitLimit > 0 ? $"1回の効果で最大{skill.hitLimit}体" : "範囲内の全対象");
            }

            if (skill.effect == SkillBaseEffect.None) return;

            text.Append(skill.effect == SkillBaseEffect.Heal ? "\n基礎回復量 " : "\n基礎威力 ");
            if (skill.powerSource != SkillPowerSource.None)
            {
                text.Append(Label((int)skill.powerSource, "固定値", "攻撃力", "魔法攻撃力", "最大HP"))
                    .Append(" × ").Append(Number(skill.powerMultiplier));
                if (skill.flatPower > 0) text.Append(" ＋ ");
            }
            if (skill.powerSource == SkillPowerSource.None || skill.flatPower > 0)
                text.Append(skill.flatPower.ToString("N0", CultureInfo.InvariantCulture));

            if (skill.effect == SkillBaseEffect.Damage)
            {
                text.Append("\n対象の").Append(skill.powerSource == SkillPowerSource.Attack ? "防御力" : "魔法防御力");
                if (element != ElementType.None) text.Append("・属性耐性");
                text.Append("でダメージが変動　会心").Append(skill.canCritical ? "あり" : "なし");
            }
        }

        private static void AppendActivation(StringBuilder text, SkillSO skill)
        {
            text.Append("\n\n<b>発動</b>\n基礎消費MP ").Append(skill.mpCost.ToString("N0", CultureInfo.InvariantCulture))
                .Append("　基礎再使用 ").Append(Number(skill.cooldown)).Append("秒")
                .Append("\n詠唱 ").Append(skill.castTime > 0f ? Number(skill.castTime) + "秒" : "なし")
                .Append("　スキル共通待機 ").Append(Number(Math.Max(.02f, skill.globalCooldown))).Append("秒");

            if (skill.targetType == SkillTargetType.Self || skill.targetType == SkillTargetType.AreaAroundSelf)
                text.Append("\n使用者を起点に発動");
            else
            {
                text.Append("\n射程 ").Append(Number(skill.range)).Append("　");
                text.Append(skill.targetType == SkillTargetType.SingleAlly
                    ? "範囲内でHP割合が最も低い味方を選択（自身を含む）" : "範囲内で最も近い敵を選択");
            }

            if (skill.projectile) AppendProjectile(text, skill);
            else text.Append("\n発動と同時に効果が出る");
        }

        private static void AppendProjectile(StringBuilder text, SkillSO skill)
        {
            var prefab = SkillProjectile.ResolvePrefab(skill);
            var motion = SkillProjectile.ResolveMotion(skill, prefab != null ? prefab.GetComponent<SkillProjectile>()?.Defaults : null);
            text.Append("\n発射物 ").Append(ProjectileMove(motion.moveType, motion.homing))
                .Append("　持続は最大").Append(Number(skill.projectileLifetime)).Append("秒");
            if (motion.timedPhase)
                text.Append("\n").Append(Number(motion.phaseTime)).Append("秒後に").Append(ProjectileMove(motion.phaseType, motion.homing)).Append("へ切替");

            text.Append("\n").Append(skill.spawnPosition == SkillSpawnPosition.Target ? "対象の位置で生成" : "使用者の位置で生成");
            if (skill.followTarget == SkillFollowTarget.Target) text.Append("し、対象に追従");
            else if (skill.followTarget == SkillFollowTarget.Owner || (motion.followOwner &&
                motion.moveType is SkillProjectileMoveType.Stay or SkillProjectileMoveType.ProximityMine or
                    SkillProjectileMoveType.ChargeGrow or SkillProjectileMoveType.Burst))
                text.Append("し、使用者に追従");

            text.Append("　").Append(skill.effectTiming switch
            {
                SkillEffectTiming.Spawn => "生成時に効果",
                SkillEffectTiming.Interval => Number(Math.Max(.02f, skill.effectInterval)) + "秒ごとに効果",
                _ => "命中時に効果"
            });

            if (skill.effectTiming == SkillEffectTiming.Hit)
            {
                bool explodesOnHit = motion.explodeOnHit || motion.moveType == SkillProjectileMoveType.ProximityMine;
                if (!explodesOnHit && (motion.piercing || motion.moveType == SkillProjectileMoveType.Burst))
                    text.Append("\n命中後も貫通");
                if (!explodesOnHit && motion.repeatHits)
                    text.Append("\n同じ相手に ").Append(Number(Math.Max(.02f, motion.repeatInterval))).Append("秒間隔で再命中");
                if (!explodesOnHit && motion.chain && motion.chainCount > 0)
                    text.Append("\n連鎖 最大").Append(motion.chainCount).Append("回　連鎖ごとに威力×").Append(Number(Math.Max(0f, motion.chainPower)));
                if (motion.split && motion.splitDepth > 0)
                    text.Append("\n分裂 ").Append(UnityEngine.Mathf.Clamp(motion.splitCount, 1, 16)).Append("発　子弾の威力×").Append(Number(Math.Max(0f, motion.splitPower)));
                if (explodesOnHit ||
                    (motion.timedPhase && motion.phaseType == SkillProjectileMoveType.ProximityMine))
                    text.Append("\n命中時に爆発（半径 ").Append(Number(Math.Max(.01f, motion.explosionRadius))).Append('）');
            }
            if (motion.explodeOnStop && (motion.moveType == SkillProjectileMoveType.DecelerateStop ||
                (motion.timedPhase && motion.phaseType == SkillProjectileMoveType.DecelerateStop)))
                text.Append("\n減速して停止時に爆発（半径 ").Append(Number(Math.Max(.01f, motion.explosionRadius))).Append('）');
        }

        private static string ProjectileMove(SkillProjectileMoveType move, bool homing) =>
            move == SkillProjectileMoveType.Straight && homing ? "追尾" :
            Label((int)move, "直進", "追尾", "拡大直進", "その場", "ブーメラン", "周回後に射出", "蛇行", "加速",
                "減速停止", "遅延追尾", "螺旋", "往復", "感応地雷", "収束成長", "爆発");

        private static void AppendAffixes(StringBuilder text, SkillAffix[] affixes)
        {
            if (affixes == null) return;

            foreach (SkillAffix affix in affixes)
            {
                if (affix == null || affix.type == SkillAffixType.None) continue;
                text.Append("\n\n<b>");
                if (affix.type == SkillAffixType.Status)
                {
                    text.Append(Label((int)affix.ailment, "状態異常", "毒", "火傷", "凍結", "麻痺", "睡眠", "沈黙", "暗闇", "混乱"))
                        .Append("</b>\n");
                    AppendAilment(text, affix);
                }
                else
                {
                    bool debuff = affix.type == SkillAffixType.Debuff;
                    text.Append(debuff ? "デバフ" : "バフ").Append("</b>\n")
                        .Append(Label((int)affix.stat, "能力値", "攻撃力", "魔法攻撃力", "防御力", "魔法防御力", "移動速度", "攻撃速度", "攻撃範囲"))
                        .Append(' ').Append(Signed(debuff ? -affix.amount : affix.amount, affix.change == SkillChange.Percent ? "%" : ""));
                }
                text.Append("\n持続 ").Append(Number(affix.duration)).Append("秒　付与確率 ").Append(Number(affix.chance)).Append('%');
                text.Append("\n再付与時 ").Append(affix.stacking switch
                {
                    SkillStacking.Stack => $"最大{affix.stackLimit}段まで重ねがけし、持続時間を更新",
                    SkillStacking.Ignore => "効果中は追加を無視",
                    _ => "持続時間を更新"
                });
            }
        }

        private static void AppendAilment(StringBuilder text, SkillAffix affix)
        {
            if (affix.ailment == SkillAilment.Poison || affix.ailment == SkillAilment.Burn)
            {
                text.Append(Number(affix.interval)).Append("秒ごとに");
                if (affix.change == SkillChange.MaxHpPercent)
                    text.Append("対象の最大HPの").Append(Number(affix.amount)).Append("%分のダメージ");
                else
                    text.Append(Number(affix.amount)).Append("の基礎ダメージ");
                return;
            }

            text.Append(affix.ailment switch
            {
                SkillAilment.Freeze or SkillAilment.Paralysis => "移動・通常攻撃・スキルを封じる",
                SkillAilment.Sleep => "移動・通常攻撃・スキルを封じる。被ダメージで解除",
                SkillAilment.Silence => "スキルを封じる",
                SkillAilment.Blind => "通常攻撃や攻撃スキルが50%の確率で外れる",
                SkillAilment.Confusion => "敵味方を問わず、射程内の相手をランダムに狙う",
                _ => "状態異常を付与"
            });
        }

        private static void AppendPassives(StringBuilder text, SkillPassive[] passives)
        {
            if (passives == null) return;

            text.Append("\n\n<b>効果</b>");
            foreach (SkillPassive passive in passives)
            {
                if (passive == null || passive.type == SkillPassiveType.None) continue;

                text.Append('\n').Append(PassiveName(passive.type));
                if (passive.type is SkillPassiveType.Melee or SkillPassiveType.Ranged)
                {
                    text.Append("（通常攻撃）");
                    continue;
                }

                (string unit, string note, bool partyBonus) = passive.type switch
                {
                    SkillPassiveType.CriticalRate => ("ポイント", null, false),
                    SkillPassiveType.MaterialDrop => ("ポイント", "装備した本人がとどめを刺した際の素材に適用", false),
                    SkillPassiveType.EquipmentDrop => ("ポイント", "ダンジョンの通常敵から装備が落ちる確率に加算", true),
                    SkillPassiveType.BreedingChance => ("ポイント", "配合の成功率に加算（上限100%）", true),
                    SkillPassiveType.WeaponCount => ("個", "ダンジョンのボスから確定入手できる装備を追加", true),
                    SkillPassiveType.TitleRolls => ("回", "モンスター捕獲時、称号が付くまでの抽選機会を追加", true),
                    SkillPassiveType.RespawnSpeed => ("%", "敵の再出現間隔を 1 ÷（1 ＋ 補正率）倍に短縮", true),
                    SkillPassiveType.DungeonMaterials => ("%", "ダンジョンクリア報酬の固有素材を増量", true),
                    SkillPassiveType.RaidDamage => ("%", "装備した本人がレイドボスに与えるダメージを増加", false),
                    SkillPassiveType.FarmExperience => ("ポイント", $"アクティブなファームへの経験値配分率に加算（例 10% → {Number(Math.Min(100d, 10d + passive.amount))}%、上限100%）", true),
                    SkillPassiveType.DefeatGold => ("%", "敵討伐時に得られるゴールドを増量", true),
                    SkillPassiveType.DefeatExperience => ("%", "敵討伐時に得られる経験値を増量", true),
                    SkillPassiveType.HealingPower => ("%", "装備した本人が使うHP回復スキルの回復量を増加", false),
                    SkillPassiveType.MpCostReduction => ("%", "装備した本人の消費MPを軽減（上限60%、消費ありのスキルは最低1MP）", false),
                    SkillPassiveType.CooldownReduction => ("%", "装備した本人のスキルごとの再使用時間を短縮（上限60%）", false),
                    _ => ("%", null, false)
                };
                text.Append(' ').Append(Signed(passive.amount, unit));
                if (note != null) text.Append('\n').Append(note);
                if (partyBonus) text.Append("。主人公と編成中の仲間の補正を合算");
            }
        }

        private static string Attribute(ElementType value) => value == ElementType.None ? "無属性" : Element(value) + "属性";
        private static string Element(ElementType value) => Label((int)value, "無", "火", "水", "風", "雷", "光", "闇");
        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Signed(double value, string unit) => (value >= 0d ? "+" : "") + Number(value) + unit;
        private static string Label(int index, params string[] labels) => index >= 0 && index < labels.Length ? labels[index] : "未登録";
        private static string PassiveName(SkillPassiveType type) => Label((int)type,
            "なし", "最大HP", "最大MP", "攻撃", "魔力", "防御", "魔防", "移動速度", "攻撃速度", "攻撃範囲",
            "クリティカル率", "クリティカル倍率", "素材ドロップ率", "装備ドロップ率", "火属性耐性", "水属性耐性",
            "風属性耐性", "雷属性耐性", "光属性耐性", "闇属性耐性", "全属性耐性", "近接攻撃タイプになる",
            "遠距離攻撃タイプになる", "配合成功率", "武器取得数", "称号抽選回数", "敵リスポーン速度",
            "ダンジョン素材獲得量", "レイドボス与ダメージ", "ファーム経験値率", "討伐ゴールド",
            "討伐経験値", "スキル回復量", "スキル消費MP軽減", "スキル再使用短縮");
    }
}
