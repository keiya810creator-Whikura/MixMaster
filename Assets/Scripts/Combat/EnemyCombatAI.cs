using System;
using System.Collections;
using UnityEngine;
using MixMaster.Monsters;

namespace MixMaster.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public sealed class EnemyCombatAI : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float detectionRange = 4f;
        [SerializeField, Min(0.1f)] private float loseTargetRange = 6f;
        [SerializeField, Min(0.02f)] private float targetRefreshInterval = 0.12f;

        [Header("Combat Stats")]
        [SerializeField, Min(1)] private long attack = 8;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float attackRange = 0.9f;
        [SerializeField, Min(0.1f)] private float attackSpeed = 0.8f;
        [SerializeField, Range(0f, 1f)] private float criticalRate = 0.03f;
        [SerializeField, Min(1f)] private float criticalMultiplier = 1.5f;

        [Header("Movement")]
        [SerializeField, Range(0.5f, 1f)] private float stopRangeRatio = 0.85f;

        [Header("Attack Lunge")]
        [SerializeField, Min(0f)] private float lungeDistance = 0.12f;
        [SerializeField, Min(0.04f)] private float lungeDuration = 0.12f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool flipSpriteHorizontally = true;

        [Header("Attack Feedback")]
        [SerializeField] private Color slashColor = new Color(1f, 0.55f, 0.4f, 1f);
        [SerializeField, Min(0.03f)] private float slashDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.07f;
        [SerializeField, Min(0.1f)] private float slashRadius = 0.55f;

        private Rigidbody2D body;
        private EnemyHealth health;
        private EnemyWanderAI wanderAI;

        private PlayerAutoAttack playerTarget;
        private PartyMemberCombat partyTarget;
        private Transform targetTransform;

        private float targetRefreshTimer;
        private float attackGauge;

        private bool isLunging;
        private Vector2 lungeStartPosition;
        private Coroutine lungeRoutine;

        private GameObject slashObject;
        private LineRenderer slashRenderer;
        private Material slashMaterial;
        private Coroutine slashRoutine;

        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public bool HasTarget => targetTransform != null;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<EnemyHealth>();
            wanderAI = GetComponent<EnemyWanderAI>();

            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            CreateSlashRenderer();
        }

        private void OnDisable()
        {
            ClearTarget();

            if (isLunging && body != null)
                body.position = lungeStartPosition;

            isLunging = false;

            if (wanderAI != null)
                wanderAI.SetCombatControlled(false);
        }

        private void OnDestroy()
        {
            if (slashMaterial != null)
                Destroy(slashMaterial);
        }

        private void Update()
        {
            if (health == null || !health.IsAlive)
                return;

            targetRefreshTimer -= Time.deltaTime;

            if (targetRefreshTimer <= 0f || !IsCurrentTargetValid())
            {
                targetRefreshTimer = targetRefreshInterval;
                FindNearestTarget();
            }

            bool hasTarget = IsCurrentTargetValid();

            if (wanderAI != null)
                wanderAI.SetCombatControlled(hasTarget || isLunging);

            if (!hasTarget)
            {
                attackGauge = 0f;
                return;
            }

            attackGauge = Mathf.Clamp01(
                attackGauge + Mathf.Max(0.1f, attackSpeed) * Time.deltaTime);

            Vector2 toTarget = targetTransform.position - transform.position;

            if (flipSpriteHorizontally &&
                spriteRenderer != null &&
                Mathf.Abs(toTarget.x) > 0.01f)
            {
                spriteRenderer.flipX = toTarget.x < 0f;
            }

            float range = Mathf.Max(0.1f, attackRange);

            if (toTarget.sqrMagnitude > range * range ||
                attackGauge < 1f ||
                isLunging)
            {
                return;
            }

            AttackTarget(toTarget.normalized);
            attackGauge = 0f;
        }

        private void FixedUpdate()
        {
            if (health == null ||
                !health.IsAlive ||
                isLunging ||
                !IsCurrentTargetValid())
            {
                return;
            }

            Vector2 current = body.position;
            Vector2 target = targetTransform.position;
            Vector2 delta = target - current;

            float stopDistance =
                Mathf.Max(0.05f, attackRange * stopRangeRatio);

            if (delta.sqrMagnitude <= stopDistance * stopDistance)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 next = Vector2.MoveTowards(
                current,
                target,
                Mathf.Max(0.1f, moveSpeed) * Time.fixedDeltaTime);

            body.MovePosition(next);
        }

        private void FindNearestTarget()
        {
            ClearTarget();

            float detectionSqr = detectionRange * detectionRange;
            float bestDistanceSqr = float.MaxValue;

            PlayerAutoAttack player = FindFirstObjectByType<PlayerAutoAttack>();

            if (player != null && player.IsAlive)
            {
                float distanceSqr =
                    ((Vector2)(player.transform.position - transform.position))
                    .sqrMagnitude;

                if (distanceSqr <= detectionSqr &&
                    distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    playerTarget = player;
                    targetTransform = player.transform;
                }
            }

            var members = PartyMemberCombat.ActiveMembers;

            for (int i = 0; i < members.Count; i++)
            {
                PartyMemberCombat member = members[i];

                if (member == null || !member.IsAlive)
                    continue;

                float distanceSqr =
                    ((Vector2)(member.transform.position - transform.position))
                    .sqrMagnitude;

                if (distanceSqr > detectionSqr ||
                    distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                playerTarget = null;
                partyTarget = member;
                targetTransform = member.transform;
            }
        }

        private bool IsCurrentTargetValid()
        {
            if (targetTransform == null)
                return false;

            if (playerTarget != null && !playerTarget.IsAlive)
                return false;

            if (partyTarget != null && !partyTarget.IsAlive)
                return false;

            float loseRange = Mathf.Max(detectionRange, loseTargetRange);
            float distanceSqr =
                ((Vector2)(targetTransform.position - transform.position))
                .sqrMagnitude;

            return distanceSqr <= loseRange * loseRange;
        }

        private void AttackTarget(Vector2 direction)
        {
            long attackPower = Math.Max(1L, attack);

            if (UnityEngine.Random.value < Mathf.Clamp01(criticalRate))
                attackPower = MultiplyLong(
                    attackPower,
                    Mathf.Max(1f, criticalMultiplier));

            if (playerTarget != null)
                playerTarget.TakePhysicalHit(attackPower);
            else if (partyTarget != null)
                partyTarget.TakePhysicalHit(attackPower);
            else
                return;

            if (slashRoutine != null)
                StopCoroutine(slashRoutine);

            slashRoutine = StartCoroutine(PlaySlash(direction));
            StartAttackLunge(direction);
        }

        private void ClearTarget()
        {
            playerTarget = null;
            partyTarget = null;
            targetTransform = null;
        }

        private void StartAttackLunge(Vector2 direction)
        {
            if (body == null ||
                direction.sqrMagnitude <= 0.000001f ||
                lungeDistance <= 0f)
            {
                return;
            }

            if (lungeRoutine != null)
            {
                StopCoroutine(lungeRoutine);

                if (isLunging)
                    body.position = lungeStartPosition;
            }

            lungeRoutine = StartCoroutine(
                AttackLungeRoutine(direction.normalized));
        }

        private IEnumerator AttackLungeRoutine(Vector2 direction)
        {
            isLunging = true;
            lungeStartPosition = body.position;

            Vector2 forwardPosition =
                lungeStartPosition + direction * lungeDistance;

            float halfDuration =
                Mathf.Max(0.02f, lungeDuration * 0.5f);

            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = Mathf.Sin(t * Mathf.PI * 0.5f);

                body.MovePosition(
                    Vector2.Lerp(
                        lungeStartPosition,
                        forwardPosition,
                        eased));

                yield return new WaitForFixedUpdate();
            }

            elapsed = 0f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float eased = t * t;

                body.MovePosition(
                    Vector2.Lerp(
                        forwardPosition,
                        lungeStartPosition,
                        eased));

                yield return new WaitForFixedUpdate();
            }

            body.MovePosition(lungeStartPosition);
            isLunging = false;
            lungeRoutine = null;
        }

        private void CreateSlashRenderer()
        {
            slashObject = new GameObject("_EnemyAttackSlash");
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

            if (spriteRenderer != null)
            {
                slashRenderer.sortingLayerID =
                    spriteRenderer.sortingLayerID;

                slashRenderer.sortingOrder =
                    spriteRenderer.sortingOrder + 5;
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
                float angle =
                    Mathf.Lerp(-55f, 55f, t) * Mathf.Deg2Rad;

                slashRenderer.SetPosition(
                    i,
                    new Vector3(
                        Mathf.Cos(angle) * slashRadius,
                        Mathf.Sin(angle) * slashRadius,
                        0f));
            }
        }

        private IEnumerator PlaySlash(Vector2 direction)
        {
            if (slashObject == null || slashRenderer == null)
                yield break;

            float angle =
                Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg;

            slashObject.transform.localRotation =
                Quaternion.Euler(0f, 0f, angle);

            slashObject.SetActive(true);

            float elapsed = 0f;

            while (elapsed < slashDuration)
            {
                elapsed += Time.deltaTime;
                float t =
                    Mathf.Clamp01(elapsed / slashDuration);

                Color c = slashColor;
                c.a = 1f - t;

                slashRenderer.startColor = c;
                slashRenderer.endColor = c;

                slashRenderer.widthMultiplier =
                    Mathf.Lerp(
                        slashWidth,
                        slashWidth * 0.35f,
                        t);

                yield return null;
            }

            slashObject.SetActive(false);
            slashRoutine = null;
        }

        private static long MultiplyLong(
            long value,
            float multiplier)
        {
            if (value <= 0L || multiplier <= 0f)
                return 0L;

            double result = value * (double)multiplier;

            return result >= long.MaxValue
                ? long.MaxValue
                : (long)result;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(
                transform.position,
                detectionRange);

            Gizmos.DrawWireSphere(
                transform.position,
                Mathf.Max(detectionRange, loseTargetRange));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            detectionRange = Mathf.Max(0.1f, detectionRange);
            loseTargetRange =
                Mathf.Max(detectionRange, loseTargetRange);

            targetRefreshInterval =
                Mathf.Max(0.02f, targetRefreshInterval);

            attack = Math.Max(1L, attack);
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackSpeed = Mathf.Max(0.1f, attackSpeed);

            criticalMultiplier =
                Mathf.Max(1f, criticalMultiplier);

            lungeDistance = Mathf.Max(0f, lungeDistance);
            lungeDuration = Mathf.Max(0.04f, lungeDuration);

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
