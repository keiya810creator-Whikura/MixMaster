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
            if (lines.Length == 0 ||
                lines[0].TrimStart('\uFEFF').Trim() != "親A,親B,配合結果")
            {
                EditorUtility.DisplayDialog("特殊配合",
                    "ヘッダーを 親A,親B,配合結果 にしてください。", "OK");
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

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                string[] values = line.Split(',');
                if (values.Length != 3)
                {
                    Debug.LogWarning("[BreedingRecipeCsvImporter] " + (i + 1) +
                        "行目: 列数が3ではありません。");
                    skipped++;
                    continue;
                }

                string parentA = values[0].Trim().Trim('"');
                string parentB = values[1].Trim().Trim('"');
                string child = values[2].Trim().Trim('"');

                if (monsters.GetMonster(parentA) == null ||
                    monsters.GetMonster(parentB) == null ||
                    monsters.GetMonster(child) == null)
                {
                    Debug.LogWarning("[BreedingRecipeCsvImporter] " + (i + 1) +
                        "行目: 存在しないMonsterIDです: " +
                        parentA + " / " + parentB + " => " + child);
                    skipped++;
                    continue;
                }

                if (string.Equals(parentA, parentB, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[BreedingRecipeCsvImporter] " + (i + 1) +
                        "行目: 同種配合は同種が優先されるため特殊レシピに登録できません。");
                    skipped++;
                    continue;
                }

                string key = string.Compare(parentA, parentB,
                    StringComparison.OrdinalIgnoreCase) <= 0
                    ? parentA.ToUpperInvariant() + "|" + parentB.ToUpperInvariant()
                    : parentB.ToUpperInvariant() + "|" + parentA.ToUpperInvariant();

                if (!keys.Add(key))
                {
                    Debug.LogWarning("[BreedingRecipeCsvImporter] " + (i + 1) +
                        "行目: 親の組み合わせが重複しています: " + key);
                    skipped++;
                    continue;
                }

                recipes.Add(new SpecialBreedingRecipe
                {
                    parentAId = parentA,
                    parentBId = parentB,
                    childId = child
                });
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
    }
}
#endif
