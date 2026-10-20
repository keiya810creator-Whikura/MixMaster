using System;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Reusable, scene-authored dungeon result and reward rows.</summary>
    [DisallowMultipleComponent]
    public sealed class DungeonResultPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private TMP_Text equipmentCountText;
        [SerializeField] private TMP_Text bossBonusText;
        [SerializeField] private GameObject emptyEquipment;
        [SerializeField] private RewardItemRowUI materialRow;
        [SerializeField] private RewardItemRowUI equipmentRowPrefab;
        [SerializeField] private RectTransform equipmentContent;
        [SerializeField] private ScrollRect equipmentScroll;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button retryButton;
        private readonly List<RewardItemRowUI> equipmentRows = new List<RewardItemRowUI>();
        private Action returnCallback;
        private Func<bool> retryCallback;
        private bool usedReturn;
        private DungeonEquipmentDetailPanelUI detailPanel;

        private void Awake()
        {
            if (returnButton != null) returnButton.onClick.AddListener(Return);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
        }
        private void OnDestroy()
        {
            if (returnButton != null) returnButton.onClick.RemoveListener(Return);
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
        }
        private void OnDisable()
        {
            if (detailPanel != null) detailPanel.Close();
            detailPanel = null;
        }

        public static DungeonResultPanelUI Show(DungeonRewardReport report, Action onReturn, Func<bool> onRetry = null)
        {
            DungeonResultPanelUI panel = FindFirstObjectByType<DungeonResultPanelUI>(FindObjectsInactive.Include);
            if (panel == null)
            {
                Debug.LogError("[DungeonResult] Assets/Prefab/UI/DungeonResultPanel.prefab をCanvas下に配置してください。");
                return null;
            }
            panel.Open(report, onReturn, onRetry);
            return panel;
        }

        private void Open(DungeonRewardReport report, Action onReturn, Func<bool> onRetry)
        {
            returnCallback = onReturn;
            retryCallback = onRetry;
            usedReturn = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SetButtonsInteractable(true);
            if (mapNameText != null) mapNameText.text = report != null ? report.mapName : string.Empty;
            MaterialSO material = report != null ? report.dungeonMaterial : null;
            materialRow?.SetData(material != null ? material.icon : null,
                material != null ? string.IsNullOrWhiteSpace(material.displayName) ? material.materialId : material.displayName : "素材未設定",
                "×" + (report != null ? report.actualMaterialAmount : 0L).ToString("N0"));
            int count = report != null ? report.equipment.Count : 0;
            if (equipmentCountText != null) equipmentCountText.text = "獲得装備  " + count.ToString("N0") + "個";
            if (bossBonusText != null) bossBonusText.text = "討伐した中ボス: " + (report != null ? report.defeatedMidBosses : 0) + "体   /  装備報酬に反映";
            if (emptyEquipment != null) emptyEquipment.SetActive(count == 0);
            int index = 0;
            if (report != null && equipmentRowPrefab != null && equipmentContent != null)
                foreach (DungeonEquipmentAward award in report.equipment)
                {
                    if (award == null) continue;
                    if (index >= equipmentRows.Count) equipmentRows.Add(Instantiate(equipmentRowPrefab, equipmentContent));
                    RewardItemRowUI row = equipmentRows[index++];
                    EquipmentSO definition = award.definition;
                    row.gameObject.SetActive(true);
                    row.SetData(definition != null ? definition.icon : null,
                        definition != null ? string.IsNullOrWhiteSpace(definition.displayName) ? definition.equipmentId : definition.displayName : "装備",
                        "Lv." + award.level + (award.wasAutoSold ? "  （自動売却済）" : string.Empty),
                        () => OpenEquipmentDetail(award));
                }
            for (int i = index; i < equipmentRows.Count; i++)
            {
                equipmentRows[i].SetData(null, string.Empty, string.Empty);
                equipmentRows[i].gameObject.SetActive(false);
            }
            Canvas.ForceUpdateCanvases();
            if (equipmentScroll != null) equipmentScroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            gameObject.SetActive(false);
            returnCallback = null;
            retryCallback = null;
        }
        private void Return()
        {
            if (usedReturn) return;
            usedReturn = true;
            SetButtonsInteractable(false);
            returnCallback?.Invoke();
        }
        private void Retry()
        {
            if (usedReturn || retryCallback == null) return;
            usedReturn = true;
            SetButtonsInteractable(false);
            if (!retryCallback())
            {
                usedReturn = false;
                SetButtonsInteractable(true);
            }
        }
        private void SetButtonsInteractable(bool enabled)
        {
            if (returnButton != null) returnButton.interactable = enabled;
            if (retryButton != null) retryButton.interactable = enabled && retryCallback != null;
        }
        private void OpenEquipmentDetail(DungeonEquipmentAward award)
        {
            if (award == null || usedReturn) return;
            if (detailPanel != null) detailPanel.Close();
            detailPanel = DungeonEquipmentDetailPanelUI.Show(transform, award);
        }
    }
}
