#if UNITY_EDITOR
using System;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>Applies the shared sprite to ALL existing equipment, not only CSV rows.</summary>
    public static class EquipmentCommonSpriteAssigner
    {
        private const string SpriteFolder = "Assets/素材/装備";

        [MenuItem("MixMaster/CSV/全装備SOの共通画像を再設定")]
        public static void AssignFromMenu()
        {
            string summary = AssignAll();
            EditorUtility.DisplayDialog("装備共通画像", summary, "OK");
        }

        public static string AssignAll()
        {
            Sprite weapon = Load(SpriteFolder + "/武器.png");
            Sprite armor = Load(SpriteFolder + "/防具.png");
            Sprite accessory = Load(SpriteFolder + "/アクセサリー.png");

            int updated = 0, already = 0, missing = 0, skipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EquipmentSO", new[] { "Assets" }))
            {
                EquipmentSO item = AssetDatabase.LoadAssetAtPath<EquipmentSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;
                Sprite sprite;
                switch (item.category)
                {
                    case EquipmentCategory.Weapon: sprite = weapon; break;
                    case EquipmentCategory.Armor: sprite = armor; break;
                    case EquipmentCategory.Accessory: sprite = accessory; break;
                    default: skipped++; continue;
                }
                if (sprite == null) { missing++; continue; }
                if (item.icon == sprite) { already++; continue; }

                Undo.RecordObject(item, "Apply Equipment Category Sprite");
                item.icon = sprite;
                EditorUtility.SetDirty(item);
                updated++;
            }
            AssetDatabase.SaveAssets();
            if (weapon == null) Debug.LogWarning("[EquipmentCommonSpriteAssigner] Missing: " + SpriteFolder + "/武器.png");
            if (armor == null) Debug.LogWarning("[EquipmentCommonSpriteAssigner] Missing: " + SpriteFolder + "/防具.png");
            if (accessory == null) Debug.LogWarning("[EquipmentCommonSpriteAssigner] Missing: " + SpriteFolder + "/アクセサリー.png");
            string summary = "変更: " + updated + " / 設定済み: " + already +
                " / 画像なし: " + missing + " / 汎用等: " + skipped;
            Debug.Log("[EquipmentCommonSpriteAssigner] " + summary);
            return summary;
        }

        private static Sprite Load(string path)
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
                foreach (UnityEngine.Object o in subAssets)
                    if (o is Sprite sub) return sub;
            return null;
        }
    }
}
#endif
