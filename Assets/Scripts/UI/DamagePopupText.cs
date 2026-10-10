using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using MixMaster.Core;

namespace MixMaster.UI
{
    /// <summary>
    /// Lightweight world-space damage number with a small reusable pool.
    /// No scene prefab or Canvas wiring is required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopupText : MonoBehaviour
    {
        private const int MaxActive = 64;
        private const int MaxPooled = 64;
        private const float Duration = 0.72f;
        private const float RiseDistance = 0.75f;

        private static readonly Stack<DamagePopupText> pooled =
            new Stack<DamagePopupText>();
        private static int activeCount;

        private static readonly Color AllyDamageColor =
            new Color(1f, 0.32f, 0.36f, 1f);

        private TextMeshPro label;
        private Vector3 startPosition;
        private Color baseColor;
        private float horizontalDrift;
        private float elapsed;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pooled.Clear();
            activeCount = 0;
        }

        /// <param name="takenByAlly">True for damage to the Player or party.</param>
        public static void Show(Vector3 worldPosition, long damage, bool takenByAlly = false)
        {
            Show(worldPosition, damage, MaterialDropSourceType.Unknown, takenByAlly);
        }

        // The attacker's material-log color is also the damage-number color.
        public static void Show(
            Vector3 worldPosition,
            long damage,
            MaterialDropSourceType attackSource,
            bool takenByAlly = false)
        {
            if (!Application.isPlaying || damage <= 0L || activeCount >= MaxActive)
                return;

            DamagePopupText popup = null;

            // Scene changes destroy pooled GameObjects, so ignore stale entries.
            while (pooled.Count > 0 && popup == null)
                popup = pooled.Pop();

            if (popup == null)
            {
                GameObject go = new GameObject("_DamagePopupText");
                popup = go.AddComponent<DamagePopupText>();
            }

            popup.gameObject.SetActive(true);
            popup.Begin(worldPosition, damage, attackSource, takenByAlly);
        }

        private void Awake()
        {
            label = GetComponent<TextMeshPro>();
            if (label == null)
                label = gameObject.AddComponent<TextMeshPro>();

            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;

            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 4.2f;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.outlineColor = new Color32(23, 18, 27, 255);
            label.outlineWidth = 0.18f;

            transform.localScale = Vector3.one;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 3000;
        }

        private void Begin(Vector3 position, long damage, MaterialDropSourceType attackSource, bool takenByAlly)
        {
            startPosition = position +
                new Vector3(Random.Range(-0.15f, 0.15f), 0.72f, -0.2f);
            horizontalDrift = Random.Range(-0.18f, 0.18f);
            transform.position = startPosition;
            elapsed = 0f;

            baseColor = takenByAlly
                ? AllyDamageColor
                : MaterialDropLogUI.GetSourceColor(attackSource);
            label.text = damage.ToString("N0", CultureInfo.InvariantCulture);
            label.color = baseColor;

            if (!showing)
            {
                showing = true;
                activeCount++;
            }
        }

        private void Update()
        {
            if (!showing)
                return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);

            transform.position = startPosition +
                new Vector3(horizontalDrift * t, RiseDistance * t, 0f);

            Color color = baseColor;
            color.a = t < 0.55f
                ? 1f
                : Mathf.Clamp01((1f - t) / 0.45f);
            label.color = color;

            if (elapsed >= Duration)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (!showing)
                return;

            showing = false;
            activeCount = Mathf.Max(0, activeCount - 1);

            if (pooled.Count < MaxPooled)
                pooled.Push(this);
            else if (Application.isPlaying)
                Destroy(gameObject);
        }
    }
}
