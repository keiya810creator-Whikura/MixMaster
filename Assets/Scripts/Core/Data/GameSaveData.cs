using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 1;
        public string savedUtc = string.Empty;

        public string mapId = string.Empty;
        public GameMode gameMode = GameMode.Exploration;
        public string sceneName = string.Empty;
        public bool hasPlayerPosition;
        public Vector3 playerPosition;

        public long gold;
        public CharacterStats playerStats = new CharacterStats();
        public int playerLevel = 1;
        public long playerExperience;
        public long playerCurrentHp;
        public long playerCurrentMp;

        public List<OwnedMonsterRecord> monsters =
            new List<OwnedMonsterRecord>();
        public List<string> partyIds = new List<string>();

        public List<MaterialInventoryRecord> materials =
            new List<MaterialInventoryRecord>();

        public List<AltarProgressRecord> monsterAltars =
            new List<AltarProgressRecord>();
        public List<MapAltarProgressRecord> mapAltars =
            new List<MapAltarProgressRecord>();

        public List<EquipmentRecord> equipment =
            new List<EquipmentRecord>();
        public List<string> playerEquippedIds =
            new List<string>();

        public OfflineRecordProfile offlineProfile;
    }
}
