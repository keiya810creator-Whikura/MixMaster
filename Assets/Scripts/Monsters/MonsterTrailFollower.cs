using UnityEngine;
using MixMaster.Player;

namespace MixMaster.Monsters
{
    /// <summary>
    /// Follows the recorded player trail like a classic party member.
    /// Follow Order 0 = first monster, 1 = second monster, and so on.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class MonsterTrailFollower : MonoBehaviour
    {
        [Header("Trail")]
        [SerializeField] private PlayerTrailRecorder playerTrail;

        [Tooltip("0 = first follower, 1 = second follower, 2 = third follower...")]
        [SerializeField, Min(0)] private int followOrder = 0;

        [Tooltip("Distance between each party member.")]
        [SerializeField, Min(0.1f)] private float followerSpacing = 1.15f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float followSpeed = 7f;
        [SerializeField, Min(0f)] private float stopDistance = 0.03f;

        [Tooltip("If the follower gets farther than this from its trail target, snap it back to the party.")]
        [SerializeField, Min(0.5f)] private float snapDistance = 8f;

        [Header("8 Direction Sprites")]
        [SerializeField] private DirectionSpriteSet sprites = new DirectionSpriteSet();
        [SerializeField] private Direction8 initialDirection = Direction8.Down;

        [Header("Walk Animation")]
        [SerializeField, Min(0.1f)] private float animationFps = 8f;
        [SerializeField, Min(0)] private int idleFrameIndex = 0;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;

        private Direction8 facingDirection;
        private float animationTimer;
        private int animationFrame;
        private Vector2 lastPosition;
        private bool isMoving;

        public int FollowOrder => followOrder;
        public bool IsMoving => isMoving;
        public Direction8 FacingDirection => facingDirection;

        private float TrailDistance => (followOrder + 1) * followerSpacing;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            body.gravityScale = 0f;
            body.freezeRotation = true;

            facingDirection = initialDirection;
            lastPosition = body.position;
            RefreshIdleSprite();
        }

        private void Start()
        {
            if (playerTrail == null)
                playerTrail = FindFirstObjectByType<PlayerTrailRecorder>();
        }

        private void FixedUpdate()
        {
            if (playerTrail == null)
            {
                body.linearVelocity = Vector2.zero;
                isMoving = false;
                return;
            }

            Vector2 current = body.position;
            Vector2 target = playerTrail.GetPositionBehind(TrailDistance);
            Vector2 toTarget = target - current;
            float distance = toTarget.magnitude;

            if (distance >= snapDistance)
            {
                body.position = target;
                body.linearVelocity = Vector2.zero;
                UpdateMovementVisual(target - lastPosition);
                lastPosition = target;
                return;
            }

            if (distance <= stopDistance)
            {
                body.linearVelocity = Vector2.zero;
                UpdateMovementVisual(Vector2.zero);
                lastPosition = current;
                return;
            }

            Vector2 nextPosition = Vector2.MoveTowards(
                current,
                target,
                followSpeed * Time.fixedDeltaTime);

            body.MovePosition(nextPosition);

            Vector2 movement = nextPosition - current;
            UpdateMovementVisual(movement);
            lastPosition = nextPosition;
        }

        private void Update()
        {
            if (isMoving)
                UpdateWalkAnimation();
            else
                RefreshIdleSprite();
        }

        public void SetFollowOrder(int order)
        {
            followOrder = Mathf.Max(0, order);
        }

        public void SetPlayerTrail(PlayerTrailRecorder trail)
        {
            playerTrail = trail;
        }

        private void UpdateMovementVisual(Vector2 movement)
        {
            isMoving = movement.sqrMagnitude > 0.000001f;

            if (!isMoving)
            {
                animationTimer = 0f;
                animationFrame = 0;
                return;
            }

            Direction8 newDirection = GetDirection8(movement);

            if (newDirection != facingDirection)
            {
                facingDirection = newDirection;
                animationTimer = 0f;
                animationFrame = 0;
            }
        }

        private void UpdateWalkAnimation()
        {
            Sprite[] frames = sprites.GetSprites(facingDirection);

            if (frames == null || frames.Length == 0)
                return;

            if (frames.Length == 1)
            {
                spriteRenderer.sprite = frames[0];
                return;
            }

            animationTimer += Time.deltaTime;
            float frameDuration = 1f / animationFps;

            while (animationTimer >= frameDuration)
            {
                animationTimer -= frameDuration;
                animationFrame = (animationFrame + 1) % frames.Length;
            }

            spriteRenderer.sprite = frames[
                Mathf.Clamp(animationFrame, 0, frames.Length - 1)];
        }

        private void RefreshIdleSprite()
        {
            Sprite[] frames = sprites.GetSprites(facingDirection);

            if (frames == null || frames.Length == 0)
                return;

            int index = Mathf.Clamp(idleFrameIndex, 0, frames.Length - 1);
            spriteRenderer.sprite = frames[index];
        }

        private static Direction8 GetDirection8(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (angle < 0f)
                angle += 360f;

            if (angle >= 337.5f || angle < 22.5f) return Direction8.Right;
            if (angle < 67.5f) return Direction8.UpRight;
            if (angle < 112.5f) return Direction8.Up;
            if (angle < 157.5f) return Direction8.UpLeft;
            if (angle < 202.5f) return Direction8.Left;
            if (angle < 247.5f) return Direction8.DownLeft;
            if (angle < 292.5f) return Direction8.Down;
            return Direction8.DownRight;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            followOrder = Mathf.Max(0, followOrder);
            followerSpacing = Mathf.Max(0.1f, followerSpacing);
            followSpeed = Mathf.Max(0.1f, followSpeed);
            stopDistance = Mathf.Max(0f, stopDistance);
            snapDistance = Mathf.Max(0.5f, snapDistance);
            animationFps = Mathf.Max(0.1f, animationFps);
            idleFrameIndex = Mathf.Max(0, idleFrameIndex);
        }
#endif
    }
}
