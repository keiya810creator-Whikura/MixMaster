using UnityEngine;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.UI;

namespace MixMaster.World
{
    [DisallowMultipleComponent]
    public sealed class MaterialDropPickup : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0.05f)] private float worldScale = 0.45f;

        [Header("Absorb")]
        [SerializeField, Min(0f)] private float waitBeforeAbsorb = 0.18f;
        [SerializeField, Min(0.1f)] private float startSpeed = 4f;
        [SerializeField, Min(0.1f)] private float acceleration = 12f;
        [SerializeField, Min(0.01f)] private float collectDistance = 0.12f;

        private MaterialSO material;
        private long quantity;
        private MaterialDropSourceInfo sourceInfo;

        private Transform playerTarget;
        private MaterialInventoryManager inventory;
        private MaterialDropLogUI logUi;

        private float age;
        private float currentSpeed;
        private bool initialized;
        private bool collected;

        public void Initialize(
            MaterialSO newMaterial,
            long newQuantity,
            MaterialDropSourceInfo newSourceInfo)
        {
            material = newMaterial;
            quantity = System.Math.Max(1L, newQuantity);

            sourceInfo =
                newSourceInfo != null
                    ? newSourceInfo.Clone()
                    : MaterialDropSourceInfo.Unknown();

            CacheReferences();
            ApplyVisual();

            age = 0f;
            currentSpeed = startSpeed;
            initialized = true;
            collected = false;
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren<SpriteRenderer>();
            }

            CacheReferences();
        }

        private void Start()
        {
            if (!initialized)
                CacheReferences();
        }

        private void Update()
        {
            if (!initialized ||
                collected ||
                material == null ||
                quantity <= 0L)
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

            Vector3 targetPosition =
                playerTarget.position;

            Vector3 delta =
                targetPosition - transform.position;

            float collectDistanceSqr =
                collectDistance * collectDistance;

            if (delta.sqrMagnitude <=
                collectDistanceSqr)
            {
                Collect();
                return;
            }

            currentSpeed +=
                acceleration * Time.deltaTime;

            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
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

            if (inventory == null)
            {
                inventory =
                    FindFirstObjectByType<MaterialInventoryManager>();
            }

            if (logUi == null)
            {
                logUi =
                    FindFirstObjectByType<MaterialDropLogUI>();
            }
        }

        private void ApplyVisual()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite =
                    material != null
                        ? material.icon
                        : null;

                spriteRenderer.sortingOrder = 40;
            }

            transform.localScale =
                Vector3.one * worldScale;
        }

        private void Collect()
        {
            if (collected)
                return;

            collected = true;

            CacheReferences();

            inventory?.AddMaterial(
                material,
                quantity);

            logUi?.AddMaterialLog(
                material,
                quantity,
                sourceInfo != null
                    ? sourceInfo.sourceType
                    : MaterialDropSourceType.Unknown);

            Destroy(gameObject);
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
