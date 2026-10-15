using MixMaster.CameraSystem;
using MixMaster.Core;
using MixMaster.Player;
using UnityEngine;

namespace MixMaster.World
{
    public enum MapExitDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// Inspector-driven transition between TilemapMapRoots in the same scene.
    /// Destination is a scene reference, so towns do not need a MapSO.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MapExitTransition : MonoBehaviour
    {
        [Header("接続設定")]
        [SerializeField] private MapExitDirection exitDirection = MapExitDirection.Up;

        [Tooltip("Hierarchyの移動先 TilemapMapRoot を割り当てます。街はMapSO不要です。")]
        [SerializeField] private TilemapMapRoot destinationMap;

        [Tooltip("移動先の反対側の端から何マス内側に出現させるか。")]
        [SerializeField, Min(1)] private int arrivalInsetCells = 2;

        private static float nextTransitionAllowedAt;
        private bool reportedMissingDestination;

        public MapExitDirection ExitDirection => exitDirection;
        public TilemapMapRoot DestinationMap => destinationMap;

        // Editor generator only updates direction. Destination stays serialized.
        public void SetDirection(MapExitDirection newDirection)
        {
            exitDirection = newDirection;
        }

        public void SetDestinationMap(TilemapMapRoot map)
        {
            destinationMap = map;
            reportedMissingDestination = false;
        }

        private void OnTriggerEnter2D(Collider2D collider)
        {
            if (Time.time < nextTransitionAllowedAt)
                return;

            PlayerController player = collider.GetComponentInParent<PlayerController>();
            if (player == null)
                return;

            TryTransition(player);
        }

        public bool TryTransition(PlayerController player)
        {
            if (player == null)
                return false;

            TilemapMapRoot currentMap = GetComponentInParent<TilemapMapRoot>();
            if (currentMap == null || destinationMap == null)
            {
                if (!reportedMissingDestination)
                {
                    Debug.LogWarning("[MapExitTransition] 接続先TilemapMapRoot未設定: " +
                        gameObject.name + "。InspectorのDestination Mapを指定してください。", this);
                    reportedMissingDestination = true;
                }
                return false;
            }

            if (currentMap == destinationMap ||
                currentMap.gameObject.scene != destinationMap.gameObject.scene)
            {
                Debug.LogWarning("[MapExitTransition] 同一シーン内の別のTilemapMapRootを指定してください。", this);
                return false;
            }

            MapExitDirection entrance = Opposite(exitDirection);
            string oppositeName = "_MapConnectionPoints/MapConnection_" + JapaneseName(entrance);
            Transform arrivalMarker = destinationMap.transform.Find(oppositeName);
            if (arrivalMarker == null || !arrivalMarker.gameObject.activeSelf)
            {
                Debug.LogWarning("[MapExitTransition] 移動先に反対側の出入口がありません: " +
                    destinationMap.name + "/" + oppositeName +
                    "。移動先のJSONで該当出入口をTRUEにしてください。", this);
                return false;
            }

            Grid destinationGrid = destinationMap.GetComponent<Grid>();
            if (destinationGrid == null || destinationMap.Ground == null)
            {
                Debug.LogWarning("[MapExitTransition] 移動先マップにGridまたはGroundがありません。", this);
                return false;
            }

            Vector3Int edgeCell = destinationGrid.WorldToCell(arrivalMarker.position);
            Vector3Int inward = InwardDirection(entrance);
            Vector3Int arrivalCell = edgeCell;
            bool found = false;

            for (int inset = Mathf.Max(1, arrivalInsetCells);
                inset <= Mathf.Max(10, arrivalInsetCells); inset++)
            {
                Vector3Int candidate = edgeCell + inward * inset;
                if (!destinationMap.Ground.HasTile(candidate) ||
                    (destinationMap.Collision != null &&
                     destinationMap.Collision.HasTile(candidate)))
                    continue;

                arrivalCell = candidate;
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogWarning("[MapExitTransition] 移動先の入口付近に通行可能なセルがありません: " +
                    destinationMap.name, this);
                return false;
            }

            // Cooldown also protects against immediate reverse triggering
            // when the two maps overlap at the same world coordinates.
            nextTransitionAllowedAt = Time.time + 0.65f;

            // The player normally lives outside the map root. Keep it alive
            // if a scene author accidentally parented it inside the old map.
            if (player.transform.IsChildOf(currentMap.transform))
                player.transform.SetParent(null, true);

            TilemapMapRoot[] roots = FindObjectsByType<TilemapMapRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roots.Length; i++)
            {
                TilemapMapRoot root = roots[i];
                if (root != null &&
                    root != destinationMap &&
                    root.gameObject.scene == currentMap.gameObject.scene)
                    root.gameObject.SetActive(false);
            }

            // Update the map ID before enabling the new map: enemy spawn
            // points can look up the current MapManager in OnEnable.
            MapManager manager = FindFirstObjectByType<MapManager>();
            if (manager != null)
            {
                MapSO mapDefinition = destinationMap.MapDefinition;
                if (mapDefinition == null)
                    manager.EnterTown(destinationMap.name);
                else
                    manager.EnterMap(!string.IsNullOrWhiteSpace(mapDefinition.mapId)
                        ? mapDefinition.mapId : destinationMap.name);
            }

            destinationMap.gameObject.SetActive(true);

            Vector3 targetWorld = destinationGrid.GetCellCenterWorld(arrivalCell);
            Vector3 target = new Vector3(targetWorld.x, targetWorld.y,
                player.transform.position.z);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = new Vector2(target.x, target.y);
            }
            player.transform.position = target;
            Physics2D.SyncTransforms();

            CameraFollow2D cameraFollow = FindFirstObjectByType<CameraFollow2D>();
            if (cameraFollow != null)
                cameraFollow.SetTarget(player.transform, true);

            Debug.Log("[MapExitTransition] " + currentMap.name +
                " -> " + destinationMap.name + " (" + exitDirection + ")", this);
            return true;
        }

        public static MapExitDirection Opposite(MapExitDirection direction)
        {
            switch (direction)
            {
                case MapExitDirection.Up: return MapExitDirection.Down;
                case MapExitDirection.Down: return MapExitDirection.Up;
                case MapExitDirection.Left: return MapExitDirection.Right;
                default: return MapExitDirection.Left;
            }
        }

        public static string JapaneseName(MapExitDirection direction)
        {
            switch (direction)
            {
                case MapExitDirection.Up: return "上";
                case MapExitDirection.Down: return "下";
                case MapExitDirection.Left: return "左";
                default: return "右";
            }
        }

        private static Vector3Int InwardDirection(MapExitDirection side)
        {
            switch (side)
            {
                case MapExitDirection.Up: return Vector3Int.down;
                case MapExitDirection.Down: return Vector3Int.up;
                case MapExitDirection.Left: return Vector3Int.right;
                default: return Vector3Int.left;
            }
        }
    }
}
