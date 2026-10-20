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
                receiptPanel.Accepted += AcknowledgeReport;
                receiptPanel.Dismissed += AcknowledgeReport;
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
                receiptPanel.Accepted -= AcknowledgeReport;
                receiptPanel.Dismissed -= AcknowledgeReport;
            }
        }
        private void Update()
        {
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
            subscribed = true;
        }
        private void BeginRecording()
        {
            if (manager == null) return;
            if (manager.PendingProfile != null) { ShowRecording(); return; }
            MapManager map = FindFirstObjectByType<MapManager>();
            if (!manager.TryBeginRecording(map != null ? map.CurrentMapId : string.Empty, out string error))
                Debug.LogWarning("[OfflineReward] " + error, this);
        }
        private void CancelRecording() => manager?.CancelRecording();

        private void Refresh()
        {
            recordControls?.Refresh(manager);
            if (manager == null || !manager.IsReady || manager.IsAway)
            {
                HidePanels();
                return;
            }
            // A saved pending sample is available through the record button.
            // Changed/load/focus notifications never open its confirmation.
            if (manager.PendingProfile == null) recordingPanel?.Hide();
            if (manager.LatestReport == null) receiptPanel?.Hide();
            else if (recordingPanel == null || !recordingPanel.IsVisible)
            {
                OfflineRewardReport report = manager.LatestReport;
                receiptPanel?.Show("放置時間 " + OfflineRewardPanelUI.Duration(report.elapsedSeconds) + " ／ 受取済み",
                    report.gains, null);
            }
        }
        private void ShowRecording()
        {
            if (manager == null || !manager.IsReady || manager.IsAway || manager.PendingProfile == null) return;
            receiptPanel?.Hide();
            recordingPanel?.Show("1分当たりの入手数", manager.PendingProfile.gains, manager.PendingProfile);
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
        private void AcknowledgeReport() => manager?.AcknowledgeReport();
        private void HidePanels() { recordingPanel?.Hide(); receiptPanel?.Hide(); }
    }
}
