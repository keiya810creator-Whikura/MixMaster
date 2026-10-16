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

        [Header("Runtime References")]
        [SerializeField] private MonsterManager monsterManager;
        [SerializeField] private BreedingManager breedingManager;
        [SerializeField] private MonsterCatalogSO monsterCatalog;

        [Header("UI")]
        [SerializeField] private Button parentAButton;
        [SerializeField] private Button parentBButton;
        [SerializeField] private TMP_Text parentAText;
        [SerializeField] private TMP_Text parentBText;
        [SerializeField] private Image parentAIcon;
        [SerializeField] private Image parentBIcon;
        [SerializeField] private TMP_Text previewText;
        [SerializeField] private Image previewIcon;
        [SerializeField] private TMP_Text selectionHeader;
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
        private readonly List<CandidateRow> rows = new List<CandidateRow>();
        private string parentAUniqueId;
        private string parentBUniqueId;
        private MonsterSO previewChild;
        private bool previewIsSpecial;
        private int selectingParent; // 0: A, 1: B
        private int page;
        private bool wired;

        private sealed class CandidateRow
        {
            public Button button;
            public TMP_Text label;
            public Image icon;
        }

        private void Awake()
        {
            WireButtons();
        }

        private void OnEnable()
        {
            WireButtons();
            EnsureReferences();
            parentAUniqueId = null;
            parentBUniqueId = null;
            previewChild = null;
            selectingParent = 0;
            page = 0;
            if (confirmationOverlay != null)
                confirmationOverlay.SetActive(false);
            if (previewText != null)
                previewText.text = "親を2体選んで「結果をプレビュー」を押してください。";
            if (noticeText != null)
                noticeText.text = "パーティ編成中・お気に入り・ロック中の個体は配合できません。";
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
        }

        private void WireButtons()
        {
            if (wired)
                return;
            if (parentAButton == null || parentBButton == null ||
                previewButton == null || breedButton == null || closeButton == null ||
                previousPageButton == null || nextPageButton == null ||
                confirmButton == null || cancelButton == null)
                return;

            parentAButton.onClick.AddListener(() => SetActiveParent(0));
            parentBButton.onClick.AddListener(() => SetActiveParent(1));
            previewButton.onClick.AddListener(Preview);
            breedButton.onClick.AddListener(OpenConfirmation);
            closeButton.onClick.AddListener(Close);
            previousPageButton.onClick.AddListener(() => SetPage(page - 1));
            nextPageButton.onClick.AddListener(() => SetPage(page + 1));
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(() => confirmationOverlay.SetActive(false));
            wired = true;
        }

        private void OnDestroy()
        {
            if (!wired)
                return;
            parentAButton.onClick.RemoveAllListeners();
            parentBButton.onClick.RemoveAllListeners();
            previewButton.onClick.RemoveListener(Preview);
            breedButton.onClick.RemoveListener(OpenConfirmation);
            closeButton.onClick.RemoveListener(Close);
            previousPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveListener(Confirm);
            cancelButton.onClick.RemoveAllListeners();
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
            if (selected == null || monsterManager.IsProtectedFromConsumption(selected))
            {
                ShowNotice("このモンスターは選択できません。");
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

            if (!breedingManager.TryPreviewOwned(
                    parentAUniqueId, parentBUniqueId,
                    out MonsterSO child, out bool special, out string error))
            {
                InvalidatePreview();
                ShowNotice(error);
                RefreshAll();
                return;
            }

            previewChild = child;
            previewIsSpecial = special;
            string mode = special ? "★ 特殊配合" : "通常配合";
            previewText.text = mode + "\n" + child.displayName +
                "   (Stage " + child.appearanceStage + ")" +
                "\n配合値 " + child.breedingValue +
                "   /   ID: " + child.monsterId +
                "\n※親の2体は配合時に消費されます。";
            SetIcon(previewIcon, child.icon != null ? child.icon : child.sprite);
            ShowNotice("プレビュー完了。「この結果で配合」を押して最終確認してください。");
            RefreshAll();
        }

        private void OpenConfirmation()
        {
            EnsureReferences();
            if (previewChild == null || breedingManager == null)
                return;

            if (!breedingManager.TryPreviewOwned(
                    parentAUniqueId, parentBUniqueId,
                    out MonsterSO child, out bool special, out string error) ||
                child != previewChild || special != previewIsSpecial)
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
                "本当に配合しますか？\n\n" +
                FormatMonster(a) + "\n＋\n" + FormatMonster(b) +
                "\n\n上記2体を消費して\n【" + child.displayName + "】を獲得します。" +
                "\n※装備は所持品に戻ります。";
            confirmationOverlay.SetActive(true);
        }

        private void Confirm()
        {
            if (confirmationOverlay == null || !confirmationOverlay.activeSelf)
                return;
            confirmationOverlay.SetActive(false);
            EnsureReferences();

            // Recheck the preview immediately before the irreversible action.
            MonsterSO nextChild = null;
            bool nextSpecial = false;
            string previewError = string.Empty;
            if (breedingManager == null ||
                !breedingManager.TryPreviewOwned(
                    parentAUniqueId, parentBUniqueId,
                    out nextChild, out nextSpecial, out previewError) ||
                previewChild == null || previewChild != nextChild ||
                previewIsSpecial != nextSpecial)
            {
                InvalidatePreview();
                ShowNotice("選択状態が変更されました。" +
                    (string.IsNullOrEmpty(previewError) ? "再度プレビューしてください。" : previewError));
                RefreshAll();
                return;
            }

            if (!breedingManager.TryBreed(
                    parentAUniqueId, parentBUniqueId,
                    out OwnedMonsterRecord newborn, out string error))
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
            selectingParent = 0;
            page = 0;
            previewText.text = "配合成功！\n" + bornSpecies.displayName +
                " が仲間になりました！\n所持一覧に追加済みです。";
            SetIcon(previewIcon, bornSpecies.icon != null
                ? bornSpecies.icon : bornSpecies.sprite);
            ShowNotice("配合に成功しました。続けて別の2体を選択できます。");
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

        private void RefreshAll()
        {
            EnsureReferences();
            UpdateSelectedCard(parentAUniqueId, parentAText, parentAIcon, "親Aを選択");
            UpdateSelectedCard(parentBUniqueId, parentBText, parentBIcon, "親Bを選択");
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

        private void BuildCandidateSnapshot()
        {
            candidates.Clear();
            if (monsterManager == null)
                return;
            IReadOnlyList<OwnedMonsterRecord> owned = monsterManager.OwnedMonsters;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null)
                    candidates.Add(owned[i]);
            int maxPage = Mathf.Max(0, (candidates.Count - 1) / PageSize);
            page = Mathf.Clamp(page, 0, maxPage);
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
                bool sameAsOtherParent = record.uniqueId ==
                    (selectingParent == 0 ? parentBUniqueId : parentAUniqueId);
                bool selected = record.uniqueId ==
                    (selectingParent == 0 ? parentAUniqueId : parentBUniqueId);
                MonsterSO species = monsterCatalog != null
                    ? monsterCatalog.GetMonster(record.monsterId) : null;
                string name = species != null ? species.displayName : record.monsterId;
                string status = protectedMonster
                    ? (record.isLocked ? " [ロック]" :
                       record.isFavorite ? " [お気に入り]" : " [パーティ編成中]")
                    : sameAsOtherParent ? " [もう一方の親]" :
                      selected ? " [選択中]" : "";
                TitleSO title = monsterCatalog != null
                    ? monsterCatalog.GetTitle(record.titleId) : null;
                string titlePrefix = title != null
                    ? "《" + title.displayName + "》" : "";
                row.label.text = titlePrefix + name + " Lv." + record.level +
                    status + "\nStage " +
                    (species != null ? species.appearanceStage : 0) +
                    " / 個体値合計 " + GetTotalIv(record);
                SetIcon(row.icon, species != null
                    ? (species.icon != null ? species.icon : species.sprite) : null);
                row.button.interactable = !protectedMonster && !sameAsOtherParent;
                row.button.GetComponent<Image>().color = selected
                    ? new Color(0.24f, 0.47f, 0.37f, 1f)
                    : new Color(0.19f, 0.27f, 0.35f, 1f);
                string capturedId = record.uniqueId;
                row.button.onClick.RemoveAllListeners();
                row.button.onClick.AddListener(() => SelectCandidate(capturedId));
            }

            int totalPages = Mathf.Max(1, (candidates.Count + PageSize - 1) / PageSize);
            if (pageText != null)
                pageText.text = (page + 1) + " / " + totalPages +
                    "  （所持 " + candidates.Count + "体）";
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

        public static BreedingPanelUI CreateRuntime(Transform host)
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
            layout.padding = new RectOffset(20, 20, 24, 18);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text heading = Label(card.transform, "Heading", "モンスター配合屋", 38f, 65f);
            heading.alignment = TextAlignmentOptions.Center;
            TMP_Text helper = Label(card.transform, "Helper",
                "親2体を選び、結果を確認して配合します", 22f, 45f);
            helper.alignment = TextAlignmentOptions.Center;

            GameObject parentRow = LayoutRow(card.transform, "Parents", 155f);
            panel.parentAButton = ParentButton(parentRow.transform, "ParentA",
                "親Aを選択", out panel.parentAText, out panel.parentAIcon);
            panel.parentBButton = ParentButton(parentRow.transform, "ParentB",
                "親Bを選択", out panel.parentBText, out panel.parentBIcon);

            GameObject preview = LayoutRow(card.transform, "Preview", 175f);
            preview.GetComponent<Image>().color = new Color(0.13f, 0.24f, 0.31f, 1f);
            panel.previewIcon = Icon(preview.transform, "ChildIcon", 120f);
            panel.previewText = Label(preview.transform, "ChildPreview",
                "親を2体選択してください。", 23f, 160f);
            LayoutElement previewTextLayout = panel.previewText.gameObject.GetComponent<LayoutElement>();
            previewTextLayout.flexibleWidth = 1f;
            panel.previewText.alignment = TextAlignmentOptions.MidlineLeft;

            panel.selectionHeader = Label(card.transform, "SelectionHeader",
                "親Aを選択", 26f, 48f);
            panel.selectionHeader.alignment = TextAlignmentOptions.Center;

            GameObject scrollBox = MakeRectObject("CandidateScroll", card.transform,
                typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollBox.GetComponent<Image>().color = new Color(0.065f, 0.105f, 0.155f, 1f);
            LayoutElement scrollSize = scrollBox.GetComponent<LayoutElement>();
            scrollSize.minHeight = 110f;
            scrollSize.flexibleHeight = 1f;
            scrollSize.preferredHeight = 380f;
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

            GameObject paging = LayoutRow(card.transform, "Pages", 60f, false);
            panel.previousPageButton = ActionButton(paging.transform, "Previous", "◀ 前", 60f);
            panel.pageText = Label(paging.transform, "PageNumber", "1 / 1", 25f, 60f);
            panel.pageText.alignment = TextAlignmentOptions.Center;
            panel.pageText.GetComponent<LayoutElement>().flexibleWidth = 1f;
            panel.nextPageButton = ActionButton(paging.transform, "Next", "次 ▶", 60f);

            panel.noticeText = Label(card.transform, "Notice", "", 20f, 86f);
            panel.noticeText.color = new Color(1f, 0.87f, 0.65f, 1f);
            panel.noticeText.alignment = TextAlignmentOptions.Center;
            panel.noticeText.enableWordWrapping = true;

            GameObject controls = LayoutRow(card.transform, "Actions", 82f);
            panel.previewButton = ActionButton(
                controls.transform, "PreviewButton", "結果をプレビュー", 82f);
            panel.breedButton = ActionButton(
                controls.transform, "BreedButton", "この結果で配合", 82f);
            panel.breedButton.GetComponent<Image>().color =
                new Color(0.19f, 0.43f, 0.32f, 1f);
            panel.closeButton = ActionButton(card.transform, "CloseButton", "閉じる", 65f);

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
            panel.WireButtons();
            return panel;
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
            out TMP_Text label, out Image icon)
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
            group.padding = new RectOffset(6, 6, 7, 7);
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            icon = Icon(objectGo.transform, "SpeciesIcon", 90f);
            label = Label(objectGo.transform, "SpeciesLabel", caption, 23f, 115f);
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            label.alignment = TextAlignmentOptions.Center;
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
            TMP_Text label = Label(row.transform, "MonsterInfo", "", 25f, 86f);
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return new CandidateRow { button = button, icon = icon, label = label };
        }
    }
}
