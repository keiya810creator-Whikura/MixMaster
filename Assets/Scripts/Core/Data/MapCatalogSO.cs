using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [CreateAssetMenu(fileName = "MapCatalog", menuName = "MixMaster/Data/Map Catalog")]
    public sealed class MapCatalogSO : ScriptableObject
    {
        public const string ResourcesPath = "Data/MapCatalog";
        [SerializeField] private List<MapSO> maps = new List<MapSO>();
        private static MapCatalogSO cached;
        public IReadOnlyList<MapSO> Maps => maps;

        public static MapCatalogSO Load()
        {
            if (cached == null) cached = Resources.Load<MapCatalogSO>(ResourcesPath);
            return cached;
        }

        public MapSO GetMap(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId) || maps == null) return null;
            return maps.Find(x => x != null && string.Equals(
                x.mapId, mapId, StringComparison.OrdinalIgnoreCase));
        }

        // Used to migrate pre-enhancement records that have no sourceMapId.
        // An ambiguous mapping is never resolved by the player's current map.
        public MapSO FindEquipmentMap(string equipmentId)
        {
            if (string.IsNullOrWhiteSpace(equipmentId) || maps == null) return null;
            MapSO found = null;
            foreach (MapSO map in maps)
            {
                if (map == null || map.dungeonEquipment == null ||
                    !map.dungeonEquipment.Exists(x => x != null && string.Equals(
                        x.equipmentId, equipmentId, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (found != null && found != map) return null;
                found = map;
            }
            return found;
        }

        public void SetData(List<MapSO> definitions) =>
            maps = definitions ?? new List<MapSO>();
    }
}
