using System;
using System.Collections.Generic;
using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Runtime-built, mobile-friendly breeding shop. Select two owned monsters,
    /// preview the deterministic child, then explicitly confirm consumption.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreedingPanelUI : MonoBehaviour
    {
        private const int PageSize = 10;
        private const int MinimumParentLevel = 10;

        [Header("Runtime References")]
        [SerializeField] private MonsterManager monsterManager;
        [SerializeField] private BreedingManager breedingManager;
        [SerializeField] private MonsterCatalogSO monsterCatalog;
        [SerializeField] private MaterialInventoryManager materialInventory;
        [SerializeField] private EquipmentManager equipmentManager;

        [Header("UI")]
        [SerializeField] private Button parentAButton;
        [SerializeField] private Button parentBButton;
        [SerializeField] private Button parentADetailButton;
        [SerializeField] private Button parentBDetailButton;
        [SerializeField] private TMP_Text parentAText;
        [SerializeField] private TMP_Text parentBText;
        [SerializeField] private Image parentAIcon;
        [SerializeField] private Image parentBIcon;
        [SerializeField] private TMP_Text previewText;
        [SerializeField] private Image previewIcon;
        [SerializeField] private TMP_Text successRateText;
        [SerializeField] private TMP_Text materialAText;
        [SerializeField] private TMP_Text materialBText;
        [SerializeField] private Button materialAPlusButton;
        [SerializeField] private Button materialAMinusButton;
        [SerializeField] private Button materialBPlusButton;
        [SerializeField] private Button materialBMinusButton;
        [SerializeField] private TMP_Text selectionHeader;
        [SerializeField] private Toggle individualValuesToggle;
        [SerializeField] private TMP_Dropdown sortDropdown;
        [SerializeField] private TMP_Dropdown statFilterDropdown;
        [SerializeField] private TMP_Dropdown thresholdDropdown;
        [SerializeField] private Button sortDirectionButton;
        [SerializeField] private TMP_Text sortDirectionText;
        [SerializeField] private OwnedMonsterDetailUI detailPanel;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private Button previewButton;
        [SerializeField] private Button breedButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private ScrollRect listScroll;
        [SerializeField] private GameObject confirmationOverlay;
        [SerializeField] private TMP_Text confirmationText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private readonly List<OwnedMonsterRecord> candidates = new List<OwnedMonsterRecord>();
        // Serialized into the editable prefab; remains stable even when rows
        // are renamed or repositioned in Prefab Mode.
        [SerializeField] private List<CandidateRow> rows = new List<CandidateRow>();
        private readonly Dictionary<string, CharacterStats> statCache =
            new Dictionary<string, CharacterStats>(StringComparer.Ordinal);
        private bool ascending;
        private string parentAUniqueId;
        private string parentBUniqueId;
        private MonsterSO previewChild;
        private bool previewIsSpecial;
        private int materialStepsA;
        private int materialStepsB;
        private int selectingParent; // 0: A, 1: B
        private int page;
        private bool wired;
        private bool showIndividualValues;

        [Serializable]
        private sealed class CandidateRow
        {
            public Button button;
            public Button detailButton;
            public TMP_Text label;
            public Image icon;
        }

        private void Awake()
        {
            CollectSavedCandidateRows();
            WireButtons();
        }

        /// <summary>
        /// Editor-authored Prefabs do not serialize the private CandidateRow
        /// helper list, so rebuild row references from its saved hierarchy.
        /// Buttons/text/icons themselves remain fully editable in the Prefab.
        /// </summary>
        private void CollectSavedCandidateRows()
        {
            if (listContent == null || rows.Count > 0)
                return;

            for (int i = 0; i < PageSize; i++)
            {
                Transform item = listContent.Find("Monster_" + i);
                if (item == null)
                    continue;
                Transform detail = item.Find("DetailsButton");
                Transform label = item.Find("MonsterInfo");
                Transform icon = item.Find("Icon");
                Button itemButton = item.GetComponent<Button>();
                Button detailButton = detail != null ? detail.GetComponent<Button>() : null;
                TMP_Text text = label != null ? label.GetComponent<TMP_Text>() : null;
                Image image = icon != null ? icon.GetComponent<Image>() : null;
                if (itemButton == null || detailButton == null ||
                    text == null || image == null)
                    continue;
                rows.Add(new CandidateRow
                {
                    button = itemButton, detailButton = detailButton,
                    label = text, icon = image
                });
            }
        }

        private void OnEnable()
        {
            WireButtons();
            EnsureReferences();
            parentAUniqueId = null;
            parentBUniqueId = null;
            previewChild = null;
            materialStepsA = 0;
            materialStepsB = 0;
            selectingParent = 0;
            page = 0;
            showIndividualValues = false;
            statCache.Clear();
            if (individualValuesToggle != null)
                individualValuesToggle.SetIsOnWithoutNotify(false);
            if (detailPanel != null)
                detailPanel.Hide();
            if (confirmationOverlay != null)
                confirmationOverlay.SetActive(false);
            if (previewText != null)
                previewText.text = "親を2体選んで「結果をプレビュー」を押してください。";
            if (noticeText != null)
                noticeText.text = "親はLv.10以上が必要です。編成中・お気に入り・ロック中も選択不可。";
            RefreshAll();
        }

        private void EnsureReferences()
        {
            if (monsterManager == null)
                monsterManager = FindFirstObjectByType<MonsterManager>();
            if (breedingManager == null)
                breedingManager = FindFirstObjectByType<BreedingManager>();
            if (monsterCatalog == null)
                monsterCatalog = MonsterCatalogSO.Load();
            if (materialInventory == null)
                materialInventory = FindFirstObjectByType<MaterialInventoryManager>();
            if (equipmentManager == null)
                equipmentManager = FindFirstObjectByType<EquipmentManager>();
        }

        private void WireButtons()
        {
            if (wired)
                return;
            if (parentAButton == null || parentBButton == null ||
                parentADetailButton == null || parentBDetailButton == null ||
                individualValuesToggle == null ||
                previewButton == null || breedButton == null || closeButton == null ||
                previousPageButton == null || nextPageButton == null ||
                confirmButton == null || cancelButton == null ||
                materialAPlusButton == null || materialAMinusButton == null ||
                materialBPlusButton == null || materialBMinusButton == null)
                return;

            parentAButton.onClick.AddListener(ChooseParentA);
            parentBButton.onClick.AddListener(ChooseParentB);
            parentADetailButton.onClick.AddListener(OpenParentADetail);
            parentBDetailButton.onClick.AddListener(OpenParentBDetail);
            individualValuesToggle.onValueChanged.AddListener(ChangeIvDisplay);
            previewButton.onClick.AddListener(Preview);
            breedButton.onClick.AddListener(OpenConfirmation);
            closeButton.onClick.AddListener(Close);
            previousPageButton.onClick.AddListener(PreviousPage);
            nextPageButton.onClick.AddListener(NextPage);
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(CancelConfirmation);
            materialAPlusButton.onClick.AddListener(AddMaterialA);
            materialAMinusButton.onClick.AddListener(RemoveMaterialA);
            materialBPlusButton.onClick.AddListener(AddMaterialB);
            materialBMinusButton.onClick.AddListener(RemoveMaterialB);
            if (sortDropdown != null)
                sortDropdown.onValueChanged.AddListener(HandleFilterChanged);
            if (statFilterDropdown != null)
                statFilterDropdown.onValueChanged.AddListener(HandleStatFilterChanged);
            if (thresholdDropdown != null)
                thresholdDropdown.onValueChanged.AddListener(HandleFilterChanged);
            if (sortDirectionButton != null)
                sortDirectionButton.onClick.AddListener(ChangeSortDirection);
            RefreshThresholdInteractable();
            wired = true;
        }

        private void OnDestroy()
        {
            if (!wired)
                return;
            parentAButton.onClick.RemoveListener(ChooseParentA);
            parentBButton.onClick.RemoveListener(ChooseParentB);
            parentADetailButton.onClick.RemoveListener(OpenParentADetail);
            parentBDetailButton.onClick.RemoveListener(OpenParentBDetail);
            individualValuesToggle.onValueChanged.RemoveListener(ChangeIvDisplay);
            previewButton.onClick.RemoveListener(Preview);
            breedButton.onClick.RemoveListener(OpenConfirmation);
            closeButton.onClick.RemoveListener(Close);
            previousPageButton.onClick.RemoveListener(PreviousPage);
            nextPageButton.onClick.RemoveListener(NextPage);
            confirmButton.onClick.RemoveListener(Confirm);
            cancelButton.onClick.RemoveListener(CancelConfirmation);
            materialAPlusButton.onClick.RemoveListener(AddMaterialA);
            materialAMinusButton.onClick.RemoveListener(RemoveMaterialA);
            materialBPlusButton.onClick.RemoveListener(AddMaterialB);
            materialBMinusButton.onClick.RemoveListener(RemoveMaterialB);
            if (sortDropdown != null)
                sortDropdown.onValueChanged.RemoveListener(HandleFilterChanged);
            if (statFilterDropdown != null)
                statFilterDropdown.onValueChanged.RemoveListener(HandleStatFilterChanged);
            if (thresholdDropdown != null)
                thresholdDropdown.onValueChanged.RemoveListener(HandleFilterChanged);
            if (sortDirectionButton != null)
                sortDirectionButton.onClick.RemoveListener(ChangeSortDirection);
        }

        private void HandleFilterChanged(int value)
        {
            page = 0;
            RefreshAll();
            if (listScroll != null)
                listScroll.verticalNormalizedPosition = 1f;
        }

        private void HandleStatFilterChanged(int value)
        {
            RefreshThresholdInteractable();
            HandleFilterChanged(value);
        }

        private void RefreshThresholdInteractable()
        {
            if (thresholdDropdown == null || statFilterDropdown == null)
                return;
            int filter = statFilterDropdown.value;
            thresholdDropdown.interactable = filter > 0 &&
                filter < InventoryStatFilter.Labels.Length;
        }

        private void ChangeSortDirection()
        {
            ascending = !ascending;
            if (sortDirectionText != null)
                sortDirectionText.text = ascending ? "昇順" : "降順";
            page = 0;
            RefreshAll();
            if (listScroll != null)
                listScroll.verticalNormalizedPosition = 1f;
        }

        private void ChangeIvDisplay(bool enabled)
        {
            showIndividualValues = enabled;
            // Changing the mode also changes stat rankings and thresholds.
            page = 0;
            RefreshAll();
            if (listScroll != null)
                listScroll.verticalNormalizedPosition = 1f;
        }

        private void OpenParentADetail() { OpenMonsterDetail(parentAUniqueId); }
        private void OpenParentBDetail() { OpenMonsterDetail(parentBUniqueId); }

        private void OpenMonsterDetail(string uniqueId)
        {
            EnsureReferences();
            OwnedMonsterRecord record = monsterManager != null
                ? monsterManager.FindOwnedMonster(uniqueId) : null;
            if (record == null)
                return;
            if (detailPanel == null)
            {
                ShowNotice("モンスター詳細Prefabが見つかりません。");
                return;
            }

            detailPanel.SetReadOnlyMode(true);
            detailPanel.Show(record, monsterCatalog, showIndividualValues);
            detailPanel.SetReadOnlyMode(true);
            detailPanel.transform.SetAsLastSibling();
        }

        private void AddMaterialA() { ChangeMaterialSteps(true, 1); }
        private void RemoveMaterialA() { ChangeMaterialSteps(true, -1); }
        private void AddMaterialB() { ChangeMaterialSteps(false, 1); }
        private void RemoveMaterialB() { ChangeMaterialSteps(false, -1); }

        private void ChangeMaterialSteps(bool forA, int delta)
        {
            EnsureReferences();
            if (breedingManager == null)
                return;
            int nextA = materialStepsA + (forA ? delta : 0);
            int nextB = materialStepsB + (forA ? 0 : delta);
            if (!breedingManager.TryGetOffer(
                    parentAUniqueId, parentBUniqueId, nextA, nextB,
                    out _, out string error))
            {
                ShowNotice(error);
                return;
            }

            materialStepsA = nextA;
            materialStepsB = nextB;
            if (previewChild != null)
                Preview(); // Keep the preview's displayed rate in sync with steps.
            else
            {
                ShowNotice("投入素材を設定しました。確定時に消費します。");
                RefreshAll();
            }
        }

        private void ChooseParentA() { SetActiveParent(0); }
        private void ChooseParentB() { SetActiveParent(1); }
        private void PreviousPage() { SetPage(page - 1); }
        private void NextPage() { SetPage(page + 1); }
        private void CancelConfirmation()
        {
            if (confirmationOverlay != null)
                confirmationOverlay.SetActive(false);
        }

        private void SetActiveParent(int index)
        {
            selectingParent = index == 0 ? 0 : 1;
            page = 0;
            RefreshAll();
            if (listScroll != null)
                listScroll.verticalNormalizedPosition = 1f;
        }

        private void SetPage(int next)
        {
            page = Mathf.Clamp(next, 0, Mathf.Max(0, (candidates.Count - 1) / PageSize));
            RefreshCandidateRows();
            if (listScroll != null)
                listScroll.verticalNormalizedPosition = 1f;
        }

        private void SelectCandidate(string uniqueId)
        {
            EnsureReferences();
            if (monsterManager == null)
                return;

            OwnedMonsterRecord selected = monsterManager.FindOwnedMonster(uniqueId);
            if (selected == null || selected.level < MinimumParentLevel ||
                monsterManager.IsProtectedFromConsumption(selected))
            {
                ShowNotice(selected != null && selected.level < MinimumParentLevel
                    ? "親として選択するにはLv.10以上が必要です。"
                    : "このモンスターは選択できません。");
                RefreshAll();
                return;
            }

            if ((selectingParent == 0 && uniqueId == parentBUniqueId) ||
                (selectingParent == 1 && uniqueId == parentAUniqueId))
            {
                ShowNotice("同じ個体は2回選択できません。");
                return;
            }

            if (selectingParent == 0)
            {
                parentAUniqueId = uniqueId;
                if (string.IsNullOrEmpty(parentBUniqueId))
                    selectingParent = 1;
            }
            else
            {
                parentBUniqueId = uniqueId;
            }

            materialStepsA = 0;
            materialStepsB = 0;
            InvalidatePreview();
            RefreshAll();
            if (listScroll != null)
                listScroll.verticalNormalizedPosition = 1f;
        }

        private void InvalidatePreview()
        {
            previewChild = null;
            previewIsSpecial = false;
            if (previewText != null)
                previewText.text = "結果はまだ確定していません。「結果をプレビュー」を押してください。";
            SetIcon(previewIcon, null);
        }

        private void Preview()
        {
            EnsureReferences();
            if (breedingManager == null)
            {
                ShowNotice("BreedingManagerが見つかりません。");
                return;
            }

            if (!breedingManager.TryGetOffer(
                    parentAUniqueId, parentBUniqueId,
                    materialStepsA, materialStepsB,
                    out BreedingMaterialOffer offer, out string error))
            {
                InvalidatePreview();
                ShowNotice(error);
                RefreshAll();
                return;
            }

            MonsterSO child = offer.child;
            previewChild = child;
            previewIsSpecial = offer.isSpecial;
            string mode = offer.isSpecial ? "★ 特殊配合" : "通常配合";
            previewText.text = mode + "\n" + child.displayName +
                "   (Stage " + child.appearanceStage + ")" +
                "\n配合値 " + child.breedingValue +
                "  /  成功率 " + offer.successPercent + "％" +
                (breedingManager.ProtectParentsOnFailure
                    ? "\n※失敗時も親2体は保護されます。"
                    : "\n※失敗しても親2体は消費されます。");
            SetIcon(previewIcon, child.icon != null ? child.icon : child.sprite);
            ShowNotice("プレビュー完了。「この結果で配合」を押して最終確認してください。");
            RefreshAll();
        }

        private void OpenConfirmation()
        {
            EnsureReferences();
            if (previewChild == null || breedingManager == null)
                return;

            if (!breedingManager.TryGetOffer(
                    parentAUniqueId, parentBUniqueId,
                    materialStepsA, materialStepsB,
                    out BreedingMaterialOffer offer, out string error) ||
                offer.child != previewChild || offer.isSpecial != previewIsSpecial)
            {
                InvalidatePreview();
                RefreshAll();
                ShowNotice(string.IsNullOrEmpty(error)
                    ? "配合結果が変わりました。もう一度プレビューしてください。" : error);
                return;
            }

            OwnedMonsterRecord a = monsterManager.FindOwnedMonster(parentAUniqueId);
            OwnedMonsterRecord b = monsterManager.FindOwnedMonster(parentBUniqueId);
            if (a == null || b == null)
            {
                InvalidatePreview();
                RefreshAll();
                ShowNotice("親モンスターが所持一覧に見つかりません。");
                return;
            }

            confirmationText.text =
                "成功率 " + offer.successPercent + "％ で配合しますか？\n\n" +
                FormatMonster(a) + "\n＋\n" + FormatMonster(b) +
                "\n\n" + FormatMaterialCost(offer.materialA, offer.requiredA) +
                "\n" + FormatMaterialCost(offer.materialB, offer.requiredB) +
                "\n\n成功: 【" + offer.child.displayName + "】獲得" +
                (breedingManager.ProtectParentsOnFailure
                    ? "\n失敗: 親2体は残る（投入素材は消費）"
                    : "\n失敗: 親2体は消失（装備は残る）");
            confirmationOverlay.SetActive(true);
        }

        private void Confirm()
        {
            if (confirmationOverlay == null || !confirmationOverlay.activeSelf)
                return;
            confirmationOverlay.SetActive(false);
            EnsureReferences();

            // Recheck the preview immediately before the irreversible action.
            string previewError = string.Empty;
            BreedingMaterialOffer currentOffer = null;
            if (breedingManager == null ||
                !breedingManager.TryGetOffer(
                    parentAUniqueId, parentBUniqueId,
                    materialStepsA, materialStepsB,
                    out currentOffer, out previewError) ||
                previewChild == null || previewChild != currentOffer.child ||
                previewIsSpecial != currentOffer.isSpecial)
            {
                InvalidatePreview();
                ShowNotice("選択状態が変更されました。" +
                    (string.IsNullOrEmpty(previewError) ? "再度プレビューしてください。" : previewError));
                RefreshAll();
                return;
            }

            if (!breedingManager.TryBreed(
                    parentAUniqueId, parentBUniqueId,
                    materialStepsA, materialStepsB,
                    out OwnedMonsterRecord newborn, out bool succeeded, out string error))
            {
                InvalidatePreview();
                ShowNotice(error);
                RefreshAll();
                return;
            }

            MonsterSO bornSpecies = previewChild;
            parentAUniqueId = null;
            parentBUniqueId = null;
            previewChild = null;
            previewIsSpecial = false;
            materialStepsA = 0;
            materialStepsB = 0;
            selectingParent = 0;
            page = 0;
            if (succeeded)
            {
                TitleSO inheritedTitle = monsterCatalog != null && newborn != null
                    ? monsterCatalog.GetTitle(newborn.titleId) : null;
                string titleLabel = inheritedTitle != null
                    ? "継承称号: " + inheritedTitle.displayName
                    : "継承称号: なし";
                previewText.text = "配合成功！\n" + bornSpecies.displayName +
                    " が仲間になりました！\n" + titleLabel +
                    " / 個体値は親から継承";
                SetIcon(previewIcon, bornSpecies.icon != null
                    ? bornSpecies.icon : bornSpecies.sprite);
                ShowNotice("成功！仲間一覧に追加しました。");
            }
            else
            {
                bool protectedParents = breedingManager.ProtectParentsOnFailure;
                previewText.text = protectedParents
                    ? "配合失敗…\n素材を失いましたが、親2体は保護されました。"
                    : "配合失敗…\n素材と親2体を失いました。";
                SetIcon(previewIcon, null);
                ShowNotice(protectedParents
                    ? "配合失敗。親2体は保護されました。"
                    : "失敗しました。親2体は消費されました。");
            }
            RefreshAll();
        }

        private string FormatMonster(OwnedMonsterRecord record)
        {
            MonsterSO species = monsterCatalog != null
                ? monsterCatalog.GetMonster(record.monsterId) : null;
            TitleSO title = monsterCatalog != null
                ? monsterCatalog.GetTitle(record.titleId) : null;
            string titleName = title != null ? "《" + title.displayName + "》" : "";
            return titleName +
                (species != null ? species.displayName : record.monsterId) +
                " Lv." + record.level + "（個体値 " + GetTotalIv(record) + "）";
        }

        private static int GetTotalIv(OwnedMonsterRecord record)
        {
            MonsterIndividualValues iv = record != null
                ? record.individualValues : null;
            if (iv == null)
                return 0;
            return iv.hp + iv.mp + iv.attack + iv.magic +
                iv.defense + iv.magicDefense + iv.criticalRate +
                iv.criticalMultiplier + iv.moveSpeed +
                iv.attackSpeed + iv.attackRange;
        }

        private static string FormatMaterialCost(MaterialSO material, long amount)
        {
            return (material != null ? material.displayName : "素材なし") +
                " × " + amount.ToString("N0");
        }

        private void RefreshMaterialControls()
        {
            if (successRateText != null)
                successRateText.text = "配合成功率: " +
                    BreedingManager.GetSuccessPercent(materialStepsA, materialStepsB) +
                    "％ （初期80％ / 1万個につき＋2％）";

            RefreshMaterialLine(true);
            RefreshMaterialLine(false);
        }

        private void RefreshMaterialLine(bool forA)
        {
            string id = forA ? parentAUniqueId : parentBUniqueId;
            MonsterSO species = null;
            if (monsterManager != null && monsterCatalog != null)
            {
                OwnedMonsterRecord record = monsterManager.FindOwnedMonster(id);
                if (record != null)
                    species = monsterCatalog.GetMonster(record.monsterId);
            }

            MaterialSO material = species != null ? species.uniqueMaterial : null;
            int steps = forA ? materialStepsA : materialStepsB;
            long amount = material != null && materialInventory != null
                ? materialInventory.GetAmount(material.materialId) : 0L;
            TMP_Text label = forA ? materialAText : materialBText;
            if (label != null)
            {
                label.text = (forA ? "親A素材: " : "親B素材: ") +
                    (material != null ? material.displayName : "未設定") +
                    "\n所持: " + amount.ToString("N0") +
                    " / 投入: " + (steps * BreedingManager.MaterialCostPerStep).ToString("N0") +
                    "（" + steps + "/5）";
            }

            Button minus = forA ? materialAMinusButton : materialBMinusButton;
            Button plus = forA ? materialAPlusButton : materialBPlusButton;
            if (minus != null)
                minus.interactable = steps > 0;

            bool canAdd = false;
            if (species != null && material != null && breedingManager != null &&
                steps < BreedingManager.MaxMaterialStepsPerParent)
            {
                canAdd = breedingManager.TryGetOffer(
                    parentAUniqueId, parentBUniqueId,
                    materialStepsA + (forA ? 1 : 0),
                    materialStepsB + (forA ? 0 : 1),
                    out _, out _);
            }
            if (plus != null)
                plus.interactable = canAdd;
        }

        private void RefreshAll()
        {
            EnsureReferences();
            RefreshMaterialControls();
            UpdateSelectedCard(parentAUniqueId, parentAText, parentAIcon, "親Aを選択");
            UpdateSelectedCard(parentBUniqueId, parentBText, parentBIcon, "親Bを選択");
            if (parentADetailButton != null)
                parentADetailButton.interactable =
                    monsterManager != null &&
                    monsterManager.FindOwnedMonster(parentAUniqueId) != null;
            if (parentBDetailButton != null)
                parentBDetailButton.interactable =
                    monsterManager != null &&
                    monsterManager.FindOwnedMonster(parentBUniqueId) != null;
            if (selectionHeader != null)
                selectionHeader.text = (selectingParent == 0 ? "親A" : "親B") +
                    "を選択（各個体をタップ）";
            if (previewButton != null)
                previewButton.interactable =
                    !string.IsNullOrEmpty(parentAUniqueId) &&
                    !string.IsNullOrEmpty(parentBUniqueId) &&
                    breedingManager != null;
            if (breedButton != null)
                breedButton.interactable = previewChild != null;
            BuildCandidateSnapshot();
            RefreshCandidateRows();
        }

        private void UpdateSelectedCard(
            string uniqueId, TMP_Text text, Image icon, string fallback)
        {
            OwnedMonsterRecord record = monsterManager != null
                ? monsterManager.FindOwnedMonster(uniqueId) : null;
            if (record == null)
            {
                if (text != null) text.text = fallback;
                SetIcon(icon, null);
                return;
            }
            if (text != null)
                text.text = FormatMonster(record);
            MonsterSO species = monsterCatalog != null
                ? monsterCatalog.GetMonster(record.monsterId) : null;
            SetIcon(icon, species == null ? null :
                species.icon != null ? species.icon : species.sprite);
        }

        private double GetValueForSorting(
            OwnedMonsterRecord record, InventoryStat stat)
        {
            if (showIndividualValues &&
                InventoryStatFilter.SupportsIndividualValues(stat))
                return InventoryStatFilter.GetIndividualValue(
                    record != null ? record.individualValues : null, stat);

            return InventoryStatFilter.GetValue(GetCachedStats(record), stat);
        }

        private CharacterStats GetCachedStats(OwnedMonsterRecord record)
        {
            if (record == null)
                return null;
            string key = record.uniqueId ?? string.Empty;
            if (!statCache.TryGetValue(key, out CharacterStats stats))
            {
                MonsterSO monster = monsterCatalog != null
                    ? monsterCatalog.GetMonster(record.monsterId) : null;
                TitleSO title = monsterCatalog != null
                    ? monsterCatalog.GetTitle(record.titleId) : null;
                stats = StatCalculator.CalculateMonsterStats(
                    monster, Mathf.Max(1, record.level),
                    record.individualValues, title,
                    equipmentManager != null
                        ? equipmentManager.GetEquippedModifiers(
                            record.equippedItemUniqueIds)
                        : null);
                statCache[key] = stats;
            }
            return stats;
        }

        private void BuildCandidateSnapshot()
        {
            candidates.Clear();
            if (monsterManager == null)
                return;

            // Exact same sorting/filtering choices and score calculation as
            // OwnedMonsterListUI. Eligibility is a separate first sort key:
            // available Lv10+ > protected Lv10+ > all monsters below Lv10.
            IReadOnlyList<OwnedMonsterRecord> owned = monsterManager.OwnedMonsters;
            int sortValue = sortDropdown != null ? sortDropdown.value : 0;
            int filterValue = statFilterDropdown != null
                ? statFilterDropdown.value : 0;
            int thresholdValue = thresholdDropdown != null
                ? thresholdDropdown.value : 0;

            var filteredIndices = new List<int>(owned.Count);
            for (int i = 0; i < owned.Count; i++)
            {
                OwnedMonsterRecord record = owned[i];
                if (record != null &&
                    InventoryProtectionIcons.MatchesCombinedStatFilter(
                        filterValue, record.isFavorite, record.isLocked))
                    filteredIndices.Add(i);
            }

            if (filterValue > 0 &&
                filterValue < InventoryStatFilter.Labels.Length &&
                filteredIndices.Count > 0)
            {
                InventoryStat selected = (InventoryStat)filterValue;
                if (thresholdValue < 3)
                {
                    var scores = new List<double>(filteredIndices.Count);
                    foreach (int index in filteredIndices)
                        scores.Add(GetValueForSorting(owned[index], selected));

                    scores.Sort((x, y) => y.CompareTo(x));
                    float fraction = thresholdValue == 0 ? 0.25f :
                        thresholdValue == 1 ? 0.5f : 0.75f;
                    int cutoff = Mathf.Clamp(
                        Mathf.CeilToInt(scores.Count * fraction) - 1,
                        0, scores.Count - 1);
                    double minimum = scores[cutoff];
                    filteredIndices.RemoveAll(index =>
                        GetValueForSorting(owned[index], selected) < minimum);
                }
                else
                {
                    filteredIndices.RemoveAll(index =>
                        Math.Abs(GetValueForSorting(owned[index], selected)) < 0.000001d);
                }
            }

            filteredIndices.Sort((a, b) =>
            {
                OwnedMonsterRecord left = owned[a];
                OwnedMonsterRecord right = owned[b];
                int groupLeft = left.level < MinimumParentLevel ? 2 :
                    monsterManager.IsProtectedFromConsumption(left) ? 1 : 0;
                int groupRight = right.level < MinimumParentLevel ? 2 :
                    monsterManager.IsProtectedFromConsumption(right) ? 1 : 0;
                if (groupLeft != groupRight)
                    return groupLeft.CompareTo(groupRight);

                int cmp;
                if (sortValue == 0)
                    cmp = b.CompareTo(a);
                else if (sortValue == 1)
                    cmp = right.level.CompareTo(left.level);
                else if (sortValue == InventoryStatFilter.Labels.Length + 1)
                    cmp = right.isFavorite.CompareTo(left.isFavorite);
                else if (sortValue == InventoryStatFilter.Labels.Length + 2)
                    cmp = right.isLocked.CompareTo(left.isLocked);
                else
                {
                    InventoryStat stat = (InventoryStat)(sortValue - 1);
                    cmp = GetValueForSorting(right, stat).CompareTo(
                        GetValueForSorting(left, stat));
                }

                if (cmp == 0) cmp = b.CompareTo(a);
                return ascending ? -cmp : cmp;
            });

            for (int i = 0; i < filteredIndices.Count; i++)
                candidates.Add(owned[filteredIndices[i]]);

            page = Mathf.Clamp(page, 0,
                Mathf.Max(0, (candidates.Count - 1) / PageSize));
        }

        private void RefreshCandidateRows()
        {
            int start = page * PageSize;
            for (int i = 0; i < rows.Count; i++)
            {
                CandidateRow row = rows[i];
                int index = start + i;
                if (index >= candidates.Count)
                {
                    row.button.gameObject.SetActive(false);
                    continue;
                }

                row.button.gameObject.SetActive(true);
                OwnedMonsterRecord record = candidates[index];
                bool protectedMonster = monsterManager.IsProtectedFromConsumption(record);
                bool tooLow = record.level < MinimumParentLevel;
                bool sameAsOtherParent = record.uniqueId ==
                    (selectingParent == 0 ? parentBUniqueId : parentAUniqueId);
                bool selected = record.uniqueId ==
                    (selectingParent == 0 ? parentAUniqueId : parentBUniqueId);
                MonsterSO species = monsterCatalog != null
                    ? monsterCatalog.GetMonster(record.monsterId) : null;
                string name = species != null ? species.displayName : record.monsterId;
                string status = tooLow ? " [Lv.10未満]" :
                    protectedMonster
                    ? (record.isLocked ? " [ロック]" :
                       record.isFavorite ? " [お気に入り]" : " [パーティ編成中]")
                    : sameAsOtherParent ? " [もう一方の親]" :
                      selected ? " [選択中]" : "";
                TitleSO title = monsterCatalog != null
                    ? monsterCatalog.GetTitle(record.titleId) : null;
                string titlePrefix = title != null
                    ? "《" + title.displayName + "》" : "";
                string stageAndIvSummary = "Stage " +
                    (species != null ? species.appearanceStage : 0) +
                    " / 個体値合計 " + GetTotalIv(record);
                int sortValue = sortDropdown != null ? sortDropdown.value : 0;
                InventoryStat sortedStat = InventorySortVisuals.SelectedStat(sortValue);
                string levelLabel = sortValue == 1
                    ? InventorySortVisuals.Highlight(record.level.ToString())
                    : record.level.ToString();
                string header = titlePrefix + name + " Lv." + levelLabel + status;
                string sortMetric = sortedStat == InventoryStat.All
                    ? string.Empty
                    : InventorySortVisuals.SortMetricLine(sortedStat,
                        GetValueForSorting(record, sortedStat), showIndividualValues);
                if (showIndividualValues)
                {
                    string ivText = OwnedMonsterDetailUI.FormatIndividualValues(
                        record.individualValues);
                    if (InventoryStatFilter.SupportsIndividualValues(sortedStat))
                        ivText = InventorySortVisuals.HighlightDisplayedStat(
                            ivText, sortedStat, true);
                    row.label.text = header + "\n" + ivText +
                        "\n" + stageAndIvSummary +
                        (InventoryStatFilter.SupportsIndividualValues(sortedStat) ||
                            sortMetric.Length == 0 ? "" : "\n" + sortMetric);
                }
                else
                {
                    row.label.text = header + "\n" + stageAndIvSummary +
                        (sortMetric.Length == 0 ? "" : "\n" + sortMetric);
                }
                float rowHeight = showIndividualValues
                    ? (sortMetric.Length > 0 &&
                        !InventoryStatFilter.SupportsIndividualValues(sortedStat)
                        ? 310f : 272f)
                    : (sortMetric.Length > 0 ? 137f : 104f);
                row.button.GetComponent<LayoutElement>().preferredHeight = rowHeight;
                row.label.GetComponent<LayoutElement>().preferredHeight =
                    rowHeight - 18f;
                SetIcon(row.icon, species != null
                    ? (species.icon != null ? species.icon : species.sprite) : null);
                row.button.interactable =
                    !tooLow && !protectedMonster && !sameAsOtherParent;
                row.button.GetComponent<Image>().color = selected
                    ? new Color(0.24f, 0.47f, 0.37f, 1f)
                    : new Color(0.19f, 0.27f, 0.35f, 1f);
                string capturedId = record.uniqueId;
                row.button.onClick.RemoveAllListeners();
                row.button.onClick.AddListener(() => SelectCandidate(capturedId));
                row.detailButton.onClick.RemoveAllListeners();
                row.detailButton.onClick.AddListener(() => OpenMonsterDetail(capturedId));
                row.detailButton.interactable = true;
            }

            int totalPages = Mathf.Max(1, (candidates.Count + PageSize - 1) / PageSize);
            if (pageText != null)
                pageText.text = (page + 1) + " / " + totalPages +
                    " （表示 " + candidates.Count + " / 所持 " +
                    (monsterManager != null ? monsterManager.OwnedMonsters.Count : 0) +
                    "体）";
            if (previousPageButton != null)
                previousPageButton.interactable = page > 0;
            if (nextPageButton != null)
                nextPageButton.interactable = page + 1 < totalPages;
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        private void ShowNotice(string message)
        {
            if (noticeText != null)
                noticeText.text = message ?? string.Empty;
        }

        public void Close()
        {
            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router != null && router.IsOpen &&
                router.CurrentBuilding == MapBuildingType.BreedingShop)
                router.CloseAll();
            else
                gameObject.SetActive(false);
            MapBuildingAccess.SuppressOpenFor(0.5f);
        }

        // The panel is a scene-placed prefab. Runtime creation is intentionally
        // disabled; this builder exists only to initially author the prefab.
#if UNITY_EDITOR
        // Editor-only asset creator also uses this initial construction code.
        // In normal gameplay CreateRuntime instantiates the saved Prefab.
        public static BreedingPanelUI BuildPrototype(Transform host)
        {
            // A town-only scene may not have any UI EventSystem yet.
            // The project uses the new Unity Input System.
            if (Application.isPlaying && EventSystem.current == null)
            {
                new GameObject("BreedingShopEventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            Canvas canvas = host != null ? host.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                Canvas[] all = FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].isRootCanvas &&
                        all[i].gameObject.activeInHierarchy &&
                        all[i].isActiveAndEnabled &&
                        all[i].renderMode != RenderMode.WorldSpace)
                    {
                        canvas = all[i];
                        break;
                    }
                }
            }
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("BreedingShopCanvas",
                    typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 630;
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
            }

            GameObject overlay = MakeRectObject("BreedingShopPanel", canvas.transform,
                typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
            overlay.SetActive(false);
            InventoryUiFactory.Stretch(overlay.GetComponent<RectTransform>());
            Image shade = overlay.GetComponent<Image>();
            shade.color = new Color(0.015f, 0.025f, 0.045f, 0.96f);
            shade.raycastTarget = true;
            Canvas top = overlay.GetComponent<Canvas>();
            top.overrideSorting = true;
            top.sortingOrder = 650;

            BreedingPanelUI panel = overlay.AddComponent<BreedingPanelUI>();

            GameObject card = MakeRectObject("BreedingCard", overlay.transform,
                typeof(Image), typeof(VerticalLayoutGroup));
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.045f, 0.065f);
            cardRect.anchorMax = new Vector2(0.955f, 0.935f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color = new Color(0.095f, 0.16f, 0.24f, 1f);
            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text heading = Label(card.transform, "Heading", "モンスター配合屋", 31f, 43f);
            heading.alignment = TextAlignmentOptions.Center;
            TMP_Text helper = Label(card.transform, "Helper",
                "親2体を選び、結果を確認して配合します", 18f, 30f);
            helper.alignment = TextAlignmentOptions.Center;

            GameObject parentRow = LayoutRow(card.transform, "Parents", 88f);
            panel.parentAButton = ParentButton(parentRow.transform, "ParentA",
                "親Aを選択", out panel.parentAText, out panel.parentAIcon,
                out panel.parentADetailButton);
            panel.parentBButton = ParentButton(parentRow.transform, "ParentB",
                "親Bを選択", out panel.parentBText, out panel.parentBIcon,
                out panel.parentBDetailButton);

            GameObject preview = LayoutRow(card.transform, "Preview", 100f);
            preview.GetComponent<Image>().color = new Color(0.13f, 0.24f, 0.31f, 1f);
            panel.previewIcon = Icon(preview.transform, "ChildIcon", 66f);
            panel.previewText = Label(preview.transform, "ChildPreview",
                "親を2体選択してください。", 18f, 90f);
            LayoutElement previewTextLayout = panel.previewText.gameObject.GetComponent<LayoutElement>();
            previewTextLayout.flexibleWidth = 1f;
            panel.previewText.alignment = TextAlignmentOptions.MidlineLeft;

            panel.successRateText = Label(card.transform, "SuccessRate",
                "配合成功率: 80％", 22f, 38f);
            panel.successRateText.alignment = TextAlignmentOptions.Center;

            BuildMaterialInput(card.transform, "ParentAMaterials", "親A素材",
                out panel.materialAText, out panel.materialAMinusButton,
                out panel.materialAPlusButton);
            BuildMaterialInput(card.transform, "ParentBMaterials", "親B素材",
                out panel.materialBText, out panel.materialBMinusButton,
                out panel.materialBPlusButton);

            GameObject selectionBar = LayoutRow(card.transform,
                "SelectionAndIvToggle", 47f, false);
            panel.selectionHeader = Label(selectionBar.transform, "SelectionHeader",
                "親Aを選択", 21f, 39f);
            panel.selectionHeader.GetComponent<LayoutElement>().flexibleWidth = 1f;
            panel.selectionHeader.alignment = TextAlignmentOptions.MidlineLeft;
            panel.individualValuesToggle = CreateIndividualValuesToggle(
                selectionBar.transform);

            // TMP dropdown templates are pre-generated by the editor baker,
            // using the same definitions as the owned monster inventory.
            GameObject filtersPrefab = Resources.Load<GameObject>(
                "UI/BreedingFilterControls");
            if (filtersPrefab != null)
            {
                GameObject filters = Instantiate(filtersPrefab, card.transform, false);
                filters.name = "BreedingFilterControls";
                Transform topFilters = filters.transform.Find("TopRow");
                Transform bottomFilters = filters.transform.Find("BottomRow");
                panel.sortDropdown = topFilters != null
                    ? topFilters.Find("SortDropdown")?.GetComponent<TMP_Dropdown>() : null;
                panel.statFilterDropdown = topFilters != null
                    ? topFilters.Find("StatFilter")?.GetComponent<TMP_Dropdown>() : null;
                panel.thresholdDropdown = bottomFilters != null
                    ? bottomFilters.Find("ThresholdFilter")?.GetComponent<TMP_Dropdown>() : null;
                panel.sortDirectionButton = bottomFilters != null
                    ? bottomFilters.Find("SortDirection")?.GetComponent<Button>() : null;
                panel.sortDirectionText = panel.sortDirectionButton != null
                    ? panel.sortDirectionButton.GetComponentInChildren<TMP_Text>() : null;
            }
            else
            {
                Debug.LogWarning(
                    "[BreedingPanelUI] 配合フィルターPrefabがありません。" +
                    " MixMaster/UI/配合フィルターPrefabを再生成 を実行してください。");
            }

            GameObject scrollBox = MakeRectObject("CandidateScroll", card.transform,
                typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollBox.GetComponent<Image>().color = new Color(0.065f, 0.105f, 0.155f, 1f);
            LayoutElement scrollSize = scrollBox.GetComponent<LayoutElement>();
            scrollSize.minHeight = 150f;
            scrollSize.flexibleHeight = 1f;
            scrollSize.preferredHeight = 540f;
            panel.listScroll = scrollBox.GetComponent<ScrollRect>();
            panel.listScroll.horizontal = false;
            panel.listScroll.vertical = true;
            panel.listScroll.inertia = true;
            panel.listScroll.scrollSensitivity = 28f;
            panel.listScroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = MakeRectObject("Viewport", scrollBox.transform,
                typeof(Image), typeof(Mask));
            InventoryUiFactory.Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            panel.listScroll.viewport = viewport.GetComponent<RectTransform>();

            GameObject content = MakeRectObject("Content", viewport.transform,
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listLayout = content.GetComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(6, 6, 6, 6);
            listLayout.spacing = 7f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter sizeFitter = content.GetComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.listContent = contentRect;
            panel.listScroll.content = contentRect;

            for (int i = 0; i < PageSize; i++)
                panel.rows.Add(CreateCandidateRow(content.transform, i));

            GameObject paging = LayoutRow(card.transform, "Pages", 49f, false);
            panel.previousPageButton = ActionButton(paging.transform, "Previous", "◀ 前", 43f);
            panel.pageText = Label(paging.transform, "PageNumber", "1 / 1", 20f, 43f);
            panel.pageText.alignment = TextAlignmentOptions.Center;
            panel.pageText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            panel.nextPageButton = ActionButton(paging.transform, "Next", "次 ▶", 43f);

            panel.noticeText = Label(card.transform, "Notice", "", 17f, 52f);
            panel.noticeText.color = new Color(1f, 0.87f, 0.65f, 1f);
            panel.noticeText.alignment = TextAlignmentOptions.Center;
            panel.noticeText.enableWordWrapping = true;

            GameObject controls = LayoutRow(card.transform, "Actions", 59f);
            panel.previewButton = ActionButton(
                controls.transform, "PreviewButton", "結果をプレビュー", 50f);
            panel.breedButton = ActionButton(
                controls.transform, "BreedButton", "この結果で配合", 50f);
            panel.breedButton.GetComponent<Image>().color =
                new Color(0.19f, 0.43f, 0.32f, 1f);
            panel.closeButton = ActionButton(card.transform, "CloseButton", "閉じる", 43f);

            GameObject confirmation = MakeRectObject("ConfirmOverlay", overlay.transform,
                typeof(Image));
            InventoryUiFactory.Stretch(confirmation.GetComponent<RectTransform>());
            confirmation.GetComponent<Image>().color =
                new Color(0f, 0f, 0f, 0.83f);
            confirmation.transform.SetAsLastSibling();
            GameObject popup = MakeRectObject("ConfirmCard", confirmation.transform,
                typeof(Image), typeof(VerticalLayoutGroup));
            RectTransform popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.12f, 0.33f);
            popupRect.anchorMax = new Vector2(0.88f, 0.67f);
            popupRect.offsetMin = Vector2.zero;
            popupRect.offsetMax = Vector2.zero;
            popup.GetComponent<Image>().color = new Color(0.14f, 0.20f, 0.27f, 1f);
            VerticalLayoutGroup popupLayout = popup.GetComponent<VerticalLayoutGroup>();
            popupLayout.padding = new RectOffset(25, 25, 28, 25);
            popupLayout.spacing = 17f;
            popupLayout.childControlWidth = true;
            popupLayout.childControlHeight = true;
            popupLayout.childForceExpandWidth = true;
            popupLayout.childForceExpandHeight = false;
            panel.confirmationText = Label(popup.transform,
                "ConfirmDescription", "", 27f, 400f);
            panel.confirmationText.alignment = TextAlignmentOptions.Center;
            panel.confirmationText.GetComponent<LayoutElement>().flexibleHeight = 1f;
            GameObject confirmButtons = LayoutRow(popup.transform, "ConfirmActions", 85f);
            panel.confirmButton = ActionButton(confirmButtons.transform,
                "Confirm", "配合を確定", 85f);
            panel.confirmButton.GetComponent<Image>().color =
                new Color(0.26f, 0.50f, 0.33f, 1f);
            panel.cancelButton = ActionButton(confirmButtons.transform,
                "Cancel", "キャンセル", 85f);
            confirmation.SetActive(false);
            panel.confirmationOverlay = confirmation;

            // The standard monster detail prefab is reused as a top-most modal.
            GameObject detailPrefab = Resources.Load<GameObject>(
                "UI/BreedingMonsterDetailPanel");
            if (detailPrefab != null)
            {
                GameObject detailObject =
                    Instantiate(detailPrefab, overlay.transform, false);
                detailObject.name = "BreedingMonsterDetail";
                detailObject.transform.SetAsLastSibling();
                panel.detailPanel = detailObject.GetComponent<OwnedMonsterDetailUI>();
                detailObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning(
                    "[BreedingPanelUI] Resources/UI/BreedingMonsterDetailPanel.prefab がありません。");
            }
            panel.WireButtons();
            return panel;
        }

        private static Toggle CreateIndividualValuesToggle(Transform parent)
        {
            GameObject root = MakeRectObject("IndividualValuesToggle", parent,
                typeof(Image), typeof(Toggle), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.19f, 0.28f, 0.36f, 1f);
            LayoutElement element = root.GetComponent<LayoutElement>();
            element.preferredWidth = 280f;
            element.minWidth = 240f;
            element.flexibleWidth = 0f;
            element.preferredHeight = 40f;

            HorizontalLayoutGroup group = root.GetComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(8, 8, 6, 6);
            group.spacing = 8f;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            GameObject checkbox = MakeRectObject("Checkbox", root.transform,
                typeof(Image), typeof(LayoutElement));
            Image checkboxImage = checkbox.GetComponent<Image>();
            checkboxImage.color = new Color(0.08f, 0.12f, 0.17f, 1f);
            LayoutElement checkboxLayout = checkbox.GetComponent<LayoutElement>();
            checkboxLayout.preferredWidth = 30f;
            checkboxLayout.preferredHeight = 30f;
            checkboxLayout.flexibleWidth = 0f;

            GameObject check = MakeRectObject("Checkmark", checkbox.transform,
                typeof(Image));
            RectTransform checkRect = check.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(checkRect);
            checkRect.offsetMin = new Vector2(8f, 8f);
            checkRect.offsetMax = new Vector2(-8f, -8f);
            Image checkImage = check.GetComponent<Image>();
            checkImage.color = new Color(0.40f, 0.91f, 0.56f, 1f);

            TMP_Text caption = Label(root.transform, "Label", "個体値表示", 19f, 35f);
            caption.alignment = TextAlignmentOptions.Center;
            caption.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = checkboxImage;
            toggle.graphic = checkImage;
            toggle.SetIsOnWithoutNotify(false);
            // Let Toggle control the checkmark graphic alpha; disabling the
            // GameObject here would prevent the tick from ever appearing.
            return toggle;
        }

        private static void BuildMaterialInput(
            Transform parent, string name, string caption,
            out TMP_Text label, out Button minus, out Button plus)
        {
            GameObject row = LayoutRow(parent, name, 56f);
            label = Label(row.transform, "MaterialInfo", caption, 17f, 49f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            minus = ActionButton(row.transform, "Decrease", "－", 43f);
            plus = ActionButton(row.transform, "Increase", "＋", 43f);
            foreach (Button button in new[] { minus, plus })
            {
                LayoutElement element = button.GetComponent<LayoutElement>();
                element.preferredWidth = 62f;
                element.flexibleWidth = 0f;
            }
        }

        private static GameObject MakeRectObject(
            string name, Transform parent, params Type[] extras)
        {
            Type[] components = new Type[extras.Length + 1];
            components[0] = typeof(RectTransform);
            Array.Copy(extras, 0, components, 1, extras.Length);
            GameObject go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetHeight(GameObject go, float height)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>() ??
                go.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private static TMP_Text Label(
            Transform parent, string name, string value, float fontSize, float height)
        {
            TMP_Text label = InventoryUiFactory.Text(parent, name,
                value, Vector2.zero, new Vector2(700f, height), fontSize);
            SetHeight(label.gameObject, height);
            label.enableWordWrapping = true;
            return label;
        }

        private static GameObject LayoutRow(
            Transform parent, string name, float height, bool colored = true)
        {
            GameObject row = MakeRectObject(name, parent,
                typeof(Image), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            SetHeight(row, height);
            row.GetComponent<Image>().color = colored
                ? new Color(0.16f, 0.23f, 0.30f, 1f) : new Color(0f, 0f, 0f, 0f);
            row.GetComponent<Image>().raycastTarget = false;
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(9, 9, 7, 7);
            group.spacing = 12f;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            group.childAlignment = TextAnchor.MiddleCenter;
            return row;
        }

        private static Button ActionButton(
            Transform parent, string name, string title, float height)
        {
            Button button = InventoryUiFactory.Button(parent, name, title,
                Vector2.zero, new Vector2(340f, height));
            SetHeight(button.gameObject, height);
            return button;
        }

        private static Image Icon(Transform parent, string name, float width)
        {
            GameObject iconGo = MakeRectObject(name, parent, typeof(Image),
                typeof(LayoutElement));
            Image img = iconGo.GetComponent<Image>();
            img.enabled = false;
            img.preserveAspect = true;
            img.raycastTarget = false;
            LayoutElement le = iconGo.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width;
            le.preferredHeight = width;
            le.flexibleWidth = 0f;
            return img;
        }

        private static Button ParentButton(
            Transform parent, string name, string caption,
            out TMP_Text label, out Image icon, out Button details)
        {
            GameObject objectGo = MakeRectObject(name, parent,
                typeof(Image), typeof(Button),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            Image img = objectGo.GetComponent<Image>();
            img.color = new Color(0.23f, 0.32f, 0.40f, 1f);
            Button button = objectGo.GetComponent<Button>();
            button.targetGraphic = img;
            objectGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
            HorizontalLayoutGroup group = objectGo.GetComponent<HorizontalLayoutGroup>();
            group.spacing = 8f;
            group.padding = new RectOffset(4, 4, 3, 3);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            icon = Icon(objectGo.transform, "SpeciesIcon", 43f);
            label = Label(objectGo.transform, "SpeciesLabel", caption, 16f, 73f);
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            label.alignment = TextAlignmentOptions.Center;
            details = ActionButton(objectGo.transform, "ShowParentDetail", "詳細", 45f);
            LayoutElement buttonLayout = details.GetComponent<LayoutElement>();
            buttonLayout.preferredWidth = 73f;
            buttonLayout.flexibleWidth = 0f;
            return button;
        }

        private static CandidateRow CreateCandidateRow(Transform parent, int rowNumber)
        {
            GameObject row = MakeRectObject("Monster_" + rowNumber, parent,
                typeof(Image), typeof(Button),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.GetComponent<Image>().color = new Color(0.19f, 0.27f, 0.35f, 1f);
            Button button = row.GetComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            SetHeight(row, 104f);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(9, 9, 6, 6);
            group.spacing = 15f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            Image icon = Icon(row.transform, "Icon", 85f);
            TMP_Text label = Label(row.transform, "MonsterInfo", "", 24f, 86f);
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Button details = ActionButton(row.transform, "DetailsButton", "詳細", 76f);
            LayoutElement detailsLayout = details.GetComponent<LayoutElement>();
            detailsLayout.preferredWidth = 116f;
            detailsLayout.flexibleWidth = 0f;
            return new CandidateRow
            {
                button = button, detailButton = details, icon = icon, label = label
            };
        }
#endif
    }
}
