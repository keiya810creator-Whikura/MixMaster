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
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerAutoAttack : MonoBehaviour
    {
        [Header("Stats Source")]
        [Tooltip("Use PlayerManager CharacterStats for attack/range/speed/critical.")]
        [SerializeField] private bool usePlayerManagerStats = true;

        [Header("UI")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text hpText;
        [Tooltip("0 to 1 attack charge gauge. When full, the player attacks.")]
        [SerializeField] private Slider attackSpeedSlider;
        [SerializeField] private Text attackTimeText;
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

        [Header("Attack Lunge")]
        [SerializeField, Min(0f)] private float lungeDistance = 0.18f;
        [SerializeField, Min(0.04f)] private float lungeDuration = 0.12f;

        [Header("Attack Feedback")]
        [SerializeField] private Color slashColor = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField, Min(0.03f)] private float slashDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.08f;
        [SerializeField, Min(0.1f)] private float slashRadius = 0.75f;

        private PlayerController playerController;
        private Rigidbody2D body;
        private PlayerManager playerManager;
        private EnemyHealth currentTarget;

        private float targetRefreshTimer;
        private float attackGauge;

        private GameObject slashObject;
        private LineRenderer slashRenderer;
        private Material slashMaterial;
        private Coroutine slashRoutine;

        private Coroutine lungeRoutine;
        private bool isLunging;
        private Vector2 lungeStartPosition;

        public EnemyHealth CurrentTarget => currentTarget;
        public float AttackGauge => attackGauge;
        public bool IsLunging => isLunging;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            body = GetComponent<Rigidbody2D>();

            if (hpText == null && hpSlider != null)
                hpText = hpSlider.GetComponentInChildren<Text>(true);

            if (attackTimeText == null && attackSpeedSlider != null)
                attackTimeText = attackSpeedSlider.GetComponentInChildren<Text>(true);

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
            else
            {
                RefreshHpSlider(1L, 1L);
            }

            RefreshAttackTimeText();
        }

        private void OnDisable()
        {
            if (isLunging && body != null)
                body.position = lungeStartPosition;

            isLunging = false;

            if (playerController != null)
                playerController.SetCombatMovementLocked(false);
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
            else
            {
                RefreshAttackTimeText();
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
            StartAttackLunge(direction);
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

            RefreshAttackTimeText();
        }

        private void RefreshAttackTimeText()
        {
            if (attackTimeText == null)
                return;

            float speed = Mathf.Max(0.1f, GetAttackSpeed());
            float remainingSeconds = Mathf.Max(0f, (1f - attackGauge) / speed);
            attackTimeText.text = $"攻撃まで{remainingSeconds:0.0}秒";
        }

        private void HandlePlayerHpChanged(long current, long max)
        {
            RefreshHpSlider(current, max);
        }

        private void RefreshHpSlider(long current, long max)
        {
            long safeMax = Math.Max(1L, max);
            long safeCurrent = Math.Max(0L, Math.Min(current, safeMax));
            double normalized = (double)safeCurrent / safeMax;

            if (hpSlider != null)
                hpSlider.SetValueWithoutNotify(Mathf.Clamp01((float)normalized));

            if (hpText != null)
                hpText.text = $"HP {safeCurrent:N0}/{safeMax:N0}";
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

        private void StartAttackLunge(Vector2 direction)
        {
            if (body == null || direction.sqrMagnitude <= 0.000001f || lungeDistance <= 0f)
                return;

            if (lungeRoutine != null)
            {
                StopCoroutine(lungeRoutine);

                if (isLunging)
                    body.position = lungeStartPosition;

                playerController.SetCombatMovementLocked(false);
            }

            lungeRoutine = StartCoroutine(AttackLungeRoutine(direction.normalized));
        }

        private IEnumerator AttackLungeRoutine(Vector2 direction)
        {
            isLunging = true;
            lungeStartPosition = body.position;
            playerController.SetCombatMovementLocked(true);

            Vector2 forwardPosition = lungeStartPosition + direction * lungeDistance;
            float halfDuration = Mathf.Max(0.02f, lungeDuration * 0.5f);

            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = Mathf.Sin(t * Mathf.PI * 0.5f);
                body.MovePosition(Vector2.Lerp(lungeStartPosition, forwardPosition, eased));
                yield return new WaitForFixedUpdate();
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = t * t;
                body.MovePosition(Vector2.Lerp(forwardPosition, lungeStartPosition, eased));
                yield return new WaitForFixedUpdate();
            }

            body.MovePosition(lungeStartPosition);
            playerController.SetCombatMovementLocked(false);
            isLunging = false;
            lungeRoutine = null;
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
            lungeDistance = Mathf.Max(0f, lungeDistance);
            lungeDuration = Mathf.Max(0.04f, lungeDuration);
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
