using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Reusable modal; existing inventory prefabs require no rebaking.</summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentEnhancementPanelUI : MonoBehaviour
    {
        private EquipmentManager equipment;
        private GameManager game;
        private MaterialInventoryManager materials;
        private string uniqueId;
        private TMP_Text equipmentName;
        private TMP_Text levelText;
        private TMP_Text mapText;
        private TMP_Text goldText;
        private TMP_Text statusText;
        private TMP_Text noticeText;
        private Button upgradeButton;
        private TMP_Text upgradeLabel;
        private readonly TMP_Text[] materialTexts = new TMP_Text[6];
        private readonly Image[] materialIcons = new Image[6];
        private static readonly Color Enough = new Color32(190, 224, 209, 255);
        private static readonly Color Missing = new Color32(255, 98, 98, 255);

        public static EquipmentEnhancementPanelUI Ensure(Transform parent)
        {
            GameObject root = new GameObject("EquipmentEnhancementOverlay",
                typeof(RectTransform), typeof(Image));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            InventoryUiFactory.Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.045f, 0.97f);
            var panel = root.AddComponent<EquipmentEnhancementPanelUI>();
            panel.Build();
            return panel;
        }

        public void Open(EquipmentManager manager, string equipmentUniqueId)
        {
            // Flush an unfinished hold before switching the selected item.
            if (gameObject.activeSelf) Close();
            equipment = manager;
            uniqueId = equipmentUniqueId;
            game = manager != null ? manager.GetComponent<GameManager>() : null;
            if (game == null) game = FindFirstObjectByType<GameManager>();
            materials = manager != null ? manager.GetComponent<MaterialInventoryManager>() : null;
            if (materials == null) materials = FindFirstObjectByType<MaterialInventoryManager>();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close() => gameObject.SetActive(false);

        private void OnEnable()
        {
            if (equipment != null) equipment.InventoryChanged += Refresh;
            if (game != null) game.GoldChanged += OnGoldChanged;
            if (materials != null) materials.MaterialChanged += OnMaterialChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.InventoryChanged -= Refresh;
            if (game != null) game.GoldChanged -= OnGoldChanged;
            if (materials != null) materials.MaterialChanged -= OnMaterialChanged;
            // The child HoldToUpgradeButton flushes its dirty batch on disable.
        }

        private void OnGoldChanged(long _) => Refresh();
        private void OnMaterialChanged(string id, long delta, long after) => Refresh();

        private bool TryUpgrade()
        {
            if (equipment == null || !isActiveAndEnabled) return false;
            bool success = equipment.TryEnhanceEquipment(uniqueId, out string error);
            Refresh();
            if (!success && !string.IsNullOrEmpty(error)) noticeText.text = error;
            return success;
        }

        private void SaveProgress()
        {
            if (equipment == null) return;
            SaveManager save = equipment.GetComponent<SaveManager>() ??
                FindFirstObjectByType<SaveManager>();
            if (save != null) save.SaveGame();
        }

        private void Refresh()
        {
            if (upgradeButton == null) return;
            EquipmentRecord record = equipment != null ? equipment.FindEquipment(uniqueId) : null;
            if (record == null)
            {
                upgradeButton.interactable = false;
                noticeText.text = "装備を選択してください。";
                return;
            }

            EquipmentSO definition = equipment.GetEquipmentDefinition(record.equipmentId);
            equipmentName.text = definition != null ? definition.displayName : record.equipmentId;
            int count = EquipmentEnhancement.Count(record);
            int level = EquipmentEnhancement.Level(record);
            bool max = count >= EquipmentEnhancement.MaxEnhancements;
            levelText.text = "Lv." + level + (max ? "　MAX" : " → Lv." + (level + 1)) +
                "　強化 ＋" + count + "/50\n取得時 Lv." + EquipmentEnhancement.OriginalLevel(record) +
                " ／ 上限 Lv." + (EquipmentEnhancement.OriginalLevel(record) + 50);
            bool affordable = equipment.CanEnhanceEquipment(uniqueId,
                out EquipmentEnhancementCost cost, out string error);
            MapSO map = cost != null ? cost.map : equipment.ResolveEnhancementMap(record);
            mapText.text = "対象マップ：" + (map != null ? map.displayName : "未設定");
            upgradeButton.interactable = affordable;
            upgradeLabel.text = max ? "強化完了 ＋50" : "Lvを1上げる";
            goldText.text = cost != null
                ? "必要ゴールド " + cost.gold.ToString("N0") + " G ／ 所持 " +
                    (game != null ? game.Gold : 0L).ToString("N0") + " G"
                : max ? "最大強化に到達しました" : "必要費用を取得できません";
            goldText.color = cost != null && (game == null || game.Gold < cost.gold) ? Missing : Enough;
            for (int i = 0; i < materialTexts.Length; i++)
            {
                bool show = cost != null && i < cost.materials.Count;
                materialTexts[i].transform.parent.gameObject.SetActive(show);
                if (!show) continue;
                MaterialSO material = cost.materials[i];
                long owned = materials != null ? materials.GetAmount(material) : 0L;
                materialIcons[i].sprite = material.icon;
                materialIcons[i].enabled = material.icon != null;
                materialTexts[i].text = material.displayName + "\n" + owned.ToString("N0") +
                    " / " + cost.amountPerMaterial.ToString("N0") + " 個";
                materialTexts[i].color = owned >= cost.amountPerMaterial ? Enough : Missing;
            }
            statusText.text = "現在の補正\n" + InventoryStatFilter.FormatModifiers(
                equipment.GetScaledModifiers(record)) + (max ? "" : "\n\n強化後の補正\n" +
                InventoryStatFilter.FormatModifiers(equipment.GetScaledModifiers(record, level + 1)));
            noticeText.text = affordable ? "タップで1回 ／ 長押しで連続強化" : error;
        }

        private void Build()
        {
            GameObject card = new GameObject("EnhancementCard", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            card.transform.SetParent(transform, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.04f, 0.04f);
            rect.anchorMax = new Vector2(0.96f, 0.96f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.20f, 1f);
            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Label(card.transform, "Heading", "装備強化", 36f, 54f).color =
                new Color(1f, 0.83f, 0.47f);
            equipmentName = Label(card.transform, "EquipmentName", "", 30f, 50f);
            levelText = Label(card.transform, "Level", "", 25f, 76f);
            mapText = Label(card.transform, "SourceMap", "", 22f, 36f);
            goldText = Label(card.transform, "GoldCost", "", 24f, 44f);

            GameObject grid = new GameObject("MaterialCosts", typeof(RectTransform),
                typeof(GridLayoutGroup), typeof(LayoutElement));
            grid.transform.SetParent(card.transform, false);
            GridLayoutGroup cells = grid.GetComponent<GridLayoutGroup>();
            cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cells.constraintCount = 2;
            cells.spacing = new Vector2(10f, 8f);
            cells.cellSize = new Vector2(400f, 78f);
            grid.GetComponent<LayoutElement>().preferredHeight = 250f;
            // Responsive cell width is updated from the laid-out parent.
            for (int i = 0; i < 6; i++)
            {
                GameObject row = new GameObject("Material" + i, typeof(RectTransform), typeof(Image));
                row.transform.SetParent(grid.transform, false);
                row.GetComponent<Image>().color = new Color(0.11f, 0.19f, 0.27f, 1f);
                GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(row.transform, false);
                InventoryUiFactory.TopLeft(icon.GetComponent<RectTransform>(),
                    new Vector2(8f, -8f), new Vector2(58f, 58f));
                materialIcons[i] = icon.GetComponent<Image>();
                materialIcons[i].preserveAspect = true;
                materialIcons[i].raycastTarget = false;
                TMP_Text text = InventoryUiFactory.Text(row.transform, "Amount", "",
                    Vector2.zero, Vector2.zero, 21f);
                InventoryUiFactory.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(74f, 4f);
                text.rectTransform.offsetMax = new Vector2(-6f, -4f);
                text.enableWordWrapping = true;
                materialTexts[i] = text;
            }
            GameObject scroll = new GameObject("StatPreview", typeof(RectTransform), typeof(Image),
                typeof(ScrollRect), typeof(LayoutElement));
            scroll.transform.SetParent(card.transform, false);
            scroll.GetComponent<Image>().color = new Color(0.045f, 0.075f, 0.12f, 1f);
            LayoutElement scrollSize = scroll.GetComponent<LayoutElement>();
            scrollSize.minHeight = 80f;
            scrollSize.flexibleHeight = 1f;
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scroll.transform, false);
            InventoryUiFactory.Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            statusText = InventoryUiFactory.Text(viewport.transform, "Modifiers", "",
                Vector2.zero, Vector2.zero, 23f);
            RectTransform textRect = statusText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = new Vector2(-24f, 0f);
            textRect.anchoredPosition = new Vector2(0f, -10f);
            statusText.enableAutoSizing = false;
            statusText.enableWordWrapping = true;
            ContentSizeFitter fitter = statusText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scrolling = scroll.GetComponent<ScrollRect>();
            scrolling.viewport = viewport.GetComponent<RectTransform>();
            scrolling.content = textRect;
            scrolling.horizontal = false;
            scrolling.movementType = ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 40f;
            noticeText = Label(card.transform, "Notice", "", 22f, 50f);
            noticeText.alignment = TextAlignmentOptions.Center;
            upgradeButton = InventoryUiFactory.Button(card.transform, "Upgrade", "Lvを1上げる",
                Vector2.zero, new Vector2(400f, 68f));
            upgradeButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 68f;
            upgradeLabel = upgradeButton.GetComponentInChildren<TMP_Text>();
            upgradeButton.GetComponent<Image>().color = new Color(0.14f, 0.43f, 0.33f, 1f);
            HoldToUpgradeButton.Attach(upgradeButton, TryUpgrade, SaveProgress);
            Button close = InventoryUiFactory.Button(card.transform, "Close", "装備一覧に戻る",
                Vector2.zero, new Vector2(400f, 60f));
            close.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            close.onClick.AddListener(Close);
        }

        private void LateUpdate()
        {
            GridLayoutGroup grid = materialTexts[0] != null
                ? materialTexts[0].transform.parent.parent.GetComponent<GridLayoutGroup>() : null;
            if (grid == null) return;
            float width = ((RectTransform)grid.transform).rect.width;
            Vector2 size = new Vector2(Mathf.Max(1f, (width - grid.spacing.x) / 2f), 78f);
            if (grid.cellSize != size) grid.cellSize = size;
        }

        private static TMP_Text Label(Transform parent, string name, string value,
            float fontSize, float height)
        {
            TMP_Text label = InventoryUiFactory.Text(parent, name, value,
                Vector2.zero, Vector2.zero, fontSize);
            label.enableWordWrapping = true;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }
    }
}
