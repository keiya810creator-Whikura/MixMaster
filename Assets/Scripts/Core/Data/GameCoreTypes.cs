using System;
using System.Collections.Generic;

namespace MixMaster.Core
{
    public enum GameMode
    {
        Exploration,
        Town,
        Dungeon
    }

    [Serializable]
    public class OwnedMonsterRecord
    {
        public string uniqueId;
        public string monsterId;

        public int level = 1;
        public long experience;

        public MonsterIndividualValues individualValues =
            new MonsterIndividualValues();

        public string titleId;

        public int skillPoints;
        public int spentSkillPoints;
        public List<string> learnedSkillIds =
            new List<string>();

        public float intimacy;

        public List<string> equippedItemUniqueIds =
            new List<string>(3);
    }

    [Serializable]
    public class AltarProgressRecord
    {
        public string mapId;
        public string monsterId;
        public long donatedMaterial;

        // Per-monster altar upgrades.
        public int spawnEfficiencyLevel;
        public int dropRateLevel;

        // Unlocked after the relevant altar upgrade reaches max.
        // 0 delay scale = immediate respawn, 1 = EnemySO base interval.
        public bool postMaxRespawnEnabled = true;
        public float postMaxRespawnDelayScale = 0f;

        // 0..1 multiplier over the unlocked maximum monster-drop rate.
        public float postMaxMonsterDropRateScale = 1f;
    }

    [Serializable]
    public class MapAltarProgressRecord
    {
        public string mapId;
        public long donatedDungeonMaterial;
        public int titledMonsterRateLevel;
    }

    [Serializable]
    public class EquipmentRecord
    {
        public string uniqueId;
        public string equipmentId;
        public int level = 1;
        public long baseValue = 1;
    }

    [Serializable]
    public class OfflineGainRecord
    {
        public string itemId;
        public long amount;
    }

    [Serializable]
    public class OfflineRecordProfile
    {
        public string mapId;
        public float recordDurationSeconds = 30f;
        public List<OfflineGainRecord> gains = new List<OfflineGainRecord>();
    }

    public static class LongMath
    {
        public static long SaturatingAdd(long a, long b)
        {
            if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
            if (b < 0 && a < long.MinValue - b) return long.MinValue;
            return a + b;
        }

        public static long SaturatingMultiply(long a, long b)
        {
            if (a == 0 || b == 0) return 0;

            if (a == -1 && b == long.MinValue) return long.MaxValue;
            if (b == -1 && a == long.MinValue) return long.MaxValue;

            if (a > 0)
            {
                if (b > 0 && a > long.MaxValue / b) return long.MaxValue;
                if (b < 0 && b < long.MinValue / a) return long.MinValue;
            }
            else
            {
                if (b > 0 && a < long.MinValue / b) return long.MinValue;
                if (b < 0 && a < long.MaxValue / b) return long.MaxValue;
            }

            return a * b;
        }
    }
}