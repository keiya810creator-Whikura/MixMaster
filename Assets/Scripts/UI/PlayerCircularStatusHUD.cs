using System;
using MixMaster.Core;
using MixMaster.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Player portrait with radial HP/MP and horizontal EXP.
    /// The portrait opens the player status menu and also invokes the
    /// existing UnityEvent for optional custom behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCircularStatusHUD : MonoBehaviour
    {
        [Header("Fill Images")]
        [SerializeField] private Image hpFill;
        [SerializeField] private Image mpFill;
        [SerializeField] private Image expFill;
        [Header("Portrait")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Button portraitButton;
        [SerializeField] private Sprite portraitOverride;
        [SerializeField] private TMP_Text levelText;
        [Header("Status Menu")]
        [SerializeField] private PlayerStatusMenuUI statusMenu;
        [SerializeField] private UnityEvent onStatusMenuRequested = new UnityEvent();

        private PlayerManager player;
        private PlayerController playerController;
        private SpriteRenderer playerRenderer;
        private bool subscribed;
        private float nextAvatarRefreshTime;

        public UnityEvent StatusMenuRequested => onStatusMenuRequested;

        public void SetStatusMenu(PlayerStatusMenuUI panel)
        {
            statusMenu = panel;
        }

        public void SetReferences(
            Image hp, Image mp, Image exp,
            Image portrait, Button button, TMP_Text level)
        {
            hpFill = hp;
            mpFill = mp;
            expFill = exp;
            portraitImage = portrait;
            portraitButton = button;
            levelText = level;
        }

        private void Awake()
        {
            if (portraitButton != null)
                portraitButton.onClick.AddListener(OpenStatusMenu);
        }

        private void OnEnable()
        {
            EnsureBindings();
            RefreshAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (portraitButton != null)
                portraitButton.onClick.RemoveListener(OpenStatusMenu);
        }

        private void Update()
        {
            if (player == null)
            {
                EnsureBindings();
                RefreshAll();
            }

            if (Time.unscaledTime >= nextAvatarRefreshTime)
            {
                nextAvatarRefreshTime = Time.unscaledTime + 0.15f;
                RefreshPortrait();
            }
        }

        private void EnsureBindings()
        {
            if (player == null)
            {
                Unsubscribe();
                player = FindFirstObjectByType<PlayerManager>();
            }

            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (playerRenderer == null && playerController != null)
                playerRenderer =
                    playerController.GetComponentInChildren<SpriteRenderer>();

            if (player == null || subscribed)
                return;

            player.HpChanged += HandleResourceChanged;
            player.MpChanged += HandleResourceChanged;
            player.ExperienceChanged += HandleExperienceChanged;
            player.LeveledUp += HandleLeveledUp;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (player != null && subscribed)
            {
                player.HpChanged -= HandleResourceChanged;
                player.MpChanged -= HandleResourceChanged;
                player.ExperienceChanged -= HandleExperienceChanged;
                player.LeveledUp -= HandleLeveledUp;
            }

            subscribed = false;
        }

        private void HandleResourceChanged(long current, long maximum)
        {
            RefreshAll();
        }

        private void HandleExperienceChanged(long current, long required)
        {
            RefreshAll();
        }

        private void HandleLeveledUp(int newLevel)
        {
            RefreshAll();
        }

        public void OpenStatusMenu()
        {
            if (statusMenu == null)
                statusMenu = FindFirstObjectByType<PlayerStatusMenuUI>(
                    FindObjectsInactive.Include);

            if (statusMenu != null)
                statusMenu.Open();
            else
                Debug.LogWarning(
                    "[PlayerCircularStatusHUD] ステータスメニューがありません。" +
                    "Unityメニューから『プレイヤーステータスメニューを作成・接続』" +
                    "を実行してください。", this);

            // Preserve custom listeners in existing HUD instances.
            onStatusMenuRequested?.Invoke();
        }

        private void RefreshAll()
        {
            if (player == null)
            {
                SetFill(hpFill, 0f);
                SetFill(mpFill, 0f);
                SetFill(expFill, 0f);
                if (levelText != null) levelText.text = "Lv.-";
                RefreshPortrait();
                return;
            }

            SetFill(hpFill, Ratio(player.CurrentHp, player.Stats.maxHp));
            SetFill(mpFill, Ratio(player.CurrentMp, player.Stats.maxMp));
            long required = player.ExperienceToNextLevel;
            SetFill(expFill, required > 0L
                ? Ratio(player.Experience, required) : 1f);

            if (levelText != null)
            {
                string value = "Lv." + player.Level;
                if (levelText.text != value) levelText.text = value;
            }

            RefreshPortrait();
        }

        private void RefreshPortrait()
        {
            if (portraitImage == null)
                return;

            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (playerRenderer == null && playerController != null)
                playerRenderer =
                    playerController.GetComponentInChildren<SpriteRenderer>();

            Sprite sprite = portraitOverride != null
                ? portraitOverride
                : playerRenderer != null ? playerRenderer.sprite : null;

            if (portraitImage.sprite != sprite)
                portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
        }

        private static float Ratio(long current, long max)
        {
            return max > 0L
                ? Mathf.Clamp01((float)((double)current / max))
                : 0f;
        }

        private static void SetFill(Image image, float amount)
        {
            if (image != null)
                image.fillAmount = amount;
        }
    }
}
