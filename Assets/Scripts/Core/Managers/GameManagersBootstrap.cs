using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Loads a user-configurable prefab from Resources before the first scene.
    /// Without a prefab, retains the old automatically generated managers.
    /// </summary>
    public static class GameManagersBootstrap
    {
        public const string RootName = "_GameManagers";
        public const string SettingsPrefabResourcePath = "Managers/GameManagers";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                GameObject prefab = Resources.Load<GameObject>(
                    SettingsPrefabResourcePath);
                if (prefab != null)
                {
                    root = Object.Instantiate(prefab);
                    root.name = RootName;
                }
                else
                {
                    root = new GameObject(RootName);
                }
            }

            Object.DontDestroyOnLoad(root);
            EnsureComponents(root);
        }

        /// <summary>
        /// Shared by runtime fallback and the Editor prefab builder.
        /// Never replaces existing components/serialized Inspector settings.
        /// </summary>
        public static void EnsureComponents(GameObject root)
        {
            if (root == null) return;

            Ensure<GameManager>(root);
            Ensure<SaveManager>(root);
            Ensure<MapManager>(root);
            Ensure<PlayerManager>(root);
            Ensure<MonsterManager>(root);
            Ensure<MixMaster.Monsters.PartyFieldSpawner>(root);
            Ensure<AltarManager>(root);
            Ensure<SpawnManager>(root);
            Ensure<DropManager>(root);
            Ensure<MaterialInventoryManager>(root);
            Ensure<MixMaster.UI.MaterialDropLogUI>(root);
            Ensure<MixMaster.UI.MonsterObtainLogUI>(root);
            Ensure<BreedingManager>(root);
            Ensure<DungeonManager>(root);
            Ensure<MixMaster.World.DungeonMapGenerator>(root);
            Ensure<MixMaster.World.DungeonEnemySpawner>(root);
            Ensure<EquipmentManager>(root);
            Ensure<OfflineRewardManager>(root);
        }

        private static void Ensure<T>(GameObject root) where T : Component
        {
            if (root.GetComponent<T>() == null)
                root.AddComponent<T>();
        }
    }
}
