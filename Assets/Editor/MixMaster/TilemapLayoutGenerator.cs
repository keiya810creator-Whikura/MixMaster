#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MixMaster.World;
using MixMaster.Player;
using MixMaster.UI;
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

        private const int NormalSpawnAreaCount = 10;
        private const int SpawnPointsPerNormalArea = 2;
        private const int StrongSpawnAreaCount = 1;
        private const float SpawnPairOffset = 0.35f;

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

            MapBuildingPrefabCreator.EnsurePrefabs();

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

            List<Vector3Int> preferredSpawnCells =
                new List<Vector3Int>();

            List<Vector3Int> fallbackSpawnCells =
                new List<Vector3Int>();

            List<Vector3Int> reservedCells =
                new List<Vector3Int>();

            Vector3Int? playerSpawnCell = null;

            Transform mapObjectsRoot =
                PrepareMapObjectsRoot(mapRoot.transform);

            Grid grid =
                mapRoot.GetComponent<Grid>();

            EnsureBuildingUiRouter();

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

                    bool isSpecial =
                        symbol == 'A' ||
                        symbol == 'D' ||
                        symbol == 'P';

                    if (isSpecial)
                    {
                        reservedCells.Add(cell);

                        if (symbol == 'P')
                            playerSpawnCell = cell;

                        if (HandleSpecialSymbol(
                                symbol,
                                cell,
                                grid,
                                mapObjectsRoot))
                        {
                            continue;
                        }
                    }

                    bool isBlockedCell = false;

                    if (symbol != '.' &&
                        legend.TryGetValue(
                            symbol,
                            out LegendEntry entry))
                    {
                        Tilemap target =
                            GetLayer(mapRoot, entry.layer);

                        bool isCollisionLayer =
                            string.Equals(
                                entry.layer,
                                "Collision",
                                StringComparison.OrdinalIgnoreCase);

                        Tile.ColliderType colliderType =
                            isCollisionLayer
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

                        isBlockedCell =
                            entry.collision ||
                            isCollisionLayer;
                    }

                    if (!isBlockedCell &&
                        !isSpecial)
                    {
                        fallbackSpawnCells.Add(cell);

                        if (symbol == '.')
                            preferredSpawnCells.Add(cell);
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

            CreateAutoEnemySpawnAreas(
                grid,
                mapObjectsRoot,
                preferredSpawnCells,
                fallbackSpawnCells,
                reservedCells,
                playerSpawnCell,
                originX,
                originY,
                layout.width,
                layout.height);

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

        private static Transform PrepareMapObjectsRoot(
            Transform mapRoot)
        {
            const string rootName = "_MapObjects";

            Transform existing =
                mapRoot.Find(rootName);

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(
                    existing.gameObject);
            }

            GameObject root =
                new GameObject(rootName);

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Map Objects Root");

            root.transform.SetParent(
                mapRoot,
                false);

            return root.transform;
        }

        private static bool HandleSpecialSymbol(
            char symbol,
            Vector3Int cell,
            Grid grid,
            Transform mapObjectsRoot)
        {
            if (symbol != 'A' &&
                symbol != 'D' &&
                symbol != 'P')
            {
                return false;
            }

            Vector3 worldPosition =
                grid != null
                    ? grid.GetCellCenterWorld(cell)
                    : (Vector3)cell;

            switch (symbol)
            {
                case 'A':
                    InstantiateBuilding(
                        MapBuildingPrefabCreator.AltarPrefabPath,
                        "Altar",
                        worldPosition,
                        mapObjectsRoot);
                    return true;

                case 'D':
                    InstantiateBuilding(
                        MapBuildingPrefabCreator.DungeonEntrancePrefabPath,
                        "DungeonEntrance",
                        worldPosition,
                        mapObjectsRoot);
                    return true;

                case 'P':
                    CreatePlayerSpawnPoint(
                        worldPosition,
                        mapObjectsRoot);
                    return true;
            }

            return false;
        }

        private static void InstantiateBuilding(
            string prefabPath,
            string objectName,
            Vector3 worldPosition,
            Transform parent)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[TilemapLayoutGenerator] 建築Prefabが見つかりません: " +
                    prefabPath);
                return;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab,
                    parent) as GameObject;

            if (instance == null)
                return;

            Undo.RegisterCreatedObjectUndo(
                instance,
                "Create " + objectName);

            instance.name = objectName;
            instance.transform.position =
                worldPosition;
        }

        private static void CreatePlayerSpawnPoint(
            Vector3 worldPosition,
            Transform parent)
        {
            GameObject marker =
                new GameObject("PlayerSpawnPoint");

            Undo.RegisterCreatedObjectUndo(
                marker,
                "Create Player Spawn Point");

            marker.transform.SetParent(
                parent,
                false);

            marker.transform.position =
                worldPosition;

            PlayerController player =
                UnityEngine.Object
                    .FindFirstObjectByType<PlayerController>();

            if (player != null)
            {
                Undo.RecordObject(
                    player.transform,
                    "Move Player To Map Spawn");

                player.transform.position =
                    worldPosition;
            }
        }

        private static void CreateAutoEnemySpawnAreas(
            Grid grid,
            Transform parent,
            List<Vector3Int> preferredCells,
            List<Vector3Int> fallbackCells,
            List<Vector3Int> reservedCells,
            Vector3Int? playerSpawnCell,
            int originX,
            int originY,
            int width,
            int height)
        {
            const int totalAreaCount =
                NormalSpawnAreaCount +
                StrongSpawnAreaCount;

            List<Vector3Int> candidates =
                BuildSpawnCandidates(
                    preferredCells,
                    reservedCells,
                    originX,
                    originY,
                    width,
                    height,
                    2,
                    3f);

            if (candidates.Count < totalAreaCount)
            {
                candidates =
                    BuildSpawnCandidates(
                        fallbackCells,
                        reservedCells,
                        originX,
                        originY,
                        width,
                        height,
                        1,
                        1.5f);
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    "[TilemapLayoutGenerator] EnemySpawnPointを置けるマスがありません。");
                return;
            }

            Vector3Int referenceCell =
                playerSpawnCell ??
                new Vector3Int(
                    originX + width / 2,
                    originY + height / 2,
                    0);

            List<Vector3Int> selected =
                SelectSpreadCells(
                    candidates,
                    Mathf.Min(
                        totalAreaCount,
                        candidates.Count),
                    referenceCell,
                    playerSpawnCell.HasValue);

            if (selected.Count == 0)
                return;

            int strongIndex =
                FindFarthestCellIndex(
                    selected,
                    referenceCell);

            GameObject spawnRootObject =
                new GameObject("_EnemySpawnAreas");

            Undo.RegisterCreatedObjectUndo(
                spawnRootObject,
                "Create Enemy Spawn Areas");

            spawnRootObject.transform.SetParent(
                parent,
                false);

            int normalNumber = 1;

            for (int i = 0; i < selected.Count; i++)
            {
                Vector3 worldPosition =
                    grid != null
                        ? grid.GetCellCenterWorld(
                            selected[i])
                        : (Vector3)selected[i];

                bool isStrong =
                    i == strongIndex;

                if (isStrong)
                {
                    CreateStrongSpawnArea(
                        spawnRootObject.transform,
                        worldPosition);
                    continue;
                }

                if (normalNumber >
                    NormalSpawnAreaCount)
                {
                    continue;
                }

                CreateNormalSpawnArea(
                    spawnRootObject.transform,
                    worldPosition,
                    normalNumber);

                normalNumber++;
            }

            Debug.Log(
                "[TilemapLayoutGenerator] EnemySpawn配置: 通常" +
                (normalNumber - 1) +
                "エリア × " +
                SpawnPointsPerNormalArea +
                "、強敵1エリア");
        }

        private static List<Vector3Int> BuildSpawnCandidates(
            List<Vector3Int> source,
            List<Vector3Int> reservedCells,
            int originX,
            int originY,
            int width,
            int height,
            int borderMargin,
            float reservedDistance)
        {
            List<Vector3Int> result =
                new List<Vector3Int>();

            if (source == null)
                return result;

            float reservedDistanceSqr =
                reservedDistance *
                reservedDistance;

            int minX =
                originX +
                Mathf.Max(0, borderMargin);

            int maxX =
                originX +
                width -
                1 -
                Mathf.Max(0, borderMargin);

            int minY =
                originY +
                Mathf.Max(0, borderMargin);

            int maxY =
                originY +
                height -
                1 -
                Mathf.Max(0, borderMargin);

            for (int i = 0; i < source.Count; i++)
            {
                Vector3Int cell = source[i];

                if (cell.x < minX ||
                    cell.x > maxX ||
                    cell.y < minY ||
                    cell.y > maxY)
                {
                    continue;
                }

                bool tooCloseToReserved = false;

                if (reservedCells != null)
                {
                    for (int j = 0;
                         j < reservedCells.Count;
                         j++)
                    {
                        Vector2 delta =
                            new Vector2(
                                cell.x -
                                reservedCells[j].x,
                                cell.y -
                                reservedCells[j].y);

                        if (delta.sqrMagnitude <
                            reservedDistanceSqr)
                        {
                            tooCloseToReserved = true;
                            break;
                        }
                    }
                }

                if (!tooCloseToReserved)
                    result.Add(cell);
            }

            return result;
        }

        private static List<Vector3Int> SelectSpreadCells(
            List<Vector3Int> candidates,
            int count,
            Vector3Int referenceCell,
            bool startFarFromReference)
        {
            List<Vector3Int> selected =
                new List<Vector3Int>();

            if (candidates == null ||
                candidates.Count == 0 ||
                count <= 0)
            {
                return selected;
            }

            int firstIndex =
                startFarFromReference
                    ? FindFarthestCellIndex(
                        candidates,
                        referenceCell)
                    : FindNearestCellIndex(
                        candidates,
                        referenceCell);

            selected.Add(
                candidates[firstIndex]);

            while (selected.Count < count)
            {
                int bestIndex = -1;
                float bestDistance = -1f;

                for (int i = 0;
                     i < candidates.Count;
                     i++)
                {
                    Vector3Int candidate =
                        candidates[i];

                    if (selected.Contains(candidate))
                        continue;

                    float minDistance =
                        float.MaxValue;

                    for (int j = 0;
                         j < selected.Count;
                         j++)
                    {
                        float distance =
                            CellDistanceSqr(
                                candidate,
                                selected[j]);

                        if (distance <
                            minDistance)
                        {
                            minDistance =
                                distance;
                        }
                    }

                    if (minDistance >
                        bestDistance)
                    {
                        bestDistance =
                            minDistance;

                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                    break;

                selected.Add(
                    candidates[bestIndex]);
            }

            return selected;
        }

        private static int FindFarthestCellIndex(
            IList<Vector3Int> cells,
            Vector3Int reference)
        {
            int bestIndex = 0;
            float bestDistance = -1f;

            for (int i = 0; i < cells.Count; i++)
            {
                float distance =
                    CellDistanceSqr(
                        cells[i],
                        reference);

                if (distance >
                    bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int FindNearestCellIndex(
            IList<Vector3Int> cells,
            Vector3Int reference)
        {
            int bestIndex = 0;
            float bestDistance =
                float.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                float distance =
                    CellDistanceSqr(
                        cells[i],
                        reference);

                if (distance <
                    bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float CellDistanceSqr(
            Vector3Int a,
            Vector3Int b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;

            return dx * dx +
                   dy * dy;
        }

        private static void CreateNormalSpawnArea(
            Transform parent,
            Vector3 worldPosition,
            int areaNumber)
        {
            GameObject area =
                new GameObject(
                    "SpawnArea_" +
                    areaNumber.ToString("00"));

            Undo.RegisterCreatedObjectUndo(
                area,
                "Create Enemy Spawn Area");

            area.transform.SetParent(
                parent,
                false);

            area.transform.position =
                worldPosition;

            for (int i = 0;
                 i < SpawnPointsPerNormalArea;
                 i++)
            {
                GameObject point =
                    new GameObject(
                        "EnemySpawnPoint_" +
                        (char)('A' + i));

                Undo.RegisterCreatedObjectUndo(
                    point,
                    "Create Enemy Spawn Point");

                point.transform.SetParent(
                    area.transform,
                    false);

                float direction =
                    i == 0
                        ? -1f
                        : 1f;

                point.transform.localPosition =
                    new Vector3(
                        SpawnPairOffset *
                        direction,
                        0f,
                        0f);

                point.AddComponent<EnemySpawnPoint>();
            }
        }

        private static void CreateStrongSpawnArea(
            Transform parent,
            Vector3 worldPosition)
        {
            GameObject area =
                new GameObject(
                    "StrongSpawnArea");

            Undo.RegisterCreatedObjectUndo(
                area,
                "Create Strong Enemy Spawn Area");

            area.transform.SetParent(
                parent,
                false);

            area.transform.position =
                worldPosition;

            GameObject point =
                new GameObject(
                    "StrongEnemySpawnPoint");

            Undo.RegisterCreatedObjectUndo(
                point,
                "Create Strong Enemy Spawn Point");

            point.transform.SetParent(
                area.transform,
                false);

            point.transform.localPosition =
                Vector3.zero;

            point.AddComponent<EnemySpawnPoint>();
        }

        private static void EnsureBuildingUiRouter()
        {
            MapBuildingUIRouter existing =
                UnityEngine.Object
                    .FindFirstObjectByType<MapBuildingUIRouter>();

            if (existing != null)
                return;

            GameObject routerObject =
                new GameObject("MapBuildingUIRouter");

            Undo.RegisterCreatedObjectUndo(
                routerObject,
                "Create Map Building UI Router");

            routerObject.AddComponent<MapBuildingUIRouter>();
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
