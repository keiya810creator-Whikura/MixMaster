using UnityEngine;
using UnityEngine.Tilemaps;

namespace MixMaster.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Grid))]
    public sealed class TilemapMapRoot : MonoBehaviour
    {
        [Header("Tilemap Layers")]
        [SerializeField] private Tilemap ground;
        [SerializeField] private Tilemap decoration;
        [SerializeField] private Tilemap collision;
        [SerializeField] private Tilemap above;

        public Grid Grid { get; private set; }
        public Tilemap Ground => ground;
        public Tilemap Decoration => decoration;
        public Tilemap Collision => collision;
        public Tilemap Above => above;

        private void Awake()
        {
            Grid = GetComponent<Grid>();
        }

        public void SetLayers(
            Tilemap groundLayer,
            Tilemap decorationLayer,
            Tilemap collisionLayer,
            Tilemap aboveLayer)
        {
            ground = groundLayer;
            decoration = decorationLayer;
            collision = collisionLayer;
            above = aboveLayer;

            if (Grid == null)
                Grid = GetComponent<Grid>();
        }

        public void ClearAllTiles()
        {
            ground?.ClearAllTiles();
            decoration?.ClearAllTiles();
            collision?.ClearAllTiles();
            above?.ClearAllTiles();
        }

        public BoundsInt GetUsedCellBounds()
        {
            bool hasBounds = false;
            BoundsInt result = default;

            ExpandBounds(ground, ref result, ref hasBounds);
            ExpandBounds(decoration, ref result, ref hasBounds);
            ExpandBounds(collision, ref result, ref hasBounds);
            ExpandBounds(above, ref result, ref hasBounds);

            return hasBounds
                ? result
                : new BoundsInt(Vector3Int.zero, Vector3Int.zero);
        }

        private static void ExpandBounds(
            Tilemap tilemap,
            ref BoundsInt result,
            ref bool hasBounds)
        {
            if (tilemap == null || tilemap.cellBounds.size == Vector3Int.zero)
                return;

            BoundsInt bounds = tilemap.cellBounds;

            if (!hasBounds)
            {
                result = bounds;
                hasBounds = true;
                return;
            }

            Vector3Int min = Vector3Int.Min(result.min, bounds.min);
            Vector3Int max = Vector3Int.Max(result.max, bounds.max);

            result = new BoundsInt(
                min,
                max - min);
        }
    }
}
