using MixMaster.Core;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Read-only equipment detail shown over the dungeon clear result.
    /// Uses the same scaled modifiers as the existing equipment inventory,
    /// even for rewards already removed by auto-sell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEquipmentDetailPanelUI : MonoBehaviour
    {
        public static DungeonEquipmentDetailPanelUI Show(
            Transform resultCanvas, DungeonEquipmentAward award)
        {
            if (resultCanvas == null || award == null)
                return null;

            GameObject root = new GameObject("DungeonEquipmentDetailOverlay",
                typeof(RectTransform), typeof(Image));
            root.transform.SetParent(resultCanvas, false);
            InventoryUiFactory.Stretch(root.GetComponent<RectTransform>());
            Image shade = root.GetComponent<Image>();
            shade.color = new Color(0.005f, 0.012f, 0.025f, 0.97f);
            shade.raycastTarget = true;

            var panel = root.AddComponent<DungeonEquipmentDetailPanelUI>();
            panel.Build(award);
            root.transform.SetAsLastSibling();
            return panel;
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        private void Build(DungeonEquipmentAward award)
        {
            GameObject card = new GameObject("StatsCard",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            card.transform.SetParent(SafeAreaLayout.CreateContent(transform), false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.07f, 0.08f);
            rect.anchorMax = new Vector2(0.93f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color =
                new Color(0.10f, 0.15f, 0.23f, 1f);

            VerticalLayoutGroup vertical =
                card.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(24, 24, 26, 26);
            vertical.spacing = 13f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            AddText(card.transform, "Title", "装備ステータス", 40f, 72f,
                new Color(1f, 0.83f, 0.47f));

            EquipmentSO def = award.definition;
            string equipmentName = def != null &&
                !string.IsNullOrWhiteSpace(def.displayName)
                ? def.displayName : "装備";
            AddText(card.transform, "EquipmentName", equipmentName,
                32f, 70f, Color.white);

            EquipmentRarity rarity =
                EquipmentRarityUtility.GetRarity(award.level);
            string rarityText =
                EquipmentRarityUtility.Labels[(int)rarity];
            AddText(card.transform, "LevelRarity",
                "Lv." + award.level + "  《" + rarityText + "》" +
                (award.wasAutoSold ? "  ［自動売却済］" : ""),
                26f, 62f, new Color(0.75f, 0.85f, 0.98f));

            if (def != null && !string.IsNullOrWhiteSpace(def.description))
                AddText(card.transform, "Description",
                    def.description, 22f, 58f,
                    new Color(0.81f, 0.83f, 0.87f));

            GameObject scrollGo = new GameObject("StatusScroll",
                typeof(RectTransform), typeof(Image),
                typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(card.transform, false);
            scrollGo.GetComponent<Image>().color =
                new Color(0.055f, 0.085f, 0.14f, 1f);
            LayoutElement le = scrollGo.GetComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = 160f;

            GameObject viewportGo = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewport =
                viewportGo.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(viewport);
            viewportGo.GetComponent<Image>().color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentGo = new GameObject("StatRows",
                typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            RectTransform content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup rows =
                contentGo.GetComponent<VerticalLayoutGroup>();
            rows.padding = new RectOffset(10, 10, 8, 8);
            rows.spacing = 6f;
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrolling = scrollGo.GetComponent<ScrollRect>();
            scrolling.viewport = viewport;
            scrolling.content = content;
            scrolling.horizontal = false;
            scrolling.vertical = true;
            scrolling.movementType =
                ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 37f;

            StatPercentageModifiers modifiers = award.scaledModifiers;
            if (modifiers == null)
            {
                AddText(content, "NoStats",
                    "ステータスを取得できませんでした", 26f, 70f,
                    Color.white);
            }
            else
            {
                for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                {
                    InventoryStat stat = (InventoryStat)i;
                    double value = InventoryStatFilter.GetValue(
                        modifiers, stat);
                    GameObject row = new GameObject(
                        "Stat_" + stat, typeof(RectTransform),
                        typeof(Image), typeof(LayoutElement));
                    row.transform.SetParent(content, false);
                    row.GetComponent<Image>().color = (i & 1) == 0
                        ? new Color(0.16f, 0.20f, 0.29f)
                        : new Color(0.12f, 0.16f, 0.24f);
                    row.GetComponent<LayoutElement>().preferredHeight = 68f;

                    TMP_Text label = InventoryUiFactory.Text(
                        row.transform, "Label",
                        InventoryStatFilter.Labels[i],
                        Vector2.zero, new Vector2(420f, 60f), 24f);
                    RectTransform labelRect = label.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = new Vector2(0.55f, 1f);
                    labelRect.offsetMin = new Vector2(13f, 0f);
                    labelRect.offsetMax = Vector2.zero;
                    label.alignment = TextAlignmentOptions.MidlineLeft;

                    string valueText = (value > 0d ? "+" : "") +
                        (value * 100d).ToString("0.##") + "%";
                    TMP_Text amount = InventoryUiFactory.Text(
                        row.transform, "Value", valueText,
                        Vector2.zero, new Vector2(270f, 60f), 26f);
                    RectTransform amountRect = amount.rectTransform;
                    amountRect.anchorMin = new Vector2(0.55f, 0f);
                    amountRect.anchorMax = Vector2.one;
                    amountRect.offsetMin = Vector2.zero;
                    amountRect.offsetMax = new Vector2(-12f, 0f);
                    amount.alignment = TextAlignmentOptions.MidlineRight;
                    amount.color = value > 0d ? new Color32(82, 232, 117, 255) :
                        value < 0d ? new Color32(255, 98, 98, 255) :
                            new Color32(184, 184, 184, 255);
                }
            }

            Button close = InventoryUiFactory.Button(
                card.transform, "CloseDetail", "リザルトに戻る",
                Vector2.zero, new Vector2(650f, 96f));
            LayoutElement closeHeight = close.gameObject
                .AddComponent<LayoutElement>();
            closeHeight.preferredHeight = 96f;
            close.GetComponent<Image>().color =
                new Color(0.23f, 0.41f, 0.46f, 1f);
            close.onClick.AddListener(Close);
        }

        private static TMP_Text AddText(Transform parent,
            string name, string content, float fontSize,
            float height, Color tint)
        {
            TMP_Text label = InventoryUiFactory.Text(
                parent, name, content, Vector2.zero,
                new Vector2(760f, height), fontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.color = tint;
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            return label;
        }
    }
}
