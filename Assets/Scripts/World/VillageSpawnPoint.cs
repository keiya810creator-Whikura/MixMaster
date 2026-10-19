using MixMaster.CameraSystem;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.UI;
using UnityEngine;

namespace MixMaster.World
{
    /// <summary>Safe startup spawn in the village, including inactive map roots.</summary>
    [DisallowMultipleComponent]
    public sealed class VillageSpawnPoint : MonoBehaviour
    {
        [SerializeField] private TilemapMapRoot villageMap;
        [SerializeField] private bool useMapCenter = true;
        [SerializeField, Min(0)] private int searchRadiusCells = 8;

        public void Configure(TilemapMapRoot map) => villageMap = map;

        public bool TryRespawnPlayer()
        {
            if (villageMap == null) villageMap = GetComponentInParent<TilemapMapRoot>(true);
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (villageMap == null || player == null) return false;

            TilemapMapRoot[] roots = FindObjectsByType<TilemapMapRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TilemapMapRoot root in roots)
            {
                if (root == null || root.gameObject.scene != villageMap.gameObject.scene) continue;
                if (player.transform.IsChildOf(root.transform))
                    player.transform.SetParent(null, true);
                if (root != villageMap) root.gameObject.SetActive(false);
            }

            MapBuildingUIRouter router = FindFirstObjectByType<MapBuildingUIRouter>();
            if (router != null) router.CloseAll();
            MapManager map = FindFirstObjectByType<MapManager>();
            if (map != null) map.EnterTown(villageMap.name);
            villageMap.gameObject.SetActive(true);
            Physics2D.SyncTransforms();

            Vector3 target = transform.position;
            if (useMapCenter && !TryGetCentralPosition(player, out target)) return false;
            target.z = player.transform.position.z;
            MapBuildingAccess.SuppressOpenFor(0.75f);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = new Vector2(target.x, target.y);
            }
            player.transform.position = target;
            Physics2D.SyncTransforms();
            PlayerManager playerState = FindFirstObjectByType<PlayerManager>();
            if (playerState != null && playerState.CurrentHp <= 0L)
                playerState.RestoreFullResources();
            PlayerTrailRecorder trail = player.GetComponent<PlayerTrailRecorder>();
            if (trail != null) trail.ResetTrail();
            MixMaster.Monsters.PartyFieldSpawner spawner =
                FindFirstObjectByType<MixMaster.Monsters.PartyFieldSpawner>();
            if (spawner != null) spawner.RefreshParty();
            CameraFollow2D cameraFollow = FindFirstObjectByType<CameraFollow2D>();
            if (cameraFollow != null) cameraFollow.SetTarget(player.transform, true);
            return true;
        }

        private bool TryGetCentralPosition(PlayerController player, out Vector3 position)
        {
            position = transform.position;
            Grid grid = villageMap.GetComponent<Grid>();
            if (grid == null || villageMap.Ground == null) return false;
            BoundsInt bounds = villageMap.Ground.cellBounds;
            Vector3Int center = new Vector3Int((bounds.xMin + bounds.xMax - 1) / 2,
                (bounds.yMin + bounds.yMax - 1) / 2, bounds.zMin);
            int bestDistance = int.MaxValue;
            bool found = false;
            int radius = Mathf.Clamp(searchRadiusCells, 0, 64);
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                int distance = x * x + y * y;
                if (distance >= bestDistance) continue;
                Vector3Int cell = center + new Vector3Int(x, y, 0);
                if (!villageMap.Ground.HasTile(cell) ||
                    (villageMap.Collision != null && villageMap.Collision.HasTile(cell))) continue;
                Vector3 candidate = grid.GetCellCenterWorld(cell);
                bool blocked = false;
                foreach (Collider2D collider in Physics2D.OverlapCircleAll(candidate, 0.20f))
                {
                    if (collider != null && !collider.isTrigger &&
                        !collider.transform.IsChildOf(player.transform)) { blocked = true; break; }
                }
                if (blocked) continue;
                bestDistance = distance;
                position = candidate;
                found = true;
            }
            return found;
        }
    }
}
