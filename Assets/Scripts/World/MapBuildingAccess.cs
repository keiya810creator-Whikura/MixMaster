using UnityEngine;
using MixMaster.Player;
using MixMaster.UI;

namespace MixMaster.World
{
    public enum MapBuildingType
    {
        Altar,
        DungeonEntrance,
        BreedingShop,
        Blacksmith,
        Reincarnation,
        ConsumableShop,
        TeleportCircle
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MapBuildingAccess : MonoBehaviour
    {
        [SerializeField] private MapBuildingType buildingType;

        private static float suppressOpenUntil;

        public MapBuildingType BuildingType => buildingType;

        internal static bool AccessSuppressed => Time.unscaledTime < suppressOpenUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSuppression() => suppressOpenUntil = 0f;

        // Briefly hide the prompt while the player is being repositioned.
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
            if (!isActiveAndEnabled || other == null) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router == null) router = FindFirstObjectByType<MapBuildingUIRouter>();
            if (router == null)
                router = new GameObject("MapBuildingUIRouter").AddComponent<MapBuildingUIRouter>();
            router.EnterAccess(this, other, player);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router != null) router.ExitAccess(this, other);
        }

        private void OnDisable()
        {
            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router != null) router.ExitAccess(this);
        }
    }
}
