using UnityEngine;

namespace MixMaster.Monsters
{
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class EnemyWanderAI : MonoBehaviour
    {
        [Header("Wander")]
        [SerializeField, Min(0f)] private float roamRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.1f;
        [SerializeField, Min(0f)] private float arriveDistance = 0.08f;

        [Header("Idle")]
        [SerializeField, Min(0.1f)] private float minIdleTime = 0.8f;
        [SerializeField, Min(0.1f)] private float maxIdleTime = 2.5f;
        [SerializeField, Range(0f, 1f)] private float moveChance = 0.7f;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool flipSpriteHorizontally = true;

        private Rigidbody2D body;
        private Vector2 homePosition;
        private Vector2 targetPosition;

        private float nextDecisionTime;
        private float combatUntil;
        private bool walking;
        private bool externallyCombatControlled;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            homePosition = body.position;
        }

        private void Start()
        {
            BeginIdle();
        }

        private void FixedUpdate()
        {
            if (externallyCombatControlled)
            {
                StopMoving();
                return;
            }

            if (Time.time < combatUntil)
            {
                StopMoving();
                return;
            }

            if (!walking)
            {
                if (Time.time >= nextDecisionTime)
                    MakeDecision();

                return;
            }

            Vector2 current = body.position;
            Vector2 delta = targetPosition - current;

            if (delta.sqrMagnitude <= arriveDistance * arriveDistance)
            {
                BeginIdle();
                return;
            }

            Vector2 next = Vector2.MoveTowards(
                current,
                targetPosition,
                moveSpeed * Time.fixedDeltaTime);

            body.MovePosition(next);

            if (flipSpriteHorizontally && spriteRenderer != null && Mathf.Abs(delta.x) > 0.01f)
                spriteRenderer.flipX = delta.x < 0f;
        }

        public void SetHome(Vector2 worldPosition)
        {
            homePosition = worldPosition;
            targetPosition = worldPosition;
            BeginIdle();
        }

        public void EnterCombat(float duration)
        {
            combatUntil = Mathf.Max(combatUntil, Time.time + Mathf.Max(0f, duration));
            walking = false;
            StopMoving();
        }

        public void SetCombatControlled(bool controlled)
        {
            externallyCombatControlled = controlled;

            if (controlled)
            {
                walking = false;
                StopMoving();
            }
            else
            {
                BeginIdle();
            }
        }

        private void MakeDecision()
        {
            if (Random.value <= moveChance && roamRadius > 0f)
            {
                targetPosition = homePosition + Random.insideUnitCircle * roamRadius;
                walking = true;
                return;
            }

            BeginIdle();
        }

        private void BeginIdle()
        {
            walking = false;
            StopMoving();

            float min = Mathf.Min(minIdleTime, maxIdleTime);
            float max = Mathf.Max(minIdleTime, maxIdleTime);
            nextDecisionTime = Time.time + Random.Range(min, max);
        }

        private void StopMoving()
        {
            if (body != null)
                body.linearVelocity = Vector2.zero;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            roamRadius = Mathf.Max(0f, roamRadius);
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            arriveDistance = Mathf.Max(0f, arriveDistance);
            minIdleTime = Mathf.Max(0.1f, minIdleTime);
            maxIdleTime = Mathf.Max(0.1f, maxIdleTime);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? (Vector3)homePosition : transform.position;
            Gizmos.DrawWireSphere(center, roamRadius);
        }
#endif
    }
}
