#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MixMaster.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MixMaster.EditorTools
{
    [InitializeOnLoad]
    public static class MapCatalogBuilder
    {
        private const string Folder = "Assets/Resources/Data";
        private const string Path = Folder + "/MapCatalog.asset";

        static MapCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) RebuildCatalog();
            };
        }

        [MenuItem("MixMaster/Data/マップカタログを更新")]
        public static void RebuildCatalog()
        {
            string parent = "Assets";
            foreach (string part in new[] { "Resources", "Data" })
            {
                if (!AssetDatabase.IsValidFolder(parent + "/" + part))
                    AssetDatabase.CreateFolder(parent, part);
                parent += "/" + part;
            }
            var maps = new List<MapSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:MapSO", new[] { "Assets" }))
            {
                MapSO map = AssetDatabase.LoadAssetAtPath<MapSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (map != null && !string.IsNullOrWhiteSpace(map.mapId)) maps.Add(map);
            }
            maps.Sort((a, b) => string.Compare(a.mapId, b.mapId, StringComparison.Ordinal));
            MapCatalogSO catalog = AssetDatabase.LoadAssetAtPath<MapCatalogSO>(Path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MapCatalogSO>();
                AssetDatabase.CreateAsset(catalog, Path);
            }
            if (SameMaps(catalog.Maps, maps)) return;
            catalog.SetData(maps);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static bool SameMaps(IReadOnlyList<MapSO> current, List<MapSO> next)
        {
            if (current == null || current.Count != next.Count) return false;
            for (int i = 0; i < current.Count; i++)
                if (current[i] != next[i]) return false;
            return true;
        }
    }

    // Guarantees that a player build includes maps even after manual edits.
    public sealed class MapCatalogBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => MapCatalogBuilder.RebuildCatalog();
    }
}
#endif
