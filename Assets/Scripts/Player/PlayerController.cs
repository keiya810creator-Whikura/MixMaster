using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MixMaster.Player
{
    public enum Direction8
    {
        Down,
        DownRight,
        Right,
        UpRight,
        Up,
        UpLeft,
        Left,
        DownLeft
    }

    [Serializable]
    public sealed class DirectionSpriteSet
    {
        [Tooltip("下方向。1枚なら静止、複数枚なら歩行アニメーションになります。")]
        public Sprite[] down;

        [Tooltip("右下方向")]
        public Sprite[] downRight;

        [Tooltip("右方向")]
        public Sprite[] right;

        [Tooltip("右上方向")]
        public Sprite[] upRight;

        [Tooltip("上方向")]
        public Sprite[] up;

        [Tooltip("左上方向")]
        public Sprite[] upLeft;

        [Tooltip("左方向")]
        public Sprite[] left;

        [Tooltip("左下方向")]
        public Sprite[] downLeft;

        public Sprite[] GetSprites(Direction8 direction)
        {
            switch (direction)
            {
                case Direction8.Down: return down;
                case Direction8.DownRight: return downRight;
                case Direction8.Right: return right;
                case Direction8.UpRight: return upRight;
                case Direction8.Up: return up;
                case Direction8.UpLeft: return upLeft;
                case Direction8.Left: return left;
                case Direction8.DownLeft: return downLeft;
                default: return down;
            }
        }
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("InputSystem_Actions > Player > Move を指定してください。")]
        [SerializeField] private InputActionProperty moveAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Range(0f, 0.9f)] private float inputDeadZone = 0.1f;

        [Header("8 Direction Sprites")]
        [SerializeField] private DirectionSpriteSet sprites = new DirectionSpriteSet();
        [SerializeField] private Direction8 initialDirection = Direction8.Down;

        [Header("Walk Animation")]
        [SerializeField, Min(0.1f)] private float animationFps = 8f;
        [SerializeField, Min(0)] private int idleFrameIndex = 0;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;

        private Vector2 moveInput;
        private Direction8 facingDirection;
        private Direction8 animationDirection;

        private float animationTimer;
        private int animationFrame;

        public Vector2 MoveInput => moveInput;
        public Direction8 FacingDirection => facingDirection;
        public bool IsMoving => moveInput.sqrMagnitude > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            body.gravityScale = 0f;
            body.freezeRotation = true;

            facingDirection = initialDirection;
            animationDirection = initialDirection;
            RefreshIdleSprite();
        }

        private void OnEnable()
        {
            if (moveAction.action != null)
                moveAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction.action != null)
                moveAction.action.Disable();

            moveInput = Vector2.zero;

            if (body != null)
                body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            ReadMovementInput();

            if (IsMoving)
            {
                Direction8 newDirection = GetDirection8(moveInput);

                if (newDirection != facingDirection)
                {
                    facingDirection = newDirection;
                    animationDirection = newDirection;
                    animationTimer = 0f;
                    animationFrame = 0;
                }

                UpdateWalkAnimation();
            }
            else
            {
                ResetWalkAnimation();
                RefreshIdleSprite();
            }
        }

        private void FixedUpdate()
        {
            body.linearVelocity = moveInput * moveSpeed;
        }

        private void ReadMovementInput()
        {
            if (moveAction.action == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            Vector2 rawInput = moveAction.action.ReadValue<Vector2>();

            if (rawInput.sqrMagnitude < inputDeadZone * inputDeadZone)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = Vector2.ClampMagnitude(rawInput, 1f);
        }

        private void UpdateWalkAnimation()
        {
            Sprite[] frames = sprites.GetSprites(facingDirection);

            if (frames == null || frames.Length == 0)
                return;

            if (animationDirection != facingDirection)
            {
                animationDirection = facingDirection;
                animationTimer = 0f;
                animationFrame = 0;
            }

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

            spriteRenderer.sprite = frames[Mathf.Clamp(animationFrame, 0, frames.Length - 1)];
        }

        private void ResetWalkAnimation()
        {
            animationTimer = 0f;
            animationFrame = 0;
            animationDirection = facingDirection;
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
            moveSpeed = Mathf.Max(0f, moveSpeed);
            animationFps = Mathf.Max(0.1f, animationFps);
            idleFrameIndex = Mathf.Max(0, idleFrameIndex);
        }
#endif
    }
}
