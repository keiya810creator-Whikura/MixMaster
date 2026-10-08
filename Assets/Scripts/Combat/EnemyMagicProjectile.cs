using UnityEngine;
using MixMaster.Monsters;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyMagicProjectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float defaultSpeed = 8f;
        [SerializeField, Min(0.1f)] private float defaultLifetime = 3f;
        [SerializeField, Min(0.01f)] private float hitDistance = 0.12f;
        [SerializeField] private bool homing = true;
        [SerializeField] private bool rotateToDirection = true;

        private PlayerAutoAttack playerTarget;
        private PartyMemberCombat partyTarget;
        private Transform targetTransform;

        private long magicPower;
        private float speed;
        private float remainingLifetime;
        private Vector2 travelDirection;
        private bool initialized;

        public void Initialize(
            PlayerAutoAttack newPlayerTarget,
            PartyMemberCombat newPartyTarget,
            long newMagicPower,
            float newSpeed,
            float lifetime,
            bool useHoming = true)
        {
            playerTarget = newPlayerTarget;
            partyTarget = newPartyTarget;

            targetTransform =
                playerTarget != null
                    ? playerTarget.transform
                    : partyTarget != null
                        ? partyTarget.transform
                        : null;

            magicPower = System.Math.Max(1L, newMagicPower);
            speed = Mathf.Max(0.1f, newSpeed);
            remainingLifetime = Mathf.Max(0.1f, lifetime);
            homing = useHoming;
            initialized = true;

            UpdateTravelDirection();
            UpdateRotation(travelDirection);
        }

        private void Start()
        {
            if (initialized)
                return;

            speed = Mathf.Max(0.1f, defaultSpeed);
            remainingLifetime = Mathf.Max(0.1f, defaultLifetime);

            if (travelDirection.sqrMagnitude <= 0.000001f)
                travelDirection = transform.right;
        }

        private void Update()
        {
            remainingLifetime -= Time.deltaTime;

            if (remainingLifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (!IsTargetValid())
            {
                Destroy(gameObject);
                return;
            }

            Vector2 current = transform.position;
            Vector2 toTarget =
                (Vector2)targetTransform.position - current;

            if (toTarget.sqrMagnitude <= hitDistance * hitDistance)
            {
                ApplyHit();
                Destroy(gameObject);
                return;
            }

            if (homing && toTarget.sqrMagnitude > 0.000001f)
                travelDirection = toTarget.normalized;

            if (travelDirection.sqrMagnitude <= 0.000001f)
                return;

            Vector2 next =
                current +
                travelDirection.normalized *
                speed *
                Time.deltaTime;

            transform.position = next;
            UpdateRotation(travelDirection);
        }

        private bool IsTargetValid()
        {
            if (targetTransform == null)
                return false;

            if (playerTarget != null)
                return playerTarget.IsAlive;

            if (partyTarget != null)
                return partyTarget.IsAlive;

            return false;
        }

        private void ApplyHit()
        {
            if (playerTarget != null)
            {
                playerTarget.TakeMagicHit(magicPower);
                return;
            }

            if (partyTarget != null)
                partyTarget.TakeMagicHit(magicPower);
        }

        private void UpdateTravelDirection()
        {
            if (targetTransform == null)
                return;

            Vector2 direction =
                targetTransform.position - transform.position;

            if (direction.sqrMagnitude > 0.000001f)
                travelDirection = direction.normalized;
        }

        private void UpdateRotation(Vector2 direction)
        {
            if (!rotateToDirection ||
                direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            float angle =
                Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(0f, 0f, angle);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            defaultSpeed = Mathf.Max(0.1f, defaultSpeed);
            defaultLifetime = Mathf.Max(0.1f, defaultLifetime);
            hitDistance = Mathf.Max(0.01f, hitDistance);
        }
#endif
    }
}
