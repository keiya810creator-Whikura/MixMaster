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
        public DungeonGenerationSettings CurrentSettings { get; private set; }
        public int DefeatedSideBosses { get; private set; }
        public bool IsDungeonActive { get; private set; }

        public void BeginDungeon(DungeonGenerationSettings settings)
        {
            CurrentSettings = settings ?? new DungeonGenerationSettings();

            if (CurrentSettings.seed == 0)
                CurrentSettings.seed = UnityEngine.Random.Range(1, int.MaxValue);

            CurrentSettings.roomCount = Mathf.Max(2, CurrentSettings.roomCount);
            CurrentSettings.branchCount = Mathf.Max(0, CurrentSettings.branchCount);
            CurrentSettings.sideBossCount = Mathf.Max(0, CurrentSettings.sideBossCount);

            DefeatedSideBosses = 0;
            IsDungeonActive = true;

            if (GameManager.Instance != null)
                GameManager.Instance.SetMode(GameMode.Dungeon);
        }

        public void RegisterSideBossDefeated()
        {
            if (!IsDungeonActive) return;
            DefeatedSideBosses++;
        }

        public int GetFinalBossEquipmentCount(int baseCount = 1)
        {
            return Mathf.Max(1, baseCount) + Mathf.Max(0, DefeatedSideBosses);
        }

        public void EndDungeon()
        {
            IsDungeonActive = false;
        }
    }
}