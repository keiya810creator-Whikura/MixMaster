#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// MAT_001 => モンスター素材/1.png
    /// MATD_001 => ダンジョン素材/1.png
    /// Missing artwork never clears an existing icon.
    /// Replaces the older MaterialSpriteAutoAssigner.cs (same class name).
    /// </summary>
    public static class MaterialSpriteAutoAssigner
    {
        private const string MonsterFolder = "Assets/素材/モンスター素材";
        private const string DungeonFolder = "Assets/素材/ダンジョン素材";

        [MenuItem("MixMaster/CSV/素材SOスプライトを自動設定")]
        public static void AssignAllMaterialSprites()
        {
            string result = AssignAll(false);
            EditorUtility.DisplayDialog("素材画像の自動設定", result, "OK");
        }

        public static string AssignAll(bool silent)
        {
            string[] guids = AssetDatabase.FindAssets("t:MaterialSO", new[] { "Assets" });
            int matched = 0, missing = 0, skipped = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MaterialSO material = AssetDatabase.LoadAssetAtPath<MaterialSO>(path);
                if (material == null || string.IsNullOrWhiteSpace(material.materialId) ||
                    material.category == MaterialCategory.Other)
                {
                    skipped++;
                    continue;
                }

                if (TryApply(material, out string expected))
                    matched++;
                else
                {
                    missing++;
                    Debug.LogWarning("[MaterialSpriteAutoAssigner] 画像なし: " +
                        material.materialId + " => " + expected, material);
                }
            }

            AssetDatabase.SaveAssets();
            string summary = "画像設定済み: " + matched + " / 画像なし: " + missing +
                " / 対象外: " + skipped;
            Debug.Log("[MaterialSpriteAutoAssigner] " + summary);
            return summary;
        }

        public static bool TryApply(MaterialSO material, out string expectedSpritePath)
        {
            expectedSpritePath = string.Empty;
            if (material == null || string.IsNullOrWhiteSpace(material.materialId))
                return false;

            string folder;
            if (material.category == MaterialCategory.Monster)
                folder = MonsterFolder;
            else if (material.category == MaterialCategory.Dungeon)
                folder = DungeonFolder;
            else
                return false;

            if (!TryGetMaterialNumber(material.materialId, out int number))
            {
                expectedSpritePath = folder + "/<ID末尾の数字>.png";
                return false;
            }

            expectedSpritePath = folder + "/" + number.ToString(CultureInfo.InvariantCulture) + ".png";
            Sprite sprite = LoadSpriteAtPath(expectedSpritePath);
            if (sprite == null)
                return false;

            if (material.icon != sprite)
            {
                Undo.RecordObject(material, "Assign Material Sprite");
                material.icon = sprite;
                EditorUtility.SetDirty(material);
            }
            return true;
        }

        private static bool TryGetMaterialNumber(string id, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            string value = id.Trim();
            int underscore = value.LastIndexOf('_');
            string suffix = underscore >= 0 ? value.Substring(underscore + 1) : value;
            if (suffix.Length == 0)
                return false;
            foreach (char c in suffix)
                if (c < '0' || c > '9')
                    return false;

            return int.TryParse(suffix, NumberStyles.None,
                CultureInfo.InvariantCulture, out number) && number >= 0;
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                return sprite;

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets != null)
                foreach (UnityEngine.Object candidate in subAssets)
                    if (candidate is Sprite subSprite)
                        return subSprite;
            return null;
        }
    }
}
#endif
