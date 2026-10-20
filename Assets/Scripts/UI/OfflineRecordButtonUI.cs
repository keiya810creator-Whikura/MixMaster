using System;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OfflineRecordButtonUI : MonoBehaviour
    {
        [SerializeField] private Button recordButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text recordButtonText;
        public event Action RecordRequested;
        public event Action CancelRequested;

        private void Awake()
        {
            if (recordButton != null) recordButton.onClick.AddListener(RequestRecord);
            if (cancelButton != null) cancelButton.onClick.AddListener(RequestCancel);
        }
        private void OnDestroy()
        {
            if (recordButton != null) recordButton.onClick.RemoveListener(RequestRecord);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(RequestCancel);
        }
        private void RequestRecord() => RecordRequested?.Invoke();
        private void RequestCancel() => CancelRequested?.Invoke();

        public void Refresh(OfflineRewardManager manager)
        {
            bool ready = manager != null && manager.IsReady && !manager.IsAway;
            bool recording = ready && manager.IsRecording;
            if (recordButton != null) recordButton.interactable = ready && !recording;
            if (cancelButton != null) cancelButton.gameObject.SetActive(recording);
            if (recordButtonText != null) recordButtonText.text = recording
                ? "記録中 " + OfflineRewardPanelUI.Duration(manager.RemainingSeconds)
                : ready && manager.PendingProfile != null ? "記録結果を確認" : "記録モード（3分）";
        }
    }
}
