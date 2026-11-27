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
        private readonly List<RewardItemRowUI> rewardRows = new List<RewardItemRowUI>();
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
            MaterialSO material = report?.dungeonMaterial;
            string materialName = material == null ? "素材未設定" :
                string.IsNullOrWhiteSpace(material.displayName) ? material.materialId : material.displayName;
            materialRow?.SetData(material != null ? material.icon : null, materialName,
                "×" + (report?.actualMaterialAmount ?? 0L).ToString("N0"));

            long dangerMaterialAmount = report?.actualDangerMaterialAmount ?? 0L;
            bool showDangerMaterial = material != null && dangerMaterialAmount > 0;
            int count = report?.equipment.Count ?? 0;
            if (equipmentCountText != null)
                equipmentCountText.text = $"獲得装備  {count:N0}個" +
                    (showDangerMaterial ? "  /  危険ルート素材" : string.Empty);
            if (bossBonusText != null)
                bossBonusText.text = $"討伐した中ボス: {report?.defeatedMidBosses ?? 0}体   /  装備報酬に反映";
            if (emptyEquipment != null) emptyEquipment.SetActive(count == 0 && !showDangerMaterial);

            int index = 0;
            if (report != null && equipmentRowPrefab != null && equipmentContent != null)
            {
                if (showDangerMaterial)
                    GetRewardRow(index++).SetData(material.icon, materialName,
                        $"×{dangerMaterialAmount:N0}  （危険ルート）");

                foreach (DungeonEquipmentAward award in report.equipment)
                {
                    if (award == null) continue;
                    EquipmentSO definition = award.definition;
                    string label = definition == null ? "装備" :
                        string.IsNullOrWhiteSpace(definition.displayName) ? definition.equipmentId : definition.displayName;
                    GetRewardRow(index++).SetData(definition != null ? definition.icon : null,
                        label + EquipmentDropOptions.Stars(award.dropOptions?.Length ?? 0),
                        $"Lv.{award.level}" +
                        (award.fromDangerRoute ? "  （危険ルート）" : string.Empty) +
                        (award.wasAutoSold ? "  （自動売却済）" : string.Empty),
                        () => OpenEquipmentDetail(award));
                }
            }
            for (int i = index; i < rewardRows.Count; i++)
            {
                rewardRows[i].SetData(null, string.Empty, string.Empty);
                rewardRows[i].gameObject.SetActive(false);
            }
            Canvas.ForceUpdateCanvases();
            if (equipmentScroll != null) equipmentScroll.verticalNormalizedPosition = 1f;
        }

        private RewardItemRowUI GetRewardRow(int index)
        {
            if (index >= rewardRows.Count)
                rewardRows.Add(Instantiate(equipmentRowPrefab, equipmentContent));

            RewardItemRowUI row = rewardRows[index];
            row.gameObject.SetActive(true);
            return row;
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
