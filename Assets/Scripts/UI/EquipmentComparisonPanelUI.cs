using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Runtime-created comparison modal. Shows every final stat
    /// (including capped resistances), not just the item modifiers.
    /// It has no side effects until the player confirms.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentComparisonPanelUI : MonoBehaviour
    {
        private const float DifferenceEpsilon = 0.00001f;
        private static readonly Color PositiveColor =
            new Color32(82, 232, 117, 255);  // #52E875
        private static readonly Color NegativeColor =
            new Color32(255, 98, 98, 255);   // #FF6262
        private static readonly Color NeutralColor =
            new Color32(184, 184, 184, 255); // #B8B8B8

        private struct Row
        {
            public TMP_Text oldValue;
            public TMP_Text newValue;
            public TMP_Text deltaValue;
        }

        private TMP_Text title;
        private TMP_Text equipmentText;
        private TMP_Text hint;
        private readonly List<Row> rows = new List<Row>();
        private Action accept;

        public static EquipmentComparisonPanelUI Create(Transform parent)
        {
            GameObject root = new GameObject("EquipmentComparisonOverlay",
                typeof(RectTransform), typeof(Image),
                typeof(EquipmentComparisonPanelUI));
            root.transform.SetParent(parent, false);
            RectTransform overlay = root.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(overlay);
            Image cover = root.GetComponent<Image>();
            cover.color = new Color(0.012f, 0.020f, 0.035f, 0.95f);
            cover.raycastTarget = true;

            GameObject cardObject = new GameObject("ComparisonCard",
                typeof(RectTransform), typeof(Image));
            cardObject.transform.SetParent(root.transform, false);
            RectTransform card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = new Vector2(0.05f, 0.09f);
            card.anchorMax = new Vector2(0.95f, 0.93f);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.color = new Color(0.082f, 0.123f, 0.184f, 1f);
            cardImage.raycastTarget = true;

            EquipmentComparisonPanelUI panel =
                root.GetComponent<EquipmentComparisonPanelUI>();
            panel.title = InventoryUiFactory.Text(
                card, "ComparisonTitle", "装備比較",
                new Vector2(27f, -20f), new Vector2(770f, 62f), 38f);
            panel.title.color = new Color(1f, 0.82f, 0.48f);

            panel.equipmentText = InventoryUiFactory.Text(
                card, "EquipmentChange", "",
                new Vector2(28f, -95f), new Vector2(775f, 108f), 25f);
            panel.equipmentText.enableWordWrapping = true;

            panel.hint = InventoryUiFactory.Text(
                card, "Hint", "緑：上昇   赤：下降   灰：変化なし",
                new Vector2(29f, -210f), new Vector2(770f, 38f), 22f);
            panel.hint.color = NeutralColor;

            GameObject head = new GameObject("ColumnHeader",
                typeof(RectTransform), typeof(Image));
            head.transform.SetParent(card, false);
            RectTransform heading = head.GetComponent<RectTransform>();
            heading.anchorMin = new Vector2(0.03f, 1f);
            heading.anchorMax = new Vector2(0.97f, 1f);
            heading.pivot = new Vector2(0.5f, 1f);
            heading.anchoredPosition = new Vector2(0f, -262f);
            heading.sizeDelta = new Vector2(0f, 48f);
            head.GetComponent<Image>().color =
                new Color(0.16f, 0.22f, 0.30f, 1f);
            panel.MakeRowText(heading, "能力値", 0.01f, 0.25f, 23f);
            panel.MakeRowText(heading, "現在", 0.27f, 0.23f, 23f);
            panel.MakeRowText(heading, "変更後", 0.51f, 0.23f, 23f);
            panel.MakeRowText(heading, "差分", 0.76f, 0.23f, 23f);

            GameObject scrollObject = new GameObject("StatsScroll",
                typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(card, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.03f, 0.12f);
            scrollRect.anchorMax = new Vector2(0.97f, 1f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = new Vector2(0f, -314f);

            GameObject viewportObject = new GameObject("Viewport",
                typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollRect, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(viewport);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportImage.raycastTarget = true;

            GameObject contentObject = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 3f;
            layout.padding = new RectOffset(0, 0, 5, 5);
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = false;
            scroll.scrollSensitivity = 43f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
            {
                GameObject rowObject = new GameObject("Stat_" + i,
                    typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                rowObject.transform.SetParent(content, false);
                RectTransform rowRect = rowObject.GetComponent<RectTransform>();
                LayoutElement le = rowObject.GetComponent<LayoutElement>();
                le.minHeight = 58f;
                le.preferredHeight = 58f;
                rowObject.GetComponent<Image>().color = i % 2 == 0
                    ? new Color(0.135f, 0.18f, 0.245f, 1f)
                    : new Color(0.107f, 0.151f, 0.213f, 1f);
                rowObject.GetComponent<Image>().raycastTarget = false;

                panel.MakeRowText(rowRect,
                    InventoryStatFilter.Labels[i], 0.01f, 0.25f, 21f);
                Row line = new Row
                {
                    oldValue = panel.MakeRowText(
                        rowRect, "-", 0.27f, 0.23f, 21f),
                    newValue = panel.MakeRowText(
                        rowRect, "-", 0.51f, 0.23f, 21f),
                    deltaValue = panel.MakeRowText(
                        rowRect, "-", 0.76f, 0.23f, 21f)
                };
                panel.rows.Add(line);
            }

            Button cancel = InventoryUiFactory.Button(card, "Cancel",
                "キャンセル", new Vector2(0f, 0f), new Vector2(250f, 68f));
            RectTransform cancelRect = cancel.GetComponent<RectTransform>();
            SetBottomButton(cancelRect, -285f, 250f);
            cancel.onClick.AddListener(panel.Close);

            Button confirm = InventoryUiFactory.Button(card, "Confirm",
                "この装備で確定", Vector2.zero, new Vector2(305f, 68f));
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            SetBottomButton(confirmRect, -22f, 305f);
            confirm.GetComponent<Image>().color =
                new Color(0.16f, 0.45f, 0.30f, 1f);
            confirm.onClick.AddListener(panel.Confirm);

            root.SetActive(false);
            return panel;
        }

        private TMP_Text MakeRowText(RectTransform parent,
            string value, float x, float width, float fontSize)
        {
            GameObject go = new GameObject("Cell",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x + width, 1f);
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-3f, 0f);
            TMP_Text label = go.GetComponent<TMP_Text>();
            label.text = value;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = 15f;
            label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static void SetBottomButton(
            RectTransform rect, float right, float width)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(right, 25f);
            rect.sizeDelta = new Vector2(width, 68f);
        }

        public void Show(EquipmentComparisonCalculator.Comparison comparison,
            Action onAccept)
        {
            if (comparison == null)
                return;
            accept = onAccept;
            title.text = (comparison.isUnequip ? "装備解除の比較" : "装備変更の比較") +
                "　" + comparison.ownerName + "　" + (comparison.slot + 1) + "枠";
            equipmentText.text = "現在: " + comparison.previousItem +
                "\n変更後: " + comparison.newItem;

            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
            {
                InventoryStat stat = (InventoryStat)i;
                double before = InventoryStatFilter.GetValue(comparison.before, stat);
                double after = InventoryStatFilter.GetValue(comparison.after, stat);
                double difference = after - before;
                Row row = rows[i - 1];
                row.oldValue.text = Format(before, stat);
                row.newValue.text = Format(after, stat);
                row.deltaValue.text = FormatDelta(difference, stat);

                Color tint = difference > DifferenceEpsilon
                    ? PositiveColor : difference < -DifferenceEpsilon
                        ? NegativeColor : NeutralColor;
                row.newValue.color = tint;
                row.deltaValue.color = tint;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        private static bool IsRate(InventoryStat stat)
        {
            return stat == InventoryStat.CriticalRate ||
                stat == InventoryStat.DropRate ||
                (stat >= InventoryStat.FireResistance &&
                 stat <= InventoryStat.DarkResistance);
        }

        private static bool IsInteger(InventoryStat stat)
        {
            return stat >= InventoryStat.MaxHp &&
                stat <= InventoryStat.MagicDefense;
        }

        private static string Format(double value, InventoryStat stat)
        {
            if (IsRate(stat))
                return (value * 100d).ToString("0.##") + "%";
            if (stat == InventoryStat.CriticalMultiplier)
                return "x" + value.ToString("0.##");
            return value.ToString(IsInteger(stat) ? "N0" : "0.##");
        }

        private static string FormatDelta(double delta, InventoryStat stat)
        {
            if (Math.Abs(delta) < DifferenceEpsilon) return "±0";
            string sign = delta > 0d ? "+" : "";
            if (IsRate(stat))
                return sign + (delta * 100d).ToString("0.##") + "pt";
            if (stat == InventoryStat.CriticalMultiplier)
                return sign + delta.ToString("0.##");
            return sign + delta.ToString(IsInteger(stat) ? "N0" : "0.##");
        }

        private void Confirm()
        {
            Action callback = accept;
            accept = null;
            Close();
            callback?.Invoke();
        }

        public void Close()
        {
            accept = null;
            gameObject.SetActive(false);
        }
    }
}
