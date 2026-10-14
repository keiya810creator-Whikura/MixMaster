using System;
using UnityEngine;

namespace MixMaster.Core
{
    [Serializable]
    public class DungeonGenerationSettings
    {
        [Min(2)] public int roomCount = 8;
        [Min(0)] public int branchCount = 2;
        [Min(0)] public int sideBossCount = 1;
        public int seed;
    }

    [DisallowMultipleComponent]
    public sealed class DungeonManager : MonoBehaviour
    {
        [Header("Default dungeon generation (Manager Prefab Inspector)")]
        [SerializeField] private DungeonGenerationSettings defaultSettings =
            new DungeonGenerationSettings();

        public DungeonGenerationSettings GetDefaultSettings()
        {
            DungeonGenerationSettings source =
                defaultSettings ?? new DungeonGenerationSettings();
            return new DungeonGenerationSettings
            {
                roomCount = source.roomCount,
                branchCount = source.branchCount,
                sideBossCount = source.sideBossCount,
                seed = source.seed
            };
        }

        public DungeonGenerationSettings CurrentSettings { get; private set; }
        public int DefeatedSideBosses { get; private set; }
        public bool IsDungeonActive { get; private set; }
        public string EntranceMapId { get; private set; } = string.Empty;
        public bool IsFinalBossDefeated { get; private set; }

        // Chest/result system will subscribe to this in the next phase.
        public event Action<Vector3> FinalBossDefeated;

        // Dungeon map generation will subscribe to this event next.
        public event Action<string, DungeonGenerationSettings> DungeonStarted;
        public event Action DungeonEnded;

        public void BeginDungeon(DungeonGenerationSettings settings,
            string entranceMapId = null)
        {
            CurrentSettings = settings ?? new DungeonGenerationSettings();

            if (CurrentSettings.seed == 0)
                CurrentSettings.seed = UnityEngine.Random.Range(1, int.MaxValue);

            CurrentSettings.roomCount = Mathf.Max(2, CurrentSettings.roomCount);
            CurrentSettings.branchCount = Mathf.Max(0, CurrentSettings.branchCount);
            CurrentSettings.sideBossCount = Mathf.Max(0, CurrentSettings.sideBossCount);

            DefeatedSideBosses = 0;
            IsFinalBossDefeated = false;
            EntranceMapId = entranceMapId ?? string.Empty;
            IsDungeonActive = true;

            if (GameManager.Instance != null)
                GameManager.Instance.SetMode(GameMode.Dungeon);

            DungeonStarted?.Invoke(EntranceMapId, CurrentSettings);
        }

        public void RegisterSideBossDefeated()
        {
            if (!IsDungeonActive) return;
            DefeatedSideBosses++;
        }

        public void NotifyFinalBossDefeated(Vector3 position)
        {
            if (!IsDungeonActive || IsFinalBossDefeated) return;
            IsFinalBossDefeated = true;
            FinalBossDefeated?.Invoke(position);
        }

        public int GetFinalBossEquipmentCount(int baseCount = 1)
        {
            return Mathf.Max(1, baseCount) + Mathf.Max(0, DefeatedSideBosses);
        }

        public void EndDungeon()
        {
            if (!IsDungeonActive) return;

            IsDungeonActive = false;
            DungeonEnded?.Invoke();
            EntranceMapId = string.Empty;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == GameMode.Dungeon)
                GameManager.Instance.SetMode(GameMode.Exploration);
        }
    }
}