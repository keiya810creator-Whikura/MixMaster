using UnityEngine;
using MixMaster.Player;
using MixMaster.UI;
using MixMaster.Combat;

namespace MixMaster.World
{
    public enum MapBuildingType
    {
        Altar,
        DungeonEntrance,
        BreedingShop,
        Blacksmith,
        Reincarnation,
        ConsumableShop
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MapBuildingAccess : MonoBehaviour
    {
        [SerializeField] private MapBuildingType buildingType;

        private static float suppressOpenUntil;

        public MapBuildingType BuildingType => buildingType;

        // Prevent the returned player from immediately reopening the
        // entrance panel while still overlapping its trigger.
        public static void SuppressOpenFor(float seconds)
        {
            suppressOpenUntil = Mathf.Max(
                suppressOpenUntil, Time.unscaledTime + Mathf.Max(0f, seconds));
        }

        public void Configure(MapBuildingType type)
        {
            buildingType = type;

            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
                trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null ||
                Time.unscaledTime < suppressOpenUntil)
                return;

            PlayerController player =
                other.GetComponentInParent<PlayerController>();

            if (player == null)
                return;

            PlayerAutoAttack autoAttack =
                player.GetComponent<PlayerAutoAttack>();

            // Auto attack lunges the Player forward/backward.
            // Entering a building trigger during that temporary movement
            // must not open the UI.
            if (autoAttack != null &&
                autoAttack.IsLunging)
            {
                return;
            }

            MapBuildingUIRouter router =
                MapBuildingUIRouter.Instance;

            if (router == null)
            {
                router =
                    FindFirstObjectByType<MapBuildingUIRouter>();
            }

            if (router == null &&
                (buildingType == MapBuildingType.DungeonEntrance ||
                 buildingType == MapBuildingType.BreedingShop ||
                 buildingType == MapBuildingType.Blacksmith ||
                 buildingType == MapBuildingType.Reincarnation ||
                 buildingType == MapBuildingType.ConsumableShop))
            {
                // The entrance must work in older exploration scenes that
                // contain the map prefab but have no UI router yet.
                GameObject routerObject = new GameObject("MapBuildingUIRouter");
                router = routerObject.AddComponent<MapBuildingUIRouter>();
            }

            if (router == null)
            {
                Debug.LogWarning(
                    "[MapBuildingAccess] MapBuildingUIRouterがSceneにありません。",
                    this);
                return;
            }

            router.Open(buildingType);
        }
    }
}
