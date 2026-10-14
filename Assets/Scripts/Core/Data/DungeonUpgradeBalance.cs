using UnityEngine;

namespace MixMaster.Core
{
    // Stable IDs: serialized levels live in MapAltarProgressRecord.
    public enum DungeonUpgradeType
    {
        MidBossEquipment = 0,
        NormalEnemyCount = 1,
        ClearMaterial = 2,
        MidBossMaterial = 3,
        FewerRooms = 4,
        NoBranches = 5,
        MinimumEquipmentLevel = 6
    }

    public static class DungeonUpgradeBalance
    {
        public static readonly DungeonUpgradeType[] All =
        {
            DungeonUpgradeType.MidBossEquipment,
            DungeonUpgradeType.NormalEnemyCount,
            DungeonUpgradeType.ClearMaterial,
            DungeonUpgradeType.MidBossMaterial,
            DungeonUpgradeType.FewerRooms,
            DungeonUpgradeType.NoBranches,
            DungeonUpgradeType.MinimumEquipmentLevel
        };

        public static string Name(DungeonUpgradeType type)
        {
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment: return "中ボス撃破装備ボーナス";
                case DungeonUpgradeType.NormalEnemyCount: return "雑魚敵の出現数";
                case DungeonUpgradeType.ClearMaterial: return "クリア素材ボーナス";
                case DungeonUpgradeType.MidBossMaterial: return "中ボス追加素材";
                case DungeonUpgradeType.FewerRooms: return "部屋数削減";
                case DungeonUpgradeType.NoBranches: return "一本道化";
                case DungeonUpgradeType.MinimumEquipmentLevel: return "装備の最低レベル";
                default: return "ダンジョン強化";
            }
        }

