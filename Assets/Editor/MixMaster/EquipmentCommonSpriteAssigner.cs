#if UNITY_EDITOR
using System;
using System.Globalization;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>Applies numbered sprites and category fallbacks to existing equipment.</summary>
    public static class EquipmentCommonSpriteAssigner
    {
        private const string SpriteFolder = "Assets/素材/装備";

        [MenuItem("MixMaster/CSV/全装備SOの画像を再設定")]
        public static void AssignFromMenu()
        {
            string summary = AssignAll();
            EditorUtility.DisplayDialog("装備画像", summary, "OK");
        }

        public static string AssignAll()
        {
            Sprite weapon = Load(SpriteFolder + "/武器.png");
            Sprite armor = Load(SpriteFolder + "/防具.png");
            Sprite accessory = Load(SpriteFolder + "/アクセサリー.png");

            int updated = 0, already = 0, missing = 0, numbered = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EquipmentSO", new[] { "Assets" }))
            {
                EquipmentSO item = AssetDatabase.LoadAssetAtPath<EquipmentSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;

                Sprite sprite = LoadNumberedSprite(item.equipmentId);
                if (sprite != null)
                    numbered++;
                else
                {
                    switch (item.category)
                    {
                        case EquipmentCategory.Weapon: sprite = weapon; break;
                        case EquipmentCategory.Armor: sprite = armor; break;
                        case EquipmentCategory.Accessory: sprite = accessory; break;
                    }
                }

                if (sprite == null) { missing++; continue; }
                if (item.icon == sprite) { already++; continue; }

                Undo.RecordObject(item, "Apply Equipment Sprite");
                item.icon = sprite;
                EditorUtility.SetDirty(item);
                updated++;
            }
            AssetDatabase.SaveAssets();
            string summary = "番号画像: " + numbered + " / 変更: " + updated +
                " / 設定済み: " + already + " / 画像なし: " + missing;
            Debug.Log("[EquipmentCommonSpriteAssigner] " + summary);
            return summary;
        }

        private static Sprite LoadNumberedSprite(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId)) return null;

            string number = equipmentId.Trim();
            if (number.StartsWith("EQP_", StringComparison.OrdinalIgnoreCase))
                number = number.Substring(4);
            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                return null;

            return Load(SpriteFolder + "/" + id.ToString(CultureInfo.InvariantCulture) + ".png");
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
