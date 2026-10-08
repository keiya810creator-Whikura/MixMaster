#if UNITY_EDITOR
using System.IO;
using MixMaster.World;
using UnityEditor;
using UnityEngine;

namespace MixMaster.EditorTools
{
    public static class MapBuildingPrefabCreator
    {
        public const string AltarPrefabPath =
            "Assets/Prefab/Map/Altar.prefab";

        public const string DungeonEntrancePrefabPath =
            "Assets/Prefab/Map/DungeonEntrance.prefab";

        private const string AltarSpritePath =
            "Assets/素材/祭壇.png";

        private const string DungeonEntranceSpritePath =
            "Assets/素材/ダンジョン入り口.png";

        [MenuItem("MixMaster/Map/建築Prefabを作成・更新")]
        public static void CreateOrUpdatePrefabs()
        {
            int created = 0;
            int updated = 0;
            int failed = 0;

            EnsureFolder("Assets/Prefab/Map");

            ProcessPrefab(
                "Altar",
                AltarPrefabPath,
                AltarSpritePath,
                MapBuildingType.Altar,
                ref created,
                ref updated,
                ref failed);

            ProcessPrefab(
                "DungeonEntrance",
                DungeonEntrancePrefabPath,
                DungeonEntranceSpritePath,
                MapBuildingType.DungeonEntrance,
                ref created,
                ref updated,
                ref failed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "建築Prefab",
                "完了しました。\n\n" +
                "新規: " + created + "\n" +
                "更新: " + updated + "\n" +
                "失敗: " + failed,
                "OK");
        }

        public static bool EnsurePrefabs()
        {
            EnsureFolder("Assets/Prefab/Map");

            int created = 0;
            int updated = 0;
            int failed = 0;

            ProcessPrefab(
                "Altar",
                AltarPrefabPath,
                AltarSpritePath,
                MapBuildingType.Altar,
                ref created,
                ref updated,
                ref failed);

            ProcessPrefab(
                "DungeonEntrance",
                DungeonEntrancePrefabPath,
                DungeonEntranceSpritePath,
                MapBuildingType.DungeonEntrance,
                ref created,
                ref updated,
                ref failed);

            AssetDatabase.SaveAssets();

            return failed == 0;
        }

        private static void ProcessPrefab(
            string rootName,
            string prefabPath,
            string spritePath,
            MapBuildingType type,
            ref int created,
            ref int updated,
            ref int failed)
        {
            Sprite sprite =
                LoadSprite(spritePath);

            if (sprite == null)
            {
                failed++;

                Debug.LogError(
                    "[MapBuildingPrefabCreator] Spriteが見つかりません: " +
                    spritePath);

                return;
            }

            bool exists =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath) != null;

            GameObject root = null;

            try
            {
                root = exists
                    ? PrefabUtility.LoadPrefabContents(prefabPath)
                    : new GameObject(rootName);

                root.name = rootName;

                SpriteRenderer renderer =
                    root.GetComponent<SpriteRenderer>();

                if (renderer == null)
                    renderer = root.AddComponent<SpriteRenderer>();

                renderer.sprite = sprite;
                renderer.sortingOrder = 12;

                BoxCollider2D solid =
                    root.GetComponent<BoxCollider2D>();

                if (solid == null)
                    solid = root.AddComponent<BoxCollider2D>();

                solid.isTrigger = false;

                Vector2 spriteSize =
                    sprite.bounds.size;

                solid.size = new Vector2(
                    Mathf.Max(0.1f, spriteSize.x * 0.70f),
                    Mathf.Max(0.1f, spriteSize.y * 0.45f));

                solid.offset = new Vector2(
                    0f,
                    -spriteSize.y * 0.18f);

                Transform triggerTransform =
                    root.transform.Find("AccessTrigger");

                GameObject triggerObject;

                if (triggerTransform == null)
                {
                    triggerObject =
                        new GameObject("AccessTrigger");

                    triggerObject.transform.SetParent(
                        root.transform,
                        false);
                }
                else
                {
                    triggerObject =
                        triggerTransform.gameObject;
                }

                BoxCollider2D accessCollider =
                    triggerObject.GetComponent<BoxCollider2D>();

                if (accessCollider == null)
                {
                    accessCollider =
                        triggerObject.AddComponent<BoxCollider2D>();
                }

                accessCollider.isTrigger = true;

                accessCollider.size = new Vector2(
                    Mathf.Max(0.2f, spriteSize.x * 1.10f),
                    Mathf.Max(0.2f, spriteSize.y * 0.85f));

                accessCollider.offset = Vector2.zero;

                MapBuildingAccess access =
                    triggerObject.GetComponent<MapBuildingAccess>();

                if (access == null)
                {
                    access =
                        triggerObject.AddComponent<MapBuildingAccess>();
                }

                access.Configure(type);

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);

                if (exists)
                    updated++;
                else
                    created++;
            }
            catch (System.Exception ex)
            {
                failed++;

                Debug.LogError(
                    "[MapBuildingPrefabCreator] " +
                    prefabPath +
                    " の作成/更新に失敗しました。\n" +
                    ex);
            }
            finally
            {
                if (root != null)
                {
                    if (exists)
                        PrefabUtility.UnloadPrefabContents(root);
                    else
                        Object.DestroyImmediate(root);
                }
            }
        }

        private static Sprite LoadSprite(
            string assetPath)
        {
            Sprite sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    assetPath);

            if (sprite != null)
                return sprite;

            Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    assetPath);

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite found)
                    return found;
            }

            return null;
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
