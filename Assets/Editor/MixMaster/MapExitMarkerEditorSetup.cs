#if UNITY_EDITOR
using System.IO;
using MixMaster.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MixMaster.EditorTools
{
    /// <summary>
    /// Adds Inspector-configurable map portals to existing connection markers.
    /// Generates one reusable 32px upward arrow sprite and rotates its child
    /// SpriteRenderer for each side. User-assigned arrow sprites are preserved.
    /// </summary>
    public static class MapExitMarkerEditorSetup
    {
        private const string ArrowSpritePath =
            "Assets/Generated/MixMaster/MapExitArrow_Up.png";

        [MenuItem("MixMaster/Map/全マップの出入口にTrigger・矢印を設定")]
        public static void SetupAllMapPortals()
        {
            TilemapMapRoot[] roots = Object.FindObjectsByType<TilemapMapRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int updated = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                TilemapMapRoot root = roots[i];
                if (root == null || !root.gameObject.scene.IsValid())
                    continue;

                Transform connectionRoot = root.transform.Find("_MapConnectionPoints");
                if (connectionRoot == null)
                    continue;

                updated += SetupExisting(connectionRoot, "上", MapExitDirection.Up);
                updated += SetupExisting(connectionRoot, "下", MapExitDirection.Down);
                updated += SetupExisting(connectionRoot, "左", MapExitDirection.Left);
                updated += SetupExisting(connectionRoot, "右", MapExitDirection.Right);

                if (updated > 0)
                    EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }

            Debug.Log("[MapExitMarkerEditorSetup] 出入口設定完了: " + updated);
            EditorUtility.DisplayDialog("マップ出入口",
                updated + "個の出入口にTriggerと矢印を設定しました。\n" +
                "Hierarchyの各 MapConnection_方向 を選択し、" +
                "Destination Map に接続先のTilemapMapRootを指定してください。", "OK");
        }

        private static int SetupExisting(
            Transform parent, string name, MapExitDirection direction)
        {
            Transform marker = parent.Find("MapConnection_" + name);
            if (marker == null || !marker.gameObject.activeSelf)
                return 0;

            EnsureMarker(marker, direction);
            return 1;
        }

        public static void EnsureMarker(Transform marker, MapExitDirection direction)
        {
            if (marker == null)
                return;

            MapExitTransition portal = marker.GetComponent<MapExitTransition>();
            if (portal == null)
                portal = Undo.AddComponent<MapExitTransition>(marker.gameObject);

            Undo.RecordObject(portal, "Configure Map Exit Direction");
            portal.SetDirection(direction);
            EditorUtility.SetDirty(portal);

            BoxCollider2D trigger = marker.GetComponent<BoxCollider2D>();
            if (trigger == null)
                trigger = Undo.AddComponent<BoxCollider2D>(marker.gameObject);

            Undo.RecordObject(trigger, "Configure Map Exit Trigger");
            trigger.isTrigger = true;
            trigger.size = Vector2.one;
            trigger.offset = Vector2.zero;
            EditorUtility.SetDirty(trigger);

            Transform arrow = marker.Find("ExitArrow");
            if (arrow == null)
            {
                GameObject go = new GameObject("ExitArrow");
                Undo.RegisterCreatedObjectUndo(go, "Create Map Exit Arrow");
                arrow = go.transform;
                arrow.SetParent(marker, false);
            }

            arrow.localPosition = Vector3.zero;
            arrow.localScale = Vector3.one * 0.78f;
            arrow.localRotation = Quaternion.Euler(0f, 0f, RotationFor(direction));

            SpriteRenderer spriteRenderer = arrow.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = Undo.AddComponent<SpriteRenderer>(arrow.gameObject);

            Undo.RecordObject(spriteRenderer, "Assign Map Exit Arrow");
            if (spriteRenderer.sprite == null)
                spriteRenderer.sprite = GetOrCreateDefaultArrowSprite();
            spriteRenderer.sortingOrder = 35;
            EditorUtility.SetDirty(spriteRenderer);
        }

        private static float RotationFor(MapExitDirection direction)
        {
            switch (direction)
            {
                case MapExitDirection.Up: return 0f;
                case MapExitDirection.Down: return 180f;
                case MapExitDirection.Left: return 90f;
                default: return -90f;
            }
        }

        private static Sprite GetOrCreateDefaultArrowSprite()
        {
            Sprite exists = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
            if (exists != null)
                return exists;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var inside = new bool[size * size];

            // Pixel arrow pointing up, with a transparent background,
            // dark outer outline and warm-gold/cyan interior.
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool shaft = y >= 4 && y <= 19 && x >= 12 && x <= 19;
                bool head = y >= 14 && y <= 28 &&
                    Mathf.Abs(x - 15.5f) <= (29 - y) * 0.88f;
                inside[y * size + x] = shaft || head;
            }

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                if (!inside[index])
                {
                    pixels[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                bool boundary = x == 0 || y == 0 || x == size - 1 ||
                    y == size - 1 ||
                    !inside[index - 1] || !inside[index + 1] ||
                    !inside[index - size] || !inside[index + size];

                pixels[index] = boundary
                    ? new Color32(30, 41, 55, 255)
                    : y > 18
                        ? new Color32(255, 232, 128, 255)
                        : new Color32(95, 230, 250, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            string absolute = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                ArrowSpritePath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(ArrowSpritePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer =
                AssetImporter.GetAtPath(ArrowSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
        }
    }
}
#endif
