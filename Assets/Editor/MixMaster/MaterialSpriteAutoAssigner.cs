#if UNITY_EDITOR
using System;
using System.IO;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class MaterialSpriteAutoAssigner
    {
        private const string MaterialSpriteFolder =
            "Assets/素材/モンスター素材";

        [MenuItem("MixMaster/CSV/素材SOスプライトを自動設定")]
        public static void AssignAllMaterialSprites()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:MaterialSO",
                    new[] { "Assets" });

            int updated = 0;
            int missing = 0;
            int skipped = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                MaterialSO material =
                    AssetDatabase.LoadAssetAtPath<MaterialSO>(
                        assetPath);

                if (material == null ||
                    string.IsNullOrWhiteSpace(material.materialId))
                {
                    skipped++;
                    continue;
                }

                if (TryApply(
                        material,
                        out string expectedSpritePath))
                {
                    updated++;
                }
                else
                {
                    missing++;

                    Debug.LogWarning(
                        "[MaterialSpriteAutoAssigner] " +
                        material.materialId +
                        " に対応するSpriteが見つかりません: " +
                        expectedSpritePath,
                        material);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message =
                "完了しました。\n\n" +
                "設定: " + updated + "\n" +
                "画像なし: " + missing + "\n" +
                "スキップ: " + skipped;

            Debug.Log(
                "[MaterialSpriteAutoAssigner] " +
                message.Replace("\n", " / "));

            EditorUtility.DisplayDialog(
                "素材SOスプライト自動設定",
                message,
                "OK");
        }

        public static bool TryApply(
            MaterialSO material,
            out string expectedSpritePath)
        {
            expectedSpritePath = string.Empty;

            if (material == null ||
                string.IsNullOrWhiteSpace(material.materialId))
            {
                return false;
            }

            if (!TryGetMaterialNumber(
                    material.materialId,
                    out int number))
            {
                return false;
            }

            expectedSpritePath =
                MaterialSpriteFolder +
                "/" +
                number +
                ".png";

            Sprite sprite =
                LoadSpriteAtPath(
                    expectedSpritePath);

            if (sprite == null)
                return false;

            if (material.icon != sprite)
            {
                Undo.RecordObject(
                    material,
                    "Assign Material Sprite");

                material.icon = sprite;

                EditorUtility.SetDirty(
                    material);
            }

            return true;
        }

        private static bool TryGetMaterialNumber(
            string materialId,
            out int number)
        {
            number = 0;

            if (string.IsNullOrWhiteSpace(materialId))
                return false;

            string value =
                materialId.Trim();

            int end = value.Length - 1;

            while (end >= 0 &&
                   !char.IsDigit(value[end]))
            {
                end--;
            }

            if (end < 0)
                return false;

            int start = end;

            while (start >= 0 &&
                   char.IsDigit(value[start]))
            {
                start--;
            }

            string numberText =
                value.Substring(
                    start + 1,
                    end - start);

            return int.TryParse(
                numberText,
                out number);
        }

        private static Sprite LoadSpriteAtPath(
            string assetPath)
        {
            Sprite direct =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    assetPath);

            if (direct != null)
                return direct;

            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    assetPath);

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            return null;
        }
    }
}
#endif
