using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MixMaster.Monsters;
using MixMaster.UI;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        private static readonly List<EnemyHealth> activeEnemies = new List<EnemyHealth>();

        [Header("Stats")]
        [SerializeField, Min(1)] private long maxHp = 50;
        [SerializeField, Min(0)] private long maxMp = 50;
        [SerializeField, Min(0)] private long defense = 0;
        [SerializeField, Min(0)] private long magicDefense = 0;

        [Header("Hit Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.08f;

        [Header("Death")]
        [SerializeField, Min(0.01f)] private float deathDuration = 0.18f;

        private Coroutine flashRoutine;
        private Color originalColor = Color.white;
        private Vector3 originalScale;
        private EnemyWanderAI wanderAI;

        public static IReadOnlyList<EnemyHealth> ActiveEnemies => activeEnemies;

        public long MaxHp => maxHp;
        public long MaxMp => maxMp;
        public long CurrentHp { get; private set; }
        public long CurrentMp { get; private set; }
        public long Defense => defense;
        public long MagicDefense => magicDefense;
        public bool IsAlive { get; private set; }

        public event Action<EnemyHealth, long> Damaged;
        public event Action<long, long> MpChanged;
        public event Action<EnemyHealth> Died;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer != null)
                originalColor = spriteRenderer.color;

            originalScale = transform.localScale;
            wanderAI = GetComponent<EnemyWanderAI>();

            CurrentHp = maxHp;
            CurrentMp = maxMp;
            IsAlive = true;
        }

        private void OnEnable()
        {
            if (!activeEnemies.Contains(this))
                activeEnemies.Add(this);

            if (CurrentHp <= 0)
                CurrentHp = maxHp;

            CurrentMp = Math.Min(Math.Max(0L, CurrentMp), maxMp);
            if (CurrentMp <= 0L)
                CurrentMp = maxMp;

            IsAlive = true;
            WorldUIManager.TryRegisterEnemy(this);
        }

        private void OnDisable()
        {
            WorldUIManager.TryUnregisterEnemy(this);
            activeEnemies.Remove(this);
        }

        public long TakePhysicalHit(long attackPower)
        {
            if (!IsAlive || attackPower <= 0)
                return 0;

            long damage = attackPower - defense;
            if (damage < 1)
                damage = 1;

            return TakeDamage(damage);
        }

        public long TakeMagicHit(long magicPower)
        {
            if (!IsAlive || magicPower <= 0)
                return 0;

            long damage = magicPower - magicDefense;
            if (damage < 1)
                damage = 1;

            return TakeDamage(damage);
        }

        public long TakeDamage(long damage)
        {
            if (!IsAlive || damage <= 0)
                return 0;

            long actualDamage = Math.Min(CurrentHp, damage);
            CurrentHp -= actualDamage;

            wanderAI?.EnterCombat(1.5f);

            Damaged?.Invoke(this, actualDamage);

            if (spriteRenderer != null)
            {
                if (flashRoutine != null)
                    StopCoroutine(flashRoutine);

                flashRoutine = StartCoroutine(HitFlashRoutine());
            }

            if (CurrentHp <= 0)
                Die();

            return actualDamage;
        }

        public void SetStats(long newMaxHp, long newDefense, bool healToFull = true)
        {
            maxHp = Math.Max(1L, newMaxHp);
            defense = Math.Max(0L, newDefense);

            if (healToFull)
                CurrentHp = maxHp;
            else
                CurrentHp = Math.Min(CurrentHp, maxHp);
        }

        public bool TrySpendMp(long amount)
        {
            if (!IsAlive || amount <= 0L)
                return amount <= 0L;

            if (CurrentMp < amount)
                return false;

            CurrentMp -= amount;
            MpChanged?.Invoke(CurrentMp, maxMp);
            return true;
        }

        public void RecoverMp(long amount)
        {
            if (!IsAlive || amount <= 0L || CurrentMp >= maxMp)
                return;

            CurrentMp = Math.Min(
                maxMp,
                LongMath.SaturatingAdd(CurrentMp, amount));

            MpChanged?.Invoke(CurrentMp, maxMp);
        }

        public void RestoreFullMp()
        {
            CurrentMp = maxMp;
            MpChanged?.Invoke(CurrentMp, maxMp);
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);

            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;

            flashRoutine = null;
        }

        private void Die()
        {
            if (!IsAlive)
                return;

            IsAlive = false;
            activeEnemies.Remove(this);

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (Collider2D col in colliders)
                col.enabled = false;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
            }

            Died?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            float elapsed = 0f;
            Color startColor = spriteRenderer != null ? originalColor : Color.white;

            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / deathDuration);
                float scale = Mathf.Lerp(1f, 0f, t);

                transform.localScale = originalScale * scale;

                if (spriteRenderer != null)
                {
                    Color c = startColor;
                    c.a = 1f - t;
                    spriteRenderer.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHp = Math.Max(1L, maxHp);
            maxMp = Math.Max(0L, maxMp);
            defense = Math.Max(0L, defense);
            magicDefense = Math.Max(0L, magicDefense);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
            deathDuration = Mathf.Max(0.01f, deathDuration);
        }
#endif
    }
}
