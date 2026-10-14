using System;
using System.Collections.Generic;
using MixMaster.Combat;
using MixMaster.Core;
using MixMaster.Monsters;
using UnityEngine;

namespace MixMaster.World
{
    /// <summary>
    /// One-time room monster placement. Existing field EnemySpawnPoints
    /// remain untouched. Boss and miniboss kills are observed for
    /// the later treasure/result phase without awarding treasure early.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEnemySpawner : MonoBehaviour
    {
        private enum EnemyRole { Normal, MidBoss, FinalBoss }

        [Header("Normal monster placement")]
        [SerializeField, Min(0)] private int minMonstersPerRoom = 2;
        [SerializeField, Min(0)] private int maxMonstersPerRoom = 3;

        [Header("Mid boss (per DungeonGenerationSettings.sideBossCount)")]
        [SerializeField, Min(1f)] private float midBossSizeMultiplier = 1.5f;
        [SerializeField, Min(1)] private int midBossHpMultiplier = 5;

        [Header("Final boss")]
        [SerializeField, Min(1f)] private float finalBossSizeMultiplier = 2f;
        [SerializeField, Min(1)] private int finalBossHpMultiplier = 10;

        [Header("Debug")]
        [SerializeField] private bool logSpawnSummary = true;

        private readonly Dictionary<EnemyHealth, EnemyRole> liveEnemies =
            new Dictionary<EnemyHealth, EnemyRole>();
        private DungeonMapGenerator generator;
        private DungeonManager dungeon;
        private bool finalBossReported;

        // Receives only generated normal-enemy defeats, never field enemies.
        public event Action<MonsterSO, Vector3> NormalEnemyDefeated;

        public int RemainingEnemies => liveEnemies.Count;
        public bool FinalBossDefeated => finalBossReported;

        private void OnEnable()
        {
            generator = GetComponent<DungeonMapGenerator>() ??
                FindFirstObjectByType<DungeonMapGenerator>();
            dungeon = GetComponent<DungeonManager>() ??
                FindFirstObjectByType<DungeonManager>();

            if (generator != null)
                generator.DungeonMapReady += HandleMapReady;
            if (dungeon != null)
                dungeon.DungeonEnded += ClearTrackedEnemies;
        }

        private void OnDisable()
        {
            if (generator != null)
                generator.DungeonMapReady -= HandleMapReady;
            if (dungeon != null)
                dungeon.DungeonEnded -= ClearTrackedEnemies;
            ClearTrackedEnemies();
        }

        private void HandleMapReady(DungeonLayout layout)
        {
            ClearTrackedEnemies();
            finalBossReported = false;

            if (layout == null || layout.EntranceRoom == null ||
                layout.BossRoom == null || generator == null)
                return;

            MapSO map = generator.SourceMap;
            if (map == null)
            {
                Debug.LogWarning("[DungeonEnemySpawner] MapSOが見つからないため、" +
                    "敵を生成できません。TilemapMapRoot.MapDefinitionを確認してください。", this);
                return;
            }

            var pool = new List<MonsterSO>();
            if (map.normalMonsters != null)
                for (int i = 0; i < map.normalMonsters.Count; i++)
                    if (map.normalMonsters[i] != null)
                        pool.Add(map.normalMonsters[i]);

            if (pool.Count == 0 && map.monsters != null)
                for (int i = 0; i < map.monsters.Count; i++)
                    if (map.monsters[i] != null &&
                        map.monsters[i] != map.bossMonster)
                        pool.Add(map.monsters[i]);

            int requestCount = dungeon != null &&
                dungeon.CurrentSettings != null
                ? dungeon.CurrentSettings.sideBossCount : 0;

            // Select deeper rooms for mid bosses, excluding the safe
            // entrance and final boss room. Never place two bosses together.
            var candidates = new List<DungeonRoom>();
            foreach (DungeonRoom room in layout.Rooms)
                if (room != layout.EntranceRoom &&
                    room != layout.BossRoom)
                    candidates.Add(room);
            candidates.Sort((a, b) =>
            {
                int result = b.DistanceFromEntrance.CompareTo(
                    a.DistanceFromEntrance);
                return result != 0 ? result : a.Index.CompareTo(b.Index);
            });

            int midCount = Mathf.Clamp(requestCount, 0, candidates.Count);
            var midBossRoomIds = new HashSet<int>();
            for (int i = 0; i < midCount; i++)
            {
                DungeonRoom room = candidates[i];
                midBossRoomIds.Add(room.Index);
                generator.TintBossRoom(room, false);
            }

            generator.TintBossRoom(layout.BossRoom, true);

            int seed = dungeon != null &&
                dungeon.CurrentSettings != null
                ? dungeon.CurrentSettings.seed : 1;
            var random = new System.Random(unchecked(seed ^ 0x41D92B));
            int normalTotal = 0;
            int midTotal = 0;
            int finalTotal = 0;

            foreach (DungeonRoom room in layout.Rooms)
            {
                if (room == layout.EntranceRoom) continue;

                if (room == layout.BossRoom)
                {
                    MonsterSO boss = map.bossMonster ??
                        (pool.Count > 0 ? pool[pool.Count - 1] : null);
                    if (Spawn(boss, room, Vector2Int.zero,
                        Mathf.Max(1, map.strongMonsterLevel),
                        EnemyRole.FinalBoss, finalBossSizeMultiplier,
                        finalBossHpMultiplier))
                        finalTotal++;
                }
                else if (midBossRoomIds.Contains(room.Index))
                {
                    MonsterSO mid = pool.Count > 0
                        ? pool[random.Next(pool.Count)] : map.bossMonster;
                    if (Spawn(mid, room, Vector2Int.zero,
                        Mathf.Max(1, map.strongMonsterLevel),
                        EnemyRole.MidBoss, midBossSizeMultiplier,
                        midBossHpMultiplier))
                        midTotal++;
                }
                else if (pool.Count > 0)
                {
                    int minimum = Mathf.Max(0, minMonstersPerRoom);
                    int maximum = Mathf.Max(minimum, maxMonstersPerRoom);
                    int baseCount = random.Next(minimum, maximum + 1);
                    int boostLevel = dungeon != null
                        ? dungeon.GetAppliedDungeonUpgradeLevel(
                            DungeonUpgradeType.NormalEnemyCount) : 0;
                    // Each level adds +10%. Stochastic rounding avoids
                    // turning 2 -> 3 at Lv1 on every single room.
                    double scaled = baseCount * (1d + 0.1d * boostLevel);
                    int count = (int)Math.Floor(scaled);
                    if (random.NextDouble() < scaled - count)
                        count++;

                    int half = Mathf.Max(1, generator.RoomSize / 2 - 2);
                    var occupied = new HashSet<Vector2Int>();

                    for (int i = 0; i < count; i++)
                    {
                        Vector2Int offset = Vector2Int.zero;
                        bool found = false;
                        for (int tries = 0; tries < 24; tries++)
                        {
                            offset = new Vector2Int(
                                random.Next(-half, half + 1),
                                random.Next(-half, half + 1));
                            if (occupied.Add(offset))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found) break;

                        MonsterSO chosen = pool[random.Next(pool.Count)];
                        if (Spawn(chosen, room, offset,
                            Mathf.Max(1, map.normalMonsterLevel),
                            EnemyRole.Normal, 1f, 1))
                            normalTotal++;
                    }
                }
            }

            if (logSpawnSummary)
                Debug.Log("[DungeonEnemySpawner] " + map.mapId +
                    " : 雑魚 " + normalTotal + " / 中ボス " + midTotal +
                    " / 最終ボス " + finalTotal +
                    " (予定中ボス " + midCount + ")", this);
        }

        private bool Spawn(MonsterSO definition, DungeonRoom room,
            Vector2Int cellOffset, int level, EnemyRole role,
            float sizeMultiplier, int hpMultiplier)
        {
            if (definition == null || definition.enemyPrefab == null)
            {
                Debug.LogWarning(
                    "[DungeonEnemySpawner] 敵Prefabがありません: " +
                    (definition != null ? definition.monsterId : "(null)"), this);
                return false;
            }

            Vector3 position = generator.GetRoomCellWorldPosition(
                room, cellOffset);
            // Spawn into world space first: retaining world scale is
            // important because the generated Grid is scaled like the field.
            GameObject enemy = Instantiate(
                definition.enemyPrefab, position, Quaternion.identity);
            if (generator.RuntimeRoot != null)
                enemy.transform.SetParent(generator.RuntimeRoot, true);

            MonsterRuntimeSetup setup =
                enemy.GetComponent<MonsterRuntimeSetup>();
            if (setup == null)
                setup = enemy.AddComponent<MonsterRuntimeSetup>();
            setup.Initialize(definition, level);

            enemy.transform.localScale *= Mathf.Max(1f, sizeMultiplier);

            EnemyHealth health = enemy.GetComponent<EnemyHealth>();
            if (health == null)
            {
                Debug.LogError("[DungeonEnemySpawner] EnemyHealthがありません: " +
                    definition.monsterId, enemy);
                Destroy(enemy);
                return false;
            }

            health.RefreshOriginalScale();

            if (hpMultiplier > 1)
                health.SetStats(
                    LongMath.SaturatingMultiply(health.MaxHp,
                        (long)hpMultiplier),
                    health.MaxMp, health.Defense, health.MagicDefense, true);

            EnemyWanderAI wander = enemy.GetComponent<EnemyWanderAI>();
            if (wander != null)
                wander.SetHome(position);

            if (role != EnemyRole.Normal)
                enemy.name = (role == EnemyRole.MidBoss
                    ? "MidBoss_" : "FinalBoss_") +
                    definition.monsterId;

            health.Died += HandleEnemyDied;
            liveEnemies[health] = role;
            return true;
        }

        private void HandleEnemyDied(EnemyHealth enemy)
        {
            if (enemy == null || !liveEnemies.TryGetValue(enemy,
                out EnemyRole role))
                return;

            enemy.Died -= HandleEnemyDied;
            liveEnemies.Remove(enemy);

            if (dungeon == null || !dungeon.IsDungeonActive)
                return;

            if (role == EnemyRole.Normal)
            {
                MonsterRuntimeSetup setup =
                    enemy.GetComponent<MonsterRuntimeSetup>();
                if (setup != null && setup.Definition != null)
                    NormalEnemyDefeated?.Invoke(
                        setup.Definition, enemy.transform.position);
            }
            else if (role == EnemyRole.MidBoss)
                dungeon.RegisterSideBossDefeated();
            else if (role == EnemyRole.FinalBoss && !finalBossReported)
            {
                finalBossReported = true;
                dungeon.NotifyFinalBossDefeated(enemy.transform.position);
            }
        }

        private void ClearTrackedEnemies()
        {
            foreach (EnemyHealth enemy in liveEnemies.Keys)
                if (enemy != null)
                    enemy.Died -= HandleEnemyDied;
            liveEnemies.Clear();
            finalBossReported = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minMonstersPerRoom = Mathf.Max(0, minMonstersPerRoom);
            maxMonstersPerRoom = Mathf.Max(
                minMonstersPerRoom, maxMonstersPerRoom);
            midBossSizeMultiplier = Mathf.Max(1f, midBossSizeMultiplier);
            finalBossSizeMultiplier = Mathf.Max(1f, finalBossSizeMultiplier);
            midBossHpMultiplier = Mathf.Max(1, midBossHpMultiplier);
            finalBossHpMultiplier = Mathf.Max(1, finalBossHpMultiplier);
        }
#endif
    }
}
