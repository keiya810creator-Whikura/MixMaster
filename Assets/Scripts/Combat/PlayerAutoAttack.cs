using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [SerializeField] private TMP_Text hpText;
        [Tooltip("0 to 1 attack charge gauge. When full, the player attacks.")]
        [SerializeField] private Slider attackSpeedSlider;
        [SerializeField] private TMP_Text attackTimeText;
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

        [Header("Hit Feedback")]
        [SerializeField] private SpriteRenderer playerSpriteRenderer;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.08f;

        [Header("Sword Swing")]
        [Tooltip("Optional sword sprite. When absent, a simple sword sprite is generated.")]
        [SerializeField] private Sprite swordSprite;
        [SerializeField, Min(0.03f)] private float swingDuration = 0.18f;
        [SerializeField, Range(15f, 150f)] private float swingArcDegrees = 115f;
        [SerializeField] private Color swordTint = Color.white;
        [SerializeField] private int swordSortingOffset = 5;

        private PlayerController playerController;
        private PlayerManager playerManager;
        private EnemyHealth currentTarget;

        private float targetRefreshTimer;
        private float attackGauge;

        private Transform swordPivot;
        private SpriteRenderer swordRenderer;
        private Coroutine swingRoutine;
        private Texture2D generatedSwordTexture;
        private Sprite generatedSwordSprite;

        private Coroutine hitFlashRoutine;
        private Color originalSpriteColor = Color.white;

        public EnemyHealth CurrentTarget => currentTarget;
        public float AttackGauge => attackGauge;
        public float AttackRange => GetAttackRange();
        // Compatibility with MapBuildingAccess; sword swings never lunge.
        public bool IsLunging => false;
        public bool IsAlive => playerManager == null || playerManager.CurrentHp > 0L;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            if (playerSpriteRenderer == null)
                playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (playerSpriteRenderer != null)
                originalSpriteColor = playerSpriteRenderer.color;

            if (hpText == null && hpSlider != null)
                hpText = hpSlider.GetComponentInChildren<TMP_Text>(true);

            if (attackTimeText == null && attackSpeedSlider != null)
                attackTimeText = attackSpeedSlider.GetComponentInChildren<TextMeshProUGUI>(true);

            ConfigureSlider(attackSpeedSlider);
            ConfigureSlider(hpSlider);
            SetAttackGauge(0f);
            CreateSwordVisual();
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
            if (swingRoutine != null)
            {
                StopCoroutine(swingRoutine);
                swingRoutine = null;
            }

            if (swordPivot != null)
                swordPivot.gameObject.SetActive(false);

            // Do not touch movement locks: attacks never lock movement.
        }

        private void OnDestroy()
        {
            if (playerManager != null)
                playerManager.HpChanged -= HandlePlayerHpChanged;

            if (generatedSwordSprite != null)
                Destroy(generatedSwordSprite);

            if (generatedSwordTexture != null)
                Destroy(generatedSwordTexture);
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

            target.TakePhysicalHit(
                attackPower,
                MaterialDropSourceInfo.Player(
                    playerManager != null
                        ? playerManager.DropRateBonus
                        : 0f));

            if (swingRoutine != null)
                StopCoroutine(swingRoutine);

            swingRoutine = StartCoroutine(SwingSword(direction));
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

        public long TakePhysicalHit(long attackPower)
        {
            if (playerManager == null || playerManager.CurrentHp <= 0L || attackPower <= 0L)
                return 0L;

            long defense = Math.Max(0L, playerManager.Stats.defense);
            long damage = attackPower - defense;

            if (damage < 1L)
                damage = 1L;

            long before = playerManager.CurrentHp;
            playerManager.TakeDamage(damage);
            long actualDamage = Math.Max(0L, before - playerManager.CurrentHp);

            if (playerSpriteRenderer != null)
            {
                if (hitFlashRoutine != null)
                    StopCoroutine(hitFlashRoutine);

                hitFlashRoutine = StartCoroutine(HitFlashRoutine());
            }

            return actualDamage;
        }

        public long TakeMagicHit(long magicPower)
        {
            if (playerManager == null ||
                playerManager.CurrentHp <= 0L ||
                magicPower <= 0L)
            {
                return 0L;
            }

            long magicDefense =
                Math.Max(0L, playerManager.Stats.magicDefense);

            long damage = magicPower - magicDefense;

            if (damage < 1L)
                damage = 1L;

            long before = playerManager.CurrentHp;
            playerManager.TakeDamage(damage);

            long actualDamage =
                Math.Max(0L, before - playerManager.CurrentHp);

            if (playerSpriteRenderer != null)
            {
                if (hitFlashRoutine != null)
                    StopCoroutine(hitFlashRoutine);

                hitFlashRoutine =
                    StartCoroutine(HitFlashRoutine());
            }

            return actualDamage;
        }

        private IEnumerator HitFlashRoutine()
        {
            playerSpriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);

            if (playerSpriteRenderer != null)
                playerSpriteRenderer.color = originalSpriteColor;

            hitFlashRoutine = null;
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

        private void CreateSwordVisual()
        {
            GameObject pivot = new GameObject("_SwordSwingPivot");
            pivot.transform.SetParent(transform, false);
            swordPivot = pivot.transform;
            swordPivot.localPosition = Vector3.zero;
            swordPivot.localScale = Vector3.one;

            GameObject visual = new GameObject("SwordSprite");
            visual.transform.SetParent(swordPivot, false);
            swordRenderer = visual.AddComponent<SpriteRenderer>();
            swordRenderer.sprite = swordSprite != null
                ? swordSprite
                : GenerateDefaultSwordSprite();
            swordRenderer.color = swordTint;

            SpriteRenderer playerSprite = GetComponentInChildren<SpriteRenderer>();
            if (playerSprite != null)
            {
                swordRenderer.sortingLayerID = playerSprite.sortingLayerID;
                swordRenderer.sortingOrder =
                    playerSprite.sortingOrder + swordSortingOffset;
            }

            swordPivot.gameObject.SetActive(false);
        }

        private void ResizeSwordToAttackRange()
        {
            if (swordRenderer == null || swordRenderer.sprite == null)
                return;

            // The sword image is centered on its own Renderer, and the
            // pivot stays at the player's position. The visible tip
            // reaches exactly to the player's current attack range.
            float swordHeight = Mathf.Max(
                0.01f, swordRenderer.sprite.bounds.size.y);
            float attackRange = Mathf.Max(0.1f, GetAttackRange());
            float scale = attackRange / swordHeight;
            swordRenderer.transform.localScale = Vector3.one * scale;
            // Works for both center-pivot and handle-pivot sword sprites.
            // The blade's lowest pixel starts at the player and the
            // highest pixel ends at the current attack-range radius.
            swordRenderer.transform.localPosition =
                Vector3.up * (-swordRenderer.sprite.bounds.min.y * scale);
            swordRenderer.color = swordTint;
        }

        private IEnumerator SwingSword(Vector2 direction)
        {
            if (swordPivot == null || swordRenderer == null)
                yield break;

            ResizeSwordToAttackRange();

            float targetAngle = Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg - 90f;
            float startAngle = targetAngle - swingArcDegrees * 0.5f;
            float endAngle = targetAngle + swingArcDegrees * 0.5f;

            swordPivot.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(
                    elapsed / Mathf.Max(0.03f, swingDuration));
                float ease = t * t * (3f - 2f * t);

                swordPivot.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Lerp(startAngle, endAngle, ease));
                yield return null;
            }

            swordPivot.gameObject.SetActive(false);
            swingRoutine = null;
        }

        private Sprite GenerateDefaultSwordSprite()
        {
            const int width = 20;
            const int height = 80;
            generatedSwordTexture = new Texture2D(
                width, height, TextureFormat.RGBA32, false);
            generatedSwordTexture.filterMode = FilterMode.Point;
            generatedSwordTexture.wrapMode = TextureWrapMode.Clamp;

            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 steel = new Color32(224, 237, 252, 255);
            Color32 edge = new Color32(132, 169, 203, 255);
            Color32 gold = new Color32(232, 181, 61, 255);
            Color32 grip = new Color32(78, 52, 48, 255);

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 value = clear;
                    int dx = Mathf.Abs(x - width / 2);
                    if (y >= 15 && y <= 72 && dx <= 3)
                        value = dx == 3 ? edge : steel;
                    if (y >= 73 && y < 80 && dx <= (80 - y) / 2)
                        value = steel;
                    if (y >= 12 && y <= 15 && dx <= 9)
                        value = gold;
                    if (y >= 3 && y < 12 && dx <= 2)
                        value = grip;
                    if (y <= 3 && dx <= 4)
                        value = gold;
                    pixels[y * width + x] = value;
                }
            }

            generatedSwordTexture.SetPixels32(pixels);
            generatedSwordTexture.Apply();
            generatedSwordSprite = Sprite.Create(
                generatedSwordTexture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 80f);
            generatedSwordSprite.name = "_RuntimeSwordSprite";
            return generatedSwordSprite;
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
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
            swingDuration = Mathf.Max(0.03f, swingDuration);
            swingArcDegrees = Mathf.Clamp(swingArcDegrees, 15f, 150f);

            if (string.IsNullOrWhiteSpace(enemyTag))
                enemyTag = "Enemy";

        }
#endif
    }
}
