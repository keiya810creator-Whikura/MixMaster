#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MixMaster.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    public static class TilemapLayoutGenerator
    {
        private const string SampleLayoutPath =
            "Assets/Data/MapLayouts/平野_テスト.json";

        private const string GeneratedTileFolder =
            "Assets/Data/Tiles/Generated";

        [Serializable]
        private sealed class LayoutData
        {
            public string mapName = "GeneratedMap";
            public int width = 20;
            public int height = 20;
            public string defaultGroundTile;
            public bool blockOuterBorder = true;
            public LegendEntry[] legend = Array.Empty<LegendEntry>();
            public string[] rows = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegendEntry
        {
            public string symbol;
            public string tile;
            public string layer = "Ground";
            public bool collision;
        }

        [MenuItem("MixMaster/Map/選択マップJSONからTilemap生成")]
        public static void GenerateFromSelectedJson()
        {
            TextAsset json = Selection.activeObject as TextAsset;

            if (json == null)
            {
                EditorUtility.DisplayDialog(
                    "Tilemap生成",
                    "ProjectウィンドウでマップJSONを選択してください。",
                    "OK");
                return;
            }

            Generate(json);
        }

        [MenuItem("MixMaster/Map/選択マップJSONからTilemap生成", true)]
        private static bool ValidateGenerateFromSelectedJson()
        {
            return Selection.activeObject is TextAsset;
        }

        [MenuItem("MixMaster/Map/サンプル/平野テストマップを生成")]
        public static void GenerateSamplePlainMap()
        {
            TextAsset json =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    SampleLayoutPath);

            if (json == null)
            {
                EditorUtility.DisplayDialog(
                    "平野テストマップ",
                    "サンプルJSONが見つかりません。\n\n" +
                    SampleLayoutPath,
                    "OK");
                return;
            }

            Generate(json);
        }

        private static void Generate(TextAsset jsonAsset)
        {
            LayoutData layout;

            try
            {
                layout =
                    JsonUtility.FromJson<LayoutData>(
                        jsonAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[TilemapLayoutGenerator] JSON解析に失敗しました。\n" +
                    ex);
                return;
            }

            if (layout == null ||
                layout.width <= 0 ||
                layout.height <= 0 ||
                string.IsNullOrWhiteSpace(layout.defaultGroundTile))
            {
                Debug.LogError(
                    "[TilemapLayoutGenerator] JSON内容が不正です。");
                return;
            }

            layout.legend ??= Array.Empty<LegendEntry>();
            layout.rows ??= Array.Empty<string>();

            TilemapMapRoot mapRoot =
                GetOrCreateMapRoot();

            if (mapRoot == null)
                return;

            mapRoot.ClearAllTiles();
            EnsureFolder(GeneratedTileFolder);

            TileBase defaultGroundTile =
                GetOrCreateVisualTile(
                    layout.defaultGroundTile,
                    Tile.ColliderType.None);

            if (defaultGroundTile == null)
            {
                EditorUtility.DisplayDialog(
                    "Tilemap生成",
                    "地面タイルが見つかりません:\n" +
                    layout.defaultGroundTile,
                    "OK");
                return;
            }

            Dictionary<char, LegendEntry> legend =
                BuildLegend(layout.legend);

            TileBase invisibleCollision =
                GetOrCreateCollisionTile();

            int originX = -(layout.width / 2);
            int originY = -(layout.height / 2);
            int missing = 0;

            Undo.RecordObject(mapRoot.Ground, "Generate Map Ground");
            Undo.RecordObject(mapRoot.Decoration, "Generate Map Decoration");
            Undo.RecordObject(mapRoot.Collision, "Generate Map Collision");
            Undo.RecordObject(mapRoot.Above, "Generate Map Above");

            for (int rowIndex = 0; rowIndex < layout.height; rowIndex++)
            {
                string row =
                    rowIndex < layout.rows.Length
                        ? layout.rows[rowIndex] ?? string.Empty
                        : string.Empty;

                int y =
                    originY +
                    (layout.height - 1 - rowIndex);

                for (int xIndex = 0; xIndex < layout.width; xIndex++)
                {
                    Vector3Int cell =
                        new Vector3Int(originX + xIndex, y, 0);

                    mapRoot.Ground.SetTile(
                        cell,
                        defaultGroundTile);

                    char symbol =
                        xIndex < row.Length
                            ? row[xIndex]
                            : '.';

                    if (symbol != '.' &&
                        legend.TryGetValue(
                            symbol,
                            out LegendEntry entry))
                    {
                        Tilemap target =
                            GetLayer(mapRoot, entry.layer);

                        Tile.ColliderType colliderType =
                            string.Equals(
                                entry.layer,
                                "Collision",
                                StringComparison.OrdinalIgnoreCase)
                                ? Tile.ColliderType.Grid
                                : Tile.ColliderType.None;

                        TileBase tile =
                            GetOrCreateVisualTile(
                                entry.tile,
                                colliderType);

                        if (target != null && tile != null)
                        {
                            target.SetTile(cell, tile);
                        }
                        else
                        {
                            missing++;
                        }

                        if (entry.collision &&
                            invisibleCollision != null)
                        {
                            mapRoot.Collision.SetTile(
                                cell,
                                invisibleCollision);
                        }
                    }

                }
            }

            if (layout.blockOuterBorder &&
                invisibleCollision != null)
            {
                PlaceOuterCollisionRing(
                    mapRoot.Collision,
                    invisibleCollision,
                    originX,
                    originY,
                    layout.width,
                    layout.height);
            }

            mapRoot.Ground.CompressBounds();
            mapRoot.Decoration.CompressBounds();
            mapRoot.Collision.CompressBounds();
            mapRoot.Above.CompressBounds();

            mapRoot.name =
                string.IsNullOrWhiteSpace(layout.mapName)
                    ? "TilemapMap"
                    : "TilemapMap_" + layout.mapName;

            EditorUtility.SetDirty(mapRoot.gameObject);
            EditorUtility.SetDirty(mapRoot.Ground);
            EditorUtility.SetDirty(mapRoot.Decoration);
            EditorUtility.SetDirty(mapRoot.Collision);
            EditorUtility.SetDirty(mapRoot.Above);

            AssetDatabase.SaveAssets();

            Selection.activeGameObject =
                mapRoot.gameObject;

            SceneView.lastActiveSceneView?.FrameSelected();

            Debug.Log(
                "[TilemapLayoutGenerator] 生成完了: " +
                layout.mapName +
                " / " +
                layout.width +
                "x" +
                layout.height +
                " / Missing=" +
                missing,
                mapRoot);
        }

        private static TilemapMapRoot GetOrCreateMapRoot()
        {
            if (Selection.activeGameObject != null)
            {
                TilemapMapRoot selected =
                    Selection.activeGameObject
                        .GetComponentInParent<TilemapMapRoot>();

                if (selected != null)
                    return selected;
            }

            TilemapMapRoot existing =
                UnityEngine.Object
                    .FindFirstObjectByType<TilemapMapRoot>();

            if (existing != null)
                return existing;

            return TilemapMapCreator.CreateMapRoot();
        }

        private static Dictionary<char, LegendEntry>
            BuildLegend(LegendEntry[] entries)
        {
            Dictionary<char, LegendEntry> result =
                new Dictionary<char, LegendEntry>();

            for (int i = 0; i < entries.Length; i++)
            {
                LegendEntry entry = entries[i];

                if (entry == null ||
                    string.IsNullOrEmpty(entry.symbol))
                {
                    continue;
                }

                char symbol = entry.symbol[0];

                if (symbol != '.')
                    result[symbol] = entry;
            }

            return result;
        }

        private static Tilemap GetLayer(
            TilemapMapRoot root,
            string layer)
        {
            if (string.Equals(layer, "Ground", StringComparison.OrdinalIgnoreCase))
                return root.Ground;

            if (string.Equals(layer, "Decoration", StringComparison.OrdinalIgnoreCase))
                return root.Decoration;

            if (string.Equals(layer, "Collision", StringComparison.OrdinalIgnoreCase))
                return root.Collision;

            if (string.Equals(layer, "Above", StringComparison.OrdinalIgnoreCase))
                return root.Above;

            return root.Decoration;
        }

        private static TileBase GetOrCreateVisualTile(
            string spriteName,
            Tile.ColliderType colliderType)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
                return null;

            Sprite sprite =
                FindSpriteExact(spriteName);

            if (sprite == null)
            {
                Debug.LogWarning(
                    "[TilemapLayoutGenerator] Spriteが見つかりません: " +
                    spriteName);
                return null;
            }

            string suffix =
                colliderType == Tile.ColliderType.None
                    ? string.Empty
                    : "_Collider";

            string tilePath =
                GeneratedTileFolder +
                "/" +
                SanitizeFileName(spriteName) +
                suffix +
                ".asset";

            Tile tile =
                AssetDatabase.LoadAssetAtPath<Tile>(
                    tilePath);

            if (tile == null)
            {
                tile =
                    ScriptableObject.CreateInstance<Tile>();

                AssetDatabase.CreateAsset(
                    tile,
                    tilePath);
            }

            tile.sprite = sprite;
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);

            return tile;
        }

        private static TileBase GetOrCreateCollisionTile()
        {
            string path =
                GeneratedTileFolder +
                "/__InvisibleCollision.asset";

            Tile tile =
                AssetDatabase.LoadAssetAtPath<Tile>(path);

            if (tile == null)
            {
                tile =
                    ScriptableObject.CreateInstance<Tile>();

                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = null;
            tile.color = Color.clear;
            tile.transform = Matrix4x4.identity;
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);

            return tile;
        }

        private static Sprite FindSpriteExact(
            string spriteName)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    spriteName,
                    new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                string fileName =
                    Path.GetFileNameWithoutExtension(path);

                if (!string.Equals(
                        fileName,
                        spriteName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Sprite direct =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        path);

                if (direct != null)
                    return direct;

                UnityEngine.Object[] assets =
                    AssetDatabase.LoadAllAssetsAtPath(path);

                for (int j = 0; j < assets.Length; j++)
                {
                    if (assets[j] is Sprite sprite)
                        return sprite;
                }
            }

            return null;
        }

        private static void PlaceOuterCollisionRing(
            Tilemap collisionMap,
            TileBase collisionTile,
            int originX,
            int originY,
            int width,
            int height)
        {
            if (collisionMap == null ||
                collisionTile == null)
            {
                return;
            }

            int left = originX - 1;
            int right = originX + width;
            int bottom = originY - 1;
            int top = originY + height;

            // Top / bottom: one full cell outside the visible map.
            for (int x = left; x <= right; x++)
            {
                collisionMap.SetTile(
                    new Vector3Int(x, bottom, 0),
                    collisionTile);

                collisionMap.SetTile(
                    new Vector3Int(x, top, 0),
                    collisionTile);
            }

            // Left / right: one full cell outside the visible map.
            for (int y = originY; y < originY + height; y++)
            {
                collisionMap.SetTile(
                    new Vector3Int(left, y, 0),
                    collisionTile);

                collisionMap.SetTile(
                    new Vector3Int(right, y, 0),
                    collisionTile);
            }
        }

        private static void EnsureFolder(
            string folderPath)
        {
            string[] parts =
                folderPath.Replace('\\', '/').Split('/');

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

        private static string SanitizeFileName(
            string fileName)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();

            char[] chars = fileName.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (chars[i] == invalid[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }
    }
}
#endif
