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
    /// Validates the entire 1..9999 EXP table and imports it into a Resources
    /// ScriptableObject. A failed import never replaces the previous table.
    /// </summary>
    public static class ExperienceCsvImporter
    {
        private const string DefaultCsvPath =
            "Assets/DataCSV/経験値テーブル.csv";
        private const string OutputFolder =
            "Assets/Resources/MixMaster";
        private const string OutputAssetPath =
            OutputFolder + "/ExperienceTable.asset";
        private const string MenuName =
            "MixMaster/CSV/経験値CSVをインポート・更新";

        [MenuItem(MenuName)]
        public static void Import()
        {
            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string csvPath = !string.IsNullOrEmpty(selectedPath) &&
                string.Equals(Path.GetExtension(selectedPath),
                    ".csv", StringComparison.OrdinalIgnoreCase)
                ? selectedPath
                : DefaultCsvPath;

            if (!File.Exists(ToAbsolutePath(csvPath)))
            {
                EditorUtility.DisplayDialog(
                    "経験値CSVインポート",
                    "CSVが見つかりません。\n" +
                    "Assets/DataCSV/経験値テーブル.csv に配置するか、" +
                    "Projectビューで対象CSVを選択して実行してください。\n" +
                    csvPath, "OK");
                return;
            }

            try
            {
                string csv = ReadCsv(ToAbsolutePath(csvPath));
                List<ExperienceTableSO.ExperienceLevelRow> rows =
                    ParseAndValidate(csv);

                EnsureFolder(OutputFolder);
                ExperienceTableSO asset =
                    AssetDatabase.LoadAssetAtPath<ExperienceTableSO>(
                        OutputAssetPath);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<ExperienceTableSO>();
                    AssetDatabase.CreateAsset(asset, OutputAssetPath);
                }

                Undo.RecordObject(asset, "Import experience CSV");
                asset.ReplaceRows(rows);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                Selection.activeObject = asset;
                Debug.Log("[ExperienceCsvImporter] " + rows.Count +
                    " レベル分の経験値を取り込みました: " + csvPath +
                    " -> " + OutputAssetPath);

                EditorUtility.DisplayDialog(
                    "経験値CSVインポート",
                    rows.Count + "レベル分の取り込みが完了しました。\n" +
                    "プレイヤー/モンスター両方に適用されます。\n" +
                    "次回のPlay開始から反映されます。", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ExperienceCsvImporter] " + ex.Message);
                EditorUtility.DisplayDialog(
                    "経験値CSVインポート失敗",
                    "既存の経験値テーブルは変更していません。\n" +
                    ex.Message, "OK");
            }
        }

        private static List<ExperienceTableSO.ExperienceLevelRow>
            ParseAndValidate(string csv)
        {
            string[] lines = csv.Replace("\r\n", "\n")
                .Replace('\r', '\n').Split('\n');

            int headerLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    headerLine = i;
                    break;
                }
            }

            if (headerLine < 0)
                throw new FormatException("CSVにデータがありません。");

            List<string> header = ParseRow(lines[headerLine]);
            if (header.Count != 3 ||
                Clean(header[0]) != "Level" ||
                Clean(header[1]) != "PlayerRequiredExp" ||
                Clean(header[2]) != "MonsterRequiredExp")
            {
                throw new FormatException(
                    "ヘッダーは Level,PlayerRequiredExp,MonsterRequiredExp " +
                    "の3列で指定してください。");
            }

            var result = new List<ExperienceTableSO.ExperienceLevelRow>(
                ExperienceProgression.MaxLevel);
            int expectedLevel = 1;

            for (int index = headerLine + 1; index < lines.Length; index++)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                List<string> cells = ParseRow(line);
                if (cells.Count != 3)
                {
                    throw new FormatException((index + 1) +
                        "行目: 列数が3ではありません。");
                }

                int level;
                long playerExp;
                long monsterExp;
                const NumberStyles NumberFormat =
                    NumberStyles.Integer | NumberStyles.AllowThousands;

                if (!int.TryParse(Clean(cells[0]),
                        NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out level) ||
                    !long.TryParse(Clean(cells[1]),
                        NumberFormat, CultureInfo.InvariantCulture,
                        out playerExp) ||
                    !long.TryParse(Clean(cells[2]),
                        NumberFormat, CultureInfo.InvariantCulture,
                        out monsterExp))
                {
                    throw new FormatException((index + 1) +
                        "行目: レベルと必要EXPは整数で指定してください。");
                }

                if (level != expectedLevel)
                {
                    throw new FormatException((index + 1) +
                        "行目: Level " + expectedLevel +
                        " が必要です（実際: " + level +
                        "）。重複・欠番・順番を確認してください。");
                }

                bool finalLevel =
                    level == ExperienceProgression.MaxLevel;
                if (finalLevel
                    ? playerExp != 0L || monsterExp != 0L
                    : playerExp <= 0L || monsterExp <= 0L)
                {
                    throw new FormatException((index + 1) +
                        "行目: Lv.1～9998の必要EXPは1以上、" +
                        "Lv.9999の必要EXPは両方0にしてください。");
                }

                result.Add(new ExperienceTableSO.ExperienceLevelRow
                {
                    level = level,
                    playerRequiredExp = playerExp,
                    monsterRequiredExp = monsterExp
                });
                expectedLevel++;
            }

            if (result.Count != ExperienceProgression.MaxLevel)
            {
                throw new FormatException(
                    "Lv.1～9999の全9999行が必要です。" +
                    " 現在 " + result.Count + " 行です。");
            }

            return result;
        }

        private static List<string> ParseRow(string line)
        {
            var cells = new List<string>(3);
            var text = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length &&
                        line[i + 1] == '"')
                    {
                        text.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (ch == ',' && !quoted)
                {
                    cells.Add(text.ToString());
                    text.Length = 0;
                }
                else
                {
                    text.Append(ch);
                }
            }

            if (quoted)
                throw new FormatException("CSVの引用符が閉じられていません。");

            cells.Add(text.ToString());
            return cells;
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Trim().TrimStart('\uFEFF');
        }

        private static string ReadCsv(string path)
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

        private static string ToAbsolutePath(string assetPath)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(root, assetPath);
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            string[] names = folder.Split('/');
            for (int i = 1; i < names.Length; i++)
            {
                string next = current + "/" + names[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, names[i]);

                current = next;
            }
        }
    }
}
#endif
