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

    // Party-wide tactical override. "Individual" keeps each monster's saved
    // behavior; other entries temporarily supersede it without modifying it.
    public enum PartyCombatOrder
    {
        Individual = 0,
        HostileEnemies = 1,
        PlayerAttackRange = 2,
        AutoHunt = 3,
        NoAttack = 4,
        OwnAttackRange = 5
    }

    // Serialized on each owned monster. Explicit numeric values preserve old saves.
    public enum MonsterCombatBehavior
    {
        PlayerAttackRange = 0,
        AutoHunt = 1,
        HostileEnemies = 2,
        NoAttack = 3,
        OwnAttackRange = 4
    }

    [Serializable]
    public class OwnedMonsterRecord
    {
        public string uniqueId;
        public string monsterId;

        public int level = 1;
        public long experience;
        public MonsterCombatBehavior combatBehavior = MonsterCombatBehavior.PlayerAttackRange;
        // An absent field in older saves keeps the previously visible circle.
        public bool hideAttackRange;

        public MonsterIndividualValues individualValues =
            new MonsterIndividualValues();

        public string titleId;

        public int skillPoints;
        public int spentSkillPoints;
        public List<string> learnedSkillIds =
            new List<string>();

        // Legacy float is kept so older save data can still be loaded.
        public float intimacy;

        // Personal kill progression: 0 kills = intimacy Lv.1.
        // The current level is derived from the accumulated kills.
        public int intimacyKills;

        // Saved per individual. Both prevent selling and future breeding
        // consumption until explicitly turned off.
        public bool isFavorite;
        public bool isLocked;

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

        // Dungeon-only progression. Saved independently per MapSO.mapId.
        // Missing fields in pre-upgrade JSON deserialize to Lv0.
        public int midBossEquipmentBonusLevel;
        public int normalEnemyCountLevel;
        public int dungeonMaterialRewardLevel;
        public int midBossMaterialRewardLevel;
        public int roomReductionLevel;
        public int noBranchesLevel;
        public int minimumEquipmentLevelBonusLevel;

        // Runtime-applied dungeon levels, separate from purchased levels.
        // Old save files have configured=false and use purchased levels.
        public bool dungeonAppliedLevelsConfigured;
        public int appliedMidBossEquipmentLevel;
        public int appliedNormalEnemyCountLevel;
        public int appliedDungeonMaterialRewardLevel;
        public int appliedMidBossMaterialRewardLevel;
        public int appliedRoomReductionLevel;
        public int appliedNoBranchesLevel;
        public int appliedMinimumEquipmentLevelBonusLevel;
    }

    [Serializable]
    public class EquipmentRecord
    {
        public string uniqueId;
        public string equipmentId;
        public int level = 1;
        // level remains the effective level used by existing combat and UI.
        // Missing fields in old saves mean an unenhanced item at its saved Lv.
        public int originalLevel;
        public int enhancementCount;
        public string sourceMapId;
        public long baseValue = 1;
        public bool isFavorite;
        public bool isLocked;
    }

    [Serializable]
    public class OfflineGainRecord
    {
        public OfflineGainKind kind;
        public string itemId;
        public long amount;
    }

    [Serializable]
    public class OfflineRecordProfile
    {
        public string mapId;
        public float recordDurationSeconds = 180f;
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
