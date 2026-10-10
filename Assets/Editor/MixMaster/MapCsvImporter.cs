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
    /// Imports the user's shared-scene, 15-column Map CSV. The two initial
    /// "マップの説明" headers are handled by column position:
    /// column 2 = display name, column 3 = description.
    /// </summary>
    public static class MapCsvImporter
    {
        private const string DefaultCsvPath = "Assets/DataCSV/マップ.csv";
        private const string MapFolder = "Assets/Data/Maps";
        private const string MaterialFolder = "Assets/Data/Materials";
        private const string LayoutFolder = "Assets/Data/MapLayouts";

        private static readonly string[] Headers =
        {
            "MapID", "マップの説明", "マップの説明", "サムネイル画像",
            "地形生成用JSON名", "ノーマルモンスター1", "ノーマルモンスター2",
            "ノーマルモンスター3", "ノーマルモンスター4", "ノーマルモンスター5",
            "ボスモンスター", "ダンジョン固有素材ID", "推奨レベル",
            "通常モンスターの出現レベル", "強敵の出現レベル"
        };

        [MenuItem("MixMaster/CSV/マップCSVをインポート・更新")]
        public static void ImportOrUpdate()
        {
            string csvPath = DefaultCsvPath;
            TextAsset selected = Selection.activeObject as TextAsset;
            if (selected != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selected);
                if (selectedPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    csvPath = selectedPath;
            }

            string absolutePath = Path.GetFullPath(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, csvPath));

            if (!File.Exists(absolutePath))
            {
                Debug.LogError("[MapCsvImporter] CSVが見つかりません: " + csvPath);
                EditorUtility.DisplayDialog("マップCSVインポート",
                    "CSVを保存してください:\n" + csvPath, "OK");
                return;
            }

            List<List<string>> rows;
            try
            {
                rows = ParseCsv(ReadCsvText(absolutePath));
            }
            catch (Exception ex)
            {
                Debug.LogError("[MapCsvImporter] CSV解析エラー: " + ex);
                return;
            }

            if (rows.Count < 2 || !ValidateHeader(rows[0]))
            {
                EditorUtility.DisplayDialog("マップCSVインポート",
                    "15列のヘッダーを確認してください。\n2列目はマップ名、3列目は説明文です。",
                    "OK");
                return;
            }

            EnsureFolder(MapFolder);
            EnsureFolder(MaterialFolder);

            Dictionary<string, MapSO> maps = IndexAssets<MapSO>(x => x.mapId);
            Dictionary<string, MonsterSO> monsters = IndexAssets<MonsterSO>(x => x.monsterId);
            Dictionary<string, MaterialSO> materials = IndexAssets<MaterialSO>(x => x.materialId);

            HashSet<string> imported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int created = 0;
            int updated = 0;
            int skipped = 0;
            int warnings = 0;

            for (int rowNumber = 1; rowNumber < rows.Count; rowNumber++)
            {
                List<string> row = rows[rowNumber];
                if (IsEmptyRow(row))
                    continue;

                int displayRow = rowNumber + 1;
                if (row.Count != Headers.Length)
                {
                    Debug.LogWarning("[MapCsvImporter] " + displayRow +
                        "行目: 列数が15ではありません (実際 " + row.Count + ")。スキップします。");
                    skipped++;
                    continue;
                }

                string id = Cell(row, 0);
                string displayName = Cell(row, 1);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName)
                    || !imported.Add(id))
                {
                    Debug.LogWarning("[MapCsvImporter] " + displayRow +
                        "行目: MapID/マップ名が空、またはCSV内で重複しています。");
                    skipped++;
                    continue;
                }

                // Resolve all six monster IDs before mutating any assets.
                List<MonsterSO> normal = new List<MonsterSO>(5);
                MonsterSO boss = null;
                bool valid = true;

                for (int slot = 0; slot < 6; slot++)
                {
                    string monsterId = Cell(row, 5 + slot);
                    MonsterSO monster;
                    if (string.IsNullOrWhiteSpace(monsterId) ||
                        !monsters.TryGetValue(monsterId, out monster) || monster == null)
                    {
                        Debug.LogError("[MapCsvImporter] " + displayRow +
                            "行目: MonsterSOが見つかりません: " + monsterId +
                            " / 先にモンスターCSVをインポートしてください。");
                        valid = false;
                        break;
                    }

                    if (slot < 5)
                        normal.Add(monster);
                    else
                        boss = monster;
                }

                if (!valid)
                {
                    skipped++;
                    continue;
                }

                int recommended = ParseLevel(Cell(row, 12), 1, displayRow, "推奨レベル");
                int normalLevel = ParseLevel(Cell(row, 13), 1, displayRow, "通常モンスターの出現レベル");
                int bossLevel = ParseLevel(Cell(row, 14), 5, displayRow, "強敵の出現レベル");

                bool exists = maps.TryGetValue(id, out MapSO map) && map != null;
                if (!exists)
                {
                    map = ScriptableObject.CreateInstance<MapSO>();
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                        MapFolder + "/" + SanitizeFileName(id) + ".asset");
                    AssetDatabase.CreateAsset(map, assetPath);
                }
                else
                {
                    Undo.RecordObject(map, "Update MapSO from CSV");
                }

                map.mapId = id;
                map.displayName = displayName;
                map.description = Cell(row, 2);
                map.thumbnailKey = Cell(row, 3);
                map.layoutJsonName = Path.GetFileNameWithoutExtension(Cell(row, 4));
                map.recommendedLevel = recommended;
                map.normalMonsterLevel = normalLevel;
                map.strongMonsterLevel = bossLevel;
                map.normalMonsters = normal;
                map.bossMonster = boss;

                // Preserve the existing all-monsters list used for title pools.
                map.monsters = new List<MonsterSO>(normal);
                if (!map.monsters.Contains(boss))
                    map.monsters.Add(boss);

                map.layoutJson = FindLayoutJson(map.layoutJsonName);
                if (map.layoutJson == null)
                {
                    Debug.LogWarning("[MapCsvImporter] " + id +
                        ": マップJSONがありません: " + map.layoutJsonName +
                        " (Assets/Data/MapLayouts に配置してください)");
                    warnings++;
                }

                // Store the CSV key even if the image is not imported yet.
                Sprite thumbnail = FindThumbnail(map.thumbnailKey);
                if (thumbnail != null)
                    map.thumbnail = thumbnail;
                else if (!string.IsNullOrWhiteSpace(map.thumbnailKey))
                {
                    Debug.LogWarning("[MapCsvImporter] " + id +
                        ": サムネイル画像が見つかりません: " + map.thumbnailKey +
                        " (Assets/素材/マップサムネイル に配置してください)");
                    warnings++;
                }

                string materialId = Cell(row, 11);
                if (string.IsNullOrWhiteSpace(materialId))
                {
                    map.dungeonMaterial = null;
                }
                else
                {
                    if (!materials.TryGetValue(materialId, out MaterialSO material) ||
                        material == null)
                    {
                        material = ScriptableObject.CreateInstance<MaterialSO>();
                        material.materialId = materialId;
                        material.displayName = materialId;
                        material.category = MaterialCategory.Dungeon;
                        string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                            MaterialFolder + "/" + SanitizeFileName(materialId) + ".asset");
                        AssetDatabase.CreateAsset(material, materialPath);
                        materials[materialId] = material;
                        Debug.Log("[MapCsvImporter] ダンジョン素材SOを新規作成: " + materialId);
                    }

                    // Keep existing name/icon/details when an asset already exists.
                    map.dungeonMaterial = material;
                }

                EditorUtility.SetDirty(map);
                maps[id] = map;
                if (exists) updated++; else created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string result = "新規 " + created + " / 更新 " + updated +
                " / スキップ " + skipped + " / 警告 " + warnings;
            Debug.Log("[MapCsvImporter] " + result + " / " + csvPath);
            EditorUtility.DisplayDialog("マップCSVインポート完了", result, "OK");
        }

        private static bool ValidateHeader(List<string> header)
        {
            if (header.Count != Headers.Length)
                return false;

            for (int i = 0; i < Headers.Length; i++)
            {
                string actual = Cell(header, i);
                if (i == 1 && (actual == "マップ名" || actual == "DisplayName"))
                    continue;
                if (i == 2 && actual == "Description")
                    continue;
                if (!string.Equals(actual, Headers[i], StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("[MapCsvImporter] " + (i + 1) + "列目は '" +
                        Headers[i] + "' を想定しています。実際: '" + actual + "'");
                    return false;
                }
            }

            return true;
        }

        private static int ParseLevel(string value, int fallback, int row, string column)
        {
            if (int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int number) && number >= 1)
                return number;

            Debug.LogWarning("[MapCsvImporter] " + row + "行目 '" + column +
                "' は1以上の整数で指定してください。代わりに " + fallback + " を使用します。");
            return fallback;
        }

        private static TextAsset FindLayoutJson(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                LayoutFolder + "/" + name + ".json");
            if (asset != null)
                return asset;

            string[] guids = AssetDatabase.FindAssets(name, new[] { LayoutFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileNameWithoutExtension(path)
                        .Equals(name, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
            return null;
        }

        private static Sprite FindThumbnail(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            string[] roots =
            {
                "Assets/素材/マップサムネイル/",
                "Assets/素材/MapThumbnail/",
                "Assets/素材/map/Thumbnails/"
            };
            string[] extensions = { ".png", ".jpg", ".jpeg" };

            for (int i = 0; i < roots.Length; i++)
                for (int j = 0; j < extensions.Length; j++)
                {
                    string path = roots[i] + key + extensions[j];
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                        return sprite;

                    UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (UnityEngine.Object obj in objects)
                        if (obj is Sprite found)
                            return found;
                }

            return null;
        }

        private static Dictionary<string, T> IndexAssets<T>(Func<T, string> id) where T : ScriptableObject
        {
            var index = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset == null)
                    continue;

                string key = id(asset);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!index.ContainsKey(key))
                    index.Add(key, asset);
                else
                    Debug.LogWarning("[MapCsvImporter] 重複IDの既存SO: " + key);
            }
            return index;
        }

        private static string ReadCsvText(string absolutePath)
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(932).GetString(bytes);
            }
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
                    if (quoted && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                        quoted = !quoted;
                    continue;
                }

                if (!quoted && c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    continue;
                }

                if (!quoted && (c == '\r' || c == '\n'))
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                        i++;

                    row.Add(cell.ToString());
                    cell.Length = 0;
                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
            }

            if (quoted)
                throw new FormatException("CSVの引用符 (\") が閉じられていません。");

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static string Cell(List<string> row, int index)
        {
            return index < row.Count
                ? (row[index] ?? string.Empty).Trim().TrimStart('\uFEFF')
                : string.Empty;
        }

        private static bool IsEmptyRow(List<string> row)
        {
            for (int i = 0; i < row.Count; i++)
                if (!string.IsNullOrWhiteSpace(row[i]))
                    return false;
            return true;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Map";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
