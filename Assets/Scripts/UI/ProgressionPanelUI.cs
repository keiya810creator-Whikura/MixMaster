using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Small paged overlay shared by collection and party management tools.</summary>
    public sealed class ProgressionPanelUI : MonoBehaviour
    {
        internal sealed class Item
        {
            public string text;
            public Action click;
            public Item(string text, Action click = null) { this.text = text; this.click = click; }
        }

        private const int PageSize = 6;
        [SerializeField] private TMP_Text title, status, pageText, body;
        [SerializeField] private Button previous, next, primary, secondary, closeButton;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_InputField numberInput;
        [SerializeField] private List<Button> rows = new List<Button>();
        private bool bound;
        private List<Item> items;
        private int page;

        internal static ProgressionPanelUI Create(Transform parent, string name)
        {
            foreach (var existing in parent.GetComponentsInChildren<ProgressionPanelUI>(true))
                if (existing.name == name) { existing.BindView(); return existing; }
            var prefab = Resources.Load<ProgressionPanelUI>("UI/ScenePanels/ProgressionPanel");
            if (prefab != null)
            {
                var view = Instantiate(prefab, parent, false);
                view.name = name;
                view.BindView();
                return view;
            }
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ProgressionPanelUI));
            root.transform.SetParent(parent, false);
            InventoryUiFactory.Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(.02f, .035f, .055f, .98f);
            var panel = root.GetComponent<ProgressionPanelUI>();
            panel.Build();
            root.SetActive(false);
            return panel;
        }

        private void Awake() => BindView();

        private void BindView()
        {
            if (bound || closeButton == null) return;
            bound = true;
            closeButton.onClick.AddListener(Close);
            previous.onClick.AddListener(() => { page--; Render(); });
            next.onClick.AddListener(() => { page++; Render(); });
            for (int i = 0; i < rows.Count; i++)
            {
                int row = i;
                rows[i].onClick.AddListener(() =>
                {
                    int index = page * PageSize + row;
                    if (items != null && index < items.Count) items[index].click?.Invoke();
                });
            }
        }

        private void Build()
        {
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(transform, false);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(980, 1480);
            title = InventoryUiFactory.Text(rect, "Title", "", new Vector2(24, -20), new Vector2(740, 70), 34);
            Button(rect, "Close", "閉じる", 760, 20, 190, 64, Close);
            status = InventoryUiFactory.Text(rect, "Status", "", new Vector2(24, -105), new Vector2(928, 100), 24);
            status.richText = false;
            for (int i = 0; i < PageSize; i++)
            {
                int row = i;
                var button = Button(rect, "Row" + i, "", 24, 220 + i * 160, 928, 150, () =>
                {
                    int index = page * PageSize + row;
                    if (items != null && index < items.Count) items[index].click?.Invoke();
                });
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.richText = false;
                label.margin = new Vector4(14, 6, 14, 6);
                rows.Add(button);
            }
            previous = Button(rect, "Previous", "前へ", 24, 1200, 200, 65, () => { page--; Render(); });
            next = Button(rect, "Next", "次へ", 752, 1200, 200, 65, () => { page++; Render(); });
            pageText = InventoryUiFactory.Text(rect, "Page", "", new Vector2(250, -1200), new Vector2(480, 65), 25);
            pageText.alignment = TextAlignmentOptions.Center;
            primary = Button(rect, "Primary", "", 24, 1305, 450, 80, null);
            secondary = Button(rect, "Secondary", "", 502, 1305, 450, 80, null);

            var view = new GameObject("TextScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            view.transform.SetParent(rect, false);
            InventoryUiFactory.TopLeft((RectTransform)view.transform, new Vector2(24, -220), new Vector2(928, 950));
            view.GetComponent<Image>().color = new Color(.07f, .11f, .17f);
            scroll = view.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)view.transform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            body = InventoryUiFactory.Text(view.transform, "Content", "", Vector2.zero, Vector2.zero, 27);
            body.rectTransform.anchorMin = new Vector2(0, 1);
            body.rectTransform.anchorMax = new Vector2(1, 1);
            body.rectTransform.pivot = new Vector2(0, 1);
            body.enableAutoSizing = false;
            body.richText = false;
            body.margin = new Vector4(14, 14, 14, 14);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = body.rectTransform;
            ScrollViewLayout.Prepare(scroll);
            TMP_Text source = transform.parent.GetComponentInChildren<TMP_Text>(true);
            if (source != null && source.font != null)
                foreach (var label in GetComponentsInChildren<TMP_Text>(true)) label.font = source.font;
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = transform.parent.gameObject.layer;
        }

        private static Button Button(Transform parent, string name, string caption,
            float x, float y, float width, float height, Action action)
        {
            var button = InventoryUiFactory.Button(parent, name, caption, new Vector2(x, -y), new Vector2(width, height));
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }

        internal void Show(string heading, List<Item> entries, string note = "",
            string primaryLabel = null, Action primaryAction = null,
            string secondaryLabel = null, Action secondaryAction = null, bool keepPage = false)
        {
            BindView();
            if (numberInput != null) numberInput.gameObject.SetActive(false);
            title.text = heading;
            status.text = note;
            items = entries;
            if (!keepPage) page = 0;
            Configure(primary, primaryLabel, primaryAction);
            Configure(secondary, secondaryLabel, secondaryAction);
            scroll.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Render();
        }

        internal void ShowText(string heading, string text, string primaryLabel = null, Action primaryAction = null)
        {
            Show(heading, new List<Item>(), "", primaryLabel, primaryAction);
            foreach (var row in rows) row.gameObject.SetActive(false);
            previous.gameObject.SetActive(false);
            next.gameObject.SetActive(false);
            pageText.gameObject.SetActive(false);
            scroll.gameObject.SetActive(true);
            body.text = text;
            scroll.StopMovement();
            body.rectTransform.anchoredPosition = Vector2.zero;
        }

        internal void ShowNumber(string heading, int current, int minimum, int maximum, Action<int> save)
        {
            ShowText(heading, $"目標レベルを {minimum}～{maximum} で入力してください。");
            if (numberInput == null)
            {
                var input = new GameObject("TargetLevel", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
                input.transform.SetParent(transform, false);
                var rect = (RectTransform)input.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(640, 100);
                input.GetComponent<Image>().color = new Color(.17f, .24f, .33f);
                var text = InventoryUiFactory.Text(input.transform, "Value", "", Vector2.zero, Vector2.zero, 34);
                InventoryUiFactory.Stretch(text.rectTransform);
                text.alignment = TextAlignmentOptions.Center;
                text.font = body.font;
                numberInput = input.GetComponent<TMP_InputField>();
                numberInput.textViewport = rect;
                numberInput.textComponent = text;
                numberInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                numberInput.characterLimit = 6;
            }
            numberInput.gameObject.SetActive(true);
            numberInput.text = current.ToString();
            Configure(primary, "保存", () =>
            {
                if (!int.TryParse(numberInput.text, out int level) || level < minimum || level > maximum)
                { status.text = $"レベルは {minimum}～{maximum} で入力してください"; return; }
                save(level);
                Close();
            });
        }

        private static void Configure(Button button, string label, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(!string.IsNullOrEmpty(label));
            button.GetComponentInChildren<TMP_Text>(true).text = label ?? "";
            button.interactable = action != null;
            if (action != null) button.onClick.AddListener(() => action());
        }

        private void Render()
        {
            if (items == null) return;
            page = Mathf.Clamp(page, 0, Mathf.Max(0, (items.Count - 1) / PageSize));
            for (int i = 0; i < rows.Count; i++)
            {
                int index = page * PageSize + i;
                rows[i].gameObject.SetActive(index < items.Count);
                if (index >= items.Count) continue;
                rows[i].GetComponentInChildren<TMP_Text>(true).text = items[index].text;
                rows[i].interactable = items[index].click != null;
            }
            previous.gameObject.SetActive(true);
            next.gameObject.SetActive(true);
            pageText.gameObject.SetActive(true);
            previous.interactable = page > 0;
            next.interactable = (page + 1) * PageSize < items.Count;
            pageText.text = items.Count == 0 ? "該当する項目はありません" :
                $"{page + 1} / {(items.Count + PageSize - 1) / PageSize}";
        }

        internal void Close()
        {
            gameObject.SetActive(false);
        }

    }
}
