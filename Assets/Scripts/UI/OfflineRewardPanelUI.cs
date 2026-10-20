using System;
using System.Collections.Generic;
using System.Globalization;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button keepButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect rewardsScroll;
        [SerializeField] private RectTransform rewardsContent;
        [SerializeField] private RewardItemRowUI rowPrefab;
        private readonly Dictionary<string, MaterialSO> materials = new Dictionary<string, MaterialSO>(StringComparer.Ordinal);
        private readonly List<RewardItemRowUI> rows = new List<RewardItemRowUI>();
        private bool pausedByPanel;
        private float previousTimeScale;
        private string originalQuestion;
        public bool IsVisible => gameObject.activeSelf;
        public event Action Accepted;
        public event Action Dismissed;

        private void Awake()
        {
            if (acceptButton != null) acceptButton.onClick.AddListener(Accept);
            if (keepButton != null) keepButton.onClick.AddListener(Dismiss);
            if (closeButton != null) closeButton.onClick.AddListener(Dismiss);
            originalQuestion = questionText != null ? questionText.text : string.Empty;
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
                materials[material.materialId] = material;
        }
        private void OnDisable() => ReleasePause();
        private void OnDestroy()
        {
            if (acceptButton != null) acceptButton.onClick.RemoveListener(Accept);
            if (keepButton != null) keepButton.onClick.RemoveListener(Dismiss);
            if (closeButton != null) closeButton.onClick.RemoveListener(Dismiss);
            ReleasePause();
        }
        private void Accept() => Accepted?.Invoke();
        private void Dismiss() => Dismissed?.Invoke();

        public void Show(string summary, IReadOnlyList<OfflineGainRecord> gains, OfflineRecordProfile profile)
        {
            bool opening = !gameObject.activeSelf;
            gameObject.SetActive(true);
            if (opening)
            {
                previousTimeScale = Time.timeScale;
                pausedByPanel = previousTimeScale > 0f;
                if (pausedByPanel) Time.timeScale = 0f;
                transform.SetAsLastSibling();
            }
            if (summaryText != null) summaryText.text = summary;
            if (questionText != null) questionText.text = originalQuestion;
            OfflineGainRecord gold = null, exp = null;
            var materialGains = new List<OfflineGainRecord>();
            if (gains != null)
                foreach (OfflineGainRecord gain in gains)
                {
                    if (gain == null) continue;
                    if (gain.kind == OfflineGainKind.Gold) gold = gain;
                    else if (gain.kind == OfflineGainKind.Experience) exp = gain;
                    else if (gain.kind == OfflineGainKind.Material) materialGains.Add(gain);
                }
            materialGains.Sort((a, b) => string.Compare(MaterialName(a.itemId), MaterialName(b.itemId), StringComparison.CurrentCulture));
            int index = 0;
            AddRow(index++, null, "ゴールド", Amount(gold, profile) + " G" + Unit(profile));
            AddRow(index++, null, "経験値", Amount(exp, profile) + " EXP" + Unit(profile));
            foreach (OfflineGainRecord gain in materialGains)
            {
                materials.TryGetValue(gain.itemId ?? string.Empty, out MaterialSO material);
                AddRow(index++, material != null ? material.icon : null, MaterialName(gain.itemId), Amount(gain, profile) + Unit(profile));
            }
            for (int i = index; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            if (rewardsScroll != null && opening) rewardsScroll.verticalNormalizedPosition = 1f;
        }
        private void AddRow(int index, Sprite sprite, string label, string amount)
        {
            if (rowPrefab == null || rewardsContent == null) return;
            if (index >= rows.Count) rows.Add(Instantiate(rowPrefab, rewardsContent));
            rows[index].gameObject.SetActive(true);
            rows[index].SetData(sprite, label, amount);
        }
        private string MaterialName(string id) => id != null && materials.TryGetValue(id, out MaterialSO material)
            ? string.IsNullOrWhiteSpace(material.displayName) ? id : material.displayName : id ?? string.Empty;
        private static string Unit(OfflineRecordProfile profile) => profile != null ? " / 分" : string.Empty;
        private static string Amount(OfflineGainRecord gain, OfflineRecordProfile profile) => gain == null ? "0" :
            profile == null ? gain.amount.ToString("N0", CultureInfo.InvariantCulture) :
            OfflineRewardRules.PerMinute(profile, gain).ToString("N3", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        public static string Duration(double seconds)
        {
            long whole = (long)Math.Ceiling(Math.Max(0d, seconds));
            long hours = whole / 3600;
            return hours > 0 ? hours + "時間 " + ((whole / 60) % 60).ToString("00") + "分"
                : (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
        public void ShowError(string error) { if (questionText != null) questionText.text = error; }
        public void Hide()
        {
            gameObject.SetActive(false);
            ReleasePause();
        }
        private void ReleasePause()
        {
            if (pausedByPanel && Time.timeScale == 0f) Time.timeScale = previousTimeScale;
            pausedByPanel = false;
        }
    }
}
