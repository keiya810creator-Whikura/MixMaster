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
    public static class BreedingRecipeCsvImporter
    {
        private const string Source = "Assets/DataCSV/特殊配合.csv";
        private const string Folder = "Assets/Resources/Data";
        private const string Output = Folder + "/BreedingRecipes.asset";

        [MenuItem("MixMaster/CSV/特殊配合CSVをインポート・更新")]
        public static void Import()
        {
            string sourcePath = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, Source));
            if (!File.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog("特殊配合",
                    "Assets/DataCSV/特殊配合.csv が見つかりません。", "OK");
                return;
            }

            string[] lines = File.ReadAllLines(sourcePath, Encoding.UTF8);
            int headerIndex = 0;
            if (lines.Length > 0 && !IsRecipeHeader(SplitFields(lines[0])))
                headerIndex = 1; // The grouped export has one row of section titles.

            string[] header = headerIndex < lines.Length
                ? SplitFields(lines[headerIndex])
                : Array.Empty<string>();
            if (!IsRecipeHeader(header) || (headerIndex == 1 && header.Length == 3))
            {
                EditorUtility.DisplayDialog("特殊配合",
                    "「親A,親B,配合結果」の3列、または区分名の下に同じ3列を横に並べた形式にしてください。", "OK");
                return;
            }

            MonsterCatalogBuilder.RebuildCatalog();
            MonsterCatalogSO monsters = MonsterCatalogSO.Load();
            if (monsters == null)
            {
                Debug.LogError("[BreedingRecipeCsvImporter] MonsterCatalogがありません。");
                return;
            }

            var recipes = new List<SpecialBreedingRecipe>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int skipped = 0;

            var rows = new List<(int LineNumber, string[] Fields)>();
            for (int i = headerIndex + 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] fields = SplitFields(line);
                if (fields.Length != header.Length)
                {
                    Debug.LogWarning($"[BreedingRecipeCsvImporter] {i + 1}行目: 列数が{header.Length}ではありません。");
                    skipped++;
                    continue;
                }

                rows.Add((i + 1, fields));
            }

            // The catalog has one result per unordered pair. Prefer the more
            // specific groups on the right, keeping the first recipe within a group.
            for (int column = header.Length - 3; column >= 0; column -= 3)
            {
                BreedingRecipeKind kind = header.Length == 9
                    ? (BreedingRecipeKind)(column / 3 + 1)
                    : BreedingRecipeKind.Unspecified;
                foreach (var row in rows)
                {
                    string parentA = row.Fields[column];
                    string parentB = row.Fields[column + 1];
                    string child = row.Fields[column + 2];
                    if (header.Length > 3 && parentA.Length == 0 && parentB.Length == 0 && child.Length == 0)
                        continue;

                    string location = $"[BreedingRecipeCsvImporter] {row.LineNumber}行目・{column + 1}列目";
                    if (monsters.GetMonster(parentA) == null ||
                        monsters.GetMonster(parentB) == null ||
                        monsters.GetMonster(child) == null)
                    {
                        Debug.LogWarning($"{location}: 存在しないMonsterIDです: {parentA} / {parentB} => {child}");
                        skipped++;
                        continue;
                    }

                    if (string.Equals(parentA, parentB, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning($"{location}: 同種配合は特殊レシピに登録できません。");
                        skipped++;
                        continue;
                    }

                    string key = string.Compare(parentA, parentB, StringComparison.OrdinalIgnoreCase) <= 0
                        ? parentA + "|" + parentB
                        : parentB + "|" + parentA;
                    if (!keys.Add(key))
                    {
                        Debug.LogWarning($"{location}: 登録済みの親の組み合わせをスキップしました: {key} => {child}");
                        skipped++;
                        continue;
                    }

                    recipes.Add(new SpecialBreedingRecipe
                    {
                        parentAId = parentA,
                        parentBId = parentB,
                        childId = child,
                        kind = kind
                    });
                }
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "Data");
            }

            BreedingRecipeCatalogSO asset =
                AssetDatabase.LoadAssetAtPath<BreedingRecipeCatalogSO>(Output);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BreedingRecipeCatalogSO>();
                AssetDatabase.CreateAsset(asset, Output);
            }

            Undo.RecordObject(asset, "Import Special Breeding CSV");
            asset.SetRecipes(recipes);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            string message = "特殊配合レシピ: " + recipes.Count +
                "件 / スキップ: " + skipped + "件";
            Debug.Log("[BreedingRecipeCsvImporter] " + message);
            EditorUtility.DisplayDialog("特殊配合インポート", message, "OK");
        }

        private static string[] SplitFields(string line)
        {
            string[] fields = line.TrimStart('\uFEFF').Split(',');
            for (int i = 0; i < fields.Length; i++)
                fields[i] = fields[i].Trim().Trim('"').Trim();
            return fields;
        }

        private static bool IsRecipeHeader(string[] fields)
        {
            if (fields.Length == 0 || fields.Length % 3 != 0)
                return false;

            for (int i = 0; i < fields.Length; i += 3)
            {
                if (fields[i] != "親A" || fields[i + 1] != "親B" || fields[i + 2] != "配合結果")
                    return false;
            }

            return true;
        }
    }
}
#endif
