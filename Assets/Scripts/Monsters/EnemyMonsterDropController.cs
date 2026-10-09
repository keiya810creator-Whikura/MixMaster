using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MixMaster.Combat;
using MixMaster.Core;
using MixMaster.World;

namespace MixMaster.Monsters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyMonsterDropController : MonoBehaviour
    {
        [SerializeField] private MonsterSO monsterDefinition;

        private EnemyHealth health;
        private DropManager dropManager;
        private AltarManager altarManager;
        private MapManager mapManager;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<EnemyHealth>();

            if (health != null)
                health.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Died -= HandleDied;
        }

        public void Configure(MonsterSO definition)
        {
            monsterDefinition = definition;
        }

        private void HandleDied(EnemyHealth enemy)
        {
            if (monsterDefinition == null)
                return;

            CacheManagers();

            if (dropManager == null)
                return;

            MaterialDropSourceInfo source =
                enemy != null
                    ? enemy.LastHitSource
                    : MaterialDropSourceInfo.Unknown();

            float bodyDropRate =
                altarManager != null
                    ? altarManager.GetMonsterBodyDropRate(
                        ResolveCurrentMapId(),
                        monsterDefinition)
                    : Mathf.Clamp01(
                        monsterDefinition.baseMonsterDropRate);

            float killerBonus =
                source != null
                    ? Mathf.Max(
                        0f,
                        source.dropRateBonus)
                    : 0f;

            if (!dropManager.RollMonsterBodyDrop(
                    bodyDropRate,
                    killerBonus))
            {
                return;
            }

            MonsterIndividualValues ivs =
                MonsterDropRoller.RollIndividualValues();

            List<TitleSO> titlePool =
                BuildCurrentMapTitlePool();

            TitleSO title =
                MonsterDropRoller.RollTitle(
                    titlePool,
                    MonsterDropRoller.BaseTitleChance);

            MonsterDropRollData rollData =
                new MonsterDropRollData
                {
                    monster = monsterDefinition,
                    individualValues = ivs,
                    title = title
                };

            dropManager.SpawnMonsterDrop(
                rollData,
                transform.position);
        }

        private void CacheManagers()
        {
            if (dropManager == null)
                dropManager = FindFirstObjectByType<DropManager>();

            if (altarManager == null)
                altarManager = FindFirstObjectByType<AltarManager>();

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

        private static List<TitleSO> BuildCurrentMapTitlePool()
        {
            List<TitleSO> result =
                new List<TitleSO>();

            HashSet<TitleSO> unique =
                new HashSet<TitleSO>();

            EnemySpawnPoint[] spawnPoints =
                FindObjectsByType<EnemySpawnPoint>(
                    FindObjectsSortMode.None);

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EnemySpawnPoint spawnPoint =
                    spawnPoints[i];

                MonsterSO monster =
                    spawnPoint != null
                        ? spawnPoint.MonsterDefinition
                        : null;

                AddTitles(
                    monster,
                    unique,
                    result);
            }

            return result;
        }

        private static void AddTitles(
            MonsterSO monster,
            HashSet<TitleSO> unique,
            List<TitleSO> result)
        {
            if (monster == null ||
                monster.titlePool == null)
            {
                return;
            }

            for (int i = 0;
                 i < monster.titlePool.Count;
                 i++)
            {
                TitleSO title =
                    monster.titlePool[i];

                if (title != null &&
                    unique.Add(title))
                {
                    result.Add(title);
                }
            }
        }
    }
}
