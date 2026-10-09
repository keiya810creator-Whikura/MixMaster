#if UNITY_EDITOR
using MixMaster.World;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    [InitializeOnLoad]
    public static class MonsterDropPrefabCreator
    {
        private const string PrefabFolder =
            "Assets/Resources/Prefabs";

        private const string PrefabPath =
            "Assets/Resources/Prefabs/MonsterDrop.prefab";

        static MonsterDropPrefabCreator()
        {
            EditorApplication.delayCall +=
                EnsurePrefabExists;
        }

        [MenuItem("MixMaster/Drop/モンスター本体ドロップPrefabを作成・更新")]
        public static void CreateOrUpdatePrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath);

            bool existed = existing != null;

            GameObject root = null;

            try
            {
                root = existed
                    ? PrefabUtility.LoadPrefabContents(PrefabPath)
                    : new GameObject("MonsterDrop");

                root.name = "MonsterDrop";

                SpriteRenderer renderer =
                    root.GetComponent<SpriteRenderer>();

                if (renderer == null)
                    renderer = root.AddComponent<SpriteRenderer>();

                renderer.sprite = null;
                renderer.sortingOrder = 41;

                MonsterDropPickup pickup =
                    root.GetComponent<MonsterDropPickup>();

                if (pickup == null)
                    root.AddComponent<MonsterDropPickup>();

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath);

                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[MonsterDropPrefabCreator] " +
                    (existed ? "更新" : "作成") +
                    ": " +
                    PrefabPath);
            }
            finally
            {
                if (root != null)
                {
                    if (existed)
                        PrefabUtility.UnloadPrefabContents(root);
                    else
                        Object.DestroyImmediate(root);
                }
            }
        }

        private static void EnsurePrefabExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath);

            if (existing != null)
                return;

            CreateOrUpdatePrefab();
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
