using System;
using MixMaster.Core;
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

        [Header("Reincarnation Sprite")]
        [SerializeField, Min(0.001f)] private float reincarnationVisualScale = 0.45f;

        [Header("Walk Animation")]
        [SerializeField, Min(0.1f)] private float animationFps = 8f;
        [SerializeField, Min(0)] private int idleFrameIndex = 0;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private PlayerManager playerManager;
        private Sprite humanFallbackSprite;
        private SpriteRenderer reincarnationRenderer;
        private bool humanRendererEnabled;

        public SpriteRenderer AppearanceRenderer =>
            reincarnationRenderer != null && reincarnationRenderer.enabled
                ? reincarnationRenderer : spriteRenderer;
        public Sprite AppearanceSprite => AppearanceRenderer != null ? AppearanceRenderer.sprite : null;

        private Vector2 moveInput;
        private Direction8 facingDirection;
        private Direction8 animationDirection;

        private float animationTimer;
        private int animationFrame;
        private bool combatMovementLocked;

        public Vector2 MoveInput => moveInput;
        public Direction8 FacingDirection => facingDirection;
        public bool IsMoving => moveInput.sqrMagnitude > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            humanFallbackSprite = spriteRenderer.sprite;
            humanRendererEnabled = spriteRenderer.enabled;

            body.gravityScale = 0f;
            body.freezeRotation = true;

            facingDirection = initialDirection;
            animationDirection = initialDirection;
            RefreshIdleSprite();
        }

        private void OnEnable()
        {
            BindPlayerManager();
            if (moveAction.action != null)
                moveAction.action.Enable();
        }

        private void OnDisable()
        {
            if (playerManager != null)
                playerManager.ReincarnationAppearanceChanged -= ApplyReincarnationAppearance;
            playerManager = null;

            if (moveAction.action != null)
                moveAction.action.Disable();

            moveInput = Vector2.zero;

            if (body != null)
                body.linearVelocity = Vector2.zero;
        }

        private void BindPlayerManager()
        {
            if (playerManager != null) return;
            playerManager = FindFirstObjectByType<PlayerManager>();
            if (playerManager == null) return;

            playerManager.ReincarnationAppearanceChanged += ApplyReincarnationAppearance;
            ApplyReincarnationAppearance(playerManager.ReincarnationSpecies);
        }

        private void ApplyReincarnationAppearance(MonsterSO species)
        {
            Sprite sprite = species != null ? (species.sprite != null ? species.sprite : species.icon) : null;
            if (sprite == null)
            {
                if (reincarnationRenderer != null) reincarnationRenderer.enabled = false;
                spriteRenderer.enabled = humanRendererEnabled;
                spriteRenderer.sprite = humanFallbackSprite;
                ResetWalkAnimation();
                RefreshIdleSprite();
                return;
            }

            if (reincarnationRenderer == null)
            {
                // Only this child renders the monster. The rigidbody,
                // colliders and gameplay transforms remain on the player.
                var visual = new GameObject("ReincarnationVisual");
                visual.layer = gameObject.layer;
                visual.transform.SetParent(transform, false);
                reincarnationRenderer = visual.AddComponent<SpriteRenderer>();
                reincarnationRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
                reincarnationRenderer.maskInteraction = spriteRenderer.maskInteraction;
                reincarnationRenderer.spriteSortPoint = spriteRenderer.spriteSortPoint;
            }

            // Match the effective X/Y scale the sprite would have if the
            // player had been authored at 0.45 under its current parent.
            Vector3 playerScale = transform.localScale;
            reincarnationRenderer.transform.localScale = new Vector3(
                ReincarnationScaleForAxis(playerScale.x), ReincarnationScaleForAxis(playerScale.y), 1f);
            reincarnationRenderer.sprite = sprite;
            SyncReincarnationRenderer();
            reincarnationRenderer.enabled = humanRendererEnabled;
            spriteRenderer.enabled = false;
        }

        private float ReincarnationScaleForAxis(float playerScale) =>
            Mathf.Abs(playerScale) > 0.000001f ? reincarnationVisualScale / Mathf.Abs(playerScale) : 1f;

        private void LateUpdate()
        {
            if (reincarnationRenderer != null && reincarnationRenderer.enabled)
                SyncReincarnationRenderer();
        }

        private void SyncReincarnationRenderer()
        {
            // Existing hit flashes and sprite ordering are authored on the
            // original renderer. Mirror them onto the visible monster.
            reincarnationRenderer.color = spriteRenderer.color;
            reincarnationRenderer.flipX = spriteRenderer.flipX;
            reincarnationRenderer.flipY = spriteRenderer.flipY;
            reincarnationRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            reincarnationRenderer.sortingOrder = spriteRenderer.sortingOrder;
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
            BindPlayerManager();

            if (playerManager?.CurrentHp == 0L)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            float effectiveSpeed = playerManager != null
                ? Mathf.Max(0f, playerManager.Stats.moveSpeed)
                : moveSpeed * MixMaster.Core.StatCalculator.MovementSpeedMultiplier;
            body.linearVelocity = combatMovementLocked || GetComponent<MixMaster.Combat.SkillActor>()?.MovementBlocked == true
                ? Vector2.zero
                : moveInput * effectiveSpeed;
        }

        private void ReadMovementInput()
        {
            if (playerManager?.CurrentHp == 0L || moveAction.action == null)
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
            if (reincarnationRenderer != null && reincarnationRenderer.enabled) return;

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
            if (reincarnationRenderer != null && reincarnationRenderer.enabled) return;

            Sprite[] frames = sprites.GetSprites(facingDirection);

            if (frames == null || frames.Length == 0)
                return;

            int index = Mathf.Clamp(idleFrameIndex, 0, frames.Length - 1);
            spriteRenderer.sprite = frames[index];
        }

        public void SetCombatMovementLocked(bool locked)
        {
            combatMovementLocked = locked;

            if (locked && body != null)
                body.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// Faces the player toward a world-space direction.
        /// Auto combat uses this while the player is standing still.
        /// </summary>
        public void FaceDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.000001f)
                return;

            Direction8 newDirection = GetDirection8(direction);

            if (newDirection == facingDirection)
                return;

            facingDirection = newDirection;
            animationDirection = newDirection;
            animationTimer = 0f;
            animationFrame = 0;

            if (!IsMoving)
                RefreshIdleSprite();
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
