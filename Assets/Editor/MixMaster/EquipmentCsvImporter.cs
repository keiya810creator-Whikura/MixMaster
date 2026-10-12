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
    /// TitleCSV-compatible equipment importer, without selection weight.
    /// Validates every row before touching SOs, updates by Equipment ID,
    /// optionally resolves named PNGs, and rebuilds the runtime catalog.
    /// </summary>
    public static class EquipmentCsvImporter
    {
        private const string DefaultCsvPath = "Assets/DataCSV/装備.csv";
        private const string EquipmentFolder = "Assets/Data/Equipment";
        private const string CatalogFolder = "Assets/Resources/Data";
        private const string CatalogPath =
            CatalogFolder + "/EquipmentCatalog.asset";
        private const string SpriteFolder = "Assets/素材/装備";

        private static readonly string[] RequiredHeaders =
        {
            "ID", "名前", "基礎HP", "基礎MP",
            "基礎攻撃力", "基礎魔力", "基礎防御力", "基礎魔防",
            "クリ率", "クリ倍", "移動速度", "攻撃速度",
            "攻撃範囲", "火耐性", "水耐性", "風耐性",
            "雷耐性", "光耐性", "闇耐性", "素材ドロ率"
        };

        private sealed class CsvEquipment
        {
            public string id;
            public string name;
            public string description;
            public string iconName;
            public EquipmentCategory category;
            public StatPercentageModifiers modifiers;
        }

        [MenuItem("MixMaster/CSV/装備CSVをインポート・更新")]
        public static void ImportOrUpdate()
        {
            string selectedPath = AssetDatabase.GetAssetPath(
                Selection.activeObject);
            string csvPath = !string.IsNullOrEmpty(selectedPath) &&
                string.Equals(Path.GetExtension(selectedPath),
                    ".csv", StringComparison.OrdinalIgnoreCase)
                    ? selectedPath : DefaultCsvPath;

            string absolutePath = ToAbsolutePath(csvPath);
            if (!File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog(
                    "装備CSVインポート",
                    "CSVが見つかりません。\n" + DefaultCsvPath +
                    "\nまたはProjectビューで対象CSVを選択して実行してください。",
                    "OK");
                return;
            }

            List<CsvEquipment> entries;
            try
            {
                entries = ParseAndValidate(ReadText(absolutePath));
            }
            catch (Exception ex)
            {
                Debug.LogError("[EquipmentCsvImporter] " + ex.Message);
                EditorUtility.DisplayDialog(
                    "装備CSVインポート失敗",
                    "CSVが不正なため既存SOは変更していません。\n" +
                    ex.Message, "OK");
                return;
            }

            EnsureFolder(EquipmentFolder);
            EnsureFolder(CatalogFolder);
            EnsureFolder(SpriteFolder);
            AssetDatabase.Refresh();

            var existing = ReadExistingEquipment();
            int created = 0;
            int updated = 0;
            int missingSprites = 0;

            try
            {
                foreach (CsvEquipment entry in entries)
                {
                    bool isNew = !existing.TryGetValue(
                        entry.id, out EquipmentSO equipment) ||
                        equipment == null;

                    if (isNew)
                    {
                        equipment =
                            ScriptableObject.CreateInstance<EquipmentSO>();
                        string file = Sanitize(entry.id + "_" + entry.name);
                        string path = AssetDatabase.GenerateUniqueAssetPath(
                            EquipmentFolder + "/" + file + ".asset");
                        AssetDatabase.CreateAsset(equipment, path);
                        existing.Add(entry.id, equipment);
                        created++;
                    }
                    else
                    {
                        Undo.RecordObject(
                            equipment, "Update Equipment from CSV");
                        updated++;
                    }

                    equipment.equipmentId = entry.id;
                    equipment.displayName = entry.name;
                    equipment.description = entry.description;
                    equipment.category = entry.category;
                    equipment.modifiers = entry.modifiers;

                    if (!string.IsNullOrEmpty(entry.iconName))
                    {
                        Sprite sprite = TryGetSprite(entry.iconName);
                        if (sprite != null)
                            equipment.icon = sprite;
                        else
                        {
                            missingSprites++;
                            Debug.LogWarning(
                                "[EquipmentCsvImporter] " + entry.id +
                                ": 画像がありません: " + SpriteFolder +
                                "/" + entry.iconName + ".png");
                        }
                    }

                    EditorUtility.SetDirty(equipment);
                }

                // Keep definitions absent from this CSV so that an import
                // cannot silently erase existing equipment references.
                var allEquipment = new List<EquipmentSO>(existing.Values);
                allEquipment.Sort((a, b) =>
                    string.Compare(a.equipmentId, b.equipmentId,
                        StringComparison.OrdinalIgnoreCase));

                EquipmentCatalogSO catalog =
                    AssetDatabase.LoadAssetAtPath<EquipmentCatalogSO>(
                        CatalogPath);

                if (catalog == null)
                {
                    catalog =
                        ScriptableObject.CreateInstance<EquipmentCatalogSO>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                else
                {
                    Undo.RecordObject(
                        catalog, "Update Equipment Catalog from CSV");
                }

                catalog.SetData(allEquipment);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError("[EquipmentCsvImporter] インポート中のエラー: " + ex);
                EditorUtility.DisplayDialog(
                    "装備CSVインポート",
                    "アセット更新中にエラーが発生しました。" +
                    "Consoleを確認してください。\n" + ex.Message, "OK");
                return;
            }

            string message = "新規: " + created +
                " / 更新: " + updated +
                " / 画像なし: " + missingSprites +
                "\n装備SO: " + EquipmentFolder +
                "\n実行時カタログ: " + CatalogPath;

            Debug.Log("[EquipmentCsvImporter] " + message);
            EditorUtility.DisplayDialog("装備CSVインポート完了", message, "OK");
        }

        private static List<CsvEquipment> ParseAndValidate(string csv)
        {
            List<List<string>> rows = ParseCsv(csv);
            if (rows.Count < 2)
                throw new FormatException("CSVにデータ行がありません。");

            var header = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rows[0].Count; i++)
            {
                string cell = (rows[0][i] ?? string.Empty)
                    .Trim().TrimStart('\uFEFF');
                if (!string.IsNullOrEmpty(cell))
                {
                    if (header.ContainsKey(cell))
                        throw new FormatException("重複した列名: " + cell);
                    header.Add(cell, i);
                }
            }

            foreach (string required in RequiredHeaders)
            {
                if (!header.ContainsKey(required))
                    throw new FormatException("必要な列がありません: " + required);
            }

            var result = new List<CsvEquipment>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < rows.Count; i++)
            {
                List<string> row = rows[i];
                if (IsEmpty(row))
                    continue;

                string id = Cell(row, header, "ID").Trim();
                string name = Cell(row, header, "名前").Trim();
                if (id.Length == 0 || name.Length == 0)
                    throw new FormatException((i + 1) +
                        "行目: IDと名前は必須です。");

                if (!seen.Add(id))
                    throw new FormatException((i + 1) +
                        "行目: 重複した装備ID: " + id);

                string type = Cell(row, header, "種類").Trim();
                EquipmentCategory category;
                switch (type.ToLowerInvariant())
                {
                    case "": case "汎用": case "any":
                        category = EquipmentCategory.Any; break;
                    case "武器": case "weapon":
                        category = EquipmentCategory.Weapon; break;
                    case "防具": case "armor":
                        category = EquipmentCategory.Armor; break;
                    case "アクセサリー": case "アクセ": case "accessory":
                        category = EquipmentCategory.Accessory; break;
                    default:
                        throw new FormatException((i + 1) +
                            "行目: 種類は武器・防具・アクセサリー・汎用のみ: " +
                            type);
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
                    dropRateBonus = ReadNumber(
                        row, header, "素材ドロ率", i),
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

                result.Add(new CsvEquipment
                {
                    id = id,
                    name = name,
                    category = category,
                    description = Cell(row, header, "説明").Trim(),
                    iconName = Cell(row, header, "画像名").Trim(),
                    modifiers = mods
                });
            }

            if (result.Count == 0)
                throw new FormatException("有効な装備データ行がありません。");

            return result;
        }

        private static float ReadNumber(
            List<string> row, Dictionary<string, int> header,
            string name, int index)
        {
            string input = Cell(row, header, name).Trim();
            if (input.Length == 0)
                return 0f;

            if (!float.TryParse(input, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new FormatException((index + 1) +
                    "行目の " + name +
                    " は数値で指定してください: " + input);
            }

            return value;
        }

        private static string Cell(
            List<string> row, Dictionary<string, int> header, string name)
        {
            return header.TryGetValue(name, out int index) &&
                   index >= 0 && index < row.Count
                ? row[index] ?? string.Empty : string.Empty;
        }

        private static bool IsEmpty(List<string> row)
        {
            foreach (string item in row)
                if (!string.IsNullOrWhiteSpace(item))
                    return false;
            return true;
        }

        private static List<List<string>> ParseCsv(string content)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var currentCell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '"')
                {
                    if (quoted && i + 1 < content.Length &&
                        content[i + 1] == '"')
                    {
                        currentCell.Append('"');
                        i++;
                    }
                    else
                        quoted = !quoted;
                }
                else if (!quoted && c == ',')
                {
                    currentRow.Add(currentCell.ToString());
                    currentCell.Length = 0;
                }
                else if (!quoted && (c == '\r' || c == '\n'))
                {
                    if (c == '\r' && i + 1 < content.Length &&
                        content[i + 1] == '\n')
                        i++;

                    currentRow.Add(currentCell.ToString());
                    currentCell.Length = 0;
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                }
                else
                    currentCell.Append(c);
            }

            if (quoted)
                throw new FormatException("CSVの引用符が閉じられていません。");

            if (currentCell.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentCell.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }

        private static Dictionary<string, EquipmentSO> ReadExistingEquipment()
        {
            var result = new Dictionary<string, EquipmentSO>(
                StringComparer.OrdinalIgnoreCase);

            string[] guids = AssetDatabase.FindAssets("t:EquipmentSO",
                new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EquipmentSO equipment =
                    AssetDatabase.LoadAssetAtPath<EquipmentSO>(path);

                if (equipment == null ||
                    string.IsNullOrWhiteSpace(equipment.equipmentId))
                    continue;

                if (!result.ContainsKey(equipment.equipmentId))
                    result.Add(equipment.equipmentId, equipment);
                else
                    Debug.LogWarning(
                        "[EquipmentCsvImporter] 装備IDが重複しています: " +
                        equipment.equipmentId + " / " + path);
            }

            return result;
        }

        private static Sprite TryGetSprite(string iconName)
        {
            if (iconName.Contains("/") || iconName.Contains("\\") ||
                iconName.Contains(".."))
                return null;

            string file = Path.GetExtension(iconName).Length > 0
                ? iconName : iconName + ".png";
            string path = SpriteFolder + "/" + file;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;

            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null &&
                importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(932).GetString(bytes);
            }
        }

        private static string ToAbsolutePath(string path)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, path);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Equipment";

            foreach (char ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return name;
        }

        private static void EnsureFolder(string folder)
        {
            string parent = "Assets";
            string[] parts = folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string current = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(current))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = current;
            }
        }
    }
}
#endif
