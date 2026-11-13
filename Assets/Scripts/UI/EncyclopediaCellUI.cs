using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaCellUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image unknownOverlay;
        [SerializeField] private TMP_Text caption;
        private TMP_Text acquiredBadge;
        private Action<int> selected;
        public int Index { get; private set; } = -1;
        public RectTransform Rect => (RectTransform)transform;

        private void Awake() { if (button != null) button.onClick.AddListener(Select); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Select); }
        private void Select() { if (button != null && button.interactable) selected?.Invoke(Index); }

        public void Bind(int index, Sprite sprite, string name, bool locked, Action<int> onSelected, bool acquired = false)
        {
            Index = index;
            selected = onSelected;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (caption != null) caption.text = locked ? "？？？" : name;
            if (unknownOverlay != null) unknownOverlay.gameObject.SetActive(locked);
            if (button != null) button.interactable = !locked;
            if (acquired && acquiredBadge == null)
            {
                acquiredBadge = InventoryUiFactory.Text(transform, "AcquiredBadge", "ゲット済み",
                    Vector2.zero, Vector2.zero, 18f);
                RectTransform rect = acquiredBadge.rectTransform;
                rect.anchorMin = new Vector2(.04f, .72f);
                rect.anchorMax = new Vector2(.96f, .94f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                acquiredBadge.alignment = TextAlignmentOptions.Center;
                acquiredBadge.color = new Color(1f, .89f, .45f);
                if (caption != null && caption.font != null) acquiredBadge.font = caption.font;
                var background = new GameObject("BadgeBackground", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(transform, false);
                background.transform.SetSiblingIndex(rect.GetSiblingIndex());
                RectTransform backgroundRect = (RectTransform)background.transform;
                backgroundRect.anchorMin = rect.anchorMin;
                backgroundRect.anchorMax = rect.anchorMax;
                backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
                Image image = background.GetComponent<Image>();
                image.color = new Color(.13f, .23f, .17f, .96f);
                image.raycastTarget = false;
                // Keep the background and label on the same visibility switch.
                rect.SetParent(backgroundRect, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            if (acquiredBadge != null) acquiredBadge.transform.parent.gameObject.SetActive(acquired);
        }
    }
}
