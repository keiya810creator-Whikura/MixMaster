using System;
using System.Globalization;
using System.Text;
using MixMaster.Core;

namespace MixMaster.UI
{
    public static class PartyPresetContent
    {
        public static string Summary(PartyPresetRecord preset, MonsterManager monsters, EquipmentManager equipment, SkillProgressionManager skills)
        {
            if (preset == null) return "この枠にはまだ保存されていません。\n現在のパーティを保存できます。\n\n保存対象：プレイヤーと仲間のスキル・装備、仲間の個体と並び順、行動設定";
            var text = new StringBuilder();
            if (DateTime.TryParse(preset.savedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved))
                text.Append("保存日時 ").Append(saved.ToLocalTime().ToString("yyyy/MM/dd HH:mm")).Append('\n');
            text.Append("一括指示：").Append(Order(preset.combatOrder)).Append('\n');
            Actor(text, preset.player, "プレイヤー", monsters, equipment, skills);
            if (preset.members != null)
                for (int i = 0; i < preset.members.Count; i++)
                    Actor(text, preset.members[i], "仲間" + (i + 1), monsters, equipment, skills, i);
            if (preset.members != null && preset.members.Count == 0) text.Append("\n仲間：未編成\n");
            return text.ToString().TrimEnd();
        }

        private static void Actor(StringBuilder text, PartyPresetActor actor, string label,
            MonsterManager monsters, EquipmentManager equipment, SkillProgressionManager skills, int partySlot = -1)
        {
            if (actor == null) { text.Append("\n").Append(label).Append("：データがありません\n"); return; }
            text.Append('\n');
            bool colored = partySlot >= 0 && partySlot < 3;
            if (colored) text.Append("<color=#").Append(UnityEngine.ColorUtility.ToHtmlStringRGB(
                MaterialDropLogUI.GetSourceColor((MaterialDropSourceType)((int)MaterialDropSourceType.Party1 + partySlot)))).Append('>');
            text.Append("【").Append(label).Append("】");
            if (actor.ownerId != "player")
            {
                OwnedMonsterRecord owned = monsters != null ? monsters.FindOwnedMonster(actor.ownerId) : null;
                text.Append(Safe(actor.displayName));
                if (owned != null) text.Append(" Lv.").Append(owned.level);
                else text.Append("（未所持）");
                if (colored) text.Append("</color>");
                text.Append("\n行動：").Append(Behavior(actor.combatBehavior));
            }
            else if (colored) text.Append("</color>");
            text.Append("\n装備\n");
            for (int i = 0; i < EquipmentManager.MaxEquipmentSlots; i++)
            {
                string id = actor.equipmentIds != null && i < actor.equipmentIds.Count ? actor.equipmentIds[i] : null;
                text.Append(i + 1).Append("：");
                if (string.IsNullOrWhiteSpace(id)) text.Append("未設定");
                else
                {
                    EquipmentRecord item = equipment != null ? equipment.FindEquipment(id) : null;
                    EquipmentSO data = item != null ? equipment.GetEquipmentDefinition(item.equipmentId) : null;
                    string name = !string.IsNullOrWhiteSpace(data?.displayName) ? data.displayName :
                        actor.equipmentNames != null && i < actor.equipmentNames.Count && !string.IsNullOrWhiteSpace(actor.equipmentNames[i]) ? actor.equipmentNames[i] : id;
                    text.Append(Safe(name));
                    if (item != null) text.Append(" Lv.").Append(item.level);
                    else text.Append("（未所持）");
                }
                text.Append('\n');
            }
            text.Append("スキル\n");
            for (int i = 0; i < SkillProgressionManager.SlotCount; i++)
            {
                SkillSlot slot = actor.skills != null && i < actor.skills.Count ? actor.skills[i] : null;
                text.Append(i + 1).Append("：");
                if (slot == null || string.IsNullOrWhiteSpace(slot.skillId)) text.Append("未設定");
                else
                {
                    SkillSO skill = skills?.Catalog != null ? skills.Catalog.Get(slot.skillId) : null;
                    text.Append(Safe(!string.IsNullOrWhiteSpace(skill?.displayName) ? skill.displayName : slot.skillId));
                    if (skill != null && skill.category == SkillCategory.Passive) text.Append("（パッシブ）");
                    else
                    {
                        text.Append(" ／ ").Append(Trigger(slot.trigger, slot.threshold));
                        if (slot.andCondition != SkillTrigger.Manual) text.Append(" ＋ ").Append(Trigger(slot.andCondition, slot.andThreshold));
                        text.Append(" ／ 優先度 ").Append(slot.priority);
                    }
                }
                text.Append('\n');
            }
        }

        private static string Safe(string value) => (value ?? string.Empty).Replace("<", "<\u200B");

        private static string Trigger(SkillTrigger trigger, float value)
        {
            string n = value.ToString("0.##", CultureInfo.InvariantCulture);
            switch (trigger)
            {
                case SkillTrigger.Manual: return "手動";
                case SkillTrigger.Ready: return "使用可能時";
                case SkillTrigger.SelfHpBelow: return "自分のHP " + n + "%以下";
                case SkillTrigger.SelfMpAbove: return "自分のMP " + n + "%以上";
                case SkillTrigger.TargetHpBelow: return "対象のHP " + n + "%以下";
                case SkillTrigger.EnemyCount: return "範囲内の敵 " + n + "体以上";
                case SkillTrigger.NormalHits: return "通常攻撃 " + n + "回命中";
                case SkillTrigger.DamageTaken: return "被弾 " + n + "回";
                case SkillTrigger.Interval: return n + "秒ごと";
                case SkillTrigger.Kill: return n + "体討伐";
                default: return "未設定";
            }
        }
        private static string Order(PartyCombatOrder order)
        {
            switch (order)
            {
                case PartyCombatOrder.Individual: return "個別設定";
                case PartyCombatOrder.HostileEnemies: return "敵対対象へ接近攻撃";
                case PartyCombatOrder.PlayerAttackRange: return "プレイヤー範囲内攻撃";
                case PartyCombatOrder.AutoHunt: return "自動狩り";
                case PartyCombatOrder.NoAttack: return "攻撃しない";
                case PartyCombatOrder.OwnAttackRange: return "自身範囲内攻撃";
                default: return "未設定";
            }
        }
        private static string Behavior(MonsterCombatBehavior behavior)
        {
            switch (behavior)
            {
                case MonsterCombatBehavior.PlayerAttackRange: return "プレイヤー範囲内攻撃";
                case MonsterCombatBehavior.AutoHunt: return "自動狩り";
                case MonsterCombatBehavior.HostileEnemies: return "敵対対象へ接近攻撃";
                case MonsterCombatBehavior.NoAttack: return "攻撃しない";
                case MonsterCombatBehavior.OwnAttackRange: return "自身範囲内攻撃";
                default: return "未設定";
            }
        }
    }
}
