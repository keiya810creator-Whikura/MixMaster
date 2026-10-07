using System.Collections;
using UnityEngine;
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

        [Header("Fallback Stats")]
        [SerializeField, Min(1)] private long fallbackAttack = 10;
        [SerializeField, Min(0.1f)] private float fallbackAttackRange = 1.5f;
        [SerializeField, Min(0.1f)] private float fallbackAttackSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float fallbackCriticalRate = 0.05f;
        [SerializeField, Min(1f)] private float fallbackCriticalMultiplier = 1.5f;

        [Header("Targeting")]
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
        private float nextAttackTime;

        private GameObject slashObject;
        private LineRenderer slashRenderer;
        private Material slashMaterial;
        private Coroutine slashRoutine;

        public EnemyHealth CurrentTarget => currentTarget;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            CreateSlashRenderer();
        }

        private void Start()
        {
            if (usePlayerManagerStats)
                playerManager = FindFirstObjectByType<PlayerManager>();
        }

        private void OnDestroy()
        {
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

            if (!IsTargetValid(currentTarget))
                return;

            if (Time.time < nextAttackTime)
                return;

            Attack(currentTarget);
        }

        private void Attack(EnemyHealth target)
        {
            if (target == null || !target.IsAlive)
                return;

            Vector2 direction = (target.transform.position - transform.position).normalized;

            if (!playerController.IsMoving)
                playerController.FaceDirection(direction);

            long attackPower = GetAttackPower();
            bool isCritical = Random.value < GetCriticalRate();

            if (isCritical)
                attackPower = MultiplyLong(attackPower, GetCriticalMultiplier());

            target.TakePhysicalHit(attackPower);

            if (slashRoutine != null)
                StopCoroutine(slashRoutine);

            slashRoutine = StartCoroutine(PlaySlash(direction));

            float speed = Mathf.Max(0.1f, GetAttackSpeed());
            nextAttackTime = Time.time + (1f / speed);
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

            float range = GetAttackRange();
            return ((Vector2)(target.transform.position - transform.position)).sqrMagnitude <= range * range;
        }

        private long GetAttackPower()
        {
            if (usePlayerManagerStats && playerManager != null)
                return Mathf.Max(1, (int)Mathf.Min(playerManager.Stats.attack, int.MaxValue)) == int.MaxValue
                    ? playerManager.Stats.attack
                    : Mathf.Max(1, (int)playerManager.Stats.attack);

            return Mathf.Max(1, (int)Mathf.Min(fallbackAttack, int.MaxValue)) == int.MaxValue
                ? fallbackAttack
                : Mathf.Max(1, (int)fallbackAttack);
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

            if (result >= long.MaxValue)
                return long.MaxValue;

            return (long)result;
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

                Vector3 point = new Vector3(
                    Mathf.Cos(angle) * slashRadius,
                    Mathf.Sin(angle) * slashRadius,
                    0f);

                slashRenderer.SetPosition(i, point);
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
            fallbackAttack = System.Math.Max(1L, fallbackAttack);
            fallbackAttackRange = Mathf.Max(0.1f, fallbackAttackRange);
            fallbackAttackSpeed = Mathf.Max(0.1f, fallbackAttackSpeed);
            fallbackCriticalMultiplier = Mathf.Max(1f, fallbackCriticalMultiplier);
            targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
            slashDuration = Mathf.Max(0.03f, slashDuration);
            slashWidth = Mathf.Max(0.01f, slashWidth);
            slashRadius = Mathf.Max(0.1f, slashRadius);

            if (slashRenderer != null)
            {
                slashRenderer.widthMultiplier = slashWidth;
                BuildSlashArc();
            }
        }
#endif
    }
}
