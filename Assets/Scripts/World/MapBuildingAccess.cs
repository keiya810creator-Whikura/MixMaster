using UnityEngine;
using MixMaster.Player;
using MixMaster.UI;

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
            if (other == null ||
                other.GetComponentInParent<PlayerController>() == null)
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
