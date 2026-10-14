#if UNITY_EDITOR
using MixMaster.Core;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// One-click bootstrap settings. Prefab Inspector changes persist in
    /// source control and are used at launch on both mobile and in Editor.
    /// </summary>
    public static class ManagerSettingsPrefabCreator
    {
        public const string PrefabPath =
            "Assets/Resources/Managers/GameManagers.prefab";

        [MenuItem("MixMaster/Managers/Manager設定Prefabを作成・表示")]
        public static void CreateOrSelect()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

            if (prefab == null)
            {
                EnsureFolder("Assets/Resources/Managers");

                // Inactive temporary root avoids running play-time logic
                // while constructing and saving a project asset.
                GameObject root = new GameObject(
                    GameManagersBootstrap.RootName);
                root.SetActive(false);
                try
                {
                    GameManagersBootstrap.EnsureComponents(root);
                    root.SetActive(true);
                    prefab = PrefabUtility.SaveAsPrefabAsset(
                        root, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }

                AssetDatabase.SaveAssets();
                Debug.Log("[MixMaster] Manager設定Prefabを作成しました: " +
                    PrefabPath);
            }
            else
            {
                // Preserve user-authored prefab settings; add only
                // manager types introduced by later updates.
                GameObject root = PrefabUtility.LoadPrefabContents(
                    PrefabPath);
                try
                {
                    GameManagersBootstrap.EnsureComponents(root);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] components = path.Split('/');
            string current = "Assets";
            for (int i = 1; i < components.Length; i++)
            {
                string next = current + "/" + components[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, components[i]);
                current = next;
            }
        }
    }
}
#endif
