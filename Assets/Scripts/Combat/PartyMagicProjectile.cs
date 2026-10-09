using UnityEngine;
using MixMaster.Core;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class PartyMagicProjectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float defaultSpeed = 8f;
        [SerializeField, Min(0.1f)] private float defaultLifetime = 3f;
        [SerializeField, Min(0.01f)] private float hitDistance = 0.12f;
        [SerializeField] private bool homing = true;
        [SerializeField] private bool rotateToDirection = true;

        private EnemyHealth target;
        private long magicPower;
        private float speed;
        private float remainingLifetime;
        private Vector2 travelDirection;
        private bool initialized;
        private MaterialDropSourceInfo sourceInfo =
            MaterialDropSourceInfo.Unknown();

        public void Initialize(
            EnemyHealth newTarget,
            long newMagicPower,
            float newSpeed,
            float lifetime,
            bool useHoming = true,
            MaterialDropSourceInfo newSourceInfo = null)
        {
            target = newTarget;
            magicPower = System.Math.Max(1L, newMagicPower);
            speed = Mathf.Max(0.1f, newSpeed);
            remainingLifetime = Mathf.Max(0.1f, lifetime);
            homing = useHoming;

            sourceInfo =
                newSourceInfo != null
                    ? newSourceInfo.Clone()
                    : MaterialDropSourceInfo.Unknown();

            initialized = true;

            if (target != null)
            {
                Vector2 direction =
                    target.transform.position - transform.position;

                if (direction.sqrMagnitude > 0.000001f)
                    travelDirection = direction.normalized;
            }

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

            if (target == null || !target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 current = transform.position;
            Vector2 toTarget =
                (Vector2)target.transform.position - current;

            if (toTarget.sqrMagnitude <= hitDistance * hitDistance)
            {
                target.TakeMagicHit(
                    magicPower,
                    sourceInfo);
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

        private void UpdateRotation(Vector2 direction)
        {
            if (!rotateToDirection ||
                direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            // Projectile artwork faces local +Y (up); Atan2 uses +X.
            float angle =
                Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg - 90f;

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
