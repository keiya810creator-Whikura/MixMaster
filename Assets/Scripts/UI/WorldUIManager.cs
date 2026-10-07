using System.Collections.Generic;
using UnityEngine;
using MixMaster.Combat;
using MixMaster.Monsters;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class WorldUIManager : MonoBehaviour
    {
        public static WorldUIManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CharacterWorldBar barPrefab;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform poolRoot;

        [Header("Pool")]
        [SerializeField, Min(0)] private int initialPoolSize = 16;

        [Header("Offsets")]
        [SerializeField] private Vector3 partyWorldOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private Vector3 enemyWorldOffset = new Vector3(0f, 0.9f, 0f);

        private readonly Queue<CharacterWorldBar> pool = new Queue<CharacterWorldBar>();
        private readonly Dictionary<PartyMemberCombat, CharacterWorldBar> partyBars =
            new Dictionary<PartyMemberCombat, CharacterWorldBar>();
        private readonly Dictionary<EnemyHealth, CharacterWorldBar> enemyBars =
            new Dictionary<EnemyHealth, CharacterWorldBar>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (worldCamera == null)
                worldCamera = Camera.main;

            if (poolRoot == null)
                poolRoot = transform;

            WarmPool();
        }

        private void Start()
        {
            RegisterExistingCharacters();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void TryRegisterParty(PartyMemberCombat member)
        {
            if (Instance != null)
                Instance.RegisterParty(member);
        }

        public static void TryUnregisterParty(PartyMemberCombat member)
        {
            if (Instance != null)
                Instance.UnregisterParty(member);
        }

        public static void TryRegisterEnemy(EnemyHealth enemy)
        {
            if (Instance != null)
                Instance.RegisterEnemy(enemy);
        }

        public static void TryUnregisterEnemy(EnemyHealth enemy)
        {
            if (Instance != null)
                Instance.UnregisterEnemy(enemy);
        }

        public void RegisterParty(PartyMemberCombat member)
        {
            if (member == null || partyBars.ContainsKey(member))
                return;

            CharacterWorldBar bar = AcquireBar();
            if (bar == null)
                return;

            partyBars.Add(member, bar);
            bar.BindParty(member, GetWorldCamera(), partyWorldOffset, HandleBarReleaseRequested);
        }

        public void UnregisterParty(PartyMemberCombat member)
        {
            if (member == null)
                return;

            if (!partyBars.TryGetValue(member, out CharacterWorldBar bar))
                return;

            partyBars.Remove(member);
            ReleaseBar(bar);
        }

        public void RegisterEnemy(EnemyHealth enemy)
        {
            if (enemy == null || !enemy.IsAlive || enemyBars.ContainsKey(enemy))
                return;

            CharacterWorldBar bar = AcquireBar();
            if (bar == null)
                return;

            enemyBars.Add(enemy, bar);
            bar.BindEnemy(enemy, GetWorldCamera(), enemyWorldOffset, HandleBarReleaseRequested);
        }

        public void UnregisterEnemy(EnemyHealth enemy)
        {
            if (enemy == null)
                return;

            if (!enemyBars.TryGetValue(enemy, out CharacterWorldBar bar))
                return;

            enemyBars.Remove(enemy);
            ReleaseBar(bar);
        }

        private void RegisterExistingCharacters()
        {
            PartyMemberCombat[] members =
                FindObjectsByType<PartyMemberCombat>(FindObjectsSortMode.None);

            for (int i = 0; i < members.Length; i++)
                RegisterParty(members[i]);

            EnemyHealth[] enemies =
                FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
                RegisterEnemy(enemies[i]);
        }

        private void WarmPool()
        {
            if (barPrefab == null)
                return;

            for (int i = 0; i < initialPoolSize; i++)
            {
                CharacterWorldBar bar = CreateBar();
                if (bar != null)
                    pool.Enqueue(bar);
            }
        }

        private CharacterWorldBar AcquireBar()
        {
            CharacterWorldBar bar = pool.Count > 0 ? pool.Dequeue() : CreateBar();

            if (bar != null)
                bar.gameObject.SetActive(true);

            return bar;
        }

        private CharacterWorldBar CreateBar()
        {
            if (barPrefab == null)
            {
                Debug.LogWarning("WorldUIManager: CharacterWorldBar prefab is not assigned.", this);
                return null;
            }

            CharacterWorldBar bar = Instantiate(barPrefab, poolRoot);
            bar.name = barPrefab.name + "_Pooled";
            bar.Unbind();
            bar.gameObject.SetActive(false);
            return bar;
        }

        private void ReleaseBar(CharacterWorldBar bar)
        {
            if (bar == null)
                return;

            bar.Unbind();
            bar.gameObject.SetActive(false);

            if (!pool.Contains(bar))
                pool.Enqueue(bar);
        }

        private void HandleBarReleaseRequested(CharacterWorldBar bar)
        {
            if (bar == null)
                return;

            Component owner = bar.Owner;

            if (owner is PartyMemberCombat party)
                partyBars.Remove(party);
            else if (owner is EnemyHealth enemy)
                enemyBars.Remove(enemy);

            ReleaseBar(bar);
        }

        private Camera GetWorldCamera()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            return worldCamera;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            initialPoolSize = Mathf.Max(0, initialPoolSize);
        }
#endif
    }
}
