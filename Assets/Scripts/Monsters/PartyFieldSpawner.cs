using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Monsters
{
    /// <summary>
    /// Creates only the three party members selected by MonsterManager.
    /// Lives on the persistent manager root; the spawned actors belong
    /// to the current scene and are replaced when the party changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyFieldSpawner : MonoBehaviour
    {
        private const string DefaultPrefabPath = "Prefabs/PartyMember";

        private readonly List<GameObject> activeActors =
            new List<GameObject>();

        private MonsterManager monsterManager;
        private MonsterCatalogSO catalog;
        private GameObject actorsRoot;
        private bool dirty = true;
        private float nextRetryTime;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TrySubscribe();
            dirty = true;
        }

        private void Start()
        {
            RefreshParty();
        }

        private void Update()
        {
            if (!dirty || Time.unscaledTime < nextRetryTime)
                return;

            if (monsterManager == null)
                TrySubscribe();

            RefreshParty();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (monsterManager != null)
                monsterManager.PartyChanged -= HandlePartyChanged;

            ClearActors();
        }

        private void OnDestroy()
        {
            ClearActors();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearActors();
            dirty = true;
            nextRetryTime = 0f;
        }

        private void TrySubscribe()
        {
            if (monsterManager != null)
                return;

            monsterManager = GetComponent<MonsterManager>();

            if (monsterManager == null)
                monsterManager = FindFirstObjectByType<MonsterManager>();

            if (monsterManager != null)
            {
                monsterManager.PartyChanged -= HandlePartyChanged;
                monsterManager.PartyChanged += HandlePartyChanged;
            }
        }

        private void HandlePartyChanged()
        {
            dirty = true;
            nextRetryTime = 0f;
            RefreshParty();
        }

        public void RefreshParty()
        {
            if (monsterManager == null)
            {
                TrySubscribe();

                if (monsterManager == null)
                {
                    dirty = true;
                    nextRetryTime = Time.unscaledTime + 0.5f;
                    return;
                }
            }

            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player == null)
            {
                ClearActors();
                dirty = true;
                nextRetryTime = Time.unscaledTime + 0.75f;
                return;
            }

            dirty = false;
            ClearActors();

            IReadOnlyList<string> partyIds =
                monsterManager.PartyMonsterUniqueIds;

            if (partyIds == null || partyIds.Count == 0)
                return;

            PlayerTrailRecorder trail = player.GetComponent<PlayerTrailRecorder>();

            if (trail == null)
                trail = player.gameObject.AddComponent<PlayerTrailRecorder>();

            catalog = MonsterCatalogSO.Load();

            for (int i = 0; i < partyIds.Count && i < 3; i++)
            {
                OwnedMonsterRecord record =
                    monsterManager.FindOwnedMonster(partyIds[i]);

                if (record == null)
                    continue;

                MonsterSO definition = catalog != null
                    ? catalog.GetMonster(record.monsterId)
                    : null;

                if (definition == null)
                {
                    Debug.LogWarning(
                        "[PartyFieldSpawner] MonsterSOが見つかりません: " +
                        record.monsterId);
                    continue;
                }

                SpawnMember(record, definition, trail, i);
            }
        }

        private void SpawnMember(
            OwnedMonsterRecord record,
            MonsterSO monster,
            PlayerTrailRecorder trail,
            int order)
        {
            GameObject prefab = monster.partyPrefab;

            // Enemy prefabs must never be spawned as allies.
            if (prefab != null &&
                prefab.GetComponent<MixMaster.Combat.EnemyHealth>() != null)
            {
                Debug.LogWarning(
                    "[PartyFieldSpawner] PartyPrefabが敵用Prefabのため共通Prefabを使用します: " +
                    monster.name);
                prefab = null;
            }

            if (prefab == null)
                prefab = Resources.Load<GameObject>(DefaultPrefabPath);

            if (actorsRoot == null)
            {
                actorsRoot = new GameObject("_RuntimePartyMembers");
                SceneManager.MoveGameObjectToScene(
                    actorsRoot, SceneManager.GetActiveScene());
            }

            Vector3 spawnPosition = trail.transform.position +
                new Vector3(-0.6f + order * 0.6f, -0.65f, 0f);

            GameObject actor = prefab != null
                ? Instantiate(prefab, spawnPosition, Quaternion.identity,
                    actorsRoot.transform)
                : CreateFallbackActor(spawnPosition, actorsRoot.transform);

            // Allies always use the same display scale regardless of prefab.
            actor.transform.localScale = Vector3.one * 0.25f;

            actor.name = "Party_" + (order + 1) + "_" +
                (!string.IsNullOrWhiteSpace(monster.displayName)
                    ? monster.displayName
                    : monster.monsterId);

            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();

            if (renderer == null)
                renderer = actor.AddComponent<SpriteRenderer>();

            if (monster.sprite != null)
                renderer.sprite = monster.sprite;

            renderer.sortingOrder = 10;

            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            if (body == null)
                body = actor.AddComponent<Rigidbody2D>();

            body.gravityScale = 0f;
            body.freezeRotation = true;

            MonsterTrailFollower follower =
                actor.GetComponent<MonsterTrailFollower>();

            if (follower == null)
                follower = actor.AddComponent<MonsterTrailFollower>();

            follower.SetPlayerTrail(trail);
            follower.SetFollowOrder(order);

            PartyMemberCombat combat = actor.GetComponent<PartyMemberCombat>();

            if (combat == null)
                combat = actor.AddComponent<PartyMemberCombat>();

            TitleSO title = catalog != null
                ? catalog.GetTitle(record.titleId)
                : null;

            CharacterStats calculated =
                StatCalculator.CalculateMonsterStats(
                    monster,
                    record.level,
                    record.individualValues,
                    title);

            combat.ConfigureOwnedMonster(monster, calculated);
            activeActors.Add(actor);
        }

        private static GameObject CreateFallbackActor(
            Vector3 position,
            Transform parent)
        {
            GameObject actor = new GameObject(
                "PartyMember",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(MonsterTrailFollower),
                typeof(PartyMemberCombat));

            actor.transform.SetParent(parent, false);
            actor.transform.position = position;
            return actor;
        }

        private void ClearActors()
        {
            for (int i = 0; i < activeActors.Count; i++)
            {
                GameObject actor = activeActors[i];

                if (actor == null)
                    continue;

                // Immediately unregister old members from enemy targeting.
                actor.SetActive(false);
                Destroy(actor);
            }

            activeActors.Clear();

            if (actorsRoot != null)
            {
                Destroy(actorsRoot);
                actorsRoot = null;
            }
        }
    }
}
