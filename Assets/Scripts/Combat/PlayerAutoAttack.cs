using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.UI;

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
        [Tooltip("Optional text displaying the player's current level.")]
        [SerializeField] private TMP_Text levelText;
        [Tooltip("Optional text displaying EXP progress toward the next level.")]
        [SerializeField] private TMP_Text experienceText;
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
        [Tooltip("The visible sword length relative to AttackRange (1 = same length).")]
        [SerializeField, Min(0.1f)] private float swordLengthMultiplier = 1f;
        [SerializeField, Min(0.03f)] private float swingDuration = 0.18f;
        [SerializeField, Range(15f, 150f)] private float swingArcDegrees = 115f;
        [SerializeField] private Color swordTint = Color.white;
        [SerializeField] private int swordSortingOffset = 5;
        [Tooltip("Thickness of the sword's gameplay hit sweep in world units.")]
        [SerializeField, Min(0.01f)] private float swordHitWidth = 0.18f;

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
                playerManager.ExperienceChanged += HandleExperienceChanged;
                playerManager.LeveledUp += HandlePlayerLeveledUp;
                RefreshHpSlider(playerManager.CurrentHp, playerManager.Stats.maxHp);
                RefreshExperienceText();
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
            {
                playerManager.HpChanged -= HandlePlayerHpChanged;
                playerManager.ExperienceChanged -= HandleExperienceChanged;
                playerManager.LeveledUp -= HandlePlayerLeveledUp;
            }

            if (swordPivot != null)
                Destroy(swordPivot.gameObject);

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

            if (swingRoutine != null)
                StopCoroutine(swingRoutine);

            swingRoutine = StartCoroutine(SwingSword(
                direction,
                attackPower,
                MaterialDropSourceInfo.Player(
                    playerManager != null
                        ? playerManager.DropRateBonus
                        : 0f)));
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
            DamagePopupText.Show(transform.position, actualDamage, true);

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
            DamagePopupText.Show(transform.position, actualDamage, true);

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

        private void HandleExperienceChanged(long current, long required)
        {
            RefreshExperienceText();
        }

        private void HandlePlayerLeveledUp(int newLevel)
        {
            RefreshExperienceText();
        }

        private void RefreshExperienceText()
        {
            if (playerManager == null)
                return;

            if (levelText != null)
                levelText.text = "Lv." + playerManager.Level;

            if (experienceText != null)
            {
                long needed = playerManager.ExperienceToNextLevel;
                experienceText.text = needed > 0L
                    ? "EXP " + playerManager.Experience.ToString("N0") +
                      "/" + needed.ToString("N0")
                    : "EXP MAX";
            }
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
            // Do NOT parent the sword to the player. The player's own
            // scale (e.g. 0.25) must not shrink its world-space reach.
            swordPivot = pivot.transform;
            swordPivot.position = transform.position;
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

            Sprite sprite = swordRenderer.sprite;
            Bounds bounds = sprite.bounds;
            bool horizontal = bounds.size.x > bounds.size.y;
            float nearEdge = horizontal ? bounds.min.x : bounds.min.y;
            float visibleLength = Mathf.Max(0.01f,
                horizontal ? bounds.size.x : bounds.size.y);

            // Many custom sword images have transparent padding.
            // Measuring nontransparent pixels gives a visible blade
            // reaching AttackRange instead of scaling the empty texture.
            TryGetOpaqueSwordBounds(
                sprite, horizontal, ref nearEdge, ref visibleLength);

            float desiredWorldLength = Mathf.Max(0.1f, GetAttackRange()) *
                Mathf.Max(0.1f, swordLengthMultiplier);

            float scale = desiredWorldLength / visibleLength;
            swordRenderer.transform.localScale = Vector3.one * scale;
            swordRenderer.transform.localRotation = horizontal
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.identity;
            swordRenderer.transform.localPosition =
                Vector3.up * (-nearEdge * scale);
            swordRenderer.color = swordTint;
        }

        private static void TryGetOpaqueSwordBounds(
            Sprite sprite,
            bool horizontal,
            ref float nearEdge,
            ref float visibleLength)
        {
            if (sprite == null)
                return;

            // Tight sprite meshes already exclude transparent margins.
            // Their vertices can be measured even when Read/Write is off.
            Vector2[] vertices = sprite.vertices;
            if (vertices != null && vertices.Length >= 3)
            {
                float minCoordinate = float.MaxValue;
                float maxCoordinate = float.MinValue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float coordinate = horizontal
                        ? vertices[i].x : vertices[i].y;
                    minCoordinate = Mathf.Min(minCoordinate, coordinate);
                    maxCoordinate = Mathf.Max(maxCoordinate, coordinate);
                }

                if (maxCoordinate > minCoordinate)
                {
                    nearEdge = minCoordinate;
                    visibleLength = maxCoordinate - minCoordinate;
                }
            }

            if (sprite.texture == null || !sprite.texture.isReadable)
                return;

            try
            {
                Texture2D texture = sprite.texture;
                Rect rect = sprite.textureRect;
                Color32[] pixels = texture.GetPixels32();
                int minX = Mathf.Max(0, Mathf.FloorToInt(rect.x));
                int minY = Mathf.Max(0, Mathf.FloorToInt(rect.y));
                int maxX = Mathf.Min(texture.width,
                    Mathf.CeilToInt(rect.xMax));
                int maxY = Mathf.Min(texture.height,
                    Mathf.CeilToInt(rect.yMax));
                int first = horizontal ? maxX : maxY;
                int last = horizontal ? minX : minY;

                for (int y = minY; y < maxY; y++)
                {
                    for (int x = minX; x < maxX; x++)
                    {
                        if (pixels[y * texture.width + x].a < 32)
                            continue;

                        int coordinate = horizontal ? x : y;
                        first = Mathf.Min(first, coordinate);
                        last = Mathf.Max(last, coordinate);
                    }
                }

                if (last < first)
                    return;

                float ppu = Mathf.Max(0.001f, sprite.pixelsPerUnit);
                float pivotPixel = horizontal
                    ? sprite.pivot.x : sprite.pivot.y;
                float rectStart = horizontal ? rect.x : rect.y;
                nearEdge = ((first - rectStart) - pivotPixel) / ppu;
                visibleLength = Mathf.Max(
                    0.01f, (last - first + 1) / ppu);
            }
            catch (UnityException)
            {
                // Tight-packed/non-readable textures use sprite bounds.
            }
        }

        private IEnumerator SwingSword(
            Vector2 direction,
            long attackPower,
            MaterialDropSourceInfo hitSource)
        {
            if (swordPivot == null || swordRenderer == null)
                yield break;

            ResizeSwordToAttackRange();

            float targetAngle = Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg - 90f;
            float startAngle = targetAngle - swingArcDegrees * 0.5f;
            float endAngle = targetAngle + swingArcDegrees * 0.5f;
            float reach = Mathf.Max(0.1f, GetAttackRange()) *
                Mathf.Max(0.1f, swordLengthMultiplier);

            HashSet<EnemyHealth> hitTargets =
                new HashSet<EnemyHealth>();

            swordPivot.position = transform.position;
            swordPivot.gameObject.SetActive(true);
            float elapsed = 0f;
            float previousAngle = startAngle;
            Vector2 previousOrigin = transform.position;

            // Include initial contact, then sweep from the previous frame
            // to the current one, including player movement.
            CheckSwordHitLine(previousOrigin, previousAngle, reach,
                attackPower, hitSource, hitTargets);

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(
                    elapsed / Mathf.Max(0.03f, swingDuration));
                float eased = t * t * (3f - 2f * t);
                float currentAngle =
                    Mathf.Lerp(startAngle, endAngle, eased);
                Vector2 currentOrigin = transform.position;

                // Sample intermediate angles so enemies are not missed
                // during low frame rate or while the player is moving.
                int steps = Mathf.Clamp(
                    Mathf.CeilToInt(
                        Mathf.Abs(currentAngle - previousAngle) / 7f), 1, 32);

                for (int step = 1; step <= steps; step++)
                {
                    float u = step / (float)steps;
                    float sampledAngle =
                        Mathf.Lerp(previousAngle, currentAngle, u);
                    Vector2 sampledOrigin =
                        Vector2.Lerp(previousOrigin, currentOrigin, u);
                    CheckSwordHitLine(
                        sampledOrigin, sampledAngle, reach,
                        attackPower, hitSource, hitTargets);
                }

                swordPivot.position = currentOrigin;
                swordPivot.rotation = Quaternion.Euler(
                    0f, 0f, currentAngle);

                previousAngle = currentAngle;
                previousOrigin = currentOrigin;
                yield return null;
            }

            swordPivot.gameObject.SetActive(false);
            swingRoutine = null;
        }

        private void CheckSwordHitLine(
            Vector2 origin,
            float angle,
            float reach,
            long attackPower,
            MaterialDropSourceInfo hitSource,
            HashSet<EnemyHealth> hitTargets)
        {
            float radians = (angle + 90f) * Mathf.Deg2Rad;
            Vector2 alongSword = new Vector2(
                Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 tip = origin + alongSword * reach;
            float hitWidthSqr = swordHitWidth * swordHitWidth;

            // Use the same live enemy registry as target acquisition.
            // Each enemy is damaged at most once for this full swing.
            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive ||
                    hitTargets.Contains(enemy))
                    continue;

                if (requireEnemyTag && !enemy.CompareTag(enemyTag))
                    continue;

                Vector2 enemyCenter = enemy.transform.position;
                float projection = Mathf.Clamp01(
                    Vector2.Dot(enemyCenter - origin, alongSword) / reach);
                Vector2 pointOnBlade =
                    Vector2.Lerp(origin, tip, projection);

                Collider2D collider = enemy.GetComponent<Collider2D>();
                if (collider == null)
                    collider = enemy.GetComponentInChildren<Collider2D>();

                Vector2 nearestEnemyPoint = collider != null &&
                    collider.enabled
                    ? collider.ClosestPoint(pointOnBlade)
                    : enemyCenter;

                if ((nearestEnemyPoint - pointOnBlade).sqrMagnitude >
                    hitWidthSqr)
                    continue;

                hitTargets.Add(enemy);
                enemy.TakePhysicalHit(attackPower, hitSource);
            }
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
            swordLengthMultiplier = Mathf.Max(0.1f, swordLengthMultiplier);
            swordHitWidth = Mathf.Max(0.01f, swordHitWidth);
            swingDuration = Mathf.Max(0.03f, swingDuration);
            swingArcDegrees = Mathf.Clamp(swingArcDegrees, 15f, 150f);

            if (string.IsNullOrWhiteSpace(enemyTag))
                enemyTag = "Enemy";

        }
#endif
    }
}
