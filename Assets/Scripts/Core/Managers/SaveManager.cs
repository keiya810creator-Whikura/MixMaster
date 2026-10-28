using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MixMaster.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Core
{
    /// <summary>
    /// Single-slot automatic game save. Uses persistentDataPath JSON,
    /// with .bak fallback. Older raw JSON stored in PlayerPrefs can be read.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveManager : MonoBehaviour
    {
        private const string SaveKey = "MixMaster.Save";
        private const string SaveFileName = "mixmaster_save_v1.json";
        private const float AutoSaveIntervalSeconds = 30f;

        private bool initialized;
        private bool starterEquipmentGranted;
        private bool hasLastLocation;
        private string lastLocationScene = string.Empty;
        private string lastLocationMapId = string.Empty;
        private Vector3 lastLocationPosition;
        private float elapsed;

        private string FilePath =>
            Path.Combine(Application.persistentDataPath, SaveFileName);

        public bool HasSaveData =>
            File.Exists(FilePath) ||
            File.Exists(FilePath + ".bak") ||
            PlayerPrefs.HasKey(SaveKey);

        public event Action Saved;
        public event Action Loaded;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            // Other manager components are created by the bootstrap before
            // Start. Restore once; no save is made over existing data first.
            bool loaded = LoadGame();
            if (!loaded && !HasSaveData)
                EnsureStarterEquipment();

            initialized = true;
            OfflineRewardManager offline = GetComponent<OfflineRewardManager>();
            if (offline != null) offline.InitializeAfterLoad();
            StartCoroutine(SpawnAtVillage());
        }

        private void Update()
        {
            if (!initialized)
                return;

            elapsed += Time.unscaledDeltaTime;

            if (elapsed < AutoSaveIntervalSeconds)
                return;

            elapsed = 0f;
            SaveGame();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!initialized) return;
            OfflineRewardManager offline = GetComponent<OfflineRewardManager>();
            if (offline != null) offline.SetApplicationPaused(paused);
            else if (paused) SaveGame();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!initialized) return;
            OfflineRewardManager offline = GetComponent<OfflineRewardManager>();
            if (offline != null) offline.SetApplicationFocused(focused);
        }

        private void OnApplicationQuit()
        {
            if (initialized)
            {
                OfflineRewardManager offline = GetComponent<OfflineRewardManager>();
                if (offline != null) offline.PrepareForQuit();
                SaveGame();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!initialized || !hasLastLocation)
                return;

            if (scene.name == lastLocationScene)
                StartCoroutine(RestorePlayerPosition());
        }

        public bool SaveGame()
        {
            if (!initialized)
                return false;

            try
            {
                GameSaveData data = Capture();
                string json = JsonUtility.ToJson(data, true);
                if (!WriteSaveFile(json))
                    return false;

                elapsed = 0f;
                Saved?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveManager] 保存に失敗しました: " + exception);
                return false;
            }
        }

        public bool LoadGame()
        {
            GameSaveData data = ReadValidSaveData();

            if (data == null)
                return false;

            try
            {
                Apply(data);
                if (initialized)
                {
                    OfflineRewardManager offline = GetComponent<OfflineRewardManager>();
                    if (offline != null) offline.InitializeAfterLoad();
                }
                Loaded?.Invoke();
                Debug.Log("[SaveManager] セーブデータをロードしました。");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveManager] 復元に失敗しました: " + exception);
                return false;
            }
        }

        private void EnsureStarterEquipment()
        {
            EquipmentManager equipment = GetComponent<EquipmentManager>();
            if (equipment == null)
                return;

            equipment.GrantStarterEquipmentIfMissing();
            starterEquipmentGranted = true;
        }

        private GameSaveData Capture()
        {
            GameSaveData data = new GameSaveData();
            data.savedUtc = DateTime.UtcNow.ToString("o");
            ConsumableManager consumables = GetComponent<ConsumableManager>();
            if (consumables != null) data.consumables = consumables.Capture();
            data.skills = GetComponent<SkillProgressionManager>()?.Capture();
            data.partyPresets = GetComponent<PartyPresetManager>()?.Capture() ?? new List<PartyPresetRecord>();
            data.defeatedMonsterIds = GetComponent<EncyclopediaManager>()?.Capture()
                ?? new List<string>();

            GameManager game = GetComponent<GameManager>();
            MapManager map = GetComponent<MapManager>();
            PlayerManager player = GetComponent<PlayerManager>();
            MonsterManager monster = GetComponent<MonsterManager>();
            MaterialInventoryManager materials =
                GetComponent<MaterialInventoryManager>();
            AltarManager altar = GetComponent<AltarManager>();
            EquipmentManager equipment = GetComponent<EquipmentManager>();
            OfflineRewardManager offline =
                GetComponent<OfflineRewardManager>();

            if (game != null)
            {
                data.gold = game.Gold;
                data.gameMode = game.CurrentMode;
                data.mapId = game.CurrentMapId;
            }

            if (map != null && !string.IsNullOrWhiteSpace(map.CurrentMapId))
                data.mapId = map.CurrentMapId;

            if (player != null)
            {
                data.playerStats = player.BaseStats;
                data.playerLevel = player.Level;
                data.playerExperience = player.Experience;
                data.playerAutoAttackEnabled = player.AutoAttackEnabled;
                data.playerCurrentHp = player.CurrentHp;
                data.playerCurrentMp = player.CurrentMp;
                data.playerReincarnationBookId = player.ReincarnationBookId;
                data.playerSkillPoints = player.SkillPoints;
                data.playerSpentSkillPoints = player.SpentSkillPoints;
                data.playerReincarnationCount = player.ReincarnationCount;
            }

            ReincarnationManager reincarnation = GetComponent<ReincarnationManager>();
            if (reincarnation != null)
                data.reincarnationBooks = new List<ReincarnationBookRecord>(reincarnation.Books);

            if (monster != null)
            {
                data.monsters = new List<OwnedMonsterRecord>(
                    monster.OwnedMonsters);
                data.partyIds = new List<string>(
                    monster.PartyMonsterUniqueIds);
                data.partyCombatOrder = monster.CurrentPartyCombatOrder;
                data.monsterAutoSellEnabled = monster.AutoSellEnabled;
                data.monsterAutoSellUntitled = monster.AutoSellUntitled;
                data.monsterAutoSellIvThreshold = monster.AutoSellIvThreshold;
                data.monsterAutoSellRequiredStats = monster.AutoSellRequiredStats;
            }

            if (materials != null)
                data.materials = new List<MaterialInventoryRecord>(
                    materials.Records);

            if (altar != null)
            {
                data.monsterAltars = new List<AltarProgressRecord>(
                    altar.MonsterRecords);
                data.mapAltars = new List<MapAltarProgressRecord>(
                    altar.MapRecords);
            }

            if (equipment != null)
            {
                data.starterEquipmentGranted = starterEquipmentGranted;
                data.equipment = new List<EquipmentRecord>(
                    equipment.Inventory);
                data.playerEquippedIds = new List<string>(
                    equipment.PlayerEquipmentUniqueIds);
                data.equipmentAutoSellEnabled = equipment.AutoSellEnabled;
                data.equipmentAutoSellKeepLevel = equipment.AutoSellKeepLevel;
            }

            if (offline != null)
            {
                data.offlineProfile = OfflineRewardRules.CopyProfile(offline.LastCompletedProfile);
                data.offlinePendingProfile = OfflineRewardRules.CopyProfile(offline.PendingProfile);
                data.offlineCheckpointUtc = offline.CheckpointUtcForSave;
            }

            PlayerController playerController =
                FindFirstObjectByType<PlayerController>();

            if (playerController != null)
            {
                lastLocationScene = playerController.gameObject.scene.name;
                lastLocationMapId = data.mapId;
                lastLocationPosition = playerController.transform.position;

                // Dungeon rooms are transient and not serialized. When
                // saving inside one, keep the safe entrance-field position
                // instead of a position in a map that won't exist on reload.
                MixMaster.World.DungeonMapGenerator dungeonMap =
                    GetComponent<MixMaster.World.DungeonMapGenerator>();
                if (dungeonMap != null && dungeonMap.IsGenerated)
                {
                    lastLocationPosition = dungeonMap.EntranceWorldPosition;
                    if (!string.IsNullOrWhiteSpace(dungeonMap.EntranceMapId))
                        lastLocationMapId = dungeonMap.EntranceMapId;
                }
                hasLastLocation = true;
            }

            if (hasLastLocation)
            {
                data.sceneName = lastLocationScene;
                data.playerPosition = lastLocationPosition;
                data.hasPlayerPosition = true;
            }

            return data;
        }

        private void Apply(GameSaveData data)
        {
            GameManager game = GetComponent<GameManager>();
            MapManager map = GetComponent<MapManager>();
            PlayerManager player = GetComponent<PlayerManager>();
            MonsterManager monster = GetComponent<MonsterManager>();
            MaterialInventoryManager materials =
                GetComponent<MaterialInventoryManager>();
            AltarManager altar = GetComponent<AltarManager>();
            EquipmentManager equipment = GetComponent<EquipmentManager>();
            OfflineRewardManager offline =
                GetComponent<OfflineRewardManager>();

            if (equipment != null)
                equipment.RestoreEquipment(
                    data.equipment, data.playerEquippedIds);

            starterEquipmentGranted =
                data.version >= 3 && data.starterEquipmentGranted;
            if (!starterEquipmentGranted)
                EnsureStarterEquipment();

            if (materials != null)
                materials.RestoreMaterials(data.materials);

            if (altar != null)
                altar.RestoreAltars(
                    data.monsterAltars, data.mapAltars);

            if (game != null)
            {
                game.RestoreGold(data.gold);
                game.SetCurrentMap(data.mapId);
                // In-progress dungeon runtime (room graph / enemies) is
                // transient; do not claim to resume it without that state.
                game.SetMode(data.gameMode == GameMode.Dungeon
                    ? GameMode.Exploration
                    : data.gameMode);
            }

            if (map != null)
                map.RestoreMapId(data.mapId);

            if (player != null)
                player.RestoreReincarnationProgress(
                    data.version >= 10 ? data.playerReincarnationBookId : ReincarnationRules.HumanBookId,
                    data.version >= 10 ? data.playerSkillPoints : Math.Max(0, data.playerLevel - 1),
                    data.version >= 10 ? data.playerSpentSkillPoints : 0,
                    data.version >= 10 ? data.playerReincarnationCount : 0);

            if (player != null && data.playerStats != null)
                player.RestorePlayerState(
                    data.playerStats,
                    data.playerCurrentHp,
                    data.playerCurrentMp,
                    data.playerLevel,
                    data.playerExperience,
                    data.version < 2 || data.playerAutoAttackEnabled);

            if (offline != null)
                offline.RestoreProgress(data.offlineProfile,
                    data.version >= 11 ? data.offlinePendingProfile : null,
                    data.version >= 11 && !string.IsNullOrWhiteSpace(data.offlineCheckpointUtc)
                        ? data.offlineCheckpointUtc : data.savedUtc);

            if (monster != null)
            {
                monster.RestorePartyCombatOrder(
                    data.version >= 4
                        ? data.partyCombatOrder
                        : PartyCombatOrder.Individual);
                monster.RestoreMonsters(data.monsters, data.partyIds);
            }

            ReincarnationManager reincarnation = GetComponent<ReincarnationManager>();
            if (reincarnation != null)
                reincarnation.RestoreBooks(data.reincarnationBooks);

            // Equipment and monsters must both be loaded before ensuring
            // that one item is never assigned to two characters.
            if (equipment != null)
                equipment.NormalizeMonsterEquipment();

            // Restore rules after the inventory and guaranteed starter gear.
            // This prevents loading a save from auto-selling older records.
            if (equipment != null)
                equipment.SetAutoSellSettings(
                    data.version >= 7 && data.equipmentAutoSellEnabled,
                    data.version >= 7 ? data.equipmentAutoSellKeepLevel : 50);
            if (monster != null)
                monster.SetAutoSellSettings(
                    data.version >= 7 && data.monsterAutoSellEnabled,
                    data.version >= 7 && data.monsterAutoSellUntitled,
                    data.version >= 7 ? data.monsterAutoSellIvThreshold : 90,
                    data.version >= 7 ? data.monsterAutoSellRequiredStats : 1);

            ConsumableManager consumables = GetComponent<ConsumableManager>();
            if (consumables != null) consumables.Restore(data.consumables);
            GetComponent<SkillProgressionManager>()?.Restore(data.skills);
            GetComponent<PartyPresetManager>()?.Restore(data.partyPresets);
            GetComponent<EncyclopediaManager>()?.Restore(data.defeatedMonsterIds);

            // Saved coordinates are retained in the file for compatibility,
            // but a new application session always starts in the village.
            hasLastLocation = false;
        }

        private IEnumerator SpawnAtVillage()
        {
            // Let scene objects and their map/player setup finish first.
            yield return null;
            yield return null;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                MixMaster.World.VillageSpawnPoint spawn =
                    FindFirstObjectByType<MixMaster.World.VillageSpawnPoint>(FindObjectsInactive.Include);
                if (spawn != null && spawn.TryRespawnPlayer()) yield break;
                yield return null;
            }
            Debug.LogWarning("[SaveManager] 村のVillageSpawnPointまたはプレイヤーが見つからず、起動位置を設定できませんでした。", this);
        }

        private IEnumerator RestorePlayerPosition()
        {
            // Wait for map/player spawn setup to finish.
            yield return null;
            yield return null;

            if (!hasLastLocation ||
                SceneManager.GetActiveScene().name != lastLocationScene)
                yield break;

            // A generic exploration scene may be reused by multiple maps.
            // Do not apply coordinates belonging to another map.
            MapManager map = GetComponent<MapManager>();

            if (map != null &&
                !string.IsNullOrWhiteSpace(lastLocationMapId) &&
                !string.IsNullOrWhiteSpace(map.CurrentMapId) &&
                map.CurrentMapId != lastLocationMapId)
            {
                yield break;
            }

            PlayerController player =
                FindFirstObjectByType<PlayerController>();

            if (player == null)
                yield break;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = lastLocationPosition;
                body.linearVelocity = Vector2.zero;
            }

            player.transform.position = lastLocationPosition;
            PlayerTrailRecorder trail =
                player.GetComponent<PlayerTrailRecorder>();

            if (trail != null)
                trail.ResetTrail();

            MixMaster.Monsters.PartyFieldSpawner spawner =
                GetComponent<MixMaster.Monsters.PartyFieldSpawner>();

            if (spawner != null)
                spawner.RefreshParty();
        }

        // Compatibility with the original SaveManager JSON API.
        public void SaveJson(string json)
        {
            WriteSaveFile(json ?? string.Empty);
        }

        public string LoadJson()
        {
            string json = TryReadFile(FilePath);
            if (!string.IsNullOrWhiteSpace(json))
                return json;

            json = TryReadFile(FilePath + ".bak");
            if (!string.IsNullOrWhiteSpace(json))
                return json;

            return PlayerPrefs.GetString(SaveKey, string.Empty);
        }

        public void DeleteSave()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);

                if (File.Exists(FilePath + ".bak"))
                    File.Delete(FilePath + ".bak");

                if (File.Exists(FilePath + ".tmp"))
                    File.Delete(FilePath + ".tmp");

                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
                Debug.Log("[SaveManager] セーブファイルを削除しました。");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveManager] セーブ削除に失敗しました: " + exception);
            }
        }

        private GameSaveData ReadValidSaveData()
        {
            string[] candidates =
            {
                TryReadFile(FilePath),
                TryReadFile(FilePath + ".bak"),
                PlayerPrefs.GetString(SaveKey, string.Empty)
            };

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                try
                {
                    GameSaveData parsed =
                        JsonUtility.FromJson<GameSaveData>(candidate);

                    if (parsed != null &&
                        parsed.version >= 1 &&
                        parsed.version <= GameSaveData.CurrentVersion &&
                        candidate.Contains("\"savedUtc\""))
                        return parsed;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[SaveManager] セーブを読み込めません。バックアップを試します: " +
                        ex.Message);
                }
            }

            return null;
        }

        private bool WriteSaveFile(string json)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);

                if (File.Exists(FilePath))
                    File.Copy(FilePath, FilePath + ".bak", true);

                File.Copy(temp, FilePath, true);
                File.Delete(temp);

                // Obsolete PlayerPrefs data should not shadow the file.
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    PlayerPrefs.DeleteKey(SaveKey);
                    PlayerPrefs.Save();
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SaveManager] JSONの書き込みに失敗しました: " + exception);
                return false;
            }
        }

        private static string TryReadFile(string path)
        {
            try
            {
                return File.Exists(path)
                    ? File.ReadAllText(path)
                    : string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SaveManager] ファイル読み込み失敗: " +
                    exception.Message);
                return string.Empty;
            }
        }
    }
}
