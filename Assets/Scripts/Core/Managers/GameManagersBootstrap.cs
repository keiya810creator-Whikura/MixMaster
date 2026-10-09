using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Creates the persistent manager root before the first scene starts.
    /// Managers do not need to be manually attached to a scene object.
    /// </summary>
    public static class GameManagersBootstrap
    {
        private const string RootName = "_GameManagers";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Object.DontDestroyOnLoad(root);
            }

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
            Ensure<EquipmentManager>(root);
            Ensure<OfflineRewardManager>(root);
        }

        private static void Ensure<T>(GameObject root) where T : Component
        {
            if (root.GetComponent<T>() == null)
            {
                root.AddComponent<T>();
            }
        }
    }
}