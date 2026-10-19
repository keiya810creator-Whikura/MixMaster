using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardsUI : MonoBehaviour
    {
        [SerializeField] private Button recordButton;
        [SerializeField] private Button cancelRecordingButton;
        [SerializeField] private TMP_Text recordButtonText;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text rewardsText;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text acceptText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button keepButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect rewardsScroll;
        private readonly Dictionary<string, string> materialNames =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private OfflineRewardManager manager;
        private bool subscribed, pausedByPanel;
        private float previousTimeScale = 1f;
        private float retryAt;

        private void Awake()
        {
            if (recordButton != null) recordButton.onClick.AddListener(BeginRecording);
            if (cancelRecordingButton != null) cancelRecordingButton.onClick.AddListener(CancelRecording);
            if (acceptButton != null) acceptButton.onClick.AddListener(Accept);
            if (keepButton != null) keepButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panelRoot != null) panelRoot.SetActive(false);
            CacheMaterialNames();
        }
        private void OnEnable() { Bind(); Refresh(); }
        private void OnDisable()
        {
            if (manager != null && subscribed) manager.Changed -= Refresh;
            subscribed = false;
            SetPanelVisible(false);
        }
        private void Update()
        {
            if (!subscribed && Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 0.5f;
                Bind();
                Refresh();
            }
            if (manager != null && manager.IsRecording && recordButtonText != null)
                recordButtonText.text = "記録中 " + Duration(manager.RemainingSeconds);
        }
        private void Bind()
        {
            if (manager == null) manager = FindFirstObjectByType<OfflineRewardManager>();
            if (manager == null || subscribed) return;
            manager.Changed += Refresh;
            subscribed = true;
        }
        private void CacheMaterialNames()
        {
            MonsterCatalogSO monsters = MonsterCatalogSO.Load();
            if (monsters != null)
                foreach (MonsterSO monster in monsters.Monsters)
                    if (monster != null) AddMaterial(monster.uniqueMaterial);
            MapCatalogSO maps = MapCatalogSO.Load();
            if (maps != null)
                foreach (MapSO map in maps.Maps)
                    if (map != null) AddMaterial(map.dungeonMaterial);
        }
        private void AddMaterial(MaterialSO material)
        {
            if (material != null && !string.IsNullOrWhiteSpace(material.materialId))
                materialNames[material.materialId] = string.IsNullOrWhiteSpace(material.displayName)
                    ? material.materialId : material.displayName;
        }
        private void BeginRecording()
        {
            if (manager == null) return;
            if (manager.PendingProfile != null) { Refresh(); return; }
            MapManager map = FindFirstObjectByType<MapManager>();
            if (!manager.TryBeginRecording(map != null ? map.CurrentMapId : string.Empty, out string error))
                Debug.LogWarning("[OfflineReward] " + error, this);
        }
        private void CancelRecording() { if (manager != null) manager.CancelRecording(); }

        private void Refresh()
        {
            bool ready = manager != null && manager.IsReady;
            if (recordButton != null) recordButton.interactable = ready && !manager.IsRecording;
            if (cancelRecordingButton != null)
                cancelRecordingButton.gameObject.SetActive(ready && manager.IsRecording);
            if (recordButtonText != null) recordButtonText.text = !ready ? "記録モード" :
                manager.IsRecording ? "記録中 " + Duration(manager.RemainingSeconds) :
                manager.PendingProfile != null ? "記録結果を確認" : "記録モード（3分）";
            if (!ready) { SetPanelVisible(false); return; }

            OfflineRecordProfile pending = manager.PendingProfile;
            OfflineRewardReport report = manager.LatestReport;
            if (pending == null && report == null) { SetPanelVisible(false); return; }
            bool confirmation = pending != null;
            if (titleText != null) titleText.text = confirmation ? "3分間の記録結果" : "オフライン報酬";
            if (summaryText != null) summaryText.text = confirmation
                ? "1分当たりの入手数" : "放置時間 " + Duration(report.elapsedSeconds) + " ／ 受取済み";
            if (rewardsText != null) rewardsText.text = BuildRewards(
                confirmation ? pending.gains : report.gains, confirmation ? pending : null);
            if (questionText != null) questionText.text = confirmation
                ? "これでオフライン報酬を上書きしてもよろしいですか？\nオフライン中は、この取得量の50％を獲得します。"
                : "素材・ゴールド・経験値を付与しました。\n経験値は主人公と現在のパーティに付与されます。";
            if (acceptText != null) acceptText.text = confirmation ? "上書きする" : "閉じる";
            if (keepButton != null) keepButton.gameObject.SetActive(confirmation);
            SetPanelVisible(true);
            Canvas.ForceUpdateCanvases();
            if (rewardsScroll != null && rewardsScroll.content != null && rewardsScroll.viewport != null && rewardsText != null)
            {
                RectTransform content = rewardsScroll.content;
                content.sizeDelta = new Vector2(content.sizeDelta.x,
                    Mathf.Max(rewardsScroll.viewport.rect.height, rewardsText.preferredHeight + 28f));
                rewardsScroll.verticalNormalizedPosition = 1f;
            }
        }

        private string BuildRewards(IReadOnlyList<OfflineGainRecord> gains, OfflineRecordProfile rateProfile)
        {
            OfflineGainRecord gold = null, exp = null;
            var materials = new List<OfflineGainRecord>();
            if (gains != null)
                foreach (OfflineGainRecord gain in gains)
                {
                    if (gain == null) continue;
                    if (gain.kind == OfflineGainKind.Gold) gold = gain;
                    else if (gain.kind == OfflineGainKind.Experience) exp = gain;
                    else materials.Add(gain);
                }
            string unit = rateProfile != null ? " / 分" : string.Empty;
            var text = new StringBuilder("ゴールド　").Append(Amount(gold, rateProfile))
                .Append(" G").Append(unit).Append("\n経験値　")
                .Append(Amount(exp, rateProfile)).Append(" EXP").Append(unit).Append("\n\n素材");
            materials.Sort((a, b) => string.Compare(Name(a.itemId), Name(b.itemId), StringComparison.CurrentCulture));
            if (materials.Count == 0) text.Append("\n（なし）");
            foreach (OfflineGainRecord gain in materials)
            {
                text.Append("\n").Append(Name(gain.itemId)).Append("　").Append(Amount(gain, rateProfile)).Append(unit);
            }
            return text.ToString();
        }
        private static string Amount(OfflineGainRecord gain, OfflineRecordProfile profile) => gain == null ? "0" :
            profile == null ? gain.amount.ToString("N0", CultureInfo.InvariantCulture) :
            OfflineRewardRules.PerMinute(profile, gain).ToString("N3", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        private string Name(string id) => id != null && materialNames.TryGetValue(id, out string name) ? name : id ?? string.Empty;
        private static string Duration(double seconds)
        {
            long whole = (long)Math.Ceiling(Math.Max(0d, seconds));
            long hours = whole / 3600;
            return hours > 0 ? hours + "時間 " + ((whole / 60) % 60).ToString("00") + "分"
                : (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
        private void Accept()
        {
            if (manager == null) return;
            if (manager.PendingProfile == null) { manager.AcknowledgeReport(); return; }
            if (!manager.TryAcceptPendingProfile(out string error) && questionText != null)
                questionText.text = error;
        }
        private void Close()
        {
            if (manager == null) { SetPanelVisible(false); return; }
            if (manager.PendingProfile == null) manager.AcknowledgeReport();
            else if (!manager.TryDiscardPendingProfile(out string error) && questionText != null)
                questionText.text = error;
        }
        private void SetPanelVisible(bool visible)
        {
            if (panelRoot == null) return;
            if (visible && !panelRoot.activeSelf)
            {
                previousTimeScale = Time.timeScale;
                pausedByPanel = previousTimeScale > 0f;
                if (pausedByPanel) Time.timeScale = 0f;
            }
            panelRoot.SetActive(visible);
            if (!visible && pausedByPanel)
            {
                if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
                pausedByPanel = false;
            }
        }
    }
}