        public static int MaxLevel(DungeonUpgradeType type)
        {
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment: return 5;
                case DungeonUpgradeType.NormalEnemyCount: return 50;
                case DungeonUpgradeType.ClearMaterial: return 1000;
                case DungeonUpgradeType.MidBossMaterial: return 1000;
                case DungeonUpgradeType.FewerRooms: return 5;
                case DungeonUpgradeType.NoBranches: return 1;
                case DungeonUpgradeType.MinimumEquipmentLevel: return 20;
                default: return 0;
            }
        }

        public static int Level(MapAltarProgressRecord record,
            DungeonUpgradeType type)
        {
            if (record == null) return 0;
            int value;
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment:
                    value = record.midBossEquipmentBonusLevel; break;
                case DungeonUpgradeType.NormalEnemyCount:
                    value = record.normalEnemyCountLevel; break;
                case DungeonUpgradeType.ClearMaterial:
                    value = record.dungeonMaterialRewardLevel; break;
                case DungeonUpgradeType.MidBossMaterial:
                    value = record.midBossMaterialRewardLevel; break;
                case DungeonUpgradeType.FewerRooms:
                    value = record.roomReductionLevel; break;
                case DungeonUpgradeType.NoBranches:
                    value = record.noBranchesLevel; break;
                case DungeonUpgradeType.MinimumEquipmentLevel:
                    value = record.minimumEquipmentLevelBonusLevel; break;
                default: return 0;
            }
            return Mathf.Clamp(value, 0, MaxLevel(type));
        }

        public static void SetLevel(MapAltarProgressRecord record,
            DungeonUpgradeType type, int newLevel)
        {
            if (record == null) return;
            int level = Mathf.Clamp(newLevel, 0, MaxLevel(type));
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment:
                    record.midBossEquipmentBonusLevel = level; break;
                case DungeonUpgradeType.NormalEnemyCount:
                    record.normalEnemyCountLevel = level; break;
                case DungeonUpgradeType.ClearMaterial:
                    record.dungeonMaterialRewardLevel = level; break;
                case DungeonUpgradeType.MidBossMaterial:
                    record.midBossMaterialRewardLevel = level; break;
                case DungeonUpgradeType.FewerRooms:
                    record.roomReductionLevel = level; break;
                case DungeonUpgradeType.NoBranches:
                    record.noBranchesLevel = level; break;
                case DungeonUpgradeType.MinimumEquipmentLevel:
                    record.minimumEquipmentLevelBonusLevel = level; break;
            }
        }

        public static int AppliedLevel(MapAltarProgressRecord record,
            DungeonUpgradeType type)
        {
            if (record == null) return 0;
            int purchased = Level(record, type);
            if (!record.dungeonAppliedLevelsConfigured) return purchased;

            int value;
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment:
                    value = record.appliedMidBossEquipmentLevel; break;
                case DungeonUpgradeType.NormalEnemyCount:
                    value = record.appliedNormalEnemyCountLevel; break;
                case DungeonUpgradeType.ClearMaterial:
                    value = record.appliedDungeonMaterialRewardLevel; break;
                case DungeonUpgradeType.MidBossMaterial:
                    value = record.appliedMidBossMaterialRewardLevel; break;
                case DungeonUpgradeType.FewerRooms:
                    value = record.appliedRoomReductionLevel; break;
                case DungeonUpgradeType.NoBranches:
                    value = record.appliedNoBranchesLevel; break;
                case DungeonUpgradeType.MinimumEquipmentLevel:
                    value = record.appliedMinimumEquipmentLevelBonusLevel; break;
                default: return 0;
            }
            return Mathf.Clamp(value, 0, purchased);
        }

        public static void SetAppliedLevel(MapAltarProgressRecord record,
            DungeonUpgradeType type, int value)
        {
            if (record == null) return;

            // Before the first explicit adjustment, all equipped values
            // match the already purchased levels. Migrate in memory once.
            if (!record.dungeonAppliedLevelsConfigured)
            {
                foreach (DungeonUpgradeType item in All)
                    WriteAppliedLevel(record, item, Level(record, item));
                record.dungeonAppliedLevelsConfigured = true;
            }
            WriteAppliedLevel(record, type,
                Mathf.Clamp(value, 0, Level(record, type)));
        }

        private static void WriteAppliedLevel(MapAltarProgressRecord record,
            DungeonUpgradeType type, int value)
        {
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment:
                    record.appliedMidBossEquipmentLevel = value; break;
                case DungeonUpgradeType.NormalEnemyCount:
                    record.appliedNormalEnemyCountLevel = value; break;
                case DungeonUpgradeType.ClearMaterial:
                    record.appliedDungeonMaterialRewardLevel = value; break;
                case DungeonUpgradeType.MidBossMaterial:
                    record.appliedMidBossMaterialRewardLevel = value; break;
                case DungeonUpgradeType.FewerRooms:
                    record.appliedRoomReductionLevel = value; break;
                case DungeonUpgradeType.NoBranches:
                    record.appliedNoBranchesLevel = value; break;
                case DungeonUpgradeType.MinimumEquipmentLevel:
                    record.appliedMinimumEquipmentLevelBonusLevel = value; break;
            }
        }

        /// <summary>
        /// Cost of one upgrade, charged in the current MapSO's dungeon
        /// material only. High-level rewards are balanced by rising costs.
        /// All arithmetic is bounded well within a long.
        /// </summary>
        public static long Cost(DungeonUpgradeType type, int currentLevel)
        {
            int level = Mathf.Clamp(currentLevel, 0, MaxLevel(type));
            long next = level + 1L;
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment:
                    return 120L * next * next;
                case DungeonUpgradeType.NormalEnemyCount:
                    return 30L + 12L * next;
                case DungeonUpgradeType.ClearMaterial:
                    return 25L + 5L * next;
                case DungeonUpgradeType.MidBossMaterial:
                    return 25L + 5L * next;
                case DungeonUpgradeType.FewerRooms:
                    return 150L * next * next;
                case DungeonUpgradeType.NoBranches:
                    return 1000L;
                case DungeonUpgradeType.MinimumEquipmentLevel:
                    return 60L * next;
                default: return long.MaxValue;
            }
        }

        public static string EffectText(DungeonUpgradeType type, int level)
        {
            level = Mathf.Clamp(level, 0, MaxLevel(type));
            switch (type)
            {
                case DungeonUpgradeType.MidBossEquipment:
                    return "中ボス1体につき装備+" + level;
                case DungeonUpgradeType.NormalEnemyCount:
                    return "雑魚敵の数+" + (10 * level) + "%";
                case DungeonUpgradeType.ClearMaterial:
                    return "クリア素材+" + (10 * level);
                case DungeonUpgradeType.MidBossMaterial:
                    return "中ボス1体につき素材+" + (2 * level);
                case DungeonUpgradeType.FewerRooms:
                    return "部屋数-" + level;
                case DungeonUpgradeType.NoBranches:
                    return level > 0 ? "分かれ道なし（一本道）" : "通常の分岐マップ";
                case DungeonUpgradeType.MinimumEquipmentLevel:
                    return "装備の最低Lv+" + level;
                default: return "";
            }
        }
    }
}
