using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class MinimapHUD : MonoBehaviour
    {
        [Header("Prefab layout")]
        [SerializeField] private Toggle visibilityToggle;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private RectTransform mapArea;
        [SerializeField] private MinimapDotGraphic dotTemplate;
        [Header("Points")]
        [SerializeField] private Color exitColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color altarColor = new Color(0.3f, 1f, 0.45f);
        [SerializeField] private Color dungeonColor = new Color(0.25f, 0.6f, 1f);
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField, Min(2f)] private float pointSize = 10f;
        [SerializeField, Min(2f)] private float playerPointSize = 14f;
        [SerializeField, Min(0f)] private float padding = 12f;
        [Header("Refresh (unscaled seconds)")]
        [SerializeField, Min(0.2f)] private float discoveryInterval = 1f;
        [SerializeField, Min(0.02f)] private float positionInterval = 0.05f;

        private sealed class Point
        {
            public Transform target;
            public Behaviour owner;
            public Vector3 fixedPosition;
            public bool fixedPoint;
            public MinimapDotGraphic dot;
        }

        private readonly List<Point> points = new List<Point>();
        private PlayerController player;
        private MapManager maps;
        private TilemapMapRoot field;
        private DungeonMapGenerator dungeon;
        private Rect worldBounds;
        private bool hasBounds;
        private bool subscribed;
        private float nextDiscovery, nextPosition;
        private int usedPoints;

        private void Awake()
        {
            if (visibilityToggle == null || mapPanel == null || mapArea == null || dotTemplate == null)
            {
                Debug.LogError("[MinimapHUD] Prefabの参照を設定してください。", this);
                enabled = false;
                return;
            }
            dotTemplate.gameObject.SetActive(false);
            visibilityToggle.onValueChanged.AddListener(SetVisible);
            SetVisible(visibilityToggle.isOn);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            nextDiscovery = nextPosition = 0f;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (maps != null && subscribed) maps.MapChanged -= OnMapChanged;
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (visibilityToggle != null) visibilityToggle.onValueChanged.RemoveListener(SetVisible);
        }

        public void SetVisible(bool visible)
        {
            if (mapPanel == null) return;
            mapPanel.SetActive(visible);
            if (visibilityToggle != null) visibilityToggle.SetIsOnWithoutNotify(visible);
            nextDiscovery = nextPosition = 0f;
        }

        private void OnMapChanged(string _) => nextDiscovery = 0f;
        private void OnSceneLoaded(Scene _, LoadSceneMode __) => nextDiscovery = 0f;

        private void LateUpdate()
        {
            if (mapPanel == null || !mapPanel.activeInHierarchy) return;
            float now = Time.unscaledTime;
            if (now >= nextDiscovery)
            {
                Discover();
                nextDiscovery = now + Mathf.Max(0.2f, discoveryInterval);
            }
            if (now < nextPosition) return;
            nextPosition = now + Mathf.Max(0.02f, positionInterval);
            UpdatePoints();
        }

        private void Discover()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (maps == null)
            {
                maps = FindFirstObjectByType<MapManager>();
                subscribed = false;
            }
            if (maps != null && !subscribed)
            {
                maps.MapChanged += OnMapChanged;
                subscribed = true;
            }
            if (dungeon == null) dungeon = FindFirstObjectByType<DungeonMapGenerator>();
            usedPoints = 0;
            hasBounds = false;
            field = null;
            bool inDungeon = dungeon != null && dungeon.IsGenerated &&
                dungeon.RuntimeRoot != null && dungeon.RuntimeRoot.gameObject.activeInHierarchy &&
                dungeon.Ground != null && player != null &&
                TryWorldBounds(dungeon.Ground, dungeon.Ground.cellBounds, out Rect dungeonBounds) &&
                dungeonBounds.Contains(player.transform.position);
            if (inDungeon)
            {
                hasBounds = TryWorldBounds(dungeon.Ground, dungeon.Ground.cellBounds, out worldBounds);
                if (dungeon.CurrentLayout != null)
                    AddPoint(null, null, exitColor, false,
                        dungeon.GetRoomWorldPosition(dungeon.CurrentLayout.EntranceRoom), true);
            }
            else
            {
                float bestScore = float.MinValue;
                foreach (TilemapMapRoot root in FindObjectsByType<TilemapMapRoot>(FindObjectsSortMode.None))
                {
                    if (root.Ground == null || !TryWorldBounds(root.Ground,
                        root.GetUsedCellBounds(), out Rect bounds)) continue;
                    bool contains = player != null && bounds.Contains(player.transform.position);
                    string id = root.MapDefinition != null ? root.MapDefinition.mapId : root.name;
                    float score = (contains ? 1000f : 0f) +
                        (maps != null && id == maps.CurrentMapId ? 100f : 0f);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    field = root;
                    worldBounds = bounds;
                    hasBounds = true;
                }
            }
            if (hasBounds)
            {
                foreach (MapExitTransition exit in FindObjectsByType<MapExitTransition>(FindObjectsSortMode.None))
                    if (exit.isActiveAndEnabled && exit.DestinationMap != null &&
                        BelongsToMap(exit.transform, inDungeon))
                        AddPoint(exit.transform, exit, exitColor);
                foreach (MapBuildingAccess building in FindObjectsByType<MapBuildingAccess>(FindObjectsSortMode.None))
                {
                    if (!building.isActiveAndEnabled || !BelongsToMap(building.transform, inDungeon)) continue;
                    if (building.BuildingType == MapBuildingType.Altar)
                        AddPoint(building.transform, building, altarColor);
                    else if (building.BuildingType == MapBuildingType.DungeonEntrance)
                        AddPoint(building.transform, building, dungeonColor);
                }
                if (player != null) AddPoint(player.transform, player, playerColor, true);
            }
            for (int i = usedPoints; i < points.Count; i++) points[i].dot.gameObject.SetActive(false);
            nextPosition = 0f;
        }

        private bool BelongsToMap(Transform target, bool inDungeon)
        {
            if (inDungeon) return target.IsChildOf(dungeon.RuntimeRoot);
            if (field == null || target.gameObject.scene != field.gameObject.scene) return false;
            TilemapMapRoot owner = target.GetComponentInParent<TilemapMapRoot>();
            return owner != null ? owner == field : worldBounds.Contains(target.position);
        }

        private void AddPoint(Transform target, Behaviour owner, Color color,
            bool isPlayer = false, Vector3 position = default, bool fixedPoint = false)
        {
            if (usedPoints == points.Count)
                points.Add(new Point { dot = Instantiate(dotTemplate, mapArea) });
            Point point = points[usedPoints++];
            point.target = target;
            point.owner = owner;
            point.fixedPosition = position;
            point.fixedPoint = fixedPoint;
            point.dot.color = color;
            point.dot.raycastTarget = false;
            RectTransform rect = point.dot.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * (isPlayer ? playerPointSize : pointSize);
            point.dot.gameObject.SetActive(true);
            rect.SetAsLastSibling();
        }

        private void UpdatePoints()
        {
            Vector2 size = mapArea.rect.size;
            for (int i = 0; i < usedPoints; i++)
            {
                Point point = points[i];
                bool visible = hasBounds && (point.fixedPoint ||
                    (point.target != null && point.owner != null && point.owner.isActiveAndEnabled));
                point.dot.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 position = point.fixedPoint ? point.fixedPosition : point.target.position;
                point.dot.rectTransform.anchoredPosition = Project(position, worldBounds, size, padding);
            }
        }

        public static Vector2 Project(Vector3 position, Rect bounds, Vector2 size, float margin)
        {
            float width = Mathf.Max(0.001f, bounds.width);
            float height = Mathf.Max(0.001f, bounds.height);
            float scale = Mathf.Min(Mathf.Max(0f, size.x - 2f * margin) / width,
                Mathf.Max(0f, size.y - 2f * margin) / height);
            return new Vector2(
                (Mathf.Clamp(position.x, bounds.xMin, bounds.xMax) - bounds.center.x) * scale,
                (Mathf.Clamp(position.y, bounds.yMin, bounds.yMax) - bounds.center.y) * scale);
        }

        private static bool TryWorldBounds(Tilemap tilemap, BoundsInt cells, out Rect bounds)
        {
            bounds = default;
            if (tilemap == null || cells.size.x <= 0 || cells.size.y <= 0) return false;
            Vector3 a = tilemap.CellToWorld(new Vector3Int(cells.xMin, cells.yMin, 0));
            Vector3 b = tilemap.CellToWorld(new Vector3Int(cells.xMax, cells.yMin, 0));
            Vector3 c = tilemap.CellToWorld(new Vector3Int(cells.xMin, cells.yMax, 0));
            Vector3 d = tilemap.CellToWorld(new Vector3Int(cells.xMax, cells.yMax, 0));
            bounds = Rect.MinMaxRect(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)),
                Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)),
                Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)),
                Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)));
            return bounds.width > 0f && bounds.height > 0f;
        }
    }
}
