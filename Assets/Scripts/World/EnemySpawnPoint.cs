using System.Collections.Generic;
using UnityEngine;
using MixMaster.Combat;
using MixMaster.Monsters;
using MixMaster.Core;
using UnityEngine.SceneManagement;

namespace MixMaster.World
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [Header("MonsterSO")]
        [SerializeField] private MonsterSO monsterDefinition;
        [SerializeField, Min(1)] private int monsterLevel = 1;

        [Header("Legacy / Fallback Enemy")]
        [Tooltip("Used only when MonsterSO has no Enemy Prefab, or when MonsterSO is not assigned.")]
        [SerializeField] private GameObject enemyPrefab;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnInterval = 5f;
        [SerializeField, Min(1)] private int maxAlive = 5;
        [SerializeField] private bool spawnImmediately = true;
        [SerializeField, Min(0f)] private float randomSpawnRadius = 1f;

        [Header("Runtime / Altar Hook")]
        [Tooltip("1 = normal, 0.5 = twice as fast, 0 = instant respawn. Later the altar system can control this value.")]
        [SerializeField, Min(0f)] private float spawnIntervalMultiplier = 1f;

        private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
        private float nextSpawnTime;

        private AltarManager altarManager;
        private SpawnManager spawnManager;
        private MapManager mapManager;

        public MonsterSO MonsterDefinition => monsterDefinition;
        public int MonsterLevel => monsterLevel;
        public float BaseSpawnInterval => GetBaseSpawnInterval();
        public float SpawnIntervalMultiplier => spawnIntervalMultiplier;
        public int AliveCount => aliveEnemies.Count;

        private void OnEnable()
        {
            CacheManagers();

            if (altarManager != null)
                altarManager.AltarProgressChanged += HandleAltarProgressChanged;

            // Re-entering a map should start a fresh spawn cycle rather than
            // keeping a timer from the last visit to this inactive map.
            if (Application.isPlaying)
                nextSpawnTime = spawnImmediately
                    ? Time.time
                    : Time.time + GetEffectiveSpawnInterval();
        }

        private void Start()
        {
            nextSpawnTime = spawnImmediately
                ? Time.time
                : Time.time + GetEffectiveSpawnInterval();
        }

        private void OnDisable()
        {
            if (altarManager != null)
                altarManager.AltarProgressChanged -= HandleAltarProgressChanged;

            // Spawned enemies live outside the TilemapMapRoot hierarchy.
            // Clean them up on leaving that map, otherwise they would remain
            // visible and hostile while the player is in a different map.
            // Only applies to spawners owned by a TilemapMapRoot.
            if (!Application.isPlaying ||
                GetComponentInParent<TilemapMapRoot>(true) == null)
                return;

            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                EnemyHealth enemy = aliveEnemies[i];
                if (enemy == null)
                    continue;

                enemy.Died -= HandleEnemyDied;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            aliveEnemies.Clear();
        }

        private void Update()
        {
            aliveEnemies.RemoveAll(x => x == null || !x.IsAlive);

            // One cooldown applies to the whole spawn batch. Fill every
            // vacant Alive slot at once instead of only spawning one enemy
            // each time the cooldown expires.
            if (aliveEnemies.Count >= maxAlive || Time.time < nextSpawnTime)
                return;

            GameObject prefab = ResolveEnemyPrefab();
            if (prefab == null)
                return;

            int missingCount = maxAlive - aliveEnemies.Count;
            for (int i = 0; i < missingCount; i++)
                SpawnEnemyInternal(prefab);

            // Do the altar/respawn-delay lookup once per batch, not per enemy.
            nextSpawnTime = Time.time + GetEffectiveSpawnInterval();
        }

        public void SetEnemyPrefab(GameObject prefab)
        {
            enemyPrefab = prefab;
        }

        public void SetSpawnIntervalMultiplier(float multiplier)
        {
            spawnIntervalMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetMonster(
            MonsterSO definition,
            int level = 1)
        {
            monsterDefinition = definition;
            monsterLevel = Mathf.Max(1, level);

            nextSpawnTime =
                Time.time + GetEffectiveSpawnInterval();
        }

        public void SpawnEnemy()
        {
            GameObject prefab = ResolveEnemyPrefab();

            if (prefab == null ||
                aliveEnemies.Count >= maxAlive)
            {
                return;
            }

            SpawnEnemyInternal(prefab);
            nextSpawnTime = Time.time + GetEffectiveSpawnInterval();
        }

        private void SpawnEnemyInternal(GameObject prefab)
        {
            Vector2 offset = randomSpawnRadius > 0f
                ? Random.insideUnitCircle * randomSpawnRadius
                : Vector2.zero;

            Vector3 spawnPosition =
                transform.position + (Vector3)offset;

            GameObject enemyObject =
                Instantiate(
                    prefab,
                    spawnPosition,
                    Quaternion.identity);

            MonsterRuntimeSetup runtimeSetup =
                enemyObject.GetComponent<MonsterRuntimeSetup>();

            if (monsterDefinition != null)
            {
                if (runtimeSetup == null)
                {
                    runtimeSetup =
                        enemyObject.AddComponent<MonsterRuntimeSetup>();
                }

                runtimeSetup.Initialize(
                    monsterDefinition,
                    monsterLevel);
            }

            EnemyHealth health =
                enemyObject.GetComponent<EnemyHealth>();

            if (health == null)
                health = enemyObject.AddComponent<EnemyHealth>();

            EnemyWanderAI wander =
                enemyObject.GetComponent<EnemyWanderAI>();

            if (wander != null)
                wander.SetHome(transform.position);

            aliveEnemies.Add(health);
            health.Died += HandleEnemyDied;
        }

        private GameObject ResolveEnemyPrefab()
        {
            if (monsterDefinition != null &&
                monsterDefinition.enemyPrefab != null)
            {
                return monsterDefinition.enemyPrefab;
            }

            return enemyPrefab;
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (enemy != null)
                enemy.Died -= HandleEnemyDied;

            aliveEnemies.Remove(enemy);
            nextSpawnTime = Time.time + GetEffectiveSpawnInterval();
        }

        private float GetBaseSpawnInterval()
        {
            if (monsterDefinition != null)
            {
                return Mathf.Max(
                    0f,
                    monsterDefinition.baseRespawnInterval);
            }

            return Mathf.Max(0f, spawnInterval);
        }

        private float GetEffectiveSpawnInterval()
        {
            float baseInterval =
                Mathf.Max(
                    0f,
                    GetBaseSpawnInterval());

            if (monsterDefinition == null)
            {
                return baseInterval *
                       Mathf.Max(0f, spawnIntervalMultiplier);
            }

            CacheManagers();

            if (altarManager == null ||
                spawnManager == null)
            {
                return baseInterval *
                       Mathf.Max(0f, spawnIntervalMultiplier);
            }

            string mapId =
                ResolveCurrentMapId();

            AltarProgressRecord record =
                altarManager.GetOrCreateMonster(
                    mapId,
                    monsterDefinition.monsterId);

            bool canRespawn =
                spawnManager.TryGetRespawnDelay(
                    baseInterval,
                    record.spawnEfficiencyLevel,
                    AltarBalance.SpawnEfficiencyMaxLevel,
                    record.postMaxRespawnEnabled,
                    record.postMaxRespawnDelayScale,
                    out float delaySeconds);

            if (!canRespawn)
                return float.PositiveInfinity;

            return Mathf.Max(
                0f,
                delaySeconds *
                Mathf.Max(0f, spawnIntervalMultiplier));
        }

        private void HandleAltarProgressChanged(
            AltarProgressRecord record)
        {
            if (record == null ||
                monsterDefinition == null)
            {
                return;
            }

            if (!string.Equals(
                    record.monsterId,
                    monsterDefinition.monsterId,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(
                    record.mapId,
                    ResolveCurrentMapId(),
                    System.StringComparison.Ordinal))
            {
                return;
            }

            float interval =
                GetEffectiveSpawnInterval();

            nextSpawnTime =
                float.IsPositiveInfinity(interval)
                    ? float.PositiveInfinity
                    : Time.time + interval;
        }

        private void CacheManagers()
        {
            if (altarManager == null)
                altarManager = FindFirstObjectByType<AltarManager>();

            if (spawnManager == null)
                spawnManager = FindFirstObjectByType<SpawnManager>();

            if (mapManager == null)
                mapManager = FindFirstObjectByType<MapManager>();
        }

        private string ResolveCurrentMapId()
        {
            if (mapManager != null &&
                !string.IsNullOrWhiteSpace(
                    mapManager.CurrentMapId))
            {
                return mapManager.CurrentMapId;
            }

            return SceneManager
                .GetActiveScene()
                .name;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            monsterLevel = Mathf.Max(1, monsterLevel);
            spawnInterval = Mathf.Max(0f, spawnInterval);
            maxAlive = Mathf.Max(1, maxAlive);
            randomSpawnRadius = Mathf.Max(0f, randomSpawnRadius);
            spawnIntervalMultiplier = Mathf.Max(0f, spawnIntervalMultiplier);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, randomSpawnRadius);
        }
#endif
    }
}
