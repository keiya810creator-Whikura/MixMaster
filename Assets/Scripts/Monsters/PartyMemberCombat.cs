using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MixMaster.Combat;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.UI;

namespace MixMaster.Monsters
{
    [RequireComponent(typeof(MonsterTrailFollower))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PartyMemberCombat : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private CharacterStats stats = new CharacterStats();

        [Header("UI")]
        [SerializeField] private Slider hpSlider;
        [Tooltip("0 to 1 attack charge gauge. When full, this monster attacks.")]
        [SerializeField] private Slider attackSpeedSlider;
        [SerializeField] private bool chargeWhileNoTarget = true;

        [Header("Targeting")]
        [SerializeField] private bool requireEnemyTag = true;
        [SerializeField] private string enemyTag = "Enemy";
        [SerializeField, Min(0.1f)] private float aggroRange = 4f;
        [SerializeField, Min(0.5f)] private float maxCombatDistanceFromPlayer = 7f;
        [SerializeField, Min(0.02f)] private float targetRefreshInterval = 0.12f;

        [Header("Combat Movement")]
        [Tooltip("Multiplier applied to the monster's moveSpeed while approaching an enemy.")]
        [SerializeField, Min(0.1f)] private float combatMoveSpeedMultiplier = 1.15f;
        [Tooltip("Distance from the assigned attack slot considered close enough.")]
        [SerializeField, Min(0.01f)] private float attackSlotArrivalDistance = 0.08f;
        [Tooltip("How far from the enemy each ally tries to stand, as a ratio of attack range.")]
        [SerializeField, Range(0.35f, 0.9f)] private float attackPositionRadiusRatio = 0.72f;
        [Tooltip("First ally flanks by this many degrees. Additional allies alternate left/right.")]
        [SerializeField, Range(10f, 80f)] private float attackSlotAngleStep = 55f;

        [Header("Attack Lunge")]
        [SerializeField, Min(0f)] private float lungeDistance = 0.16f;
        [SerializeField, Min(0.04f)] private float lungeDuration = 0.12f;

        [Header("Hit Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.08f;

        [Header("Attack Feedback")]
        [SerializeField] private Color slashColor = new Color(0.75f, 0.95f, 1f, 1f);
        [SerializeField, Min(0.03f)] private float slashDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.07f;
        [SerializeField, Min(0.1f)] private float slashRadius = 0.65f;

        private MonsterTrailFollower follower;
        private Rigidbody2D body;
        private Transform playerTransform;
        private EnemyHealth currentTarget;

        private long currentHp;
        private bool isAlive = true;
        private float attackGauge;
        private float targetRefreshTimer;

        private Color originalColor = Color.white;
        private Coroutine hitFlashRoutine;

        private GameObject slashObject;
        private LineRenderer slashRenderer;
        private Material slashMaterial;
        private Coroutine slashRoutine;

        private Coroutine lungeRoutine;
        private bool isAttackLunging;
        private Vector2 lungeStartPosition;

        public CharacterStats Stats => stats;
        public long CurrentHp => currentHp;
        public bool IsAlive => isAlive;
        public EnemyHealth CurrentTarget => currentTarget;
        public float AttackGauge => attackGauge;

        public event Action<long, long> HpChanged;
        public event Action Died;

        private void Awake()
        {
            follower = GetComponent<MonsterTrailFollower>();
            body = GetComponent<Rigidbody2D>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer != null)
                originalColor = spriteRenderer.color;

            currentHp = Math.Max(1L, stats.maxHp);
            isAlive = true;

            ConfigureSlider(hpSlider);
            ConfigureSlider(attackSpeedSlider);
            RefreshHpSlider();
            SetAttackGauge(0f);
            CreateSlashRenderer();
        }

        private void OnEnable()
        {
            WorldUIManager.TryRegisterParty(this);
        }

        private void Start()
        {
            PlayerTrailRecorder trail = FindFirstObjectByType<PlayerTrailRecorder>();
            if (trail != null)
                playerTransform = trail.transform;
        }

        private void OnDisable()
        {
            WorldUIManager.TryUnregisterParty(this);

            if (isAttackLunging && body != null)
                body.position = lungeStartPosition;

            isAttackLunging = false;

            if (follower != null)
            {
                follower.SetCombatControlled(false);
                follower.StopCombatMovement();
            }
        }

        private void OnDestroy()
        {
            if (slashMaterial != null)
                Destroy(slashMaterial);
        }

        private void Update()
        {
            if (!isAlive)
                return;

            targetRefreshTimer -= Time.deltaTime;

            if (targetRefreshTimer <= 0f || !IsTargetValid(currentTarget))
            {
                targetRefreshTimer = targetRefreshInterval;
                currentTarget = FindNearestTarget();
            }

            bool hasTarget = IsTargetValid(currentTarget);
            follower.SetCombatControlled(hasTarget || isAttackLunging);

            if (chargeWhileNoTarget || hasTarget)
                SetAttackGauge(attackGauge + Mathf.Max(0.1f, stats.attackSpeed) * Time.deltaTime);

            if (!hasTarget || isAttackLunging)
                return;

            Vector2 toTarget = currentTarget.transform.position - transform.position;
            float attackRange = Mathf.Max(0.1f, stats.attackRange);

            if (toTarget.sqrMagnitude > attackRange * attackRange)
                return;

            Vector2 attackPosition = GetDesiredAttackPosition(currentTarget);
            float slotDistance = Vector2.Distance(transform.position, attackPosition);

            if (slotDistance > attackSlotArrivalDistance)
                return;

            follower.StopCombatMovement();
            follower.FaceDirection(toTarget);

            if (attackGauge < 1f)
                return;

            Attack(currentTarget);
            SetAttackGauge(0f);
        }

        private void FixedUpdate()
        {
            if (!isAlive || isAttackLunging || !IsTargetValid(currentTarget))
                return;

            Vector2 attackPosition = GetDesiredAttackPosition(currentTarget);
            float distanceToSlot = Vector2.Distance(body.position, attackPosition);

            if (distanceToSlot <= attackSlotArrivalDistance)
            {
                follower.StopCombatMovement();
                return;
            }

            float moveSpeed = Mathf.Max(0.1f, stats.moveSpeed) * combatMoveSpeedMultiplier;
            follower.MoveForCombat(
                attackPosition,
                moveSpeed,
                attackSlotArrivalDistance);
        }

        private Vector2 GetDesiredAttackPosition(EnemyHealth target)
        {
            if (target == null)
                return body != null ? body.position : (Vector2)transform.position;

            Vector2 enemyPosition = target.transform.position;
            float attackRange = Mathf.Max(0.1f, stats.attackRange);
            float radius = Mathf.Max(0.08f, attackRange * attackPositionRadiusRatio);
            radius = Mathf.Min(radius, attackRange * 0.9f);

            Vector2 baseDirection = Vector2.down;

            if (playerTransform != null)
            {
                baseDirection = (Vector2)playerTransform.position - enemyPosition;

                if (baseDirection.sqrMagnitude > 0.0001f)
                    baseDirection.Normalize();
                else
                    baseDirection = Vector2.down;
            }

            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            float slotOffset = GetSlotAngleOffset(follower.FollowOrder);
            float angle = (baseAngle + slotOffset) * Mathf.Deg2Rad;

            Vector2 slotDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return enemyPosition + slotDirection * radius;
        }

        private float GetSlotAngleOffset(int order)
        {
            int pairIndex = order / 2;
            float magnitude = attackSlotAngleStep * (pairIndex + 1);
            return order % 2 == 0 ? magnitude : -magnitude;
        }

        private void Attack(EnemyHealth target)
        {
            if (target == null || !target.IsAlive)
                return;

            Vector2 direction = (target.transform.position - transform.position).normalized;
            follower.FaceDirection(direction);

            long attackPower = Math.Max(1L, stats.attack);

            if (UnityEngine.Random.value < Mathf.Clamp01((float)stats.criticalRate))
                attackPower = MultiplyLong(
                    attackPower,
                    Mathf.Max(1f, (float)stats.criticalMultiplier));

            target.TakePhysicalHit(attackPower);

            if (slashRoutine != null)
                StopCoroutine(slashRoutine);

            slashRoutine = StartCoroutine(PlaySlash(direction));
            StartAttackLunge(direction);
        }

        private EnemyHealth FindNearestTarget()
        {
            float searchRange = Mathf.Max(aggroRange, stats.attackRange);
            float searchRangeSqr = searchRange * searchRange;
            float maxFromPlayerSqr = maxCombatDistanceFromPlayer * maxCombatDistanceFromPlayer;

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

                Vector2 fromMember = enemy.transform.position - transform.position;
                float memberDistanceSqr = fromMember.sqrMagnitude;

                if (memberDistanceSqr > searchRangeSqr)
                    continue;

                if (playerTransform != null)
                {
                    Vector2 fromPlayer = enemy.transform.position - playerTransform.position;
                    if (fromPlayer.sqrMagnitude > maxFromPlayerSqr)
                        continue;
                }

                if (memberDistanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = memberDistanceSqr;
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

            if (playerTransform != null)
            {
                float maxSqr = maxCombatDistanceFromPlayer * maxCombatDistanceFromPlayer;
                Vector2 fromPlayer = target.transform.position - playerTransform.position;

                if (fromPlayer.sqrMagnitude > maxSqr)
                    return false;
            }

            return true;
        }

        public long TakePhysicalHit(long attackPower)
        {
            if (!isAlive || attackPower <= 0)
                return 0;

            long damage = attackPower - Math.Max(0L, stats.defense);

            if (damage < 1)
                damage = 1;

            return TakeDamage(damage);
        }

        public long TakeDamage(long damage)
        {
            if (!isAlive || damage <= 0)
                return 0;

            long actual = Math.Min(currentHp, damage);
            currentHp -= actual;

            RefreshHpSlider();
            HpChanged?.Invoke(currentHp, Math.Max(1L, stats.maxHp));

            if (spriteRenderer != null)
            {
                if (hitFlashRoutine != null)
                    StopCoroutine(hitFlashRoutine);

                hitFlashRoutine = StartCoroutine(HitFlashRoutine());
            }

            if (currentHp <= 0)
                Die();

            return actual;
        }

        public void Heal(long amount)
        {
            if (!isAlive || amount <= 0)
                return;

            currentHp = Math.Min(
                Math.Max(1L, stats.maxHp),
                LongMath.SaturatingAdd(currentHp, amount));

            RefreshHpSlider();
            HpChanged?.Invoke(currentHp, Math.Max(1L, stats.maxHp));
        }

        public void RestoreFullHp()
        {
            currentHp = Math.Max(1L, stats.maxHp);
            isAlive = true;

            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;

            RefreshHpSlider();
            HpChanged?.Invoke(currentHp, Math.Max(1L, stats.maxHp));
        }

        private void Die()
        {
            if (!isAlive)
                return;

            if (lungeRoutine != null)
            {
                StopCoroutine(lungeRoutine);
                lungeRoutine = null;
            }

            if (isAttackLunging && body != null)
                body.position = lungeStartPosition;

            isAttackLunging = false;
            isAlive = false;
            currentTarget = null;

            follower.SetCombatControlled(true);
            follower.StopCombatMovement();

            SetAttackGauge(0f);
            RefreshHpSlider();

            if (spriteRenderer != null)
            {
                Color c = originalColor;
                c.a = 0.4f;
                spriteRenderer.color = c;
            }

            Died?.Invoke();
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);

            if (spriteRenderer != null && isAlive)
                spriteRenderer.color = originalColor;

            hitFlashRoutine = null;
        }

        private void SetAttackGauge(float value)
        {
            attackGauge = Mathf.Clamp01(value);

            if (attackSpeedSlider != null)
                attackSpeedSlider.SetValueWithoutNotify(attackGauge);
        }

        private void RefreshHpSlider()
        {
            if (hpSlider == null)
                return;

            long max = Math.Max(1L, stats.maxHp);
            float normalized = Mathf.Clamp01((float)((double)currentHp / max));
            hpSlider.SetValueWithoutNotify(normalized);
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

                if (isAttackLunging)
                    body.position = lungeStartPosition;
            }

            lungeRoutine = StartCoroutine(AttackLungeRoutine(direction.normalized));
        }

        private IEnumerator AttackLungeRoutine(Vector2 direction)
        {
            isAttackLunging = true;
            lungeStartPosition = body.position;
            follower.SetCombatControlled(true);
            follower.StopCombatMovement();

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
            isAttackLunging = false;
            lungeRoutine = null;
        }

        private void CreateSlashRenderer()
        {
            slashObject = new GameObject("_PartyAttackSlash");
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
                slashRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                slashRenderer.sortingOrder = spriteRenderer.sortingOrder + 5;
            }

            BuildSlashArc();
        }

        private void BuildSlashArc()
        {
            if (slashRenderer == null)
                return;

            const int pointCount = 9;

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
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(aggroRange, stats.attackRange));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stats.maxHp = Math.Max(1L, stats.maxHp);
            stats.attack = Math.Max(1L, stats.attack);
            stats.defense = Math.Max(0L, stats.defense);
            stats.attackSpeed = Mathf.Max(0.1f, stats.attackSpeed);
            stats.attackRange = Mathf.Max(0.1f, stats.attackRange);
            stats.moveSpeed = Mathf.Max(0.1f, stats.moveSpeed);

            aggroRange = Mathf.Max(0.1f, aggroRange);
            maxCombatDistanceFromPlayer = Mathf.Max(0.5f, maxCombatDistanceFromPlayer);
            targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
            combatMoveSpeedMultiplier = Mathf.Max(0.1f, combatMoveSpeedMultiplier);
            attackSlotArrivalDistance = Mathf.Max(0.01f, attackSlotArrivalDistance);
            lungeDistance = Mathf.Max(0f, lungeDistance);
            lungeDuration = Mathf.Max(0.04f, lungeDuration);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
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
