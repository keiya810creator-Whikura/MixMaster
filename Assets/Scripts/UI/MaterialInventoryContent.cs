using System;
using System.Collections.Generic;
using System.Globalization;
using MixMaster.Core;

namespace MixMaster.UI
{
    /// <summary>Read-only lookup of material names, icons and every registered drop source.</summary>
    public sealed class MaterialInventoryContent
    {
        public sealed class Entry
        {
            public string Id;
            public MaterialSO Material;
            public long Amount;
            public string Name => Material != null ? EncyclopediaContent.Name(Material.displayName, Id) : Id;
            public string Count => Amount.ToString("N0", CultureInfo.InvariantCulture);
        }

        private sealed class Definition
        {
            public MaterialSO material;
            public readonly List<string> enemies = new List<string>(), dungeons = new List<string>();
            public readonly HashSet<string> enemyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> dungeonIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        private readonly Dictionary<string, Definition> definitions = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);

        public MaterialInventoryContent(MonsterCatalogSO monsters, MapCatalogSO maps)
        {
            if (monsters != null && monsters.Monsters != null)
                foreach (MonsterSO monster in monsters.Monsters) AddMonster(monster);
            if (maps == null || maps.Maps == null) return;
            foreach (MapSO map in maps.Maps)
            {
                if (map == null) continue;
                AddMonster(map.bossMonster);
                if (map.normalMonsters != null) foreach (MonsterSO monster in map.normalMonsters) AddMonster(monster);
                if (map.monsters != null) foreach (MonsterSO monster in map.monsters) AddMonster(monster);
                Definition definition = Register(map.dungeonMaterial);
                if (definition == null) continue;
                string name = EncyclopediaContent.Name(map.displayName, map.mapId);
                string id = string.IsNullOrWhiteSpace(map.mapId) ? name : map.mapId;
                if (definition.dungeonIds.Add(id))
                    definition.dungeons.Add(name.EndsWith("ダンジョン", StringComparison.Ordinal) ? name : name + " ダンジョン");
            }
        }

        private Definition Register(MaterialSO material)
        {
            if (material == null || string.IsNullOrWhiteSpace(material.materialId)) return null;
            if (!definitions.TryGetValue(material.materialId, out Definition result))
            {
                result = new Definition { material = material };
                definitions.Add(material.materialId, result);
            }
            return result;
        }

        private void AddMonster(MonsterSO monster)
        {
            if (monster == null) return;
            Definition definition = Register(monster.uniqueMaterial);
            if (definition == null) return;
            string name = EncyclopediaContent.Name(monster.displayName, monster.monsterId);
            string id = string.IsNullOrWhiteSpace(monster.monsterId) ? name : monster.monsterId;
            if (definition.enemyIds.Add(id)) definition.enemies.Add(name);
        }

        public List<Entry> Owned(IReadOnlyList<MaterialInventoryRecord> records)
        {
            var result = new List<Entry>();
            if (records != null) foreach (MaterialInventoryRecord record in records)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.materialId) || record.amount <= 0) continue;
                definitions.TryGetValue(record.materialId, out Definition definition);
                result.Add(new Entry { Id = record.materialId, Amount = record.amount, Material = definition?.material });
            }
            result.Sort((a, b) => EncyclopediaContent.CompareIds(a.Id, b.Id));
            return result;
        }

        public string Sources(string id)
        {
            definitions.TryGetValue(id ?? string.Empty, out Definition definition);
            string Enemies() => definition != null && definition.enemies.Count > 0 ? string.Join("\n", definition.enemies) : "なし";
            string Dungeons() => definition != null && definition.dungeons.Count > 0 ? string.Join("\n", definition.dungeons) : "なし";
            return definition == null ? "入手元：未登録" : "ドロップする敵\n" + Enemies() + "\n\nドロップするダンジョン\n" + Dungeons();
        }
    }
}
