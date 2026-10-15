#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MixMaster.World;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace MixMaster.EditorTools
{
    public static class TilemapLayoutGenerator
    {
        private const string SampleLayoutPath =
            "Assets/Data/MapLayouts/平野_テスト.json";

        private const string GeneratedTileFolder =
            "Assets/Data/Tiles/Generated";

        private const string CommonEnemyPrefabPath =
            "Assets/Prefab/Enemy.prefab";

        private const int NormalMonsterTypeCount = 5;
        private const int SpawnLocationsPerNormalMonster = 2;
        private const int NormalSpawnLocationCount =
            NormalMonsterTypeCount *
            SpawnLocationsPerNormalMonster;

        private const int StrongSpawnAreaCount = 1;

        [Serializable]
        private sealed class LayoutData
        {
            public string mapName = "GeneratedMap";
            public int width = 20;
            public int height = 20;
            public string defaultGroundTile;
            public bool blockOuterBorder = true;
            // Present in JSONs generated from the 12-column terrain CSV.
            public bool hasExitSettings;
            public bool exitUp;
            public bool exitDown;
            public bool exitLeft;
            public bool exitRight;
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

        [Flags]
        private enum ConnectionDirection
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 4,
            Right = 8
        }

        private sealed class RoadTiles
        {
            public TileBase Vertical;
            public TileBase Horizontal;
            public TileBase Cross;
        }

        private const int RoadHalfWidth = 1; // 3-tile-wide walkable roads.


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

        [MenuItem("MixMaster/Map/選択MapSOからTilemap生成")]
        public static void GenerateFromSelectedMap()
        {
            MapSO map = Selection.activeObject as MapSO;
            if (map == null)
                return;

            TextAsset json = map.layoutJson;
            if (json == null && !string.IsNullOrWhiteSpace(map.layoutJsonName))
            {
                json = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Data/MapLayouts/" + map.layoutJsonName + ".json");
            }

            if (json == null)
            {
                EditorUtility.DisplayDialog(
                    "MapSOからTilemap生成",
                    "MapSOの地形生成用JSONが見つかりません: " +
                    map.layoutJsonName, "OK");
                return;
            }

            Generate(json, map);
        }

        [MenuItem("MixMaster/Map/選択マップを有効化 (同一シーン)")]
        public static void ActivateSelectedMap()
        {
            TilemapMapRoot target = null;

            if (Selection.activeObject is MapSO selectedMap)
                target = FindExistingMapRoot(selectedMap, null);
            else if (Selection.activeGameObject != null)
                target = Selection.activeGameObject.GetComponentInParent<TilemapMapRoot>();

            if (target == null || target.gameObject.scene != SceneManager.GetActiveScene())
            {
                EditorUtility.DisplayDialog(
                    "マップ切替",
                    "現在のシーンで生成済みのMapSO、またはHierarchy上のマップRootを選択してください。",
                    "OK");
                return;
            }

            SetOnlyActiveMap(target);
            MovePlayerToMapSpawn(target);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Selection.activeGameObject = target.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("MixMaster/Map/選択MapSOからTilemap生成", true)]
        private static bool ValidateGenerateFromSelectedMap()
        {
            return Selection.activeObject is MapSO;
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

        private static MapSO FindMapForJson(TextAsset jsonAsset)
        {
            if (jsonAsset == null)
                return null;

            string jsonName = Path.GetFileNameWithoutExtension(
                AssetDatabase.GetAssetPath(jsonAsset));

            MapSO match = null;
            string[] guids = AssetDatabase.FindAssets("t:MapSO");

            foreach (string guid in guids)
            {
                MapSO candidate = AssetDatabase.LoadAssetAtPath<MapSO>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (candidate == null)
                    continue;

                bool sameJson = candidate.layoutJson == jsonAsset ||
                    (!string.IsNullOrWhiteSpace(candidate.layoutJsonName) &&
                     string.Equals(candidate.layoutJsonName, jsonName,
                         StringComparison.OrdinalIgnoreCase));

                if (!sameJson)
                    continue;

                if (match != null)
                {
                    Debug.LogWarning(
                        "[TilemapLayoutGenerator] 同じJSONを使用するMapSOが複数あります。 " +
                        "割り当てるMapSOを選択し「選択MapSOからTilemap生成」を使用してください。");
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private static bool HasDungeonContent(MapSO mapDefinition)
        {
            if (mapDefinition == null)
                return false;

            if (mapDefinition.dungeonMaterial != null)
                return true;

            var equipment = mapDefinition.dungeonEquipment;
            if (equipment == null)
                return false;

            for (int i = 0; i < equipment.Count; i++)
            {
                if (equipment[i] != null)
                    return true;
            }

            return false;
        }

        private static void Generate(TextAsset jsonAsset, MapSO mapDefinition = null)
        {
            // The original JSON menu also benefits from map CSV imports.
            // If one imported MapSO references this JSON, use its spawn setup.
            if (mapDefinition == null)
                mapDefinition = FindMapForJson(jsonAsset);

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

            bool hasDungeonContent = HasDungeonContent(mapDefinition);
            ConnectionDirection connections;
            if (layout.hasExitSettings)
            {
                // Terrain CSV is authoritative, including explicit FALSE.
                connections = ConnectionDirection.None;
                if (layout.exitUp) connections |= ConnectionDirection.Up;
                if (layout.exitDown) connections |= ConnectionDirection.Down;
                if (layout.exitLeft) connections |= ConnectionDirection.Left;
                if (layout.exitRight) connections |= ConnectionDirection.Right;
            }
            else
            {
                // Backward compatibility with old hand-authored JSON layouts.
                connections = mapDefinition == null
                    ? ConnectionDirection.None
                    : ParseConnections(mapDefinition.entranceDirections) |
                      ParseConnections(mapDefinition.exitDirections);
            }

            Dictionary<Vector3Int, int> roadOverlay = null;
            RoadTiles roadTiles = null;
            int originX = -(layout.width / 2);
            int originY = -(layout.height / 2);

            if (connections != ConnectionDirection.None)
            {
                roadOverlay = BuildRoadOverlay(
                    layout, originX, originY, connections, hasDungeonContent);
                if (!TryFindRoadTiles(layout, out roadTiles))
                {
                    EditorUtility.DisplayDialog("マップ生成",
                        "出入口方向が設定されていますが、JSONのlegendに" +
                        "地面用の「_縦」「_横」「_十字」道路タイルが揃っていません。\n" +
                        "例: 平野_土_縦 / 平野_土_横 / 平野_土_十字",
                        "OK");
                    return;
                }
            }

            if (hasDungeonContent)
                MapBuildingPrefabCreator.EnsurePrefabs();

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

            TilemapMapRoot mapRoot =
                GetOrCreateMapRoot(mapDefinition, layout.mapName);

            if (mapRoot == null)
                return;

            SetOnlyActiveMap(mapRoot);
            Undo.RecordObject(mapRoot, "Assign Map Data");
            mapRoot.SetMapDefinition(mapDefinition);
            mapRoot.ClearAllTiles();

            Dictionary<char, LegendEntry> legend =
                BuildLegend(layout.legend);

            TileBase invisibleCollision =
                GetOrCreateCollisionTile();

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

            if (hasDungeonContent)
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
                        symbol == 'P' ||
                        (hasDungeonContent &&
                         (symbol == 'A' || symbol == 'D'));

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
                        // Old JSON roads may wander in unrelated directions.
                        // Once CSV connections are set, generate only the
                        // intentional connected road network instead.
                        if (roadOverlay != null && !entry.collision &&
                            string.Equals(entry.layer, "Ground",
                                StringComparison.OrdinalIgnoreCase) &&
                            IsRoadTileName(entry.tile))
                            continue;

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

            if (roadOverlay != null)
            {
                // Roads take priority over random decorations, impassable
                // props and collision tiles. Do not spawn enemies on the road.
                foreach (var item in roadOverlay)
                {
                    Vector3Int cell = item.Key;
                    TileBase road = item.Value == 1
                        ? roadTiles.Vertical
                        : item.Value == 2
                            ? roadTiles.Horizontal
                            : roadTiles.Cross;

                    mapRoot.Ground.SetTile(cell, road);
                    mapRoot.Decoration.SetTile(cell, null);
                    mapRoot.Above.SetTile(cell, null);
                    mapRoot.Collision.SetTile(cell, null);
                    reservedCells.Add(cell);
                }

                preferredSpawnCells.RemoveAll(cell => roadOverlay.ContainsKey(cell));
                fallbackSpawnCells.RemoveAll(cell => roadOverlay.ContainsKey(cell));
            }

            // Deactivate obsolete markers as well when directions are cleared.
            CreateConnectionMarkers(
                mapRoot.transform, grid, connections,
                originX, originY, layout.width, layout.height);

            if (layout.blockOuterBorder &&
                invisibleCollision != null)
            {
                PlaceOuterCollisionRing(
                    mapRoot.Collision,
                    invisibleCollision,
                    originX,
                    originY,
                    layout.width,
                    layout.height,
                    connections);
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
                layout.height,
                mapDefinition);

            mapRoot.Ground.CompressBounds();
            mapRoot.Decoration.CompressBounds();
            mapRoot.Collision.CompressBounds();
            mapRoot.Above.CompressBounds();

            mapRoot.name = GetMapRootName(mapDefinition, layout.mapName);

            EditorUtility.SetDirty(mapRoot);
            EditorUtility.SetDirty(mapRoot.gameObject);
            EditorUtility.SetDirty(mapRoot.Ground);
            EditorUtility.SetDirty(mapRoot.Decoration);
            EditorUtility.SetDirty(mapRoot.Collision);
            EditorUtility.SetDirty(mapRoot.Above);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(mapRoot.gameObject.scene);

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

        private static string GetMapRootName(MapSO mapDefinition, string layoutName)
        {
            string identity = mapDefinition != null
                ? (!string.IsNullOrWhiteSpace(mapDefinition.mapId)
                    ? mapDefinition.mapId
                    : mapDefinition.name)
                : layoutName;

            if (string.IsNullOrWhiteSpace(identity))
                identity = "GeneratedMap";

            return "TilemapMap_" +
                   identity.Trim().Replace('/', '_').Replace('\\', '_');
        }

        private static TilemapMapRoot FindExistingMapRoot(
            MapSO mapDefinition,
            string layoutName)
        {
            string expectedName = GetMapRootName(mapDefinition, layoutName);
            Scene activeScene = SceneManager.GetActiveScene();

            TilemapMapRoot[] roots =
                UnityEngine.Object.FindObjectsByType<TilemapMapRoot>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (TilemapMapRoot root in roots)
            {
                if (root == null || root.gameObject.scene != activeScene)
                    continue;

                if (mapDefinition != null)
                {
                    if (root.MapDefinition == mapDefinition)
                        return root;

                    if (root.MapDefinition != null &&
                        !string.IsNullOrWhiteSpace(mapDefinition.mapId) &&
                        string.Equals(root.MapDefinition.mapId, mapDefinition.mapId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return root;
                    }
                }
                else if (root.MapDefinition == null &&
                         string.Equals(root.name, expectedName,
                             StringComparison.OrdinalIgnoreCase))
                {
                    return root;
                }
            }

            return null;
        }

        private static TilemapMapRoot GetOrCreateMapRoot(
            MapSO mapDefinition,
            string layoutName)
        {
            TilemapMapRoot existing =
                FindExistingMapRoot(mapDefinition, layoutName);

            if (existing != null)
                return existing;

            TilemapMapRoot created = TilemapMapCreator.CreateMapRoot();
            created.name = GetMapRootName(mapDefinition, layoutName);
            return created;
        }

        private static void SetOnlyActiveMap(TilemapMapRoot target)
        {
            TilemapMapRoot[] roots =
                UnityEngine.Object.FindObjectsByType<TilemapMapRoot>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (TilemapMapRoot root in roots)
            {
                if (root == null || root.gameObject.scene != target.gameObject.scene)
                    continue;

                bool shouldBeActive = root == target;
                if (root.gameObject.activeSelf == shouldBeActive)
                    continue;

                Undo.RecordObject(root.gameObject, "Switch Active Map");
                root.gameObject.SetActive(shouldBeActive);
                EditorUtility.SetDirty(root.gameObject);
            }
        }

        private static void MovePlayerToMapSpawn(TilemapMapRoot mapRoot)
        {
            Transform marker = mapRoot.transform.Find("_MapObjects/PlayerSpawnPoint");
            if (marker == null)
                return;

            PlayerController player =
                UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
                return;

            Undo.RecordObject(player.transform, "Move Player To Selected Map");
            player.transform.position = marker.position;
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

            // Both map-generated Altar and DungeonEntrance are
            // displayed at a consistent half scale.
            instance.transform.localScale =
                Vector3.one * 0.5f;
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
            int height,
            MapSO mapDefinition)
        {
            const int totalAreaCount =
                NormalSpawnLocationCount +
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
                        worldPosition,
                        mapDefinition);
                    continue;
                }

                if (normalNumber >
                    NormalSpawnLocationCount)
                {
                    continue;
                }

                CreateNormalSpawnLocation(
                    spawnRootObject.transform,
                    worldPosition,
                    normalNumber,
                    mapDefinition);

                normalNumber++;
            }

            Debug.Log(
                "[TilemapLayoutGenerator] EnemySpawn配置: 雑魚" +
                NormalMonsterTypeCount +
                "種 × " +
                SpawnLocationsPerNormalMonster +
                "か所 = " +
                (normalNumber - 1) +
                "か所、強敵1か所");
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

        private static void CreateNormalSpawnLocation(
            Transform parent,
            Vector3 worldPosition,
            int locationNumber,
            MapSO mapDefinition)
        {
            int monsterSlot =
                ((locationNumber - 1) /
                 SpawnLocationsPerNormalMonster) +
                1;

            int occurrenceIndex =
                (locationNumber - 1) %
                SpawnLocationsPerNormalMonster;

            char occurrence =
                (char)('A' + occurrenceIndex);

            GameObject point =
                new GameObject(
                    "NormalSpawn_" +
                    monsterSlot.ToString("00") +
                    "_" +
                    occurrence);

            Undo.RegisterCreatedObjectUndo(
                point,
                "Create Enemy Spawn Point");

            point.transform.SetParent(
                parent,
                false);

            point.transform.position =
                worldPosition;

            MonsterSO monster = null;
            if (mapDefinition != null &&
                mapDefinition.normalMonsters != null &&
                monsterSlot <= mapDefinition.normalMonsters.Count)
            {
                monster = mapDefinition.normalMonsters[monsterSlot - 1];
            }

            AddConfiguredEnemySpawnPoint(
                point,
                monster,
                mapDefinition != null
                    ? mapDefinition.normalMonsterLevel
                    : 1);
        }

        private static void CreateStrongSpawnArea(
            Transform parent,
            Vector3 worldPosition,
            MapSO mapDefinition)
        {
            GameObject point =
                new GameObject(
                    "StrongEnemySpawn");

            Undo.RegisterCreatedObjectUndo(
                point,
                "Create Strong Enemy Spawn Point");

            point.transform.SetParent(
                parent,
                false);

            point.transform.position =
                worldPosition;

            AddConfiguredEnemySpawnPoint(
                point,
                mapDefinition != null ? mapDefinition.bossMonster : null,
                mapDefinition != null ? mapDefinition.strongMonsterLevel : 1);
        }

        private static void AddConfiguredEnemySpawnPoint(
            GameObject point,
            MonsterSO monster,
            int level)
        {
            if (point == null)
                return;

            EnemySpawnPoint spawnPoint =
                point.AddComponent<EnemySpawnPoint>();

            GameObject enemyPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CommonEnemyPrefabPath);

            if (enemyPrefab == null)
            {
                Debug.LogWarning(
                    "[TilemapLayoutGenerator] Enemy Prefabが見つかりません: " +
                    CommonEnemyPrefabPath,
                    point);
            }
            else
            {
                spawnPoint.SetEnemyPrefab(enemyPrefab);
            }

            if (monster != null)
                spawnPoint.SetMonster(monster, Mathf.Max(1, level));

            EditorUtility.SetDirty(spawnPoint);
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
            // CSV-generated layouts use exact PNG names in the dedicated folder.
            // Resolve there first to avoid accidentally using same-named assets
            // from unrelated map themes or other parts of the project.
            string[] extensions = { ".png", ".jpg", ".jpeg" };
            for (int i = 0; i < extensions.Length; i++)
            {
                string exactPath =
                    MapTerrainCsvGenerator.CsvSpriteFolder + "/" +
                    spriteName + extensions[i];

                Sprite csvSprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>(exactPath);

                if (csvSprite != null)
                    return csvSprite;
            }

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


        private static ConnectionDirection ParseConnections(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return ConnectionDirection.None;

            ConnectionDirection directions = ConnectionDirection.None;
            string[] tokens = raw.Replace("　", " ").Split(
                new[] { ',', '，', '、', '・', '|', '/', ';', '+', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                string value = token.Trim().ToUpperInvariant();
                if (value.Contains("上") || value == "N" || value == "NORTH" || value == "UP")
                    directions |= ConnectionDirection.Up;
                if (value.Contains("下") || value == "S" || value == "SOUTH" || value == "DOWN")
                    directions |= ConnectionDirection.Down;
                if (value.Contains("左") || value == "W" || value == "WEST" || value == "LEFT")
                    directions |= ConnectionDirection.Left;
                if (value.Contains("右") || value == "E" || value == "EAST" || value == "RIGHT")
                    directions |= ConnectionDirection.Right;
            }
            return directions;
        }

        private static Dictionary<Vector3Int, int> BuildRoadOverlay(
            LayoutData layout, int originX, int originY,
            ConnectionDirection connections, bool hasDungeonContent)
        {
            var result = new Dictionary<Vector3Int, int>();
            int midX = originX + layout.width / 2;
            int midY = originY + layout.height / 2;
            int maxX = originX + layout.width - 1;
            int maxY = originY + layout.height - 1;

            if ((connections & ConnectionDirection.Up) != 0)
                AddVerticalRoad(result, midX, midY, maxY,
                    originX, originY, layout.width, layout.height);
            if ((connections & ConnectionDirection.Down) != 0)
                AddVerticalRoad(result, midX, originY, midY,
                    originX, originY, layout.width, layout.height);
            if ((connections & ConnectionDirection.Left) != 0)
                AddHorizontalRoad(result, midY, originX, midX,
                    originX, originY, layout.width, layout.height);
            if ((connections & ConnectionDirection.Right) != 0)
                AddHorizontalRoad(result, midY, midX, maxX,
                    originX, originY, layout.width, layout.height);

            // Connect spawn and usable buildings to the hub, then to the
            // designated map edge(s). No dead-end random branches.
            bool connectedSpawn = false;
            for (int rowIndex = 0; rowIndex < layout.height; rowIndex++)
            {
                string row = rowIndex < layout.rows.Length
                    ? layout.rows[rowIndex] ?? string.Empty
                    : string.Empty;

                for (int x = 0; x < Mathf.Min(row.Length, layout.width); x++)
                {
                    char symbol = row[x];
                    bool connectSpawn = symbol == 'P' && !connectedSpawn;
                    bool connectBuilding = hasDungeonContent &&
                        (symbol == 'A' || symbol == 'D');
                    if (!connectSpawn && !connectBuilding)
                        continue;

                    if (connectSpawn)
                        connectedSpawn = true;

                    int px = originX + x;
                    int py = originY + layout.height - 1 - rowIndex;
                    AddVerticalRoad(result, px, py, midY,
                        originX, originY, layout.width, layout.height);
                    AddHorizontalRoad(result, midY, px, midX,
                        originX, originY, layout.width, layout.height);
                }
            }
            return result;
        }

        private static void AddVerticalRoad(
            Dictionary<Vector3Int, int> cells, int x, int y0, int y1,
            int originX, int originY, int width, int height)
        {
            for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
            for (int dx = -RoadHalfWidth; dx <= RoadHalfWidth; dx++)
                AddRoadCell(cells, x + dx, y, 1,
                    originX, originY, width, height);
        }

        private static void AddHorizontalRoad(
            Dictionary<Vector3Int, int> cells, int y, int x0, int x1,
            int originX, int originY, int width, int height)
        {
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
            for (int dy = -RoadHalfWidth; dy <= RoadHalfWidth; dy++)
                AddRoadCell(cells, x, y + dy, 2,
                    originX, originY, width, height);
        }

        private static void AddRoadCell(
            Dictionary<Vector3Int, int> cells, int x, int y, int orientation,
            int originX, int originY, int width, int height)
        {
            if (x < originX || x >= originX + width ||
                y < originY || y >= originY + height)
                return;
            var cell = new Vector3Int(x, y, 0);
            cells.TryGetValue(cell, out int old);
            cells[cell] = old | orientation;
        }

        private static bool IsRoadTileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            bool roadShape = name.EndsWith("_縦", StringComparison.Ordinal) ||
                             name.EndsWith("_横", StringComparison.Ordinal) ||
                             name.EndsWith("_十字", StringComparison.Ordinal);
            return roadShape && (name.Contains("土") || name.Contains("道") ||
                                  name.Contains("通路") || name.Contains("舗装"));
        }

        private static bool TryFindRoadTiles(LayoutData layout, out RoadTiles roadTiles)
        {
            roadTiles = null;
            var candidates = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (LegendEntry entry in layout.legend)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.tile) ||
                    entry.collision || !string.Equals(entry.layer, "Ground",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                int shape = -1;
                string suffix = null;
                if (entry.tile.EndsWith("_縦", StringComparison.Ordinal))
                {
                    shape = 0;
                    suffix = "_縦";
                }
                else if (entry.tile.EndsWith("_横", StringComparison.Ordinal))
                {
                    shape = 1;
                    suffix = "_横";
                }
                else if (entry.tile.EndsWith("_十字", StringComparison.Ordinal))
                {
                    shape = 2;
                    suffix = "_十字";
                }

                if (shape < 0)
                    continue;

                string prefix = entry.tile.Substring(0, entry.tile.Length - suffix.Length);
                if (!prefix.Contains("土") && !prefix.Contains("道") &&
                    !prefix.Contains("通路") && !prefix.Contains("舗装"))
                    continue;

                if (!candidates.TryGetValue(prefix, out string[] values))
                    candidates[prefix] = values = new string[3];
                values[shape] = entry.tile;
            }

            // Prefer the current biome's road style, not another biome's assets.
            string biome = layout.defaultGroundTile.Split('_')[0];
            foreach (bool preferBiome in new[] { true, false })
            {
                foreach (var candidate in candidates)
                {
                    if (preferBiome && !candidate.Key.StartsWith(biome + "_",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    string[] values = candidate.Value;
                    if (values[0] == null || values[1] == null || values[2] == null)
                        continue;

                    TileBase vertical = GetOrCreateVisualTile(values[0], Tile.ColliderType.None);
                    TileBase horizontal = GetOrCreateVisualTile(values[1], Tile.ColliderType.None);
                    TileBase cross = GetOrCreateVisualTile(values[2], Tile.ColliderType.None);
                    if (vertical == null || horizontal == null || cross == null)
                        continue;

                    roadTiles = new RoadTiles
                    {
                        Vertical = vertical,
                        Horizontal = horizontal,
                        Cross = cross
                    };
                    return true;
                }
            }
            return false;
        }

        private static void CreateConnectionMarkers(
            Transform mapRoot, Grid grid, ConnectionDirection directions,
            int originX, int originY, int width, int height)
        {
            const string rootName = "_MapConnectionPoints";
            Transform markerRoot = mapRoot.Find(rootName);
            if (markerRoot == null && directions == ConnectionDirection.None)
                return;

            if (markerRoot == null)
            {
                GameObject obj = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(obj, "Create Map Connection Points");
                markerRoot = obj.transform;
                markerRoot.SetParent(mapRoot, false);
            }

            int midX = originX + width / 2;
            int midY = originY + height / 2;
            EnsureConnectionMarker(markerRoot, grid, "上",
                (directions & ConnectionDirection.Up) != 0,
                new Vector3Int(midX, originY + height - 1, 0));
            EnsureConnectionMarker(markerRoot, grid, "下",
                (directions & ConnectionDirection.Down) != 0,
                new Vector3Int(midX, originY, 0));
            EnsureConnectionMarker(markerRoot, grid, "左",
                (directions & ConnectionDirection.Left) != 0,
                new Vector3Int(originX, midY, 0));
            EnsureConnectionMarker(markerRoot, grid, "右",
                (directions & ConnectionDirection.Right) != 0,
                new Vector3Int(originX + width - 1, midY, 0));
        }

        private static void EnsureConnectionMarker(
            Transform parent, Grid grid, string direction,
            bool enabled, Vector3Int cell)
        {
            string markerName = "MapConnection_" + direction;
            Transform marker = parent.Find(markerName);
            if (marker == null && !enabled)
                return;

            if (marker == null)
            {
                GameObject obj = new GameObject(markerName);
                Undo.RegisterCreatedObjectUndo(obj, "Create " + markerName);
                marker = obj.transform;
                marker.SetParent(parent, false);
            }

            Undo.RecordObject(marker.gameObject, "Update Map Connection");
            marker.gameObject.SetActive(enabled);
            if (enabled)
            {
                Undo.RecordObject(marker, "Move Map Connection");
                marker.position = grid != null
                    ? grid.GetCellCenterWorld(cell)
                    : (Vector3)cell;
            }
            // Marker is intentionally collider-free: map transition triggers
            // can be added to this object without generator overwrites.
        }

        private static void PlaceOuterCollisionRing(
            Tilemap collisionMap,
            TileBase collisionTile,
            int originX,
            int originY,
            int width,
            int height,
            ConnectionDirection connections)
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
            int midX = originX + width / 2;
            int midY = originY + height / 2;

            for (int x = left; x <= right; x++)
            {
                bool insideExitWidth = Mathf.Abs(x - midX) <= RoadHalfWidth;
                if (!insideExitWidth ||
                    (connections & ConnectionDirection.Down) == 0)
                    collisionMap.SetTile(new Vector3Int(x, bottom, 0), collisionTile);

                if (!insideExitWidth ||
                    (connections & ConnectionDirection.Up) == 0)
                    collisionMap.SetTile(new Vector3Int(x, top, 0), collisionTile);
            }

            // Left / right: one full cell outside the visible map.
            for (int y = originY; y < originY + height; y++)
            {
                bool insideExitWidth = Mathf.Abs(y - midY) <= RoadHalfWidth;
                if (!insideExitWidth ||
                    (connections & ConnectionDirection.Left) == 0)
                    collisionMap.SetTile(new Vector3Int(left, y, 0), collisionTile);

                if (!insideExitWidth ||
                    (connections & ConnectionDirection.Right) == 0)
                    collisionMap.SetTile(new Vector3Int(right, y, 0), collisionTile);
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
