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
        [HideInInspector] public bool forceLinearPath;
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
                seed = source.seed,
                forceLinearPath = false
            };
        }

        public DungeonGenerationSettings CurrentSettings { get; private set; }
        public int DefeatedSideBosses { get; private set; }
        public bool IsDungeonActive { get; private set; }
        public string EntranceMapId { get; private set; } = string.Empty;

        private readonly int[] appliedDungeonUpgrades = new int[7];

        public int GetAppliedDungeonUpgradeLevel(DungeonUpgradeType type)
        {
            int index = (int)type;
            return IsDungeonActive && index >= 0 &&
                index < appliedDungeonUpgrades.Length
                ? appliedDungeonUpgrades[index] : 0;
        }
        public bool IsFinalBossDefeated { get; private set; }

        // Chest/result system will subscribe to this in the next phase.
        public event Action<Vector3> FinalBossDefeated;

        // Dungeon map generation will subscribe to this event next.
        public event Action<string, DungeonGenerationSettings> DungeonStarted;
        public event Action DungeonEnded;

        public void BeginDungeon(DungeonGenerationSettings settings,
            string entranceMapId = null)
        {
            DungeonGenerationSettings requested =
                settings ?? GetDefaultSettings();
            // Never mutate the authored Inspector template.
            CurrentSettings = new DungeonGenerationSettings
            {
                roomCount = requested.roomCount,
                branchCount = requested.branchCount,
                sideBossCount = requested.sideBossCount,
                seed = requested.seed
            };

            EntranceMapId = entranceMapId ?? string.Empty;
            Array.Clear(appliedDungeonUpgrades, 0,
                appliedDungeonUpgrades.Length);

            AltarManager altar = GetComponent<AltarManager>() ??
                FindFirstObjectByType<AltarManager>();
            if (altar != null && !string.IsNullOrWhiteSpace(EntranceMapId))
                foreach (DungeonUpgradeType type in DungeonUpgradeBalance.All)
                    appliedDungeonUpgrades[(int)type] =
                        altar.GetDungeonUpgradeLevel(EntranceMapId, type);

            CurrentSettings.roomCount = Mathf.Max(2,
                CurrentSettings.roomCount - GetRawAppliedLevel(
                    DungeonUpgradeType.FewerRooms));
            if (GetRawAppliedLevel(DungeonUpgradeType.NoBranches) > 0)
            {
                CurrentSettings.forceLinearPath = true;
                CurrentSettings.branchCount = 0;
            }

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

        private int GetRawAppliedLevel(DungeonUpgradeType type)
        {
            return appliedDungeonUpgrades[(int)type];
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
            Array.Clear(appliedDungeonUpgrades, 0,
                appliedDungeonUpgrades.Length);
            EntranceMapId = string.Empty;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentMode == GameMode.Dungeon)
                GameManager.Instance.SetMode(GameMode.Exploration);
        }
    }
}