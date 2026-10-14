using System;
using MixMaster.Player;
using UnityEngine;

namespace MixMaster.World
{
    /// <summary>
    /// Drops from the final boss, then automatically accelerates towards
    /// the player. Collection calls a one-shot reward transaction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonRewardChestPickup : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Min(0f)] private float dropWait = 0.65f;
        [SerializeField, Min(0.1f)] private float initialSpeed = 3f;
        [SerializeField, Min(0.1f)] private float acceleration = 12f;
        [SerializeField, Min(0.05f)] private float pickupDistance = 0.25f;
        [SerializeField, Min(0f)] private float initialBounce = 0.55f;

        private Transform target;
        private Action collected;
        private float timeAlive;
        private float speed;
        private Vector3 origin;
        private bool done;

        public void Initialize(Sprite chestSprite, Transform playerTarget,
            Action onCollected)
        {
            target = playerTarget;
            collected = onCollected;
            spriteRenderer = spriteRenderer != null
                ? spriteRenderer : GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 60;
            spriteRenderer.sprite = chestSprite != null
                ? chestSprite : CreateFallbackSprite();
            spriteRenderer.color = Color.white;
            timeAlive = 0f;
            speed = initialSpeed;
            origin = transform.position;
            done = false;
        }

        private void Update()
        {
            if (done) return;
            timeAlive += Time.deltaTime;
            if (timeAlive < dropWait)
            {
                float normalized = dropWait > 0f
                    ? timeAlive / dropWait : 1f;
                // Small non-destructive bounce on landing.
                transform.position = origin + Vector3.up *
                    (Mathf.Sin(normalized * Mathf.PI) * initialBounce);
                return;
            }

            if (target == null)
            {
                PlayerController player =
                    FindFirstObjectByType<PlayerController>();
                if (player != null) target = player.transform;
                if (target == null) return;
            }

            Vector3 difference = target.position - transform.position;
            if (difference.sqrMagnitude <= pickupDistance * pickupDistance)
            {
                CollectOnce();
                return;
            }

            speed += acceleration * Time.deltaTime;
            transform.position = Vector3.MoveTowards(
                transform.position, target.position, speed * Time.deltaTime);
        }

        private void CollectOnce()
        {
            if (done) return;
            done = true;
            Action callback = collected;
            collected = null;
            callback?.Invoke();
            Destroy(gameObject);
        }

        private static Sprite fallback;
        private static Sprite CreateFallbackSprite()
        {
            if (fallback != null) return fallback;
            const int width = 26;
            const int height = 22;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32,
                false);
            tex.name = "DungeonChestFallback";
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[width * height];
            Color outline = new Color(0.19f, 0.12f, 0.08f);
            Color wood = new Color(0.55f, 0.29f, 0.11f);
            Color woodLight = new Color(0.77f, 0.45f, 0.18f);
            Color gold = new Color(1f, 0.83f, 0.30f);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool inside = x >= 2 && x <= 23 && y >= 3 && y <= 18;
                    if (!inside) { pixels[y * width + x] = Color.clear; continue; }
                    bool edge = x == 2 || x == 23 || y == 3 || y == 18;
                    bool band = y == 10 || y == 11 || x == 6 || x == 19;
                    pixels[y * width + x] = edge ? outline :
                        band ? gold :
                        y >= 12 ? woodLight : wood;
                    if (x >= 11 && x <= 14 && y >= 8 && y <= 13)
                        pixels[y * width + x] = gold;
                }

            tex.SetPixels(pixels);
            tex.Apply();
            fallback = Sprite.Create(tex, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), width);
            fallback.name = "DungeonChestFallbackSprite";
            return fallback;
        }
    }
}
