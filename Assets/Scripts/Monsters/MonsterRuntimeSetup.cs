using UnityEngine;
using MixMaster.Combat;
using MixMaster.Core;

namespace MixMaster.Monsters
{
    [DisallowMultipleComponent]
    public sealed class MonsterRuntimeSetup : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private MonsterSO monsterDefinition;
        [SerializeField, Min(1)] private int level = 1;

        [Header("Runtime Individual")]
        [SerializeField] private MonsterIndividualValues individualValues =
            new MonsterIndividualValues();

        [SerializeField] private TitleSO title;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private EnemyHealth enemyHealth;
        private EnemyCombatAI enemyCombatAI;
        private EnemyWanderAI wanderAI;
        private EnemyMaterialDropController materialDropController;
        private EnemyMonsterDropController monsterDropController;
        private bool initialized;

        public MonsterSO Definition => monsterDefinition;
        public int Level => level;
        public MonsterIndividualValues IndividualValues => individualValues;
        public TitleSO Title => title;
        public CharacterStats CurrentStats { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void Start()
        {
            if (!initialized && monsterDefinition != null)
                ApplyDefinition();
        }

        public void Initialize(
            MonsterSO definition,
            int monsterLevel = 1)
        {
            Initialize(
                definition,
                monsterLevel,
                null,
                null);
        }

        public void Initialize(
            MonsterSO definition,
            int monsterLevel,
            MonsterIndividualValues ivs,
            TitleSO monsterTitle)
        {
            monsterDefinition = definition;
            level = Mathf.Max(1, monsterLevel);

            if (ivs != null)
                individualValues = ivs;

            individualValues ??=
                new MonsterIndividualValues();

            title = monsterTitle;
            initialized = true;

            ApplyDefinition();
        }

        public void Reapply()
        {
            if (monsterDefinition == null)
                return;

            ApplyDefinition();
        }

        private void ApplyDefinition()
        {
            if (monsterDefinition == null)
                return;

            CacheComponents();

            individualValues ??=
                new MonsterIndividualValues();

            CurrentStats =
                StatCalculator.CalculateMonsterStats(
                    monsterDefinition,
                    Mathf.Max(1, level),
                    individualValues,
                    title);

            if (spriteRenderer != null &&
                monsterDefinition.sprite != null)
            {
                spriteRenderer.sprite =
                    monsterDefinition.sprite;
            }

            if (enemyHealth != null)
            {
                enemyHealth.SetStats(
                    CurrentStats.maxHp,
                    CurrentStats.maxMp,
                    CurrentStats.defense,
                    CurrentStats.magicDefense,
                    true);
            }

            if (enemyCombatAI != null)
            {
                enemyCombatAI.ApplyMonsterData(
                    monsterDefinition,
                    CurrentStats);
            }

            if (wanderAI != null)
            {
                wanderAI.SetMoveSpeed(
                    CurrentStats.moveSpeed);
            }

            if (materialDropController != null)
            {
                materialDropController.Configure(
                    monsterDefinition);
            }

            if (monsterDropController != null)
            {
                monsterDropController.Configure(
                    monsterDefinition);
            }
        }

        private void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren<SpriteRenderer>();
            }

            enemyHealth =
                GetComponent<EnemyHealth>();

            if (enemyHealth == null)
                enemyHealth = gameObject.AddComponent<EnemyHealth>();

            enemyCombatAI =
                GetComponent<EnemyCombatAI>();

            if (enemyCombatAI == null)
                enemyCombatAI = gameObject.AddComponent<EnemyCombatAI>();

            wanderAI =
                GetComponent<EnemyWanderAI>();

            materialDropController =
                GetComponent<EnemyMaterialDropController>();

            if (materialDropController == null)
            {
                materialDropController =
                    gameObject.AddComponent<EnemyMaterialDropController>();
            }

            monsterDropController =
                GetComponent<EnemyMonsterDropController>();

            if (monsterDropController == null)
            {
                monsterDropController =
                    gameObject.AddComponent<EnemyMonsterDropController>();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            level = Mathf.Max(1, level);

            individualValues ??=
                new MonsterIndividualValues();

            individualValues.ClampAll();
        }
#endif
    }
}
