using System;
using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Dungeon entrance: map-wide title-rate upgrade, challenge and close.
    /// The UI is created once, then reused by MapBuildingUIRouter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEntrancePanelUI : MonoBehaviour
    {
        [Header("Optional map override")]
        [SerializeField] private MapSO mapOverride;
        [SerializeField] private string mapIdOverride;

        [Header("Dungeon generation settings")]
        [Tooltip("Enabled: use the DungeonManager default generation settings " +
            "configured in the Manager Settings Prefab Inspector.")]
        [SerializeField] private bool useManagerDungeonSettings = true;
        [SerializeField] private DungeonGenerationSettings generationSettings =
            new DungeonGenerationSettings();

        [Header("UI references")]
        [SerializeField] private TMP_Text mapNameText;
        [SerializeField] private Image materialIcon;
        [SerializeField] private TMP_Text materialNameText;
        [SerializeField] private TMP_Text materialCountText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text rateText;
        [SerializeField] private TMP_Text upgradeCostText;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private Button closeButton;

        private AltarManager altar;
        private MaterialInventoryManager materials;
        private MapManager maps;
        private MapSO resolvedMap;
        private string currentMapId = string.Empty;
        private bool listening;
        private bool buttonsWired;

        private sealed class DungeonUpgradeRow
        {
            public TMP_Text heading;
            public TMP_Text effect;
            public TMP_Text cost;
            public TMP_Text applied;
            public Button upgradeButton;
            public Button minButton;
            public Button minusButton;
            public Button plusButton;
            public Button maxButton;
        }

        private readonly System.Collections.Generic.Dictionary<
            DungeonUpgradeType, DungeonUpgradeRow> dungeonUpgradeRows =
            new System.Collections.Generic.Dictionary<
                DungeonUpgradeType, DungeonUpgradeRow>();


        private void Awake()
        {
            WireButtons();
        }

        private void WireButtons()
        {
            if (buttonsWired || upgradeButton == null ||
                challengeButton == null || closeButton == null)
                return;
            HoldToUpgradeButton.Attach(
                upgradeButton, TryUpgradeTitleRate, SaveUpgradeProgress);
            challengeButton.onClick.AddListener(Challenge);
            closeButton.onClick.AddListener(Close);
            buttonsWired = true;
        }

        private void OnEnable()
        {
            WireButtons();
            EnsureDungeonUpgradeControls();
            CacheManagers();
            ResolveMap();
            Subscribe();
            Refresh();
            transform.SetAsLastSibling();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(UpgradeTitleRate);
            if (challengeButton != null)
                challengeButton.onClick.RemoveListener(Challenge);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void SetMapDefinition(MapSO definition)
        {
            mapOverride = definition;
            if (isActiveAndEnabled)
            {
                ResolveMap();
                Refresh();
            }
        }

        public void Refresh()
        {
            CacheManagers();
            if (string.IsNullOrEmpty(currentMapId))
                ResolveMap();

            if (mapNameText != null)
                mapNameText.text = resolvedMap != null &&
                    !string.IsNullOrWhiteSpace(resolvedMap.displayName)
                    ? resolvedMap.displayName + " のダンジョン"
                    : "ダンジョン  " + currentMapId;

            MaterialSO dungeonMaterial =
                resolvedMap != null ? resolvedMap.dungeonMaterial : null;
            long owned = materials != null && dungeonMaterial != null
                ? materials.GetAmount(dungeonMaterial) : 0L;

            if (materialIcon != null)
            {
                materialIcon.sprite =
                    dungeonMaterial != null ? dungeonMaterial.icon : null;
                materialIcon.enabled = materialIcon.sprite != null;
            }
            if (materialNameText != null)
                materialNameText.text = dungeonMaterial != null
                    ? (!string.IsNullOrWhiteSpace(dungeonMaterial.displayName)
                        ? dungeonMaterial.displayName : dungeonMaterial.materialId)
                    : "ダンジョン素材が未設定です";
            if (materialCountText != null)
                materialCountText.text = "所持数  " + owned.ToString("N0");

            int level = altar != null &&
                !string.IsNullOrWhiteSpace(currentMapId)
                ? altar.GetOrCreateMap(currentMapId).titledMonsterRateLevel : 0;
            level = Mathf.Clamp(level, 0, AltarBalance.TitleRateMaxLevel);
            bool maxed = level >= AltarBalance.TitleRateMaxLevel;
            long cost = maxed ? 0L : AltarBalance.GetUpgradeCost(level);

            if (levelText != null)
                levelText.text = "称号付与率強化  Lv " + level + " / " +
                    AltarBalance.TitleRateMaxLevel;
            if (rateText != null)
            {
                float current = AltarBalance.GetMapTitleRate(level) * 100f;
                rateText.text = maxed
                    ? "称号付与率  " + current.ToString("0.00") + "%  MAX"
                    : "称号付与率  " + current.ToString("0.00") +
                      "%  →  " +
                      (AltarBalance.GetMapTitleRate(level + 1) * 100f)
                      .ToString("0.00") + "%";
            }
            if (upgradeCostText != null)
                upgradeCostText.text = maxed ? "強化完了" :
                    "必要ダンジョン素材  ×" + cost.ToString("N0");

            if (upgradeButton != null)
                upgradeButton.interactable =
                    altar != null && materials != null &&
                    dungeonMaterial != null &&
                    !maxed && owned >= cost &&
                    !string.IsNullOrWhiteSpace(currentMapId);
            if (challengeButton != null)
            {
                DungeonManager dungeon =
                    FindFirstObjectByType<DungeonManager>();
                challengeButton.interactable =
                    !string.IsNullOrWhiteSpace(currentMapId) &&
                    dungeon != null && !dungeon.IsDungeonActive;
            }
            if (noticeText != null)
                noticeText.text = dungeonMaterial == null
                    ? "このマップのMapSOとダンジョン素材を設定してください。"
                    : "強化はこのマップ専用です。他のマップには影響しません。";

            foreach (DungeonUpgradeType type in DungeonUpgradeBalance.All)
            {
                if (!dungeonUpgradeRows.TryGetValue(type, out DungeonUpgradeRow row))
                    continue;
                int upgradeLevel = altar != null &&
                    !string.IsNullOrWhiteSpace(currentMapId)
                    ? altar.GetDungeonUpgradeLevel(currentMapId, type) : 0;
                int cap = DungeonUpgradeBalance.MaxLevel(type);
                bool finished = upgradeLevel >= cap;
                long needed = finished ? 0L :
                    DungeonUpgradeBalance.Cost(type, upgradeLevel);
                int appliedLevel = altar != null &&
                    !string.IsNullOrWhiteSpace(currentMapId)
                    ? altar.GetDungeonAppliedLevel(currentMapId, type) : 0;
                row.heading.text = DungeonUpgradeBalance.Name(type) +
                    "  購入Lv " + upgradeLevel + " / " + cap;
                row.applied.text = "適用 Lv " + appliedLevel +
                    " / " + upgradeLevel;
                row.minButton.interactable = appliedLevel > 0;
                row.minusButton.interactable = appliedLevel > 0;
                row.plusButton.interactable = appliedLevel < upgradeLevel;
                row.maxButton.interactable = appliedLevel < upgradeLevel;
                row.effect.text = finished
                    ? DungeonUpgradeBalance.EffectText(type, upgradeLevel) +
                        "  MAX"
                    : DungeonUpgradeBalance.EffectText(type, upgradeLevel) +
                        " → " +
                        DungeonUpgradeBalance.EffectText(type, upgradeLevel + 1);
                row.cost.text = finished ? "最大強化済み" :
                    "素材 ×" + needed.ToString("N0") +
                    "   （所持 " + owned.ToString("N0") + "）";
                row.upgradeButton.interactable =
                    altar != null && materials != null &&
                    dungeonMaterial != null && !finished &&
                    owned >= needed &&
                    !string.IsNullOrWhiteSpace(currentMapId);
            }
        }

        private void SetDungeonAppliedLevel(
            DungeonUpgradeType type, int requestedLevel)
        {
            if (altar == null || string.IsNullOrWhiteSpace(currentMapId))
                return;

            if (altar.SetDungeonAppliedLevel(
                currentMapId, type, requestedLevel))
            {
                SaveManager save = FindFirstObjectByType<SaveManager>();
                save?.SaveGame();
            }
            Refresh();
        }

        private void AdjustDungeonAppliedLevel(
            DungeonUpgradeType type, int delta)
        {
            if (altar == null) return;
            int current = altar.GetDungeonAppliedLevel(currentMapId, type);
            SetDungeonAppliedLevel(type, current + delta);
        }

        private bool TryUpgradeDungeon(DungeonUpgradeType type)
        {
            if (altar == null || materials == null || resolvedMap == null ||
                resolvedMap.dungeonMaterial == null) return false;

            bool success = altar.TryUpgradeDungeon(
                currentMapId, type, resolvedMap.dungeonMaterial, materials);
            Refresh();
            return success;
        }

        private bool TryUpgradeTitleRate()
        {
            if (altar == null || materials == null || resolvedMap == null ||
                resolvedMap.dungeonMaterial == null)
                return false;

            bool success = altar.TryUpgradeMapTitleRate(
                currentMapId, resolvedMap.dungeonMaterial, materials);
            Refresh();
            return success;
        }

        private void SaveUpgradeProgress()
        {
            SaveManager save = FindFirstObjectByType<SaveManager>();
            save?.SaveGame();
        }

        private void Challenge()
        {
            DungeonManager dungeon = FindFirstObjectByType<DungeonManager>();
            if (dungeon == null || string.IsNullOrWhiteSpace(currentMapId))
                return;

            // First release input/movement locks and close the panel.
            // Next implementation step will listen for DungeonStarted
            // to build the 10x10-room map and relocate the player.
            string entranceMap = currentMapId;

            // Persist the safe field position before switching to the
            // transient dungeon map. In-dungeon autosaves also record this
            // position, so restarting never strands the player in empty space.
            SaveManager save = FindFirstObjectByType<SaveManager>();
            if (save != null)
                save.SaveGame();

            Close();

            // Copy the Inspector settings. DungeonManager fills in a random
            // seed when it is zero; never mutate the serialized template.
            DungeonGenerationSettings source =
                useManagerDungeonSettings
                    ? dungeon.GetDefaultSettings()
                    : (generationSettings ?? new DungeonGenerationSettings());
            var requested = new DungeonGenerationSettings
            {
                roomCount = source.roomCount,
                branchCount = source.branchCount,
                sideBossCount = source.sideBossCount,
                seed = source.seed
            };
            dungeon.BeginDungeon(requested, entranceMap);
        }

        public void Close()
        {
            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router == null)
                router = FindFirstObjectByType<MapBuildingUIRouter>();
            if (router != null && router.IsOpen)
                router.CloseAll();
            else
                gameObject.SetActive(false);
        }

        private void CacheManagers()
        {
            if (altar == null) altar = FindFirstObjectByType<AltarManager>();
            if (materials == null)
                materials = FindFirstObjectByType<MaterialInventoryManager>();
            if (maps == null) maps = FindFirstObjectByType<MapManager>();
        }

        private void ResolveMap()
        {
            currentMapId = !string.IsNullOrWhiteSpace(mapIdOverride)
                ? mapIdOverride.Trim()
                : maps != null && !string.IsNullOrWhiteSpace(maps.CurrentMapId)
                    ? maps.CurrentMapId
                    : SceneManager.GetActiveScene().name;

            resolvedMap = mapOverride;
            if (resolvedMap == null)
            {
                TilemapMapRoot[] roots =
                    FindObjectsByType<TilemapMapRoot>(
                        FindObjectsSortMode.None);
                for (int i = 0; i < roots.Length; i++)
                {
                    MapSO candidate = roots[i] != null
                        ? roots[i].MapDefinition : null;
                    if (candidate == null) continue;
                    if (string.Equals(candidate.mapId, currentMapId,
                        StringComparison.Ordinal))
                    {
                        resolvedMap = candidate;
                        break;
                    }
                }
                if (resolvedMap == null && roots.Length == 1 &&
                    roots[0] != null)
                    resolvedMap = roots[0].MapDefinition;
            }

            if (resolvedMap != null &&
                string.IsNullOrWhiteSpace(mapIdOverride) &&
                !string.IsNullOrWhiteSpace(resolvedMap.mapId))
                currentMapId = resolvedMap.mapId;
        }

        private void Subscribe()
        {
            if (listening) return;
            if (altar != null)
                altar.MapAltarProgressChanged += OnMapUpgrade;
            if (materials != null)
                materials.MaterialChanged += OnMaterialChanged;
            listening = true;
        }

        private void Unsubscribe()
        {
            if (!listening) return;
            if (altar != null)
                altar.MapAltarProgressChanged -= OnMapUpgrade;
            if (materials != null)
                materials.MaterialChanged -= OnMaterialChanged;
            listening = false;
        }

        private void OnMapUpgrade(MapAltarProgressRecord record)
        {
            if (record != null && record.mapId == currentMapId)
                Refresh();
        }

        private void OnMaterialChanged(string materialId,
            long delta, long total)
        {
            if (resolvedMap != null && resolvedMap.dungeonMaterial != null &&
                resolvedMap.dungeonMaterial.materialId == materialId)
                Refresh();
        }

        /// <summary>
        /// Adds an upgrade list to both runtime-generated and hand-authored
        /// panels. Preserves existing title upgrade and challenge buttons.
        /// </summary>
        private void EnsureDungeonUpgradeControls()
        {
            if (dungeonUpgradeRows.Count > 0 || challengeButton == null)
                return;

            Transform card = challengeButton.transform.parent;
            if (card == null) return;

            GameObject scrollObject = new GameObject(
                "DungeonUpgradeScroll", typeof(RectTransform),
                typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.transform.SetParent(card, false);
            scrollObject.transform.SetSiblingIndex(
                challengeButton.transform.GetSiblingIndex());
            scrollObject.GetComponent<Image>().color =
                new Color(0.06f, 0.12f, 0.18f, 1f);
            LayoutElement scrollSize =
                scrollObject.GetComponent<LayoutElement>();
            scrollSize.preferredHeight = 565f;
            scrollSize.minHeight = 280f;

            GameObject viewportGo = new GameObject(
                "Viewport", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport =
                viewportGo.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(viewport);
            viewportGo.GetComponent<Image>().color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentGo = new GameObject(
                "UpgradeRows", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            RectTransform content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 10, 10);
            layout.spacing = 9f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38f;

            foreach (DungeonUpgradeType type in DungeonUpgradeBalance.All)
            {
                GameObject panel = new GameObject(
                    "Upgrade_" + type, typeof(RectTransform),
                    typeof(Image), typeof(LayoutElement));
                panel.transform.SetParent(content, false);
                panel.GetComponent<Image>().color =
                    new Color(0.15f, 0.23f, 0.31f, 1f);
                panel.GetComponent<LayoutElement>().preferredHeight = 220f;

                var row = new DungeonUpgradeRow();
                row.heading = InventoryUiFactory.Text(
                    panel.transform, "Name", "", new Vector2(12f, -9f),
                    new Vector2(700f, 36f), 26f);
                row.heading.alignment = TextAlignmentOptions.MidlineLeft;
                row.effect = InventoryUiFactory.Text(
                    panel.transform, "Effect", "", new Vector2(12f, -49f),
                    new Vector2(700f, 33f), 21f);
                row.effect.alignment = TextAlignmentOptions.MidlineLeft;
                row.cost = InventoryUiFactory.Text(
                    panel.transform, "Cost", "", new Vector2(12f, -97f),
                    new Vector2(530f, 38f), 23f);
                row.cost.alignment = TextAlignmentOptions.MidlineLeft;

                row.upgradeButton = InventoryUiFactory.Button(
                    panel.transform, "UpgradeButton", "強化",
                    Vector2.zero, new Vector2(148f, 65f));
                RectTransform buttonRect =
                    row.upgradeButton.GetComponent<RectTransform>();
                buttonRect.anchorMin = Vector2.one;
                buttonRect.anchorMax = Vector2.one;
                buttonRect.pivot = Vector2.one;
                buttonRect.anchoredPosition = new Vector2(-10f, -79f);
                HoldToUpgradeButton.Attach(
                    row.upgradeButton, () => TryUpgradeDungeon(type),
                    SaveUpgradeProgress);

                row.minButton = InventoryUiFactory.Button(
                    panel.transform, "MinimumApplied", "Lv0",
                    new Vector2(12f, -151f), new Vector2(106f, 60f));
                row.minusButton = InventoryUiFactory.Button(
                    panel.transform, "DecreaseApplied", "−",
                    new Vector2(126f, -151f), new Vector2(80f, 60f));
                row.applied = InventoryUiFactory.Text(
                    panel.transform, "AppliedLevel", "",
                    new Vector2(214f, -153f), new Vector2(215f, 56f), 23f);
                row.applied.alignment = TextAlignmentOptions.Center;
                row.plusButton = InventoryUiFactory.Button(
                    panel.transform, "IncreaseApplied", "＋",
                    new Vector2(434f, -151f), new Vector2(80f, 60f));
                row.maxButton = InventoryUiFactory.Button(
                    panel.transform, "MaximumApplied", "最大",
                    new Vector2(522f, -151f), new Vector2(130f, 60f));

                row.minButton.onClick.AddListener(
                    () => SetDungeonAppliedLevel(type, 0));
                row.minusButton.onClick.AddListener(
                    () => AdjustDungeonAppliedLevel(type, -1));
                row.plusButton.onClick.AddListener(
                    () => AdjustDungeonAppliedLevel(type, 1));
                row.maxButton.onClick.AddListener(() =>
                    SetDungeonAppliedLevel(type,
                        altar != null ? altar.GetDungeonUpgradeLevel(
                            currentMapId, type) : 0));

                dungeonUpgradeRows.Add(type, row);
            }
        }

        /// <summary>
        /// Runtime fallback if no hand-authored entrance panel is wired into
        /// MapBuildingUIRouter. A UI Canvas is reused whenever available.
        /// </summary>
        public static DungeonEntrancePanelUI CreateRuntime(Transform host)
        {
            Canvas canvas = host != null
                ? host.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                Canvas[] available =
                    FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int i = 0; i < available.Length; i++)
                {
                    if (available[i] != null && available[i].isRootCanvas &&
                        available[i].renderMode != RenderMode.WorldSpace)
                    {
                        canvas = available[i];
                        break;
                    }
                }
            }
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject(
                    "DungeonEntranceUICanvas", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 600;
                CanvasScaler scaler =
                    canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
            }

            GameObject overlay = new GameObject("DungeonEntrancePanel",
                typeof(RectTransform), typeof(Image),
                typeof(Canvas), typeof(GraphicRaycaster));
            overlay.transform.SetParent(canvas.transform, false);
            overlay.SetActive(false);
            InventoryUiFactory.Stretch(overlay.GetComponent<RectTransform>());
            Image shade = overlay.GetComponent<Image>();
            shade.color = new Color(0.015f, 0.025f, 0.045f, 0.94f);
            shade.raycastTarget = true;
            Canvas overlayCanvas = overlay.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 610;

            DungeonEntrancePanelUI view =
                overlay.AddComponent<DungeonEntrancePanelUI>();

            GameObject card = new GameObject("DungeonEntranceCard",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            card.transform.SetParent(overlay.transform, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.07f, 0.055f);
            rect.anchorMax = new Vector2(0.93f, 0.945f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color =
                new Color(0.11f, 0.18f, 0.26f, 1f);

            VerticalLayoutGroup layout =
                card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text title = AddLabel(card.transform,
                "Heading", "ダンジョンへの挑戦", 35f, 60f);
            title.alignment = TextAlignmentOptions.Center;
            view.mapNameText = AddLabel(card.transform,
                "MapName", "", 29f, 55f);
            view.mapNameText.alignment = TextAlignmentOptions.Center;

            GameObject materialRow = new GameObject("MaterialRow",
                typeof(RectTransform), typeof(LayoutElement),
                typeof(Image));
            materialRow.transform.SetParent(card.transform, false);
            materialRow.GetComponent<Image>().color =
                new Color(0.18f, 0.25f, 0.33f, 1f);
            Size(materialRow, 82f);
            GameObject materialIconObject = new GameObject("MaterialIcon",
                typeof(RectTransform), typeof(Image));
            materialIconObject.transform.SetParent(materialRow.transform, false);
            RectTransform iconRect =
                materialIconObject.GetComponent<RectTransform>();
            InventoryUiFactory.TopLeft(iconRect,
                new Vector2(16f, -9f), new Vector2(62f, 62f));
            view.materialIcon = materialIconObject.GetComponent<Image>();
            view.materialIcon.preserveAspect = true;
            view.materialIcon.raycastTarget = false;
            view.materialNameText = InventoryUiFactory.Text(
                materialRow.transform, "MaterialName", "",
                Vector2.zero, new Vector2(600f, 75f), 27f);
            RectTransform nameRect =
                view.materialNameText.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(93f, 0f);
            nameRect.offsetMax = new Vector2(-12f, 0f);
            view.materialNameText.alignment = TextAlignmentOptions.MidlineLeft;

            view.materialCountText =
                AddLabel(card.transform, "OwnedCount", "", 24f, 50f);
            view.levelText =
                AddLabel(card.transform, "UpgradeLevel", "", 27f, 55f);
            view.rateText =
                AddLabel(card.transform, "TitleRate", "", 28f, 55f);
            view.upgradeCostText =
                AddLabel(card.transform, "UpgradeCost", "", 24f, 50f);

            view.upgradeButton = AddButton(card.transform,
                "UpgradeTitleRate", "ダンジョン素材を投入して強化", 69f);
            view.challengeButton = AddButton(card.transform,
                "ChallengeDungeon", "ダンジョンに挑戦する", 79f);
            view.challengeButton.GetComponent<Image>().color =
                new Color(0.23f, 0.42f, 0.36f, 1f);
            view.closeButton = AddButton(card.transform,
                "CloseDungeonEntrance", "閉じる", 63f);
            view.noticeText = AddLabel(card.transform,
                "Notice", "", 21f, 78f);
            view.noticeText.enableWordWrapping = true;
            view.noticeText.alignment = TextAlignmentOptions.Center;

            view.WireButtons();
            view.EnsureDungeonUpgradeControls();
            return view;
        }

        private static TMP_Text AddLabel(Transform parent,
            string name, string label, float fontSize, float height)
        {
            TMP_Text text = InventoryUiFactory.Text(
                parent, name, label, Vector2.zero,
                new Vector2(700f, height), fontSize);
            text.alignment = TextAlignmentOptions.Center;
            Size(text.gameObject, height);
            return text;
        }

        private static Button AddButton(Transform parent,
            string name, string caption, float height)
        {
            Button button = InventoryUiFactory.Button(
                parent, name, caption,
                Vector2.zero, new Vector2(720f, height));
            Size(button.gameObject, height);
            return button;
        }

        private static void Size(GameObject obj, float height)
        {
            LayoutElement element = obj.GetComponent<LayoutElement>() ??
                obj.AddComponent<LayoutElement>();
            element.preferredHeight = height;
        }
    }
}
