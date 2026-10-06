using UnityEngine;

namespace MixMaster.CameraSystem
{
    /// <summary>
    /// Smooth 2D camera follow.
    /// Attach this to the Main Camera and assign the Player transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow")]
        [SerializeField] private Vector2 offset = Vector2.zero;
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        [Tooltip("Keeps the camera's current Z value instead of copying the target Z.")]
        [SerializeField] private bool keepCurrentZ = true;

        private Vector3 velocity;
        private float cameraZ;

        public Transform Target => target;

        private void Awake()
        {
            cameraZ = transform.position.z;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desiredPosition = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                keepCurrentZ ? cameraZ : target.position.z);

            if (smoothTime <= 0f)
            {
                transform.position = desiredPosition;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocity,
                smoothTime);
        }

        public void SetTarget(Transform newTarget, bool snapImmediately = false)
        {
            target = newTarget;

            if (target == null || !snapImmediately)
                return;

            transform.position = new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                keepCurrentZ ? cameraZ : target.position.z);

            velocity = Vector3.zero;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            smoothTime = Mathf.Max(0f, smoothTime);
        }
#endif
    }
}
