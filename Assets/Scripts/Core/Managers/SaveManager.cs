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
        private bool hasLastLocation;
        private string lastLocationScene = string.Empty;
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
            LoadGame();
            initialized = true;
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
            if (paused && initialized)
                SaveGame();
        }

        private void OnApplicationQuit()
        {
            if (initialized)
                SaveGame();
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

        private GameSaveData Capture()
        {
            GameSaveData data = new GameSaveData();
            data.savedUtc = DateTime.UtcNow.ToString("o");

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
                data.playerStats = player.Stats;
                data.playerCurrentHp = player.CurrentHp;
                data.playerCurrentMp = player.CurrentMp;
            }

            if (monster != null)
            {
                data.monsters = new List<OwnedMonsterRecord>(
                    monster.OwnedMonsters);
                data.partyIds = new List<string>(
                    monster.PartyMonsterUniqueIds);
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
                data.equipment = new List<EquipmentRecord>(
                    equipment.Inventory);
                data.playerEquippedIds = new List<string>(
                    equipment.PlayerEquipmentUniqueIds);
            }

            if (offline != null)
                data.offlineProfile = offline.LastCompletedProfile;

            PlayerController playerController =
                FindFirstObjectByType<PlayerController>();

            if (playerController != null)
            {
                lastLocationScene = playerController.gameObject.scene.name;
                lastLocationPosition = playerController.transform.position;
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

            if (materials != null)
                materials.RestoreMaterials(data.materials);

            if (altar != null)
                altar.RestoreAltars(
                    data.monsterAltars, data.mapAltars);

            if (game != null)
            {
                game.RestoreGold(data.gold);
                game.SetCurrentMap(data.mapId);
                game.SetMode(data.gameMode);
            }

            if (map != null)
                map.RestoreMapId(data.mapId);

            if (player != null && data.playerStats != null)
                player.RestorePlayerState(
                    data.playerStats,
                    data.playerCurrentHp,
                    data.playerCurrentMp);

            if (offline != null)
                offline.RestoreLastCompletedProfile(data.offlineProfile);

            if (monster != null)
                monster.RestoreMonsters(data.monsters, data.partyIds);

            hasLastLocation = data.hasPlayerPosition &&
                !string.IsNullOrWhiteSpace(data.sceneName);

            if (hasLastLocation)
            {
                lastLocationScene = data.sceneName;
                lastLocationPosition = data.playerPosition;

                if (SceneManager.GetActiveScene().name == lastLocationScene)
                    StartCoroutine(RestorePlayerPosition());
            }
        }

        private IEnumerator RestorePlayerPosition()
        {
            // Wait for map/player spawn setup to finish.
            yield return null;
            yield return null;

            if (!hasLastLocation ||
                SceneManager.GetActiveScene().name != lastLocationScene)
                yield break;

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

                    if (parsed != null && parsed.version == 1 &&
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
