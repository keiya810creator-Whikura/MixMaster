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
    public enum PartyCombatMode
    {
        Follow,
        AutoHunt
    }

    public enum PartyAttackStyle
    {
        Melee,
        Ranged
    }

    [RequireComponent(typeof(MonsterTrailFollower))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PartyMemberCombat : MonoBehaviour
    {
        private static readonly List<PartyMemberCombat> activeMembers =
            new List<PartyMemberCombat>();

        public static IReadOnlyList<PartyMemberCombat> ActiveMembers => activeMembers;

        [Header("Behavior")]
        [SerializeField] private PartyCombatMode combatMode = PartyCombatMode.Follow;
        [SerializeField] private PartyAttackStyle attackStyle = PartyAttackStyle.Melee;

        [Header("Ranged Attack")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 3f;
        [SerializeField] private bool projectileHoming = true;
        [Tooltip("Legacy value kept for existing prefabs. Ranged spacing is now automatically about 3x melee spacing.")]
        [SerializeField, HideInInspector] private float rangedPositionRadiusRatio = 0.82f;

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
        [Tooltip("Distance from the assigned fixed attack slot considered close enough.")]
        [SerializeField, Min(0.01f)] private float attackSlotArrivalDistance = 0.18f;
        [Tooltip("Base spacing ratio. Melee stands at half of this value, Ranged stands at about three times the melee distance.")]
        [SerializeField, Range(0.2f, 0.9f)] private float attackPositionRadiusRatio = 0.72f;

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
        private PlayerAutoAttack playerAutoAttack;
        private EnemyHealth currentTarget;

        private long currentHp;
        private long currentMp;
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
        public long CurrentMp => currentMp;
        public float DropRateBonus => Mathf.Max(0f, stats.dropRateBonus);
        public bool IsAlive => isAlive;
        public EnemyHealth CurrentTarget => currentTarget;
        public float AttackGauge => attackGauge;
        public PartyCombatMode CombatMode => combatMode;
        public PartyAttackStyle AttackStyle => attackStyle;

        public event Action<long, long> HpChanged;
        public event Action<long, long> MpChanged;
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
            currentMp = Math.Max(0L, stats.maxMp);
            isAlive = true;

            ConfigureSlider(hpSlider);
            ConfigureSlider(attackSpeedSlider);
            RefreshHpSlider();
            SetAttackGauge(0f);
            CreateSlashRenderer();
        }

        private void OnEnable()
        {
            if (!activeMembers.Contains(this))
                activeMembers.Add(this);

            WorldUIManager.TryRegisterParty(this);
        }

        private void Start()
        {
            playerAutoAttack = FindFirstObjectByType<PlayerAutoAttack>();

            if (playerAutoAttack != null)
            {
                playerTransform = playerAutoAttack.transform;
            }
            else
            {
                PlayerTrailRecorder trail = FindFirstObjectByType<PlayerTrailRecorder>();
                if (trail != null)
                    playerTransform = trail.transform;
            }
        }

        private void OnDisable()
        {
            activeMembers.Remove(this);
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
            float attackRange = GetEffectiveAttackRange();

            if (toTarget.sqrMagnitude > attackRange * attackRange)
                return;

            Vector2 attackPosition = GetDesiredAttackPosition(currentTarget);
            float slotDistance = Vector2.Distance(transform.position, attackPosition);

            // The attack gauge may finish charging before reaching the assigned
            // slot. As long as the enemy is in range, attack from here.
            // FixedUpdate continues moving toward the slot when not lunging.
            if (slotDistance <= GetAttackSlotTolerance())
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

            float slotTolerance = GetAttackSlotTolerance();

            if (distanceToSlot <= slotTolerance)
            {
                follower.StopCombatMovement();
                return;
            }

            float moveSpeed =
                Mathf.Max(0.1f, stats.moveSpeed) *
                combatMoveSpeedMultiplier;

            follower.MoveForCombat(
                attackPosition,
                moveSpeed,
                slotTolerance);
        }

        private Vector2 GetDesiredAttackPosition(EnemyHealth target)
        {
            if (target == null)
                return body != null ? body.position : (Vector2)transform.position;

            Vector2 enemyPosition = target.transform.position;

            // Positioning distance is intentionally independent from the
            // doubled ranged attack reach.
            // Existing melee spacing was a bit too wide, so melee uses
            // half of the configured base ratio. Ranged stands at about
            // three times that melee distance.
            float baseAttackRange = Mathf.Max(0.1f, stats.attackRange);
            float meleeRadius = Mathf.Max(
                0.08f,
                baseAttackRange * attackPositionRadiusRatio * 0.5f);

            float radius = attackStyle == PartyAttackStyle.Ranged
                ? meleeRadius * 3f
                : meleeRadius;

            float effectiveAttackRange = GetEffectiveAttackRange();
            radius = Mathf.Min(radius, effectiveAttackRange * 0.9f);

            // The assigned side never rotates with the Player.
            // This prevents multiple allies from continuously chasing
            // moving/flipping attack slots around the same enemy.
            Vector2 slotDirection =
                GetFixedAttackSlotDirection(follower.FollowOrder);

            return enemyPosition + slotDirection * radius;
        }

        private static Vector2 GetFixedAttackSlotDirection(int followOrder)
        {
            // Party order:
            // 0 = Left, 1 = Down, 2 = Right, 3 = Up.
            // Extra members use fixed diagonal positions.
            switch (Mathf.Abs(followOrder) % 8)
            {
                case 0: return Vector2.left;
                case 1: return Vector2.down;
                case 2: return Vector2.right;
                case 3: return Vector2.up;
                case 4: return new Vector2(-1f, -1f).normalized;
                case 5: return new Vector2(1f, -1f).normalized;
                case 6: return new Vector2(1f, 1f).normalized;
                default: return new Vector2(-1f, 1f).normalized;
            }
        }

        private float GetAttackSlotTolerance()
        {
            // A small minimum tolerance prevents allies from endlessly
            // correcting their position by tiny amounts.
            return Mathf.Max(0.18f, attackSlotArrivalDistance);
        }

        private void Attack(EnemyHealth target)
        {
            if (target == null || !target.IsAlive)
                return;

            Vector2 direction =
                (target.transform.position - transform.position).normalized;

            follower.FaceDirection(direction);

            if (attackStyle == PartyAttackStyle.Ranged)
            {
                long magicPower = Math.Max(1L, stats.magic);

                if (UnityEngine.Random.value <
                    Mathf.Clamp01((float)stats.criticalRate))
                {
                    magicPower = MultiplyLong(
                        magicPower,
                        Mathf.Max(1f, (float)stats.criticalMultiplier));
                }

                FireProjectile(target, magicPower);
                return;
            }

            long attackPower = Math.Max(1L, stats.attack);

            if (UnityEngine.Random.value <
                Mathf.Clamp01((float)stats.criticalRate))
            {
                attackPower = MultiplyLong(
                    attackPower,
                    Mathf.Max(1f, (float)stats.criticalMultiplier));
            }

            target.TakePhysicalHit(
                attackPower,
                CreateDropSourceInfo());

            if (slashRoutine != null)
                StopCoroutine(slashRoutine);

            slashRoutine = StartCoroutine(PlaySlash(direction));
            StartAttackLunge(direction);
        }

        private void FireProjectile(
            EnemyHealth target,
            long magicPower)
        {
            if (target == null || !target.IsAlive)
                return;

            if (projectilePrefab == null)
            {
                Debug.LogWarning(
                    "PartyMemberCombat: Ranged attack requires Projectile Prefab. " +
                    "Applying magic damage directly as fallback.",
                    this);

                target.TakeMagicHit(
                    magicPower,
                    CreateDropSourceInfo());

                return;
            }

            Vector3 spawnPosition =
                projectileSpawnPoint != null
                    ? projectileSpawnPoint.position
                    : transform.position;

            Quaternion spawnRotation =
                projectileSpawnPoint != null
                    ? projectileSpawnPoint.rotation
                    : Quaternion.identity;

            GameObject projectileObject =
                Instantiate(
                    projectilePrefab,
                    spawnPosition,
                    spawnRotation);

            // MonsterSO may point to the same projectile prefab enemies use.
            // Disable its enemy-only script so it cannot destroy or redirect
            // an allied projectile before PartyMagicProjectile initializes.
            EnemyMagicProjectile enemyProjectile =
                projectileObject.GetComponent<EnemyMagicProjectile>();

            if (enemyProjectile != null)
                enemyProjectile.enabled = false;

            PartyMagicProjectile projectile =
                projectileObject.GetComponent<PartyMagicProjectile>();

            if (projectile == null)
                projectile =
                    projectileObject.AddComponent<PartyMagicProjectile>();

            projectile.Initialize(
                target,
                magicPower,
                projectileSpeed,
                projectileLifetime,
                projectileHoming,
                CreateDropSourceInfo());
        }

        private MaterialDropSourceInfo CreateDropSourceInfo()
        {
            int followOrder =
                follower != null
                    ? follower.FollowOrder
                    : 0;

            return MaterialDropSourceInfo.Party(
                followOrder,
                DropRateBonus);
        }

        private EnemyHealth FindNearestTarget()
        {
            if (playerTransform == null)
                return null;

            float bestDistanceSqr = float.MaxValue;
            EnemyHealth best = null;
            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

            float playerRange = playerAutoAttack != null
                ? playerAutoAttack.AttackRange
                : Mathf.Max(0.1f, stats.attackRange);

            float playerRangeSqr = playerRange * playerRange;
            float autoHuntRange = Mathf.Max(aggroRange, GetEffectiveAttackRange());
            float autoHuntRangeSqr = autoHuntRange * autoHuntRange;
            float maxFromPlayerSqr =
                maxCombatDistanceFromPlayer * maxCombatDistanceFromPlayer;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];

                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (requireEnemyTag && !enemy.CompareTag(enemyTag))
                    continue;

                Vector2 fromPlayer = enemy.transform.position - playerTransform.position;
                Vector2 fromMember = enemy.transform.position - transform.position;
                float memberDistanceSqr = fromMember.sqrMagnitude;

                if (combatMode == PartyCombatMode.Follow)
                {
                    // Follow mode only fights enemies that are currently
                    // inside the player's own attack range.
                    if (fromPlayer.sqrMagnitude > playerRangeSqr)
                        continue;
                }
                else
                {
                    // Auto Hunt uses the party member's own detection range,
                    // but will not chase infinitely far away from the player.
                    if (memberDistanceSqr > autoHuntRangeSqr)
                        continue;

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

            if (playerTransform == null)
                return false;

            Vector2 fromPlayer = target.transform.position - playerTransform.position;

            if (combatMode == PartyCombatMode.Follow)
            {
                float playerRange = playerAutoAttack != null
                    ? playerAutoAttack.AttackRange
                    : Mathf.Max(0.1f, stats.attackRange);

                return fromPlayer.sqrMagnitude <= playerRange * playerRange;
            }

            float maxSqr =
                maxCombatDistanceFromPlayer * maxCombatDistanceFromPlayer;

            return fromPlayer.sqrMagnitude <= maxSqr;
        }

        private float GetEffectiveAttackRange()
        {
            float baseRange = Mathf.Max(0.1f, stats.attackRange);

            return attackStyle == PartyAttackStyle.Ranged
                ? baseRange * 2f
                : baseRange;
        }

        /// <summary>
        /// Applies the exact owned individual instead of the prefab's
        /// placeholder combat values.
        /// </summary>
        public void ConfigureOwnedMonster(
            MonsterSO definition,
            CharacterStats calculatedStats)
        {
            if (definition == null || calculatedStats == null)
                return;

            stats = calculatedStats;

            attackStyle = definition.attackType == MonsterAttackType.Ranged
                ? PartyAttackStyle.Ranged
                : PartyAttackStyle.Melee;

            projectilePrefab = definition.projectilePrefab;
            projectileSpeed = Mathf.Max(0.1f, definition.projectileSpeed);
            projectileLifetime = Mathf.Max(0.1f, definition.projectileLifetime);
            projectileHoming = definition.projectileHoming;
            aggroRange = Mathf.Max(0.1f, definition.detectionRange);

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer != null && definition.sprite != null)
            {
                spriteRenderer.sprite = definition.sprite;
                spriteRenderer.color = Color.white;
                originalColor = spriteRenderer.color;
            }

            currentTarget = null;
            targetRefreshTimer = 0f;
            attackGauge = 0f;
            RestoreFullResources();
            SetAttackGauge(0f);

            if (follower == null)
                follower = GetComponent<MonsterTrailFollower>();

            if (follower != null)
            {
                follower.SetCombatControlled(false);
                follower.StopCombatMovement();
            }
        }

        /// <summary>
        /// Applies new growth stats without recreating the follower, resetting
        /// the attack gauge, losing the target, or restoring an injured monster
        /// to full HP.
        /// </summary>
        public void ApplyLevelUpStats(CharacterStats newStats)
        {
            if (newStats == null)
                return;

            long oldMaxHp = Math.Max(1L, stats.maxHp);
            long oldMaxMp = Math.Max(0L, stats.maxMp);
            stats = newStats;

            if (isAlive)
            {
                currentHp = Math.Min(Math.Max(1L, stats.maxHp),
                    LongMath.SaturatingAdd(currentHp,
                        Math.Max(0L, stats.maxHp - oldMaxHp)));
                currentMp = Math.Min(Math.Max(0L, stats.maxMp),
                    LongMath.SaturatingAdd(currentMp,
                        Math.Max(0L, stats.maxMp - oldMaxMp)));
            }

            RefreshHpSlider();
            HpChanged?.Invoke(currentHp, Math.Max(1L, stats.maxHp));
            MpChanged?.Invoke(currentMp, Math.Max(0L, stats.maxMp));
        }

        public void SetCombatMode(PartyCombatMode mode)
        {
            combatMode = mode;
            currentTarget = null;

            if (!isAttackLunging)
            {
                follower.SetCombatControlled(false);
                follower.StopCombatMovement();
            }
        }

        public void SetAttackStyle(PartyAttackStyle style)
        {
            attackStyle = style;
            currentTarget = null;

            if (!isAttackLunging)
            {
                follower.SetCombatControlled(false);
                follower.StopCombatMovement();
            }
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

        public long TakeMagicHit(long magicPower)
        {
            if (!isAlive || magicPower <= 0L)
                return 0L;

            long damage =
                magicPower -
                Math.Max(0L, stats.magicDefense);

            if (damage < 1L)
                damage = 1L;

            return TakeDamage(damage);
        }

        public long TakeDamage(long damage)
        {
            if (!isAlive || damage <= 0)
                return 0;

            long actual = Math.Min(currentHp, damage);
            currentHp -= actual;

            DamagePopupText.Show(transform.position, actual, true);

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

        public void RestoreFullMp()
        {
            currentMp = Math.Max(0L, stats.maxMp);
            MpChanged?.Invoke(currentMp, Math.Max(0L, stats.maxMp));
        }

        public void RestoreFullResources()
        {
            RestoreFullHp();
            RestoreFullMp();
        }

        public bool TrySpendMp(long amount)
        {
            if (amount <= 0L)
                return true;

            if (currentMp < amount)
                return false;

            currentMp -= amount;
            MpChanged?.Invoke(currentMp, Math.Max(0L, stats.maxMp));
            return true;
        }

        public void RecoverMp(long amount)
        {
            if (amount <= 0L || currentMp >= stats.maxMp)
                return;

            currentMp = Math.Min(
                Math.Max(0L, stats.maxMp),
                LongMath.SaturatingAdd(currentMp, amount));

            MpChanged?.Invoke(currentMp, Math.Max(0L, stats.maxMp));
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
            float effectiveRange = attackStyle == PartyAttackStyle.Ranged
                ? Mathf.Max(0.1f, stats.attackRange) * 2f
                : Mathf.Max(0.1f, stats.attackRange);

            Gizmos.DrawWireSphere(
                transform.position,
                Mathf.Max(aggroRange, effectiveRange));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stats.maxHp = Math.Max(1L, stats.maxHp);
            stats.maxMp = Math.Max(0L, stats.maxMp);
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
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            attackPositionRadiusRatio =
                Mathf.Clamp(attackPositionRadiusRatio, 0.2f, 0.9f);
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
