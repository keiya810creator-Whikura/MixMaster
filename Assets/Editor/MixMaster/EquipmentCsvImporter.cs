#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Replaces the existing importer. 画像名 is deliberately ignored:
    /// 武器/防具/アクセサリー are each assigned one shared sprite.
    /// Updates EquipmentSO by equipmentId and rebuilds EquipmentCatalogSO.
    /// </summary>
    public static class EquipmentCsvImporter
    {
        private const string DefaultCsvPath = "Assets/DataCSV/装備.csv";
        private const string EquipmentFolder = "Assets/Data/Equipment";
        private const string CatalogFolder = "Assets/Resources/Data";
        private const string CatalogPath = CatalogFolder + "/EquipmentCatalog.asset";
        private const string SpriteFolder = "Assets/素材/装備";

        private static readonly string[] RequiredHeaders =
        {
            "ID", "名前", "基礎HP", "基礎MP", "基礎攻撃力", "基礎魔力",
            "基礎防御力", "基礎魔防", "クリ率", "クリ倍", "移動速度",
            "攻撃速度", "攻撃範囲", "火耐性", "水耐性", "風耐性",
            "雷耐性", "光耐性", "闇耐性", "素材ドロ率"
        };

        private sealed class CsvEquipment
        {
            public string id;
            public string name;
            public string description;
            public EquipmentCategory category;
            public StatPercentageModifiers modifiers;
        }

        [MenuItem("MixMaster/CSV/装備CSVをインポート・更新")]
        public static void ImportOrUpdate()
        {
            bool ok = Import(false, out string result);
            EditorUtility.DisplayDialog(ok ? "装備CSVインポート完了" : "装備CSVインポート失敗",
                result, "OK");
        }

        public static bool Import(bool silent, out string summary)
        {
            string csvPath = DefaultCsvPath;
            if (!silent)
            {
                string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selected) && selected.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    csvPath = selected;
            }

            string absolute = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, csvPath));
            if (!File.Exists(absolute))
            {
                summary = "CSVが見つかりません: " + csvPath;
                Debug.LogError("[EquipmentCsvImporter] " + summary);
                return false;
            }

            List<CsvEquipment> entries;
            try
            {
                byte[] bytes = File.ReadAllBytes(absolute);
                string csv;
                try { csv = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException) { csv = Encoding.GetEncoding(932).GetString(bytes); }
                entries = ParseAndValidate(csv);
            }
            catch (Exception ex)
            {
                summary = "CSVの検証に失敗しました。既存SOは変更していません。\n" + ex.Message;
                Debug.LogError("[EquipmentCsvImporter] " + summary);
                return false;
            }

            try
            {
                EnsureFolder(EquipmentFolder);
                EnsureFolder(CatalogFolder);
                var existing = ReadExistingEquipment();
                int created = 0, updated = 0, missingSprites = 0;

                foreach (CsvEquipment entry in entries)
                {
                    if (!existing.TryGetValue(entry.id, out EquipmentSO equipment) || equipment == null)
                    {
                        equipment = ScriptableObject.CreateInstance<EquipmentSO>();
                        string file = Sanitize(entry.id + "_" + entry.name);
                        string path = AssetDatabase.GenerateUniqueAssetPath(
                            EquipmentFolder + "/" + file + ".asset");
                        AssetDatabase.CreateAsset(equipment, path);
                        existing[entry.id] = equipment;
                        created++;
                    }
                    else
                    {
                        Undo.RecordObject(equipment, "Import Equipment CSV");
                        updated++;
                    }

                    equipment.equipmentId = entry.id;
                    equipment.displayName = entry.name;
                    equipment.description = entry.description;
                    equipment.category = entry.category;
                    equipment.modifiers = entry.modifiers;

                    string spriteName = GetCommonSpriteName(entry.category);
                    if (spriteName != null)
                    {
                        string expected = SpriteFolder + "/" + spriteName + ".png";
                        Sprite sprite = LoadSprite(expected);
                        if (sprite != null)
                            equipment.icon = sprite;
                        else
                        {
                            missingSprites++;
                            Debug.LogWarning("[EquipmentCsvImporter] " + entry.id +
                                ": 共通画像が見つかりません: " + expected +
                                " (既存アイコンは維持します)", equipment);
                        }
                    }
                    EditorUtility.SetDirty(equipment);
                }

                // Preserve SO references not listed in this CSV.
                var allEquipment = new List<EquipmentSO>(existing.Values);
                allEquipment.Sort((a, b) => string.Compare(a.equipmentId, b.equipmentId,
                    StringComparison.OrdinalIgnoreCase));

                EquipmentCatalogSO catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalogSO>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<EquipmentCatalogSO>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                else Undo.RecordObject(catalog, "Import Equipment Catalog");

                catalog.SetData(allEquipment);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                // Includes older EquipmentSO assets not present in the current CSV.
                string sharedSpriteSummary = EquipmentCommonSpriteAssigner.AssignAll();

                summary = "新規: " + created + " / 更新: " + updated +
                    " / 共通画像なし: " + missingSprites + "\n装備SO: " + EquipmentFolder +
                    "\n装備カタログ: " + CatalogPath +
                    "\n既存装備の共通画像: " + sharedSpriteSummary;
                Debug.Log("[EquipmentCsvImporter] " + summary.Replace("\n", " / "));
                return true;
            }
            catch (Exception ex)
            {
                summary = "SO更新中にエラーが発生しました（途中まで更新された可能性があります）。\n" + ex;
                Debug.LogError("[EquipmentCsvImporter] " + summary);
                return false;
            }
        }

        private static string GetCommonSpriteName(EquipmentCategory category)
        {
            switch (category)
            {
                case EquipmentCategory.Weapon: return "武器";
                case EquipmentCategory.Armor: return "防具";
                case EquipmentCategory.Accessory: return "アクセサリー";
                default: return null;
            }
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets != null)
                foreach (UnityEngine.Object obj in subAssets)
                    if (obj is Sprite subSprite) return subSprite;
            return null;
        }

        private static List<CsvEquipment> ParseAndValidate(string csv)
        {
            List<List<string>> rows = ParseCsv(csv);
            if (rows.Count < 2) throw new FormatException("CSVにデータ行がありません。");

            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++)
            {
                string cell = (rows[0][i] ?? "").Trim().TrimStart('\uFEFF');
                if (cell.Length == 0) continue;
                if (header.ContainsKey(cell)) throw new FormatException("列名が重複しています: " + cell);
                header.Add(cell, i);
            }
            foreach (string required in RequiredHeaders)
                if (!header.ContainsKey(required))
                    throw new FormatException("必要な列がありません: " + required);

            var entries = new List<CsvEquipment>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < rows.Count; i++)
            {
                List<string> row = rows[i];
                bool empty = true;
                foreach (string col in row)
                    if (!string.IsNullOrWhiteSpace(col)) { empty = false; break; }
                if (empty) continue;

                string id = Cell(row, header, "ID").Trim();
                string name = Cell(row, header, "名前").Trim();
                if (id.Length == 0 || name.Length == 0)
                    throw new FormatException((i + 1) + "行目: IDと名前は必須です。");
                if (!seen.Add(id)) throw new FormatException((i + 1) + "行目: IDが重複しています: " + id);

                EquipmentCategory category;
                string type = Cell(row, header, "種類").Trim().ToLowerInvariant();
                switch (type)
                {
                    case "": case "any": case "汎用": category = EquipmentCategory.Any; break;
                    case "weapon": case "武器": category = EquipmentCategory.Weapon; break;
                    case "armor": case "防具": category = EquipmentCategory.Armor; break;
                    case "accessory": case "アクセ": case "アクセサリー": category = EquipmentCategory.Accessory; break;
                    default: throw new FormatException((i + 1) + "行目: 不明な装備種類: " + type);
                }

                var mods = new StatPercentageModifiers
                {
                    maxHp = ReadNumber(row, header, "基礎HP", i),
                    maxMp = ReadNumber(row, header, "基礎MP", i),
                    attack = ReadNumber(row, header, "基礎攻撃力", i),
                    magic = ReadNumber(row, header, "基礎魔力", i),
                    defense = ReadNumber(row, header, "基礎防御力", i),
                    magicDefense = ReadNumber(row, header, "基礎魔防", i),
                    criticalRate = ReadNumber(row, header, "クリ率", i),
                    criticalMultiplier = ReadNumber(row, header, "クリ倍", i),
                    moveSpeed = ReadNumber(row, header, "移動速度", i),
                    attackSpeed = ReadNumber(row, header, "攻撃速度", i),
                    attackRange = ReadNumber(row, header, "攻撃範囲", i),
                    dropRateBonus = ReadNumber(row, header, "素材ドロ率", i),
                    resistanceBonus = new ElementResistanceSet
                    {
                        fire = ReadNumber(row, header, "火耐性", i),
                        water = ReadNumber(row, header, "水耐性", i),
                        wind = ReadNumber(row, header, "風耐性", i),
                        lightning = ReadNumber(row, header, "雷耐性", i),
                        light = ReadNumber(row, header, "光耐性", i),
                        dark = ReadNumber(row, header, "闇耐性", i)
                    }
                };
                mods.resistanceBonus.ClampAll();
                mods.dropRateBonus = Mathf.Max(0f, mods.dropRateBonus);

                entries.Add(new CsvEquipment
                {
                    id = id, name = name,
                    description = Cell(row, header, "説明").Trim(),
                    category = category, modifiers = mods
                });
            }
            if (entries.Count == 0)
                throw new FormatException("有効な装備データ行がありません。");
            return entries;
        }

        private static float ReadNumber(List<string> row, Dictionary<string, int> header, string column, int rowIndex)
        {
            string input = Cell(row, header, column).Trim();
            if (input.Length == 0) return 0f;
            if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException((rowIndex + 1) + "行目: " + column + " に不正な数値: " + input);
            return value;
        }

        private static string Cell(List<string> row, Dictionary<string, int> header, string column)
        {
            return header.TryGetValue(column, out int index) && index >= 0 && index < row.Count
                ? row[index] ?? "" : "";
        }

        private static List<List<string>> ParseCsv(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (!quoted && c == ',') { row.Add(cell.ToString()); cell.Length = 0; }
                else if (!quoted && (c == '\r' || c == '\n'))
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Length = 0;
                    rows.Add(row); row = new List<string>();
                }
                else cell.Append(c);
            }
            if (quoted) throw new FormatException("CSVの引用符が閉じられていません。");
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        private static Dictionary<string, EquipmentSO> ReadExistingEquipment()
        {
            var map = new Dictionary<string, EquipmentSO>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:EquipmentSO", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EquipmentSO so = AssetDatabase.LoadAssetAtPath<EquipmentSO>(path);
                if (so == null || string.IsNullOrWhiteSpace(so.equipmentId)) continue;
                if (!map.ContainsKey(so.equipmentId)) map.Add(so.equipmentId, so);
                else Debug.LogWarning("[EquipmentCsvImporter] 既存の装備ID重複: " + so.equipmentId + " / " + path);
            }
            return map;
        }

        private static string Sanitize(string name)
        {
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return name;
        }

        private static void EnsureFolder(string folder)
        {
            string parent = "Assets";
            string[] parts = folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }
}
#endif
