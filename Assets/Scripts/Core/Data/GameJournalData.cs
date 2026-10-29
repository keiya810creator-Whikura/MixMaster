using System;
using System.Collections.Generic;

namespace MixMaster.Core
{
    public enum GoalKind { Monster, Equipment, Material }

    [Serializable]
    public sealed class JournalMonster
    {
        public string monsterId, name, title;
        public int level;
    }

    [Serializable]
    public sealed class BreedingHistoryRecord
    {
        public string utc;
        public bool succeeded, specialRecipe, parentsProtected;
        public int successPercent;
        public JournalMonster parentA, parentB, child;
        public List<string> inheritedSkills = new List<string>();
    }

    [Serializable]
    public sealed class GoalRecord
    {
        public string goalId, definitionId, name;
        public GoalKind kind;
        public long targetCount = 1;
        public bool showOnHud;
    }

    [Serializable]
    public sealed class GameJournalData
    {
        public string startedUtc;
        public double playSeconds;
        public long defeatedEnemies, maximumDamage, breedingAttempts, breedingSuccesses, breedingFailures;
        public List<BreedingHistoryRecord> history = new List<BreedingHistoryRecord>();
        public List<GoalRecord> goals = new List<GoalRecord>();
    }
}
