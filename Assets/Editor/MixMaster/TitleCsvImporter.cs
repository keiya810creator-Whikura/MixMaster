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
    public static class TitleCsvImporter
    {
        private const string CsvAssetPath =
            "Assets/DataCSV/称号.csv";

        private const string TitleOutputFolder =
            "Assets/Data/Titles";

        [MenuItem("MixMaster/CSV/称号CSVをインポート・更新")]
        public static void ImportOrUpdate()
        {
            string absoluteCsvPath =
                GetAbsoluteProjectPath(CsvAssetPath);

            if (!File.Exists(absoluteCsvPath))
            {
                Debug.LogError(
                    "[TitleCsvImporter] CSVが見つかりません: " +
                    CsvAssetPath);

                EditorUtility.DisplayDialog(
                    "称号CSVインポート",
                    "CSVが見つかりません。\n\n" +
                    CsvAssetPath,
                    "OK");

                return;
            }

            EnsureFolder(TitleOutputFolder);

            string csvText =
                ReadCsvText(absoluteCsvPath);

            List<List<string>> rows =
                ParseCsv(csvText);

            if (rows.Count <= 1)
            {
                EditorUtility.DisplayDialog(
                    "称号CSVインポート",
                    "CSVにデータ行がありません。",
                    "OK");

                return;
            }

            Dictionary<string, int> header =
                BuildHeaderMap(rows[0]);

            string[] requiredHeaders =
            {
                "ID",
                "名前",
                "基礎HP",
                "基礎MP",
                "基礎攻撃力",
                "基礎魔力",
                "基礎防御力",
                "基礎魔防",
                "クリ率",
                "クリ倍",
                "移動速度",
                "攻撃速度",
                "攻撃範囲",
                "火耐性",
                "水耐性",
                "風耐性",
                "雷耐性",
                "光耐性",
                "闇耐性",
                "素材ドロ率",
                "抽選ウェイト"
            };

            for (int i = 0; i < requiredHeaders.Length; i++)
            {
                if (header.ContainsKey(requiredHeaders[i]))
                    continue;

                Debug.LogError(
                    "[TitleCsvImporter] 必要な列がありません: " +
                    requiredHeaders[i]);

                EditorUtility.DisplayDialog(
                    "称号CSVインポート",
                    "必要な列がありません:\n" +
                    requiredHeaders[i],
                    "OK");

                return;
            }

            Dictionary<string, TitleSO> titlesById =
                BuildTitleMap();

            HashSet<string> importedIds =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            int createdCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;
            int warningCount = 0;

            try
            {
                for (int rowIndex = 1;
                     rowIndex < rows.Count;
                     rowIndex++)
                {
                    List<string> row = rows[rowIndex];

                    if (IsEmptyRow(row))
                        continue;

                    string titleId =
                        GetCell(row, header, "ID").Trim();

                    if (string.IsNullOrWhiteSpace(titleId))
                    {
                        skippedCount++;
                        warningCount++;

                        Debug.LogWarning(
                            "[TitleCsvImporter] " +
                            (rowIndex + 1) +
                            "行目: IDが空なのでスキップしました。");

                        continue;
                    }

                    if (!importedIds.Add(titleId))
                    {
                        skippedCount++;
                        warningCount++;

                        Debug.LogWarning(
                            "[TitleCsvImporter] " +
                            (rowIndex + 1) +
                            "行目: ID '" +
                            titleId +
                            "' がCSV内で重複しているためスキップしました。");

                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "称号CSVインポート",
                        titleId + " を更新中...",
                        (float)rowIndex /
                        Mathf.Max(1, rows.Count - 1));

                    bool isNew =
                        !titlesById.TryGetValue(
                            titleId,
                            out TitleSO title) ||
                        title == null;

                    if (isNew)
                    {
                        title =
                            ScriptableObject.CreateInstance<TitleSO>();

                        string displayName =
                            GetCell(
                                row,
                                header,
                                "名前").Trim();

                        string fileName =
                            SanitizeFileName(
                                titleId + "_" + displayName);

                        string assetPath =
                            AssetDatabase.GenerateUniqueAssetPath(
                                TitleOutputFolder +
                                "/" +
                                fileName +
                                ".asset");

                        AssetDatabase.CreateAsset(
                            title,
                            assetPath);

                        titlesById[titleId] = title;
                        createdCount++;
                    }
                    else
                    {
                        Undo.RecordObject(
                            title,
                            "Update TitleSO from CSV");

                        updatedCount++;
                    }

                    try
                    {
                        ApplyRow(
                            title,
                            row,
                            header);

                        EditorUtility.SetDirty(title);
                    }
                    catch (Exception ex)
                    {
                        warningCount++;

                        Debug.LogError(
                            "[TitleCsvImporter] " +
                            (rowIndex + 1) +
                            "行目 / " +
                            titleId +
                            " の更新に失敗しました。\n" +
                            ex);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string resultMessage =
                "完了しました。\n\n" +
                "新規作成: " + createdCount + "\n" +
                "更新: " + updatedCount + "\n" +
                "スキップ: " + skippedCount + "\n" +
                "警告: " + warningCount;

            Debug.Log(
                "[TitleCsvImporter] " +
                resultMessage.Replace("\n", " / "));

            EditorUtility.DisplayDialog(
                "称号CSVインポート",
                resultMessage,
                "OK");
        }

        private static void ApplyRow(
            TitleSO title,
            List<string> row,
            Dictionary<string, int> header)
        {
            title.titleId =
                GetCell(row, header, "ID").Trim();

            title.displayName =
                GetCell(row, header, "名前").Trim();

            title.modifiers ??=
                new StatPercentageModifiers();

            title.modifiers.resistanceBonus ??=
                new ElementResistanceSet();

            title.modifiers.maxHp =
                ReadFloat(
                    row,
                    header,
                    "基礎HP",
                    0f);

            title.modifiers.maxMp =
                ReadFloat(
                    row,
                    header,
                    "基礎MP",
                    0f);

            title.modifiers.attack =
                ReadFloat(
                    row,
                    header,
                    "基礎攻撃力",
                    0f);

            title.modifiers.magic =
                ReadFloat(
                    row,
                    header,
                    "基礎魔力",
                    0f);

            title.modifiers.defense =
                ReadFloat(
                    row,
                    header,
                    "基礎防御力",
                    0f);

            title.modifiers.magicDefense =
                ReadFloat(
                    row,
                    header,
                    "基礎魔防",
                    0f);

            title.modifiers.criticalRate =
                ReadFloat(
                    row,
                    header,
                    "クリ率",
                    0f);

            title.modifiers.criticalMultiplier =
                ReadFloat(
                    row,
                    header,
                    "クリ倍",
                    0f);

            title.modifiers.moveSpeed =
                ReadFloat(
                    row,
                    header,
                    "移動速度",
                    0f);

            title.modifiers.attackSpeed =
                ReadFloat(
                    row,
                    header,
                    "攻撃速度",
                    0f);

            title.modifiers.attackRange =
                ReadFloat(
                    row,
                    header,
                    "攻撃範囲",
                    0f);

            title.modifiers.resistanceBonus.fire =
                ReadFloat(
                    row,
                    header,
                    "火耐性",
                    0f);

            title.modifiers.resistanceBonus.water =
                ReadFloat(
                    row,
                    header,
                    "水耐性",
                    0f);

            title.modifiers.resistanceBonus.wind =
                ReadFloat(
                    row,
                    header,
                    "風耐性",
                    0f);

            title.modifiers.resistanceBonus.lightning =
                ReadFloat(
                    row,
                    header,
                    "雷耐性",
                    0f);

            title.modifiers.resistanceBonus.light =
                ReadFloat(
                    row,
                    header,
                    "光耐性",
                    0f);

            title.modifiers.resistanceBonus.dark =
                ReadFloat(
                    row,
                    header,
                    "闇耐性",
                    0f);

            title.modifiers.resistanceBonus.ClampAll();

            title.modifiers.dropRateBonus =
                Mathf.Max(
                    0f,
                    ReadFloat(
                        row,
                        header,
                        "素材ドロ率",
                        0f));

            title.selectionWeight =
                Mathf.Max(
                    0.0001f,
                    ReadFloat(
                        row,
                        header,
                        "抽選ウェイト",
                        1f));
        }

        private static Dictionary<string, TitleSO>
            BuildTitleMap()
        {
            Dictionary<string, TitleSO> result =
                new Dictionary<string, TitleSO>(
                    StringComparer.OrdinalIgnoreCase);

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:TitleSO",
                    new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                TitleSO asset =
                    AssetDatabase.LoadAssetAtPath<TitleSO>(
                        path);

                if (asset == null ||
                    string.IsNullOrWhiteSpace(asset.titleId))
                {
                    continue;
                }

                if (!result.ContainsKey(asset.titleId))
                {
                    result.Add(
                        asset.titleId,
                        asset);
                }
                else
                {
                    Debug.LogWarning(
                        "[TitleCsvImporter] 重複したTitleIDがあります: " +
                        asset.titleId +
                        " / " +
                        path);
                }
            }

            return result;
        }

        private static string ReadCsvText(
            string absolutePath)
        {
            byte[] bytes =
                File.ReadAllBytes(absolutePath);

            try
            {
                return new UTF8Encoding(
                    false,
                    true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    return Encoding.GetEncoding(
                        932).GetString(bytes);
                }
                catch
                {
                    return Encoding.UTF8.GetString(bytes);
                }
            }
        }

        private static List<List<string>> ParseCsv(
            string text)
        {
            List<List<string>> rows =
                new List<List<string>>();

            List<string> row =
                new List<string>();

            StringBuilder cell =
                new StringBuilder();

            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    if (inQuotes &&
                        i + 1 < text.Length &&
                        text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    continue;
                }

                if (!inQuotes &&
                    (c == '\r' || c == '\n'))
                {
                    if (c == '\r' &&
                        i + 1 < text.Length &&
                        text[i + 1] == '\n')
                    {
                        i++;
                    }

                    row.Add(cell.ToString());
                    cell.Clear();

                    rows.Add(row);
                    row = new List<string>();
                    continue;
                }

                cell.Append(c);
            }

            if (cell.Length > 0 ||
                row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            return rows;
        }

        private static Dictionary<string, int>
            BuildHeaderMap(
                List<string> headerRow)
        {
            Dictionary<string, int> result =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headerRow.Count; i++)
            {
                string name =
                    (headerRow[i] ?? string.Empty)
                    .Trim()
                    .TrimStart('\uFEFF');

                if (!string.IsNullOrWhiteSpace(name) &&
                    !result.ContainsKey(name))
                {
                    result.Add(name, i);
                }
            }

            return result;
        }

        private static string GetCell(
            List<string> row,
            Dictionary<string, int> header,
            string column)
        {
            if (!header.TryGetValue(
                    column,
                    out int index))
            {
                return string.Empty;
            }

            if (index < 0 ||
                index >= row.Count)
            {
                return string.Empty;
            }

            return row[index] ?? string.Empty;
        }

        private static float ReadFloat(
            List<string> row,
            Dictionary<string, int> header,
            string column,
            float fallback)
        {
            string value =
                GetCell(
                    row,
                    header,
                    column).Trim();

            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float result))
            {
                return result;
            }

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out result))
            {
                return result;
            }

            Debug.LogWarning(
                "[TitleCsvImporter] " +
                column +
                " の数値を読めません: '" +
                value +
                "'");

            return fallback;
        }

        private static bool IsEmptyRow(
            List<string> row)
        {
            if (row == null ||
                row.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < row.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                    return false;
            }

            return true;
        }

        private static void EnsureFolder(
            string folderPath)
        {
            string normalized =
                folderPath.Replace('\\', '/');

            string[] parts =
                normalized.Split('/');

            if (parts.Length == 0 ||
                parts[0] != "Assets")
            {
                throw new ArgumentException(
                    "Assets配下のフォルダを指定してください: " +
                    folderPath);
            }

            string current = "Assets";

            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current = next;
            }
        }

        private static string GetAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath).FullName;

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath));
        }

        private static string SanitizeFileName(
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Unnamed";

            char[] invalid =
                Path.GetInvalidFileNameChars();

            StringBuilder builder =
                new StringBuilder(fileName.Length);

            for (int i = 0;
                 i < fileName.Length;
                 i++)
            {
                char c = fileName[i];

                bool isInvalid = false;

                for (int j = 0;
                     j < invalid.Length;
                     j++)
                {
                    if (c == invalid[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }

                builder.Append(
                    isInvalid ? '_' : c);
            }

            return builder.ToString();
        }
    }
}
#endif
