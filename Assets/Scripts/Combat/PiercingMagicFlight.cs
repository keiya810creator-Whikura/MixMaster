using System.Collections.Generic;
using MixMaster.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MixMaster.Combat
{
    internal sealed class PiercingMagicFlight
    {
        private const float Lifetime = 3f;
        private readonly HashSet<Component> hit = new HashSet<Component>();
        private Component aim;
        private Vector2 direction;
        private float speed;
        private float remaining;
        private bool homing;

        internal bool Expired => remaining <= 0f;

        internal void Begin(Transform origin, Component target, float projectileSpeed, bool useHoming)
        {
            hit.Clear();
            aim = target;
            speed = Mathf.Max(.1f, projectileSpeed);
            remaining = Lifetime;
            homing = useHoming;
            Vector2 offset = target != null ? target.transform.position - origin.position : Vector3.zero;
            direction = offset.sqrMagnitude > .000001f ? offset.normalized : (Vector2)origin.right;
        }

        internal Vector2 Step(Transform shot, bool aimAlive, bool rotate)
        {
            Vector2 start = shot.position;
            if (homing && aimAlive && aim != null && !hit.Contains(aim))
            {
                Vector2 offset = (Vector2)aim.transform.position - start;
                if (offset.sqrMagnitude > .000001f) direction = offset.normalized;
            }

            float elapsed = Mathf.Min(remaining, Time.deltaTime);
            remaining -= elapsed;
            shot.position += (Vector3)(direction * (speed * elapsed));
            if (rotate)
                shot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            return start;
        }

        internal bool TryHit(Component target, PhysicsScene2D scene, Vector2 start, Vector2 end, float radius)
        {
            if (hit.Contains(target) || !target.gameObject.scene.GetPhysicsScene2D().Equals(scene))
                return false;

            Vector2 segment = end - start;
            Vector2 offset = (Vector2)target.transform.position - start;
            float lengthSquared = segment.sqrMagnitude;
            float projection = lengthSquared > 0f
                ? Mathf.Clamp01(Vector2.Dot(offset, segment) / lengthSquared) : 0f;
            return (offset - segment * projection).sqrMagnitude <= radius * radius && hit.Add(target);
        }

        private static Texture2D orbTexture;
        private static Sprite orbSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVisual()
        {
            if (orbSprite != null) Object.Destroy(orbSprite);
            if (orbTexture != null) Object.Destroy(orbTexture);
            orbSprite = null;
            orbTexture = null;
        }

        internal static GameObject CreateShot(GameObject prefab, Vector3 position, Quaternion rotation,
            Scene scene, ElementType element)
        {
            GameObject shot;
            if (prefab != null)
                shot = Object.Instantiate(prefab, position, rotation);
            else
            {
                shot = new GameObject("MagicOrb", typeof(SpriteRenderer));
                shot.transform.SetPositionAndRotation(position, rotation);
                var renderer = shot.GetComponent<SpriteRenderer>();
                renderer.sprite = GetOrbSprite();
                renderer.color = PartyMeleeSwordSwing.ElementColor(element);
                renderer.sortingOrder = 60;
            }

            if (shot.scene != scene) SceneManager.MoveGameObjectToScene(shot, scene);
            return shot;
        }

        private static Sprite GetOrbSprite()
        {
            if (orbSprite != null) return orbSprite;
            const int size = 16;
            orbTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var offset = new Vector2(x + .5f, y + .5f) / (size * .5f) - Vector2.one;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - offset.magnitude) * 3f));
                }
            orbTexture.SetPixels(pixels);
            orbTexture.Apply();
            orbSprite = Sprite.Create(orbTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 48f);
            return orbSprite;
        }
    }
}
