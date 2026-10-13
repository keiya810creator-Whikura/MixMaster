using System;
using System.Globalization;
using MixMaster.Core;
using MixMaster.Monsters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Attach this component to the altar's existing recovery Button.
    /// Restores full HP/MP to the player and all living field party members.
    ///
    /// Dead allies must be revived at their death circle; this button does
    /// not bypass the five-second field revival mechanic.
    /// A single shared cooldown applies across altar buttons and survives
    /// scene changes / app restarts using a UTC timestamp.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AltarPartyRecoveryButton : MonoBehaviour
    {
        private const string CooldownEndKey =
            "MixMaster.AltarPartyRecovery.ReadyUtcTicks";

        [Header("Optional UI")]
        [SerializeField] private Button recoveryButton;
        [Tooltip("Optional: a separate TMP label for remaining time. " +
                 "Leave blank to keep the existing button label untouched.")]
        [SerializeField] private TMP_Text cooldownText;

        [Header("Cooldown")]
        [SerializeField, Min(1f)] private float cooldownSeconds = 180f;
        [SerializeField] private string readyMessage = "HP・MP回復";
        [SerializeField] private string cooldownPrefix = "回復まで ";

        private PlayerManager player;
        private float nextUiRefreshTime;

        public float RemainingSeconds
        {
            get
            {
                long ticks = GetReadyUtcTicks();
                if (ticks <= 0L) return 0f;

                long now = DateTime.UtcNow.Ticks;
                if (ticks <= now) return 0f;

                return (float)Math.Max(0d,
                    (ticks - now) / (double)TimeSpan.TicksPerSecond);
            }
        }

        public bool CanRecover => RemainingSeconds <= 0f;

        private void Awake()
        {
            if (recoveryButton == null)
                recoveryButton = GetComponent<Button>();

            // Setting this script on the button is sufficient; no OnClick
            // Inspector entry is required.
            if (recoveryButton != null)
            {
                recoveryButton.onClick.RemoveListener(TryRecover);
                recoveryButton.onClick.AddListener(TryRecover);
            }
        }

        private void OnEnable()
        {
            nextUiRefreshTime = 0f;
            RefreshVisual();
        }

        private void Update()
        {
            // Unscaled time keeps the label accurate while menus are paused.
            if (Time.unscaledTime < nextUiRefreshTime)
                return;

            nextUiRefreshTime = Time.unscaledTime + 0.2f;
            RefreshVisual();
        }

        private void OnDestroy()
        {
            if (recoveryButton != null)
                recoveryButton.onClick.RemoveListener(TryRecover);
        }

        /// <summary>
        /// Can also be called via a button's OnClick in the Inspector.
        /// Calling it multiple times during cooldown has no effect.
        /// </summary>
        public void TryRecover()
        {
            if (!CanRecover)
            {
                RefreshVisual();
                return;
            }

            if (player == null)
                player = FindFirstObjectByType<PlayerManager>();

            bool foundCharacter = false;
            if (player != null)
            {
                player.RestoreFullResources();
                foundCharacter = true;
            }

            // ActiveMembers includes dead bodies. Do not revive them here.
            var members = PartyMemberCombat.ActiveMembers;
            for (int i = 0; i < members.Count; i++)
            {
                PartyMemberCombat member = members[i];
                if (member == null || !member.IsAlive)
                    continue;

                member.RestoreFullResources();
                foundCharacter = true;
            }

            // Avoid consuming the cooldown before managers/party spawn.
            if (!foundCharacter)
            {
                Debug.LogWarning(
                    "[AltarPartyRecoveryButton] 回復対象を取得できませんでした。", this);
                RefreshVisual();
                return;
            }

            long durationTicks = (long)Math.Ceiling(
                Math.Max(1d, cooldownSeconds) * TimeSpan.TicksPerSecond);
            long nowTicks = DateTime.UtcNow.Ticks;
            long readyTicks = nowTicks > long.MaxValue - durationTicks
                ? long.MaxValue
                : nowTicks + durationTicks;

            PlayerPrefs.SetString(
                CooldownEndKey,
                readyTicks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            float left = RemainingSeconds;
            if (recoveryButton != null)
                recoveryButton.interactable = left <= 0f;

            if (cooldownText == null)
                return;

            if (left <= 0f)
            {
                cooldownText.text = readyMessage;
                return;
            }

            int seconds = Mathf.CeilToInt(left);
            int minutes = seconds / 60;
            int remaining = seconds % 60;
            cooldownText.text = cooldownPrefix +
                minutes.ToString("00") + ":" +
                remaining.ToString("00");
        }

        private static long GetReadyUtcTicks()
        {
            string raw = PlayerPrefs.GetString(CooldownEndKey, "");
            return long.TryParse(raw, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long ticks)
                ? Math.Max(0L, ticks) : 0L;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cooldownSeconds = Mathf.Max(1f, cooldownSeconds);
        }
#endif
    }
}
