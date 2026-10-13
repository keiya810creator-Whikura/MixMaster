using System;
using MixMaster.Player;
using TMPro;
using UnityEngine;

namespace MixMaster.Monsters
{
    /// <summary>
    /// Attached automatically to each field party member by PartyFieldSpawner.
    /// On death, leaves a world-sized circle at the member's death position.
    /// The PLAYER must stand inside it continuously for 5 seconds.
    /// Leaving the circle resets the countdown. Revive restores 30% max HP.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PartyMemberCombat))]
    public sealed class PartyReviveZone : MonoBehaviour
    {
        [Header("Revival")]
        [SerializeField, Min(0.1f)] private float radius = 1.6f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 5f;
        [SerializeField, Range(0.01f, 1f)] private float reviveHpFraction = 0.30f;

        [Header("Circle Appearance")]
        [SerializeField] private Color idleColor =
            new Color(0.34f, 0.75f, 1f, 0.88f);
        [SerializeField] private Color countingColor =
            new Color(0.38f, 1f, 0.68f, 1f);
        [SerializeField, Range(16, 128)] private int ringSegments = 64;
        [SerializeField, Min(0.01f)] private float ringWidth = 0.055f;

        private PartyMemberCombat member;
        private Transform player;
        private GameObject marker;
        private LineRenderer ring;
        private TextMeshPro timerText;
        private Material ringMaterial;
        private float progress;
        private float playerSearchAt;

        public float Progress => progress;
        public bool IsCountingDown =>
            member != null && !member.IsAlive && progress > 0f;

        private void Awake()
        {
            member = GetComponent<PartyMemberCombat>();
        }

        private void OnEnable()
        {
            if (member == null)
                member = GetComponent<PartyMemberCombat>();

            if (member != null)
            {
                member.Died -= HandleDied;
                member.Died += HandleDied;
                if (!member.IsAlive)
                    ShowDeathZone();
            }
        }

        private void OnDisable()
        {
            if (member != null)
                member.Died -= HandleDied;

            ClearDeathZone();
        }

        private void OnDestroy()
        {
            if (member != null)
                member.Died -= HandleDied;

            ClearDeathZone();
        }

        private void HandleDied()
        {
            ShowDeathZone();
        }

        private void Update()
        {
            if (member == null)
                return;

            if (member.IsAlive)
            {
                if (marker != null)
                    ClearDeathZone();
                return;
            }

            if (marker == null)
                ShowDeathZone();

            if (player == null && Time.unscaledTime >= playerSearchAt)
            {
                playerSearchAt = Time.unscaledTime + 0.5f;
                PlayerController controller =
                    FindFirstObjectByType<PlayerController>();
                if (controller != null)
                    player = controller.transform;
                else
                {
                    PlayerTrailRecorder trail =
                        FindFirstObjectByType<PlayerTrailRecorder>();
                    if (trail != null)
                        player = trail.transform;
                }
            }

            bool inRange = player != null &&
                ((Vector2)player.position -
                 (Vector2)marker.transform.position).sqrMagnitude
                <= radius * radius;

            if (!inRange)
            {
                progress = 0f;
                SetVisualState(false);
                if (timerText != null)
                    timerText.text = "近づくと蘇生";
                return;
            }

            // Pausing the game pauses the revival countdown as well.
            progress += Time.deltaTime;
            SetVisualState(true);

            float secondsLeft = Mathf.Max(0f, holdSeconds - progress);
            if (timerText != null)
                timerText.text = "蘇生 " +
                    Mathf.CeilToInt(secondsLeft).ToString() + "秒";

            if (progress < holdSeconds)
                return;

            // Successful revival stops the countdown and removes the circle.
            if (member.ReviveWithHpPercent(reviveHpFraction))
                ClearDeathZone();
        }

        private void ShowDeathZone()
        {
            if (marker != null || member == null || member.IsAlive)
                return;

            progress = 0f;
            marker = new GameObject("RevivalCircle_" + gameObject.name);
            marker.transform.position = transform.position;

            // Deliberately NOT parented under the monster: field allies are
            // scaled to 0.25, but the revival radius is measured in WORLD units.
            ring = marker.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Mathf.Clamp(ringSegments, 16, 128);
            ring.startWidth = ringWidth;
            ring.endWidth = ringWidth;
            ring.numCapVertices = 2;
            ring.sortingOrder = 19;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                ringMaterial = new Material(shader);
                ringMaterial.name = "RuntimePartyRevivalRing";
                ring.material = ringMaterial;
            }

            int count = ring.positionCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius, 0f));
            }

            GameObject textObject = new GameObject("ReviveCountdown");
            textObject.transform.SetParent(marker.transform, false);
            textObject.transform.localPosition =
                new Vector3(0f, radius + 0.26f, 0f);
            textObject.transform.localScale = Vector3.one * 0.19f;

            timerText = textObject.AddComponent<TextMeshPro>();
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.fontSize = 5f;
            timerText.enableWordWrapping = false;
            timerText.text = "近づくと蘇生";
            timerText.color = Color.white;
            MeshRenderer textRenderer = timerText.GetComponent<MeshRenderer>();
            if (textRenderer != null)
                textRenderer.sortingOrder = 20;

            SetVisualState(false);
        }

        private void SetVisualState(bool counting)
        {
            if (ring == null) return;
            Color color = counting ? countingColor : idleColor;
            ring.startColor = color;
            ring.endColor = color;
        }

        private void ClearDeathZone()
        {
            progress = 0f;
            ring = null;
            timerText = null;

            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }

            if (ringMaterial != null)
            {
                Destroy(ringMaterial);
                ringMaterial = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = countingColor;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, radius));
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.1f, radius);
            holdSeconds = Mathf.Max(0.1f, holdSeconds);
            reviveHpFraction = Mathf.Clamp01(reviveHpFraction);
            ringSegments = Mathf.Clamp(ringSegments, 16, 128);
            ringWidth = Mathf.Max(0.01f, ringWidth);
        }
#endif
    }
}
