using System.Collections.Generic;
using UnityEngine;
using MixMaster.Combat;
using MixMaster.Monsters;

namespace MixMaster.World
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [Header("Enemy")]
        [SerializeField] private GameObject enemyPrefab;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnInterval = 5f;
        [SerializeField, Min(1)] private int maxAlive = 1;
        [SerializeField] private bool spawnImmediately = true;
        [SerializeField, Min(0f)] private float randomSpawnRadius = 0f;

        [Header("Runtime / Altar Hook")]
        [Tooltip("1 = normal, 0.5 = twice as fast, 0 = instant respawn. Later the altar system can control this value.")]
        [SerializeField, Min(0f)] private float spawnIntervalMultiplier = 1f;

        private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
        private float nextSpawnTime;

        public float BaseSpawnInterval => spawnInterval;
        public float SpawnIntervalMultiplier => spawnIntervalMultiplier;
        public int AliveCount => aliveEnemies.Count;

        private void Start()
        {
            nextSpawnTime = spawnImmediately
                ? Time.time
                : Time.time + GetEffectiveSpawnInterval();
        }

        private void Update()
        {
            aliveEnemies.RemoveAll(x => x == null || !x.IsAlive);

            if (enemyPrefab == null || aliveEnemies.Count >= maxAlive)
                return;

            if (Time.time < nextSpawnTime)
                return;

            SpawnEnemy();

            if (aliveEnemies.Count < maxAlive)
                nextSpawnTime = Time.time + GetEffectiveSpawnInterval();
        }

        public void SetSpawnIntervalMultiplier(float multiplier)
        {
            spawnIntervalMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SpawnEnemy()
        {
            if (enemyPrefab == null || aliveEnemies.Count >= maxAlive)
                return;

            Vector2 offset = randomSpawnRadius > 0f
                ? Random.insideUnitCircle * randomSpawnRadius
                : Vector2.zero;

            Vector3 spawnPosition = transform.position + (Vector3)offset;
            GameObject enemyObject = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            EnemyHealth health = enemyObject.GetComponent<EnemyHealth>();
            if (health == null)
                health = enemyObject.AddComponent<EnemyHealth>();

            EnemyWanderAI wander = enemyObject.GetComponent<EnemyWanderAI>();
            if (wander != null)
                wander.SetHome(transform.position);

            aliveEnemies.Add(health);
            health.Died += HandleEnemyDied;

            nextSpawnTime = Time.time + GetEffectiveSpawnInterval();
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (enemy != null)
                enemy.Died -= HandleEnemyDied;

            aliveEnemies.Remove(enemy);
            nextSpawnTime = Time.time + GetEffectiveSpawnInterval();
        }

        private float GetEffectiveSpawnInterval()
        {
            return Mathf.Max(0f, spawnInterval * spawnIntervalMultiplier);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
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
