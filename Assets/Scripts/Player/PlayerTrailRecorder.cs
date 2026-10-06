using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Player
{
    /// <summary>
    /// Records the path travelled by the player.
    /// Followers can ask for a position a specified distance behind the player,
    /// allowing them to trace corners instead of directly chasing the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTrailRecorder : MonoBehaviour
    {
        [Header("Trail Recording")]
        [Tooltip("Distance between recorded trail points. Smaller values follow corners more accurately.")]
        [SerializeField, Min(0.01f)] private float pointSpacing = 0.08f;

        [Tooltip("Maximum trail length retained in world units.")]
        [SerializeField, Min(2f)] private float maxTrailDistance = 40f;

        [Tooltip("If the player moves farther than this in one physics step, treat it as a teleport and reset the trail.")]
        [SerializeField, Min(0.5f)] private float teleportResetDistance = 6f;

        private readonly List<Vector2> points = new List<Vector2>(512);
        private Vector2 lastRecordedPosition;

        public int PointCount => points.Count;

        private void Awake()
        {
            ResetTrail();
        }

        private void OnEnable()
        {
            if (points.Count == 0)
                ResetTrail();
        }

        private void FixedUpdate()
        {
            RecordCurrentPosition();
        }

        public void ResetTrail()
        {
            points.Clear();

            Vector2 current = transform.position;
            points.Add(current);
            lastRecordedPosition = current;
        }

        public Vector2 GetPositionBehind(float distanceBehind)
        {
            Vector2 current = transform.position;

            if (distanceBehind <= 0f || points.Count == 0)
                return current;

            float remaining = distanceBehind;
            Vector2 from = current;

            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 to = points[i];
                float segmentLength = Vector2.Distance(from, to);

                if (segmentLength <= Mathf.Epsilon)
                {
                    from = to;
                    continue;
                }

                if (remaining <= segmentLength)
                {
                    float t = remaining / segmentLength;
                    return Vector2.Lerp(from, to, t);
                }

                remaining -= segmentLength;
                from = to;
            }

            return points[0];
        }

        private void RecordCurrentPosition()
        {
            Vector2 current = transform.position;
            float movedDistance = Vector2.Distance(lastRecordedPosition, current);

            if (movedDistance <= Mathf.Epsilon)
                return;

            if (movedDistance >= teleportResetDistance)
            {
                ResetTrail();
                return;
            }

            if (movedDistance < pointSpacing)
                return;

            Vector2 direction = (current - lastRecordedPosition).normalized;
            float remaining = movedDistance;

            while (remaining >= pointSpacing)
            {
                lastRecordedPosition += direction * pointSpacing;
                points.Add(lastRecordedPosition);
                remaining -= pointSpacing;
            }

            TrimOldPoints();
        }

        private void TrimOldPoints()
        {
            if (points.Count <= 2)
                return;

            float accumulatedDistance = Vector2.Distance(transform.position, points[points.Count - 1]);

            for (int i = points.Count - 1; i > 0; i--)
            {
                accumulatedDistance += Vector2.Distance(points[i], points[i - 1]);

                if (accumulatedDistance <= maxTrailDistance)
                    continue;

                int removeCount = i;
                if (removeCount > 0)
                    points.RemoveRange(0, removeCount);

                break;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            pointSpacing = Mathf.Max(0.01f, pointSpacing);
            maxTrailDistance = Mathf.Max(2f, maxTrailDistance);
            teleportResetDistance = Mathf.Max(0.5f, teleportResetDistance);
        }
#endif
    }
}
