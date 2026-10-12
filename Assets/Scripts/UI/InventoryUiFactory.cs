using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Small runtime UI building helpers. Existing prefab instances can gain
    /// inventory filters without regenerating or overwriting their assets.
    /// </summary>
    public static class InventoryUiFactory
    {
        public static TMP_Dropdown Dropdown(
            Transform parent, string name, Vector2 position,
            Vector2 size, IList<string> labels)
        {
            GameObject go = new GameObject(
                name, typeof(RectTransform), typeof(Image),
                typeof(TMP_Dropdown));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            TopLeft(rect, position, size);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.19f, 0.25f, 0.34f, 1f);
            TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();

            TMP_Text label = Text(rect, "Caption", "", 
                new Vector2(8f, -5f),
                new Vector2(size.x - 16f, size.y - 10f), 21f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText = label;

            GameObject templateGo = new GameObject(
                "Template", typeof(RectTransform),
                typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(rect, false);
            RectTransform template = templateGo.GetComponent<RectTransform>();
            template.anchorMin = Vector2.zero;
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = Vector2.zero;
            template.sizeDelta = new Vector2(0f, 260f);
            templateGo.GetComponent<Image>().color =
                new Color(0.11f, 0.16f, 0.23f, 1f);

            GameObject viewportGo = new GameObject(
                "Viewport", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(template, false);
            RectTransform viewport = viewportGo.GetComponent<RectTransform>();
            Stretch(viewport);
            Image maskGraphic = viewportGo.GetComponent<Image>();
            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = true;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentGo = new GameObject(
                "Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            RectTransform content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 45f);

            GameObject itemGo = new GameObject(
                "Item", typeof(RectTransform),
                typeof(Image), typeof(Toggle));
            itemGo.transform.SetParent(content, false);
            RectTransform item = itemGo.GetComponent<RectTransform>();
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.anchoredPosition = new Vector2(0f, -22.5f);
            item.sizeDelta = new Vector2(0f, 45f);

            Image itemImage = itemGo.GetComponent<Image>();
            itemImage.color = new Color(0.22f, 0.28f, 0.36f, 1f);
            Toggle toggle = itemGo.GetComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            toggle.isOn = true;
            TMP_Text itemLabel = Text(item, "Item Label", "",
                new Vector2(7f, -3f), 
                new Vector2(size.x - 15f, 39f), 20f);
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.itemText = itemLabel;

            ScrollRect scrolling = templateGo.GetComponent<ScrollRect>();
            scrolling.viewport = viewport;
            scrolling.content = content;
            scrolling.horizontal = false;
            scrolling.vertical = labels.Count > 5;
            scrolling.inertia = false;
            scrolling.movementType = ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 32f;

            dropdown.template = template;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(labels));
            dropdown.SetValueWithoutNotify(0);
            templateGo.SetActive(false);
            return dropdown;
        }

        public static TMP_Text Text(Transform parent, string name,
            string value, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            TopLeft(rect, position, size);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(14f, fontSize - 7f);
            label.fontSizeMax = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name,
            string caption, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            TopLeft(rect, position, size);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.23f, 0.34f, 0.42f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            TMP_Text label = Text(rect, "Label", caption,
                Vector2.zero, size, 22f);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        public static void TopLeft(RectTransform rect,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
