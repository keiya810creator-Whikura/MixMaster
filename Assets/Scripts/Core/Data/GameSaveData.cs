using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class GameSaveData
    {
        public const int CurrentVersion = 20;
        public DisplaySettingsData displaySettings;
        public List<PartyPresetRecord> partyPresets = new List<PartyPresetRecord>();
        // Older saves have no per-species defeat history. Start empty rather
        // than treating bred/obtained monsters as proof of a battle victory.
        public List<string> defeatedMonsterIds = new List<string>();
        public SkillSaveData skills;
        public int version = CurrentVersion;
        public GameJournalData journal;
        public string savedUtc = string.Empty;

        public string mapId = string.Empty;
        public GameMode gameMode = GameMode.Exploration;
        public string sceneName = string.Empty;
        public bool hasPlayerPosition;
        public Vector3 playerPosition;

        public long gold;
        public ConsumableSaveData consumables;
        public CharacterStats playerStats = new CharacterStats();
        public int playerLevel = 1;
        public long playerExperience;
        public bool playerAutoAttackEnabled = true;
        public long playerCurrentHp;
        public long playerCurrentMp;
        public string playerReincarnationBookId = ReincarnationRules.HumanBookId;
        public int playerSkillPoints;
        public int playerSpentSkillPoints;
        public int playerReincarnationCount;
        public List<ReincarnationBookRecord> reincarnationBooks = new List<ReincarnationBookRecord>();

        public List<OwnedMonsterRecord> monsters =
            new List<OwnedMonsterRecord>();
        public List<string> partyIds = new List<string>();
        public PartyCombatOrder partyCombatOrder = PartyCombatOrder.Individual;

        public List<MaterialInventoryRecord> materials =
            new List<MaterialInventoryRecord>();

        public List<AltarProgressRecord> monsterAltars =
            new List<AltarProgressRecord>();
        public List<MapAltarProgressRecord> mapAltars =
            new List<MapAltarProgressRecord>();

        public bool starterEquipmentGranted;
        public List<EquipmentRecord> equipment =
            new List<EquipmentRecord>();
        public List<string> playerEquippedIds =
            new List<string>();

        // Automatic selling applies to newly acquired records only.
        // Defaults are intentionally disabled for older save versions.
        public bool equipmentAutoSellEnabled;
        public int equipmentAutoSellKeepLevel = 50;

        public bool monsterAutoSellEnabled;
        public bool monsterAutoSellUntitled;
        public int monsterAutoSellIvThreshold = 90;
        public int monsterAutoSellRequiredStats = 1;

        public OfflineRecordProfile offlineProfile;
        public OfflineRecordProfile offlinePendingProfile;
        public bool hasOfflinePendingProfile;
        public string offlineCheckpointUtc = string.Empty;
    }
}
