using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MixMaster.Core;

namespace MixMaster.UI
{
    public sealed class GameGuideContent
    {
        public sealed class Target
        {
            public GoalKind kind;
            public string id, name, sources;
        }
        public readonly List<Target> Targets = new List<Target>();
        public GameGuideContent(MonsterCatalogSO monsters, EquipmentCatalogSO equipment, MapCatalogSO maps)
        {
            var materials = new MaterialInventoryContent(monsters, maps);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(GoalKind kind, string id, string name, string sources)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(kind + ":" + id)) return;
                Targets.Add(new Target { kind = kind, id = id, name = EncyclopediaContent.Name(name, id), sources = sources });
            }
            void Material(MaterialSO x) { if (x != null) Add(GoalKind.Material, x.materialId, x.displayName, materials.Sources(x.materialId)); }
            void Monster(MonsterSO x)
            {
                if (x == null) return;
                Add(GoalKind.Monster, x.monsterId, x.displayName, "生息地\n" + EncyclopediaContent.Habitats(x, maps)); Material(x.uniqueMaterial);
            }
            if (monsters?.Monsters != null) foreach (var x in monsters.Monsters) Monster(x);
            if (maps?.Maps != null) foreach (var map in maps.Maps)
            {
                if (map == null) continue;
                Monster(map.bossMonster);
                if (map.normalMonsters != null) foreach (var x in map.normalMonsters) Monster(x);
                if (map.monsters != null) foreach (var x in map.monsters) Monster(x);
                Material(map.dungeonMaterial);
            }
            if (equipment?.Equipment != null) foreach (var x in equipment.Equipment)
                if (x != null) Add(GoalKind.Equipment, x.equipmentId, x.displayName, "入手ダンジョン\n" + EncyclopediaContent.EquipmentDungeons(x, maps));
            Targets.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind) : EncyclopediaContent.CompareIds(a.id, b.id));
        }
        public Target Resolve(GoalRecord goal) => Targets.Find(x => x.kind == goal.kind && string.Equals(x.id, goal.definitionId, StringComparison.OrdinalIgnoreCase))
            ?? new Target { kind = goal.kind, id = goal.definitionId, name = EncyclopediaContent.Name(goal.name, goal.definitionId), sources = "入手先：未登録" };
        public static long Owned(GoalRecord goal, MonsterManager monsters, EquipmentManager equipment, MaterialInventoryManager materials)
        {
            long count = 0;
            bool Same(string id) => string.Equals(id, goal.definitionId, StringComparison.OrdinalIgnoreCase);
            if (goal.kind == GoalKind.Monster && monsters != null)
                foreach (var x in monsters.OwnedMonsters) { if (x != null && Same(x.monsterId)) count = LongMath.SaturatingAdd(count, 1); }
            if (goal.kind == GoalKind.Equipment && equipment != null)
                foreach (var x in equipment.Inventory) { if (x != null && Same(x.equipmentId)) count = LongMath.SaturatingAdd(count, 1); }
            if (goal.kind == GoalKind.Material && materials != null)
                foreach (var x in materials.Records) { if (x != null && Same(x.materialId)) count = LongMath.SaturatingAdd(count, Math.Max(0, x.amount)); }
            return count;
        }
        public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        public static string Date(string utc) => DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)
            ? date.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") : "日時不明";
        public static string Monster(JournalMonster x) => x == null ? "なし" : EncyclopediaContent.Name(x.name, x.monsterId) + " Lv." + x.level + (string.IsNullOrWhiteSpace(x.title) ? "" : " ［" + x.title + "］");
        public static string History(BreedingHistoryRecord x)
        {
            var text = new StringBuilder(Date(x.utc)).Append(x.succeeded ? "　成功" : "　失敗");
            text.Append("\n").Append(x.specialRecipe ? "特殊配合" : "通常配合").Append("　成功率 ").Append(x.successPercent).Append("％\n\n親1\n").Append(Monster(x.parentA));
            text.Append("\n\n親2\n").Append(Monster(x.parentB)).Append("\n\n子\n").Append(Monster(x.child));
            text.Append("\n\n継承スキル\n").Append(x.inheritedSkills.Count == 0 ? "なし" : string.Join("\n", x.inheritedSkills));
            if (!x.succeeded) text.Append(x.parentsProtected ? "\n\n失敗時の親保護あり" : "\n\n親は消費されました。");
            return text.ToString();
        }
    }
}
