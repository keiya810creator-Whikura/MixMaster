using UnityEngine;
using MixMaster.Core;
using MixMaster.Player;

namespace MixMaster.World
{
    [DisallowMultipleComponent]
    public sealed class MonsterDropPickup : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0.05f)] private float worldScale = 0.65f;

        [Header("Absorb")]
        [SerializeField, Min(0f)] private float waitBeforeAbsorb = 0.25f;
        [SerializeField, Min(0.1f)] private float startSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float acceleration = 10f;
        [SerializeField, Min(0.01f)] private float collectDistance = 0.15f;

        private MonsterDropRollData rollData;
        private Transform playerTarget;
        private MonsterManager monsterManager;

        private float age;
        private float currentSpeed;
        private bool initialized;
        private bool collected;

        public MonsterDropRollData RollData => rollData;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren<SpriteRenderer>();
            }

            CacheReferences();
        }

        public void Initialize(
            MonsterDropRollData newRollData)
        {
            rollData = newRollData;

            if (rollData != null)
            {
                rollData.individualValues =
                    MonsterDropRoller.CloneIndividualValues(
                        rollData.individualValues);
            }

            CacheReferences();
            ApplyVisual();

            age = 0f;
            currentSpeed = startSpeed;
            initialized = true;
            collected = false;
        }

        private void Update()
        {
            if (!initialized ||
                collected ||
                rollData == null ||
                rollData.monster == null)
            {
                return;
            }

            age += Time.deltaTime;

            if (age < waitBeforeAbsorb)
                return;

            if (playerTarget == null)
            {
                CacheReferences();

                if (playerTarget == null)
                    return;
            }

            Vector3 delta =
                playerTarget.position -
                transform.position;

            if (delta.sqrMagnitude <=
                collectDistance * collectDistance)
            {
                Collect();
                return;
            }

            currentSpeed +=
                acceleration * Time.deltaTime;

            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    playerTarget.position,
                    currentSpeed * Time.deltaTime);
        }

        private void CacheReferences()
        {
            if (playerTarget == null)
            {
                PlayerController player =
                    FindFirstObjectByType<PlayerController>();

                if (player != null)
                    playerTarget = player.transform;
            }

            if (monsterManager == null)
            {
                monsterManager =
                    FindFirstObjectByType<MonsterManager>();
            }
        }

        private void ApplyVisual()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer == null)
                return;

            spriteRenderer.sprite =
                rollData != null &&
                rollData.monster != null
                    ? rollData.monster.sprite
                    : null;

            spriteRenderer.sortingOrder = 41;

            transform.localScale =
                Vector3.one * worldScale;
        }

        private void Collect()
        {
            if (collected)
                return;

            collected = true;
            CacheReferences();

            if (monsterManager != null &&
                rollData != null &&
                rollData.monster != null)
            {
                OwnedMonsterRecord obtained =
                    monsterManager.ObtainMonster(
                        rollData.monster,
                        rollData.individualValues,
                        rollData.title);

                Debug.Log(
                    BuildObtainedLog(
                        rollData,
                        obtained),
                    this);
            }

            Destroy(gameObject);
        }

        private static string BuildObtainedLog(
            MonsterDropRollData data,
            OwnedMonsterRecord obtained)
        {
            MonsterIndividualValues iv =
                data.individualValues;

            string monsterName =
                !string.IsNullOrWhiteSpace(
                    data.monster.displayName)
                    ? data.monster.displayName
                    : data.monster.monsterId;

            string titleName =
                data.title != null
                    ? (!string.IsNullOrWhiteSpace(
                        data.title.displayName)
                        ? data.title.displayName
                        : data.title.titleId)
                    : "なし";

            return
                "[MonsterDropPickup] " +
                monsterName +
                " を獲得 / 称号: " +
                titleName +
                " / IV " +
                "HP=" + iv.hp +
                ", MP=" + iv.mp +
                ", ATK=" + iv.attack +
                ", MAG=" + iv.magic +
                ", DEF=" + iv.defense +
                ", MDEF=" + iv.magicDefense +
                ", CRIT=" + iv.criticalRate +
                ", CRITx=" + iv.criticalMultiplier +
                ", MOVE=" + iv.moveSpeed +
                ", ASPD=" + iv.attackSpeed +
                ", RANGE=" + iv.attackRange +
                " / UniqueId=" +
                (obtained != null
                    ? obtained.uniqueId
                    : string.Empty);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            worldScale = Mathf.Max(0.05f, worldScale);
            waitBeforeAbsorb = Mathf.Max(0f, waitBeforeAbsorb);
            startSpeed = Mathf.Max(0.1f, startSpeed);
            acceleration = Mathf.Max(0.1f, acceleration);
            collectDistance = Mathf.Max(0.01f, collectDistance);
        }
#endif
    }
}
