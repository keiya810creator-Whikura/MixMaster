using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Core;
using MixMaster.Player;

namespace MixMaster.Combat
{
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public sealed class PlayerAutoAttack : MonoBehaviour
    {
        [Header("Stats Source")]
        [Tooltip("Use PlayerManager CharacterStats for attack/range/speed/critical.")]
        [SerializeField] private bool usePlayerManagerStats = true;

        [Header("UI")]
        [SerializeField] private Slider hpSlider;
        [Tooltip("0 to 1 attack charge gauge. When full, the player attacks.")]
        [SerializeField] private Slider attackSpeedSlider;
        [SerializeField] private bool chargeWhileNoTarget = true;

        [Header("Fallback Stats")]
        [SerializeField, Min(1)] private long fallbackAttack = 10;
        [SerializeField, Min(0.1f)] private float fallbackAttackRange = 1.5f;
        [SerializeField, Min(0.1f)] private float fallbackAttackSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float fallbackCriticalRate = 0.05f;
        [SerializeField, Min(1f)] private float fallbackCriticalMultiplier = 1.5f;

        [Header("Targeting")]
        [SerializeField] private bool requireEnemyTag = true;
        [SerializeField] private string enemyTag = "Enemy";
        [SerializeField, Min(0.02f)] private float targetRefreshInterval = 0.12f;

        [Header("Attack Feedback")]
        [SerializeField] private Color slashColor = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField, Min(0.03f)] private float slashDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.08f;
        [SerializeField, Min(0.1f)] private float slashRadius = 0.75f;

        private PlayerController playerController;
        private PlayerManager playerManager;
        private EnemyHealth currentTarget;

        private float targetRefreshTimer;
        private float attackGauge;

        private GameObject slashObject;
        private LineRenderer slashRenderer;
        private Material slashMaterial;
        private Coroutine slashRoutine;

