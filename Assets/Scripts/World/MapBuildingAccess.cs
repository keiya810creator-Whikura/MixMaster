using UnityEngine;
using MixMaster.Player;
using MixMaster.UI;
using MixMaster.Combat;

namespace MixMaster.World
{
    public enum MapBuildingType
    {
        Altar,
        DungeonEntrance
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MapBuildingAccess : MonoBehaviour
    {
        [SerializeField] private MapBuildingType buildingType;

        public MapBuildingType BuildingType => buildingType;

        public void Configure(MapBuildingType type)
        {
            buildingType = type;

            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
                trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
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
