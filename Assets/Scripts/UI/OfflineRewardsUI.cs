using MixMaster.Core;
using UnityEngine;

namespace MixMaster.UI
{
    /// <summary>Connects independently placed recording and receipt prefabs.</summary>
    [DisallowMultipleComponent]
    public sealed class OfflineRewardsUI : MonoBehaviour
    {
        [SerializeField] private OfflineRecordButtonUI recordControls;
        [SerializeField] private OfflineRewardPanelUI recordingPanel;
        [SerializeField] private OfflineRewardPanelUI receiptPanel;

        private OfflineRewardManager manager;
        private PlayerManager player;
        private OfflineRewardReport receipt;
        private bool doubledReceipt;
        private bool claiming;
        private bool playerDefeated;
        private bool refreshForHealth;
        private bool subscribed;
        private float retryAt;

        private void Awake()
        {
            if (recordControls != null)
            {
                recordControls.RecordRequested += BeginRecording;
                recordControls.CancelRequested += CancelRecording;
            }
            if (recordingPanel != null)
            {
                recordingPanel.Accepted += AcceptRecording;
                recordingPanel.Dismissed += DiscardRecording;
            }
            if (receiptPanel != null)
            {
                receiptPanel.Accepted += ReceiveNormallyOrClose;
                receiptPanel.Dismissed += ReceiveNormallyOrClose;
                receiptPanel.AdClaimRequested += ReceiveWithAd;
            }
        }

        private void OnEnable() { Bind(); Refresh(); }
        private void OnDisable()
        {
            if (manager != null && subscribed)
            {
                manager.Changed -= Refresh;
                manager.RecordingCompleted -= ShowRecording;
            }
            if (player != null) player.HpChanged -= OnPlayerHealthChanged;
            subscribed = false;
            HidePanels();
        }
        private void OnDestroy()
        {
            if (recordControls != null)
            {
                recordControls.RecordRequested -= BeginRecording;
                recordControls.CancelRequested -= CancelRecording;
            }
            if (recordingPanel != null)
            {
                recordingPanel.Accepted -= AcceptRecording;
                recordingPanel.Dismissed -= DiscardRecording;
            }
            if (receiptPanel != null)
            {
                receiptPanel.Accepted -= ReceiveNormallyOrClose;
                receiptPanel.Dismissed -= ReceiveNormallyOrClose;
                receiptPanel.AdClaimRequested -= ReceiveWithAd;
            }
        }
        private void Update()
        {
            if (refreshForHealth)
            {
                refreshForHealth = false;
                Refresh();
            }
            if (!subscribed && Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 0.5f;
                Bind();
                Refresh();
            }
            if (manager != null && manager.IsRecording)
                recordControls?.Refresh(manager);
        }
        private void Bind()
        {
            if (manager == null) manager = FindFirstObjectByType<OfflineRewardManager>();
            if (manager == null || subscribed) return;
            manager.Changed += Refresh;
            manager.RecordingCompleted += ShowRecording;
            player = manager.GetComponent<PlayerManager>();
            if (player != null)
            {
                playerDefeated = player.CurrentHp <= 0L;
                player.HpChanged += OnPlayerHealthChanged;
            }
            subscribed = true;
        }

        private void OnPlayerHealthChanged(long hp, long maximum)
        {
            bool defeated = hp <= 0L;
            if (defeated == playerDefeated) return;
            playerDefeated = defeated;
            // Wait until the death flow has restored the gameplay time scale.
            refreshForHealth = true;
        }
        private void BeginRecording()
        {
            if (claiming || playerDefeated || manager == null) return;
            if (manager.PendingProfile != null) { ShowRecording(); return; }
            MapManager map = FindFirstObjectByType<MapManager>();
            if (!manager.TryBeginRecording(map != null ? map.CurrentMapId : string.Empty, out string error))
                Debug.LogWarning("[OfflineReward] " + error, this);
        }
        private void CancelRecording() => manager?.CancelRecording();

        private void Refresh()
        {
            recordControls?.Refresh(manager);
            if (claiming) return;
            if (manager == null || !manager.IsReady || manager.IsAway || playerDefeated)
            {
                HidePanels();
                return;
            }
            // A saved pending sample is available through the record button.
            // Changed/load/focus notifications never open its confirmation.
            if (manager.PendingProfile == null) recordingPanel?.Hide();
            if (receiptPanel == null) return;
            if (manager.PendingReward != null) receipt = null;
            OfflineRewardReport report = manager.PendingReward ?? receipt;
            if (report == null) { receiptPanel.Hide(); return; }
            if (recordingPanel != null && recordingPanel.IsVisible) return;

            bool canClaim = manager.PendingReward != null;
            string status = canClaim ? "受取前" : doubledReceipt ? "受取済み（2倍）" : "受取済み";
            receiptPanel.Show("放置時間 " + OfflineRewardPanelUI.Duration(report.elapsedSeconds) +
                " ／ " + status, report.gains, null);
            receiptPanel.SetClaimControls(canClaim, false);
            if (!canClaim) receiptPanel.ShowError("素材・ゴールド・経験値を受け取りました。");
        }
        private void ShowRecording()
        {
            if (claiming || playerDefeated || manager == null || !manager.IsReady ||
                manager.IsAway || manager.PendingProfile == null) return;
            receiptPanel?.Hide();
            recordingPanel?.Show("1分当たりの入手数", manager.PendingProfile.gains, manager.PendingProfile, manager.LastCompletedProfile);
        }
        private void AcceptRecording()
        {
            if (manager != null && !manager.TryAcceptPendingProfile(out string error))
                recordingPanel?.ShowError(error);
        }
        private void DiscardRecording()
        {
            if (manager != null && !manager.TryDiscardPendingProfile(out string error))
                recordingPanel?.ShowError(error);
        }
        private void ReceiveNormallyOrClose()
        {
            if (claiming || manager == null || manager.IsAway) return;
            if (receipt != null)
            {
                receipt = null;
                Refresh();
                return;
            }

            claiming = true;
            manager.TryClaimReward(manager.PendingReward, false, out OfflineRewardReport awarded);
            FinishClaim(awarded, false);
        }

        private void ReceiveWithAd()
        {
            if (claiming || manager == null || manager.IsAway || manager.PendingReward == null) return;
            var ads = RewardedAdManager.Instance;
            if (ads == null)
            {
                receiptPanel.ShowError("広告を利用できません。");
                return;
            }

            OfflineRewardManager rewards = manager;
            OfflineRewardReport pending = rewards.PendingReward;
            claiming = true;
            receiptPanel.SetClaimControls(true, true);
            if (ads.TryShow(message =>
            {
                OfflineRewardReport awarded = null;
                if (message == null && rewards != null)
                    rewards.TryClaimReward(pending, true, out awarded);
                if (this != null)
                    FinishClaim(awarded, true, message ?? (awarded == null ? "この報酬は受取済みです。" : null));
            }, out string error)) return;

            FinishClaim(null, true, error);
        }

        private void FinishClaim(OfflineRewardReport awarded, bool doubled, string error = null)
        {
            claiming = false;
            if (awarded != null)
            {
                receipt = awarded;
                doubledReceipt = doubled;
            }
            if (!isActiveAndEnabled) return;
            Refresh();
            if (error != null && receiptPanel != null) receiptPanel.ShowError(error);
        }

        private void HidePanels() { recordingPanel?.Hide(); receiptPanel?.Hide(); }
    }
}
