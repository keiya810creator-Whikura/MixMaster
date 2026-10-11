using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Imported level-by-level thresholds. The index of a row is level - 1.
    /// Stored under Assets/Resources/MixMaster/ExperienceTable.asset to be
    /// available in device builds without the original CSV.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ExperienceTable",
        menuName = "MixMaster/Data/Experience Table")]
    public sealed class ExperienceTableSO : ScriptableObject
    {
        [Serializable]
        public struct ExperienceLevelRow
        {
            public int level;
            public long playerRequiredExp;
            public long monsterRequiredExp;
        }

        [SerializeField] private List<ExperienceLevelRow> rows =
            new List<ExperienceLevelRow>();

        public int RowCount => rows != null ? rows.Count : 0;

        public long GetRequiredExperience(int level, bool forMonster)
        {
            if (level < 1 || level >= ExperienceProgression.MaxLevel)
                return 0L;

            int index = level - 1;
            if (rows == null || index >= rows.Count)
                return -1L;

            ExperienceLevelRow row = rows[index];
            if (row.level != level)
                return -1L;

            return forMonster ? row.monsterRequiredExp : row.playerRequiredExp;
        }

#if UNITY_EDITOR
        public void ReplaceRows(List<ExperienceLevelRow> importedRows)
        {
            rows = importedRows != null
                ? new List<ExperienceLevelRow>(importedRows)
                : new List<ExperienceLevelRow>();
        }
#endif
    }
}
