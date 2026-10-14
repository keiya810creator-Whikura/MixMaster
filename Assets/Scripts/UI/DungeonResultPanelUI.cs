using System;
using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Full-screen, scrollable dungeon clear result.
    /// Lists the actual credited dungeon material and all rolled equipment
    /// (including items sold by an enabled auto-sell rule).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonResultPanelUI : MonoBehaviour
    {
        private Action returnCallback;
        private Button returnButton;
        private bool usedReturn;

        public static DungeonResultPanelUI Show(
            DungeonRewardReport report, Action onReturn)
        {
            GameObject root = new GameObject("DungeonResultCanvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            DungeonResultPanelUI panel =
                root.AddComponent<DungeonResultPanelUI>();
            panel.returnCallback = onReturn;
            panel.Build(report);
            return panel;
        }

        public void Close()
        {
            if (returnButton != null)
                returnButton.onClick.RemoveListener(Return);
            returnCallback = null;
            Destroy(gameObject);
        }

        private void Return()
        {
            if (usedReturn) return;
            usedReturn = true;
            returnButton.interactable = false;
            returnCallback?.Invoke();
        }

        private void Build(DungeonRewardReport report)
        {
            RectTransform canvasRect =
                GetComponent<RectTransform>();
            GameObject overlay = new GameObject("ModalBackdrop",
                typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(transform, false);
            InventoryUiFactory.Stretch(overlay.GetComponent<RectTransform>());
            Image background = overlay.GetComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.07f, 0.96f);
            background.raycastTarget = true;

            GameObject card = new GameObject("ResultCard",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            card.transform.SetParent(overlay.transform, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.10f);
            rect.anchorMax = new Vector2(0.94f, 0.90f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color =
                new Color(0.11f, 0.16f, 0.24f, 1f);

            VerticalLayoutGroup layout =
                card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 30, 30);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TMP_Text title = AddText(card.transform, "Heading",
                "ダンジョンクリア！", 42f, 82f);
            title.color = new Color(1f, 0.85f, 0.41f);
            AddText(card.transform, "MapName",
                report != null ? report.mapName : "", 27f, 58f);

            AddText(card.transform, "MaterialHeader",
                "獲得ダンジョン素材", 30f, 65f);
            MaterialSO material =
                report != null ? report.dungeonMaterial : null;
            AddRewardRow(card.transform,
                material != null ? material.icon : null,
                material != null && !string.IsNullOrWhiteSpace(
                    material.displayName) ? material.displayName :
                    material != null ? material.materialId : "素材未設定",
                "×" + (report != null
                    ? report.actualMaterialAmount : 0L).ToString("N0"));

            int count = report != null ? report.equipment.Count : 0;
            AddText(card.transform, "EquipmentHeader",
                "獲得装備  " + count.ToString("N0") + "個", 30f, 68f);

            GameObject scrollObject = new GameObject("EquipmentScroll",
                typeof(RectTransform), typeof(Image),
                typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.transform.SetParent(card.transform, false);
            scrollObject.GetComponent<Image>().color =
                new Color(0.07f, 0.10f, 0.16f);
            LayoutElement scrollSize =
                scrollObject.GetComponent<LayoutElement>();
            scrollSize.flexibleHeight = 1f;
            scrollSize.minHeight = 135f;

            GameObject viewportObj = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image),
                typeof(Mask));
            viewportObj.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport =
                viewportObj.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(viewport);
            viewportObj.GetComponent<Image>().color = Color.white;
            viewportObj.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObj = new GameObject("Items",
                typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewport, false);
            RectTransform content =
                contentObj.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup column =
                contentObj.GetComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(5, 5, 6, 6);
            column.spacing = 7f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                contentObj.GetComponent<ContentSizeFitter>();
            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            if (count == 0)
                AddText(content, "EmptyEquipment",
                    "獲得装備なし", 25f, 90f);

            if (report != null)
                foreach (DungeonEquipmentAward award in report.equipment)
                {
                    EquipmentSO definition = award.definition;
                    string name = definition != null &&
                        !string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.displayName :
                        definition != null ? definition.equipmentId : "装備";
                    string value = "Lv." + award.level +
                        (award.wasAutoSold ? "  （自動売却済）" : "");
                    AddRewardRow(content,
                        definition != null ? definition.icon : null,
                        name, value);
                }

            if (report != null)
                AddText(card.transform, "BossBonus",
                    "討伐した中ボス: " + report.defeatedMidBosses +
                    "体   /  装備報酬に反映", 23f, 58f);

            returnButton = InventoryUiFactory.Button(
                card.transform, "ReturnButton",
                "ダンジョン入口に帰還", Vector2.zero,
                new Vector2(680f, 100f));
            returnButton.GetComponent<Image>().color =
                new Color(0.22f, 0.43f, 0.36f);
            AddHeight(returnButton.gameObject, 100f);
            returnButton.onClick.AddListener(Return);
        }

        private static TMP_Text AddText(Transform parent,
            string name, string label, float fontSize, float height)
        {
            TMP_Text text = InventoryUiFactory.Text(
                parent, name, label, Vector2.zero,
                new Vector2(700f, height), fontSize);
            text.alignment = TextAlignmentOptions.Center;
            AddHeight(text.gameObject, height);
            return text;
        }

        private static void AddRewardRow(Transform parent,
            Sprite icon, string label, string value)
        {
            GameObject row = new GameObject("Reward_" + label,
                typeof(RectTransform), typeof(Image),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color =
                new Color(0.17f, 0.23f, 0.32f);
            AddHeight(row, 86f);

            if (icon != null)
            {
                GameObject iconObj = new GameObject("Icon",
                    typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(row.transform, false);
                RectTransform iconRect =
                    iconObj.GetComponent<RectTransform>();
                InventoryUiFactory.TopLeft(iconRect,
                    new Vector2(12f, -11f), new Vector2(64f, 64f));
                Image image = iconObj.GetComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            TMP_Text nameText = InventoryUiFactory.Text(
                row.transform, "RewardName", label, Vector2.zero,
                new Vector2(520f, 70f), 25f);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(0.68f, 1f);
            nameRect.pivot = new Vector2(0f, 0.5f);
            nameRect.offsetMin = new Vector2(88f, 0f);
            nameRect.offsetMax = Vector2.zero;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text amountText = InventoryUiFactory.Text(
                row.transform, "RewardValue", value, Vector2.zero,
                new Vector2(280f, 72f), 23f);
            RectTransform amountRect = amountText.rectTransform;
            amountRect.anchorMin = new Vector2(0.68f, 0f);
            amountRect.anchorMax = Vector2.one;
            amountRect.offsetMin = Vector2.zero;
            amountRect.offsetMax = new Vector2(-10f, 0f);
            amountText.alignment = TextAlignmentOptions.MidlineRight;
        }

        private static void AddHeight(GameObject go, float height)
        {
            LayoutElement size = go.GetComponent<LayoutElement>() ??
                go.AddComponent<LayoutElement>();
            size.preferredHeight = height;
        }
    }
}
