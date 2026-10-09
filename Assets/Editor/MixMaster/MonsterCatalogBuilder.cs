#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    [InitializeOnLoad]
    public static class MonsterCatalogBuilder
    {
        private const string CatalogFolder =
            "Assets/Resources/Data";

        private const string CatalogPath =
            "Assets/Resources/Data/MonsterCatalog.asset";

        static MonsterCatalogBuilder()
        {
            EditorApplication.delayCall +=
                EnsureCatalog;
        }

        [MenuItem("MixMaster/Data/モンスターカタログを更新")]
        public static void RebuildCatalog()
        {
            EnsureFolder(CatalogFolder);

            List<MonsterSO> monsters =
                FindAssets<MonsterSO>(
                    x => x.monsterId);

            List<TitleSO> titles =
                FindAssets<TitleSO>(
                    x => x.titleId);

            MonsterCatalogSO catalog =
                AssetDatabase.LoadAssetAtPath<MonsterCatalogSO>(
                    CatalogPath);

            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<MonsterCatalogSO>();

                AssetDatabase.CreateAsset(
                    catalog,
                    CatalogPath);
            }

            Undo.RecordObject(
                catalog,
                "Update Monster Catalog");

            catalog.SetData(
                monsters,
                titles);

            EditorUtility.SetDirty(
                catalog);

            AssetDatabase.SaveAssets();

            Debug.Log(
                "[MonsterCatalogBuilder] 更新完了: Monster=" +
                monsters.Count +
                " / Title=" +
                titles.Count);
        }

        private static void EnsureCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RebuildCatalog();
        }

        private static List<T> FindAssets<T>(
            Func<T, string> idSelector)
            where T : ScriptableObject
        {
            List<T> result =
                new List<T>();

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:" + typeof(T).Name,
                    new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                T asset =
                    AssetDatabase.LoadAssetAtPath<T>(
                        path);

                if (asset != null)
                    result.Add(asset);
            }

            result.Sort(
                (a, b) =>
                    string.Compare(
                        idSelector(a) ?? string.Empty,
                        idSelector(b) ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase));

            return result;
        }

        private static void EnsureFolder(
            string folderPath)
        {
            string[] parts =
                folderPath
                    .Replace('\\', '/')
                    .Split('/');

            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
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
    }
}
#endif
