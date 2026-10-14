using System;
using System.Collections.Generic;
using MixMaster.CameraSystem;
using MixMaster.Combat;
using MixMaster.Core;
using MixMaster.Monsters;
using MixMaster.Player;
using MixMaster.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace MixMaster.World
{
    /// <summary>
    /// Creates an isolated dungeon Tilemap with the same ground tile
    /// and cell scale as the entrance field, then moves the player into it.
    /// The original field is preserved and restored at dungeon exit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonMapGenerator : MonoBehaviour
    {
        [Header("Dungeon Rooms")]
        [SerializeField, Range(6, 24)] private int roomSize = 10;
        [SerializeField, Range(2, 32)] private int gapBetweenRooms = 6;
        [SerializeField, Range(1, 7)] private int corridorWidth = 3;

        [Header("Boss Room Floor Colors")]
        [SerializeField] private Color midBossFloorColor =
            new Color(0.90f, 0.73f, 0.46f, 1f);
        [SerializeField] private Color finalBossFloorColor =
            new Color(0.64f, 0.50f, 0.88f, 1f);

        [Header("Temporary development navigation")]
        [Tooltip("Allows returning while bosses/results are not yet implemented.")]
        [SerializeField] private bool showTemporaryReturnButton = true;

        private sealed class SuspendedEnemy
        {
            public EnemyCombatAI combat;
            public bool combatWasEnabled;
            public EnemyWanderAI wander;
            public bool wanderWasEnabled;
            public Rigidbody2D body;
            public bool bodyWasSimulated;
            public Vector2 oldVelocity;
        }

        private readonly List<SuspendedEnemy> suspendedEnemies =
            new List<SuspendedEnemy>();

        private DungeonManager dungeon;
        private MapManager mapManager;
        private TilemapMapRoot fieldRoot;
        private bool fieldRootWasActive;
        private GameObject generatedRoot;
        private Tilemap generatedGround;
        private Tilemap generatedWalls;
        private Tile collisionTile;
        private GameObject temporaryReturnCanvas;
        private PlayerController player;
        private Vector3 entranceWorldPosition;
        private string fieldMapId;
        private bool isGenerated;

        public bool IsGenerated => isGenerated;
        public Vector3 EntranceWorldPosition => entranceWorldPosition;
        public string EntranceMapId => fieldMapId;
        public DungeonLayout CurrentLayout { get; private set; }
        public Tilemap Ground => generatedGround;
        public Tilemap Walls => generatedWalls;
        public Transform RuntimeRoot =>
            generatedRoot != null ? generatedRoot.transform : null;
        public MapSO SourceMap =>
            fieldRoot != null ? fieldRoot.MapDefinition : null;
        public int RoomSize => roomSize;

        public event Action<DungeonLayout> DungeonMapReady;

        private void OnEnable()
        {
            dungeon = GetComponent<DungeonManager>() ??
                FindFirstObjectByType<DungeonManager>();
            if (dungeon != null)
            {
                dungeon.DungeonStarted -= HandleDungeonStarted;
                dungeon.DungeonStarted += HandleDungeonStarted;
                dungeon.DungeonEnded -= HandleDungeonEnded;
                dungeon.DungeonEnded += HandleDungeonEnded;
            }

            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            if (dungeon != null)
            {
                dungeon.DungeonStarted -= HandleDungeonStarted;
                dungeon.DungeonEnded -= HandleDungeonEnded;
            }
            RestoreField();
        }

        private void HandleSceneChanged(Scene previous, Scene next)
        {
            if (dungeon != null && dungeon.IsDungeonActive)
                dungeon.EndDungeon();
            else
                RestoreField();
        }

        private void HandleDungeonStarted(string mapId,
            DungeonGenerationSettings settings)
        {
            if (isGenerated || generatedRoot != null)
            {
                Debug.LogWarning(
                    "[DungeonMapGenerator] Already inside a dungeon.", this);
                return;
            }

            try
            {
                if (!GenerateAndEnter(mapId, settings))
                {
                    Debug.LogWarning(
                        "[DungeonMapGenerator] Dungeon generation failed; returning to field.");
                    dungeon?.EndDungeon();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[DungeonMapGenerator] " + ex, this);
                dungeon?.EndDungeon();
            }
        }

        private bool GenerateAndEnter(string mapId,
            DungeonGenerationSettings settings)
        {
            player = FindFirstObjectByType<PlayerController>();
            mapManager = GetComponent<MapManager>() ??
                FindFirstObjectByType<MapManager>();
            if (player == null)
                return false;

            fieldRoot = FindFieldRoot(mapId);
            if (fieldRoot == null || fieldRoot.Ground == null)
                return false;

            TileBase floorTile = FindMostCommonGroundTile(fieldRoot.Ground);
            if (floorTile == null)
                return false;

            entranceWorldPosition = player.transform.position;
            fieldMapId = !string.IsNullOrWhiteSpace(mapId) ? mapId :
                mapManager != null ? mapManager.CurrentMapId : string.Empty;

            CurrentLayout = DungeonLayoutBuilder.Generate(
                settings, roomSize, gapBetweenRooms, corridorWidth);

            // The dungeon lives far away so any active, scene-level systems
            // outside TilemapMapRoot cannot interact with its characters.
            generatedRoot = new GameObject("_GeneratedDungeon",
                typeof(Grid));
            generatedRoot.transform.position =
                fieldRoot.transform.position + new Vector3(2048f, 0f, 0f);
            generatedRoot.transform.localScale =
                fieldRoot.transform.lossyScale;

            Grid sourceGrid = fieldRoot.GetComponent<Grid>();
            Grid grid = generatedRoot.GetComponent<Grid>();
            if (sourceGrid != null)
            {
                grid.cellSize = sourceGrid.cellSize;
                grid.cellGap = sourceGrid.cellGap;
                grid.cellLayout = sourceGrid.cellLayout;
                grid.cellSwizzle = sourceGrid.cellSwizzle;
            }

            generatedGround = CreateTilemap(
                generatedRoot.transform, "Ground", 0, false);
            generatedWalls = CreateTilemap(
                generatedRoot.transform, "InvisibleBoundary", 0, true);

            // Never change the original grass Tile asset or source Tilemap.
            foreach (Vector2Int cell in CurrentLayout.FloorCells)
                generatedGround.SetTile(
                    new Vector3Int(cell.x, cell.y, 0), floorTile);

            collisionTile = ScriptableObject.CreateInstance<Tile>();
            collisionTile.name = "DungeonInvisibleWallRuntime";
            collisionTile.sprite = null;
            collisionTile.colliderType = Tile.ColliderType.Grid;

            // One-tile (8-neighbor) wall border around all floor cells
            // prevents walking out or slipping diagonally between corners.
            var wallCells = new HashSet<Vector2Int>();
            foreach (Vector2Int floor in CurrentLayout.FloorCells)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Vector2Int neighbor = new Vector2Int(
                            floor.x + dx, floor.y + dy);
                        if (!CurrentLayout.FloorCells.Contains(neighbor))
                            wallCells.Add(neighbor);
                    }
            foreach (Vector2Int wall in wallCells)
                generatedWalls.SetTile(
                    new Vector3Int(wall.x, wall.y, 0), collisionTile);

            generatedGround.CompressBounds();
            generatedWalls.CompressBounds();

            fieldRootWasActive = fieldRoot.gameObject.activeSelf;
            // An unusual scene may parent the player beneath the map root.
            // In that case keep the source root active rather than hiding
            // the player along with it.
            bool playerUnderField =
                player.transform.IsChildOf(fieldRoot.transform);
            if (!playerUnderField && fieldRootWasActive)
                fieldRoot.gameObject.SetActive(false);

            SuspendOriginalEnemies();

            isGenerated = true;
            Vector3 startWorld = GetRoomWorldPosition(
                CurrentLayout.EntranceRoom);
            WarpParty(startWorld);
            if (showTemporaryReturnButton)
                CreateTemporaryReturnButton();

            Debug.Log(
                "[DungeonMapGenerator] Dungeon created. map=" + fieldMapId +
                ", seed=" + settings.seed +
                ", rooms=" + CurrentLayout.Rooms.Count +
                ", floor=" + CurrentLayout.FloorCells.Count +
                ", bossRoom=" + CurrentLayout.BossRoom.Index, this);
            DungeonMapReady?.Invoke(CurrentLayout);
            return true;
        }

        public Vector3 GetRoomWorldPosition(DungeonRoom room)
        {
            return GetRoomCellWorldPosition(room, Vector2Int.zero);
        }

        public Vector3 GetRoomCellWorldPosition(
            DungeonRoom room, Vector2Int offset)
        {
            if (room == null || generatedRoot == null)
                return Vector3.zero;

            Grid grid = generatedRoot.GetComponent<Grid>();
            Vector2Int cell = room.CenterCell + offset;
            return grid.GetCellCenterWorld(
                new Vector3Int(cell.x, cell.y, 0));
        }

        /// <summary>
        /// Tint only the square floor of a boss room. The source TileBase
        /// asset and all other rooms keep their original colors.
        /// Unlock tile-specific colors (generated tiles commonly lock them).
        /// </summary>
        public void TintBossRoom(DungeonRoom room, bool isFinalBoss)
        {
            if (room == null || generatedGround == null)
                return;

            Color tint = isFinalBoss ?
                finalBossFloorColor : midBossFloorColor;
            int side = Mathf.Clamp(roomSize, 6, 24);
            int half = side / 2;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    var cell = new Vector3Int(
                        room.CenterCell.x + x - half,
                        room.CenterCell.y + y - half, 0);
                    if (!generatedGround.HasTile(cell)) continue;
                    generatedGround.SetTileFlags(cell, TileFlags.None);
                    generatedGround.SetColor(cell, tint);
                }
        }

        /// <summary>
        /// This will later be called by the dungeon result Return button.
        /// For now a small temporary on-screen return button invokes it.
        /// </summary>
        public void ReturnToEntrance()
        {
            if (dungeon != null && dungeon.IsDungeonActive)
                dungeon.EndDungeon();
            else
                RestoreField();
        }

        private void HandleDungeonEnded()
        {
            RestoreField();
        }

        private void RestoreField()
        {
            if (generatedRoot == null && !isGenerated) return;

            if (fieldRoot != null && fieldRootWasActive &&
                !fieldRoot.gameObject.activeSelf)
                fieldRoot.gameObject.SetActive(true);

            for (int i = 0; i < suspendedEnemies.Count; i++)
            {
                SuspendedEnemy state = suspendedEnemies[i];
                if (state.body != null)
                {
                    state.body.simulated = state.bodyWasSimulated;
                    state.body.linearVelocity = state.oldVelocity;
                }
                if (state.combat != null)
                    state.combat.enabled = state.combatWasEnabled;
                if (state.wander != null)
                    state.wander.enabled = state.wanderWasEnabled;
            }
            suspendedEnemies.Clear();

            if (isGenerated && player != null)
            {
                MapBuildingAccess.SuppressOpenFor(1.5f);
                WarpParty(entranceWorldPosition);
            }

            if (temporaryReturnCanvas != null)
                Destroy(temporaryReturnCanvas);
            if (generatedRoot != null)
                Destroy(generatedRoot);
            if (collisionTile != null)
                Destroy(collisionTile);

            temporaryReturnCanvas = null;
            generatedRoot = null;
            generatedGround = null;
            generatedWalls = null;
            collisionTile = null;
            CurrentLayout = null;
            isGenerated = false;

            if (mapManager != null && !string.IsNullOrWhiteSpace(fieldMapId))
                mapManager.EnterMap(fieldMapId);
            else if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == GameMode.Dungeon)
                GameManager.Instance.SetMode(GameMode.Exploration);

            // Avoid leaving a saved world position inside the transient
            // dungeon after returning (normal save still runs periodically).
        }

        private static TilemapMapRoot FindFieldRoot(string mapId)
        {
            TilemapMapRoot[] roots =
                FindObjectsByType<TilemapMapRoot>(
                    FindObjectsSortMode.None);
            TilemapMapRoot fallback = null;
            for (int i = 0; i < roots.Length; i++)
            {
                TilemapMapRoot root = roots[i];
                if (root == null || root.Ground == null) continue;
                if (fallback == null) fallback = root;
                if (root.MapDefinition != null &&
                    string.Equals(root.MapDefinition.mapId, mapId,
                        StringComparison.Ordinal))
                    return root;
            }
            return fallback;
        }

        private static TileBase FindMostCommonGroundTile(Tilemap ground)
        {
            var counts = new Dictionary<TileBase, int>();
            TileBase mostCommon = null;
            int highest = 0;
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                TileBase tile = ground.GetTile(cell);
                if (tile == null) continue;
                counts.TryGetValue(tile, out int count);
                count++;
                counts[tile] = count;
                if (count > highest)
                {
                    mostCommon = tile;
                    highest = count;
                }
            }
            return mostCommon;
        }

        private static Tilemap CreateTilemap(
            Transform parent, string name, int order, bool collider)
        {
            GameObject child = new GameObject(name,
                typeof(Tilemap), typeof(TilemapRenderer));
            child.transform.SetParent(parent, false);
            Tilemap tiles = child.GetComponent<Tilemap>();
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;

            if (collider)
            {
                // Invisible walls with CompositeCollider2D: one physics
                // collider instead of hundreds of individual BoxColliders.
                renderer.enabled = false;
                Rigidbody2D body = child.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                CompositeCollider2D composite =
                    child.AddComponent<CompositeCollider2D>();
                composite.geometryType =
                    CompositeCollider2D.GeometryType.Polygons;
                TilemapCollider2D wall =
                    child.AddComponent<TilemapCollider2D>();
#if UNITY_6000_0_OR_NEWER
                wall.compositeOperation =
                    Collider2D.CompositeOperation.Merge;
#else
#pragma warning disable CS0618
                wall.usedByComposite = true;
#pragma warning restore CS0618
#endif
            }
            return tiles;
        }

        private void SuspendOriginalEnemies()
        {
            suspendedEnemies.Clear();
            EnemyHealth[] enemies =
                FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;

                var state = new SuspendedEnemy();
                state.combat = enemy.GetComponent<EnemyCombatAI>();
                state.wander = enemy.GetComponent<EnemyWanderAI>();
                state.body = enemy.GetComponent<Rigidbody2D>();
                if (state.combat != null)
                {
                    state.combatWasEnabled = state.combat.enabled;
                    state.combat.enabled = false;
                }
                if (state.wander != null)
                {
                    state.wanderWasEnabled = state.wander.enabled;
                    state.wander.enabled = false;
                }
                if (state.body != null)
                {
                    state.bodyWasSimulated = state.body.simulated;
                    state.oldVelocity = state.body.linearVelocity;
                    state.body.linearVelocity = Vector2.zero;
                    state.body.simulated = false;
                }
                suspendedEnemies.Add(state);
            }
        }

        private void WarpParty(Vector3 worldPosition)
        {
            if (player == null) return;

            // Rigidbody position + transform + trail all update together,
            // so followers/camera do not travel across the whole map.
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = worldPosition;
            }

            player.transform.position = new Vector3(
                worldPosition.x, worldPosition.y,
                player.transform.position.z);
            PlayerTrailRecorder trail =
                player.GetComponent<PlayerTrailRecorder>();
            if (trail != null)
                trail.ResetTrail();

            PartyFieldSpawner spawner =
                GetComponent<PartyFieldSpawner>() ??
                FindFirstObjectByType<PartyFieldSpawner>();
            MonsterManager monsters =
                GetComponent<MonsterManager>() ??
                FindFirstObjectByType<MonsterManager>();

            if (spawner != null && monsters != null)
            {
                for (int i = 0; i < monsters.PartyMonsterUniqueIds.Count; i++)
                {
                    PartyMemberCombat ally = spawner.GetActiveMember(
                        monsters.PartyMonsterUniqueIds[i]);
                    if (ally == null) continue;
                    Vector3 position = worldPosition +
                        new Vector3((i - 1) * 0.6f, -0.5f, 0f);
                    Rigidbody2D rb = ally.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.position = position;
                    }
                    ally.transform.position = position;
                }
            }

            Physics2D.SyncTransforms();
            CameraFollow2D camera =
                FindFirstObjectByType<CameraFollow2D>();
            if (camera != null)
                camera.SetTarget(player.transform, true);
        }

        private void CreateTemporaryReturnButton()
        {
            GameObject canvasObject = new GameObject(
                "_DungeonTemporaryReturnCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            temporaryReturnCanvas = canvasObject;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            Button exit = InventoryUiFactory.Button(
                canvasObject.transform, "TemporaryReturn",
                "帰還（仮）", Vector2.zero, new Vector2(260f, 86f));
            RectTransform rect = exit.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-28f, -115f);
            exit.onClick.AddListener(ReturnToEntrance);

            TMP_Text note = InventoryUiFactory.Text(
                canvasObject.transform, "TestingNotice",
                "マップ生成テスト中：ボス・宝箱は次の段階で追加",
                Vector2.zero, new Vector2(750f, 65f), 23f);
            RectTransform textRect = note.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -24f);
            note.alignment = TextAlignmentOptions.Center;
        }
    }
}