        public EnemyHealth CurrentTarget => currentTarget;
        public float AttackGauge => attackGauge;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            ConfigureSlider(attackSpeedSlider);
            ConfigureSlider(hpSlider);
            SetAttackGauge(0f);
            CreateSlashRenderer();
        }

        private void Start()
        {
            playerManager = FindFirstObjectByType<PlayerManager>();

            if (playerManager != null)
            {
                playerManager.HpChanged += HandlePlayerHpChanged;
                RefreshHpSlider(playerManager.CurrentHp, playerManager.Stats.maxHp);
            }
            else if (hpSlider != null)
            {
                hpSlider.SetValueWithoutNotify(1f);
            }
        }

        private void OnDestroy()
        {
            if (playerManager != null)
                playerManager.HpChanged -= HandlePlayerHpChanged;

            if (slashMaterial != null)
                Destroy(slashMaterial);
        }

        private void Update()
        {
            targetRefreshTimer -= Time.deltaTime;

            if (targetRefreshTimer <= 0f || !IsTargetValid(currentTarget))
            {
                targetRefreshTimer = targetRefreshInterval;
                currentTarget = FindNearestTarget();
            }

            bool hasTarget = IsTargetValid(currentTarget);

            if (chargeWhileNoTarget || hasTarget)
            {
                float speed = Mathf.Max(0.1f, GetAttackSpeed());
                SetAttackGauge(attackGauge + speed * Time.deltaTime);
            }

            if (!hasTarget || attackGauge < 1f)
                return;

            Attack(currentTarget);
            SetAttackGauge(0f);
        }

        private void Attack(EnemyHealth target)
        {
            if (target == null || !target.IsAlive)
                return;

            Vector2 direction = (target.transform.position - transform.position).normalized;

            if (!playerController.IsMoving)
                playerController.FaceDirection(direction);

            long attackPower = GetAttackPower();

            if (UnityEngine.Random.value < GetCriticalRate())
                attackPower = MultiplyLong(attackPower, GetCriticalMultiplier());

            target.TakePhysicalHit(attackPower);

            if (slashRoutine != null)
                StopCoroutine(slashRoutine);

            slashRoutine = StartCoroutine(PlaySlash(direction));
        }

        private EnemyHealth FindNearestTarget()
        {
            float range = GetAttackRange();
            float rangeSqr = range * range;
            float bestDistanceSqr = float.MaxValue;
            EnemyHealth best = null;

            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];

                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (requireEnemyTag && !enemy.CompareTag(enemyTag))
                    continue;

                Vector2 delta = enemy.transform.position - transform.position;
                float distanceSqr = delta.sqrMagnitude;

                if (distanceSqr > rangeSqr || distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                best = enemy;
            }

            return best;
        }

        private bool IsTargetValid(EnemyHealth target)
        {
            if (target == null || !target.IsAlive)
                return false;

            if (requireEnemyTag && !target.CompareTag(enemyTag))
                return false;

            float range = GetAttackRange();
            return ((Vector2)(target.transform.position - transform.position)).sqrMagnitude <= range * range;
        }

        private void SetAttackGauge(float value)
        {
            attackGauge = Mathf.Clamp01(value);

            if (attackSpeedSlider != null)
                attackSpeedSlider.SetValueWithoutNotify(attackGauge);
        }

        private void HandlePlayerHpChanged(long current, long max)
        {
            RefreshHpSlider(current, max);
        }

        private void RefreshHpSlider(long current, long max)
        {
            if (hpSlider == null)
                return;

            double normalized = max <= 0 ? 0d : (double)current / max;
            hpSlider.SetValueWithoutNotify(Mathf.Clamp01((float)normalized));
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
        }

        private long GetAttackPower()
        {
            if (usePlayerManagerStats && playerManager != null)
                return Math.Max(1L, playerManager.Stats.attack);

            return Math.Max(1L, fallbackAttack);
        }

        private float GetAttackRange()
        {
            if (usePlayerManagerStats && playerManager != null)
                return Mathf.Max(0.1f, playerManager.Stats.attackRange);

            return Mathf.Max(0.1f, fallbackAttackRange);
        }

        private float GetAttackSpeed()
        {
            if (usePlayerManagerStats && playerManager != null)
                return Mathf.Max(0.1f, playerManager.Stats.attackSpeed);

            return Mathf.Max(0.1f, fallbackAttackSpeed);
        }

        private float GetCriticalRate()
        {
            if (usePlayerManagerStats && playerManager != null)
                return Mathf.Clamp01((float)playerManager.Stats.criticalRate);

            return Mathf.Clamp01(fallbackCriticalRate);
        }

        private float GetCriticalMultiplier()
        {
            if (usePlayerManagerStats && playerManager != null)
                return Mathf.Max(1f, (float)playerManager.Stats.criticalMultiplier);

            return Mathf.Max(1f, fallbackCriticalMultiplier);
        }

        private static long MultiplyLong(long value, float multiplier)
        {
            if (value <= 0 || multiplier <= 0f)
                return 0;

            double result = value * (double)multiplier;
            return result >= long.MaxValue ? long.MaxValue : (long)result;
        }

        private void CreateSlashRenderer()
        {
            slashObject = new GameObject("_AutoAttackSlash");
            slashObject.transform.SetParent(transform, false);
            slashObject.SetActive(false);

            slashRenderer = slashObject.AddComponent<LineRenderer>();
            slashRenderer.useWorldSpace = false;
            slashRenderer.loop = false;
            slashRenderer.positionCount = 9;
            slashRenderer.numCapVertices = 2;
            slashRenderer.widthMultiplier = slashWidth;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                slashMaterial = new Material(shader);
                slashRenderer.material = slashMaterial;
            }

            SpriteRenderer playerSprite = GetComponentInChildren<SpriteRenderer>();
            if (playerSprite != null)
            {
                slashRenderer.sortingLayerID = playerSprite.sortingLayerID;
                slashRenderer.sortingOrder = playerSprite.sortingOrder + 5;
            }

            BuildSlashArc();
        }

        private void BuildSlashArc()
        {
            if (slashRenderer == null)
                return;

            const int pointCount = 9;
            slashRenderer.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)(pointCount - 1);
                float angle = Mathf.Lerp(-55f, 55f, t) * Mathf.Deg2Rad;

                slashRenderer.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * slashRadius,
                    Mathf.Sin(angle) * slashRadius,
                    0f));
            }
        }

        private IEnumerator PlaySlash(Vector2 direction)
        {
            if (slashObject == null || slashRenderer == null)
                yield break;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            slashObject.transform.localPosition = Vector3.zero;
            slashObject.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            slashObject.SetActive(true);

            float elapsed = 0f;

            while (elapsed < slashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slashDuration);

                Color c = slashColor;
                c.a = 1f - t;

                slashRenderer.startColor = c;
                slashRenderer.endColor = c;
                slashRenderer.widthMultiplier = Mathf.Lerp(slashWidth, slashWidth * 0.35f, t);
                yield return null;
            }

            slashObject.SetActive(false);
            slashRoutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            float range = fallbackAttackRange;

            if (Application.isPlaying && usePlayerManagerStats && playerManager != null)
                range = GetAttackRange();

            Gizmos.DrawWireSphere(transform.position, range);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            fallbackAttack = Math.Max(1L, fallbackAttack);
            fallbackAttackRange = Mathf.Max(0.1f, fallbackAttackRange);
            fallbackAttackSpeed = Mathf.Max(0.1f, fallbackAttackSpeed);
            fallbackCriticalMultiplier = Mathf.Max(1f, fallbackCriticalMultiplier);
            targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
            slashDuration = Mathf.Max(0.03f, slashDuration);
            slashWidth = Mathf.Max(0.01f, slashWidth);
            slashRadius = Mathf.Max(0.1f, slashRadius);

            if (string.IsNullOrWhiteSpace(enemyTag))
                enemyTag = "Enemy";

            if (slashRenderer != null)
            {
                slashRenderer.widthMultiplier = slashWidth;
                BuildSlashArc();
            }
        }
#endif
    }
}
