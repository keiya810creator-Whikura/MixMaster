#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Imports 素材.csv (ID, 名前, 種別; 説明 is optional), updating MaterialSO by ID.
    /// This class never deletes materials which are absent from the CSV.
    /// </summary>
    public static class MaterialCsvImporter
    {
        private const string CsvPath = "Assets/DataCSV/素材.csv";
        private const string OutputFolder = "Assets/Data/Materials";

        private sealed class Item
        {
            public string id;
            public string name;
            public string description;
            public MaterialCategory category;
        }

        [MenuItem("MixMaster/CSV/素材CSVをインポート・更新")]
        public static void ImportOrUpdate()
        {
            bool ok = Import(false, out string result);
            EditorUtility.DisplayDialog(ok ? "素材CSVインポート完了" : "素材CSVインポート失敗",
                result, "OK");
        }

        /// <summary>Silent batch entry point; logs all errors and warnings.</summary>
        public static bool Import(bool silent, out string summary)
        {
            string csvPath = CsvPath;
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
                Debug.LogError("[MaterialCsvImporter] " + summary);
                return false;
            }

            List<Item> items;
            try
            {
                byte[] bytes = File.ReadAllBytes(absolute);
                string text;
                try { text = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException) { text = Encoding.GetEncoding(932).GetString(bytes); }
                items = ParseAndValidate(text);
            }
            catch (Exception ex)
            {
                summary = "CSVの検証に失敗しました。SOは変更していません。\n" + ex.Message;
                Debug.LogError("[MaterialCsvImporter] " + summary);
                return false;
            }

            try
            {
                EnsureFolder(OutputFolder);
                var existing = new Dictionary<string, MaterialSO>(StringComparer.OrdinalIgnoreCase);
                foreach (string guid in AssetDatabase.FindAssets("t:MaterialSO", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    MaterialSO asset = AssetDatabase.LoadAssetAtPath<MaterialSO>(path);
                    if (asset == null || string.IsNullOrWhiteSpace(asset.materialId))
                        continue;
                    if (!existing.ContainsKey(asset.materialId))
                        existing.Add(asset.materialId, asset);
                    else
                        Debug.LogWarning("[MaterialCsvImporter] 既存の素材ID重複: " + asset.materialId + " / " + path);
                }

                int created = 0, updated = 0, missing = 0;
                foreach (Item item in items)
                {
                    if (!existing.TryGetValue(item.id, out MaterialSO asset) || asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<MaterialSO>();
                        string path = AssetDatabase.GenerateUniqueAssetPath(
                            OutputFolder + "/" + CleanFilename(item.id) + ".asset");
                        AssetDatabase.CreateAsset(asset, path);
                        existing[item.id] = asset;
                        created++;
                    }
                    else
                    {
                        Undo.RecordObject(asset, "Import Material CSV");
                        updated++;
                    }

                    asset.materialId = item.id;
                    asset.displayName = item.name;
                    asset.category = item.category;
                    // 説明列がなければ手動入力済みの説明を残す
                    if (item.description != null)
                        asset.description = item.description;

                    if (!MaterialSpriteAutoAssigner.TryApply(asset, out string expected))
                    {
                        if (item.category != MaterialCategory.Other)
                        {
                            missing++;
                            Debug.LogWarning("[MaterialCsvImporter] " + item.id +
                                ": スプライトが見つかりません: " + expected +
                                " (既存アイコンは維持します)", asset);
                        }
                    }
                    EditorUtility.SetDirty(asset);
                }
                AssetDatabase.SaveAssets();
                summary = "新規: " + created + " / 更新: " + updated +
                    " / 画像なし: " + missing + "\n保存先: " + OutputFolder;
                Debug.Log("[MaterialCsvImporter] " + summary.Replace("\n", " / "));
                return true;
            }
            catch (Exception ex)
            {
                summary = "SOの更新中にエラーが発生しました（途中まで更新された可能性があります）。\n" + ex;
                Debug.LogError("[MaterialCsvImporter] " + summary);
                return false;
            }
        }

        private static List<Item> ParseAndValidate(string csv)
        {
            List<List<string>> rows = ParseCsv(csv);
            if (rows.Count < 2)
                throw new FormatException("データ行がありません。");

            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++)
            {
                string key = (rows[0][i] ?? "").Trim().TrimStart('\uFEFF');
                if (key.Length > 0)
                {
                    if (headers.ContainsKey(key)) throw new FormatException("列名の重複: " + key);
                    headers.Add(key, i);
                }
            }
            foreach (string required in new[] { "ID", "名前", "種別" })
                if (!headers.ContainsKey(required))
                    throw new FormatException("必須列がありません: " + required);

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<Item>();
            for (int i = 1; i < rows.Count; i++)
            {
                List<string> row = rows[i];
                bool empty = true;
                foreach (string c in row) if (!string.IsNullOrWhiteSpace(c)) { empty = false; break; }
                if (empty) continue;

                string id = Cell(row, headers, "ID").Trim();
                string name = Cell(row, headers, "名前").Trim();
                string type = Cell(row, headers, "種別").Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                    throw new FormatException((i + 1) + "行目: ID・名前は必須です。");
                if (!ids.Add(id))
                    throw new FormatException((i + 1) + "行目: IDが重複しています: " + id);

                MaterialCategory category;
                switch (type.ToLowerInvariant())
                {
                    case "monster": case "モンスター": category = MaterialCategory.Monster; break;
                    case "dungeon": case "ダンジョン": category = MaterialCategory.Dungeon; break;
                    case "other": case "その他": category = MaterialCategory.Other; break;
                    default: throw new FormatException((i + 1) + "行目: 不正な種別: " + type);
                }

                items.Add(new Item
                {
                    id = id, name = name, category = category,
                    description = headers.ContainsKey("説明") ? Cell(row, headers, "説明") : null
                });
            }
            if (items.Count == 0)
                throw new FormatException("有効な行がありません。");
            return items;
        }

        private static string Cell(List<string> row, Dictionary<string, int> headers, string column)
        {
            return headers.TryGetValue(column, out int index) && index >= 0 && index < row.Count
                ? row[index] ?? "" : "";
        }

        private static List<List<string>> ParseCsv(string csv)
        {
            var result = new List<List<string>>();
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
                else if (!quoted && (c == '\n' || c == '\r'))
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Length = 0;
                    result.Add(row); row = new List<string>();
                }
                else cell.Append(c);
            }
            if (quoted) throw new FormatException("CSVの引用符が閉じられていません。");
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); result.Add(row); }
            return result;
        }

        private static string CleanFilename(string file)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
            return file;
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
