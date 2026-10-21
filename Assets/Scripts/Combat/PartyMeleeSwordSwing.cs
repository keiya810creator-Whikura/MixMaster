using System.Collections;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.Monsters;
using UnityEngine;

namespace MixMaster.Combat
{
    [DisallowMultipleComponent]
    public sealed class PartyMeleeSwordSwing : MonoBehaviour
    {
        [Header("Sword Sprite")]
        [Tooltip("White sword, with its tip facing up (or right for a horizontal sprite). Element tint is applied automatically.")]
        [SerializeField] private Sprite swordSprite;
        [SerializeField, Min(0.03f)] private float swingDuration = 0.12f;
        [SerializeField, Range(15f, 150f)] private float swingArcDegrees = 115f;
        [SerializeField, Min(0.01f)] private float swordHitWidth = 0.18f;
        [SerializeField] private int sortingOrderOffset = 5;

        private Transform pivot;
        private SpriteRenderer blade;
        private Texture2D generatedTexture;
        private Sprite generatedSprite;
        private Coroutine swing;
        private Sprite measuredSprite;
        private float nearEdge;
        private float visibleLength;
        private bool horizontal;
        private readonly HashSet<EnemyHealth> hitTargets = new HashSet<EnemyHealth>();

        public void Play(PartyMemberCombat owner, Vector2 direction, long attackPower,
            MaterialDropSourceInfo source, SpriteRenderer ownerRenderer)
        {
            StopSwing();
            if (owner == null || !owner.IsAlive || !isActiveAndEnabled)
                return;

            EnsureVisual();
            Resize(owner.AttackRange);
            blade.color = ElementColor(owner.Stats.element);
            if (ownerRenderer != null)
            {
                blade.sortingLayerID = ownerRenderer.sortingLayerID;
                blade.sortingOrder = ownerRenderer.sortingOrder + sortingOrderOffset;
            }
            swing = StartCoroutine(Swing(owner, direction, owner.AttackRange, attackPower, source));
        }

        private void EnsureVisual()
        {
            if (pivot == null)
            {
                // World-space root avoids the party actor's 0.25 scale and flips.
                pivot = new GameObject("_PartySwordSwingPivot").transform;
                var visual = new GameObject("SwordSprite");
                visual.transform.SetParent(pivot, false);
                blade = visual.AddComponent<SpriteRenderer>();
            }
            if (swordSprite == null && generatedSprite == null)
                generatedSprite = SwordSpriteUtility.CreateDefault(out generatedTexture, true);
            blade.sprite = swordSprite != null ? swordSprite : generatedSprite;
            pivot.localScale = Vector3.one;
        }

        private void Resize(float reach)
        {
            Sprite sprite = blade.sprite;
            if (measuredSprite != sprite)
            {
                measuredSprite = sprite;
                horizontal = sprite.bounds.size.x > sprite.bounds.size.y;
                nearEdge = horizontal ? sprite.bounds.min.x : sprite.bounds.min.y;
                visibleLength = Mathf.Max(0.01f, horizontal ? sprite.bounds.size.x : sprite.bounds.size.y);
                SwordSpriteUtility.MeasureOpaqueBounds(sprite, horizontal, ref nearEdge, ref visibleLength);
            }
            float scale = Mathf.Max(0.1f, reach) / visibleLength;
            blade.transform.localScale = Vector3.one * scale;
            blade.transform.localRotation = horizontal ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            blade.transform.localPosition = Vector3.up * (-nearEdge * scale);
        }

        private IEnumerator Swing(PartyMemberCombat owner, Vector2 direction, float reach,
            long attackPower, MaterialDropSourceInfo source)
        {
            hitTargets.Clear();
            float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float start = facing - swingArcDegrees * 0.5f;
            float end = facing + swingArcDegrees * 0.5f;
            float duration = Mathf.Min(Mathf.Max(0.03f, swingDuration),
                1f / Mathf.Max(0.1f, owner.Stats.attackSpeed));
            float elapsed = 0f;
            float previousAngle = start;
            Vector2 previousOrigin = owner.transform.position;
            pivot.SetPositionAndRotation(previousOrigin, Quaternion.Euler(0f, 0f, start));
            pivot.gameObject.SetActive(true);
            HitLine(owner, previousOrigin, start, reach, attackPower, source);

            while (elapsed < duration && owner.IsAlive && owner.isActiveAndEnabled)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float angle = Mathf.Lerp(start, end, t * t * (3f - 2f * t));
                Vector2 origin = owner.transform.position;
                int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(angle - previousAngle) / 7f), 1, 32);
                for (int step = 1; step <= steps; step++)
                {
                    float u = step / (float)steps;
                    HitLine(owner, Vector2.Lerp(previousOrigin, origin, u),
                        Mathf.Lerp(previousAngle, angle, u), reach, attackPower, source);
                }
                pivot.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));
                previousAngle = angle;
                previousOrigin = origin;
                yield return null;
            }
            pivot.gameObject.SetActive(false);
            swing = null;
        }

        private void HitLine(PartyMemberCombat owner, Vector2 origin, float angle,
            float reach, long power, MaterialDropSourceInfo source)
        {
            float radians = (angle + 90f) * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;
            // A killing blow can remove an enemy from this registry immediately.
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (i >= enemies.Count) continue;
                EnemyHealth enemy = enemies[i];
                if (enemy == null || hitTargets.Contains(enemy) || !owner.CanHitWithSword(enemy))
                    continue;
                Vector2 center = enemy.transform.position;
                float distance = Mathf.Clamp(Vector2.Dot(center - origin, along), 0f, reach);
                Vector2 onBlade = origin + along * distance;
                Collider2D collider = enemy.GetComponent<Collider2D>() ?? enemy.GetComponentInChildren<Collider2D>();
                Vector2 nearest = collider != null && collider.enabled ? collider.ClosestPoint(onBlade) : center;
                if ((nearest - onBlade).sqrMagnitude > swordHitWidth * swordHitWidth)
                    continue;
                // Register first: death callbacks may select the next target.
                hitTargets.Add(enemy);
                enemy.TakePhysicalHit(power, source);
            }
        }

        public void StopSwing()
        {
            if (swing != null) StopCoroutine(swing);
            swing = null;
            if (pivot != null) pivot.gameObject.SetActive(false);
            hitTargets.Clear();
        }

        private void OnDisable() => StopSwing();

        private void OnDestroy()
        {
            StopSwing();
            if (pivot != null) Destroy(pivot.gameObject);
            if (generatedSprite != null) Destroy(generatedSprite);
            if (generatedTexture != null) Destroy(generatedTexture);
        }

        public static Color ElementColor(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1f, 0.3f, 0.2f);
                case ElementType.Water: return new Color(0.3f, 0.65f, 1f);
                case ElementType.Wind: return new Color(0.35f, 1f, 0.55f);
                case ElementType.Lightning: return new Color(1f, 0.85f, 0.2f);
                case ElementType.Light: return new Color(1f, 0.97f, 0.7f);
                case ElementType.Dark: return new Color(0.7f, 0.35f, 1f);
                default: return Color.white;
            }
        }
    }
}
