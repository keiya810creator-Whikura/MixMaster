using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [CreateAssetMenu(
        fileName = "Map_",
        menuName = "MixMaster/Data/Map")]
    public sealed class MapSO : ScriptableObject
    {
        [Header("Identity")]
        public string mapId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite thumbnail;

        [Header("Scene (Legacy / shared scene)")]
        public string explorationSceneName;
        public string dungeonSceneName;

        [Header("Map Layout")]
        public string layoutJsonName;
        public TextAsset layoutJson;
        public string thumbnailKey;
        [Min(1)] public int recommendedLevel = 1;

        [Header("Monsters")]
        // Maintained for compatibility with existing title pool and other systems.
        public List<MonsterSO> monsters = new List<MonsterSO>();

        [Tooltip("CSV normal monster slots 01-05, in exact spawn order.")]
        public List<MonsterSO> normalMonsters = new List<MonsterSO>();
        public MonsterSO bossMonster;
        [Min(1)] public int normalMonsterLevel = 1;
        [Min(1)] public int strongMonsterLevel = 5;

        [Header("Dungeon")]
        public MaterialSO dungeonMaterial;

        /// <summary>
        /// Creates the map-wide title pool from every monster on this map.
        /// Duplicate TitleSO references are returned only once.
        /// </summary>
        public List<TitleSO> BuildTitlePool()
        {
            List<TitleSO> result = new List<TitleSO>();
            HashSet<TitleSO> unique = new HashSet<TitleSO>();

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterSO monster = monsters[i];

                if (monster == null || monster.titlePool == null)
                    continue;

                for (int j = 0; j < monster.titlePool.Count; j++)
                {
                    TitleSO title = monster.titlePool[j];

                    if (title != null && unique.Add(title))
                        result.Add(title);
                }
            }

            return result;
        }
    }
}
