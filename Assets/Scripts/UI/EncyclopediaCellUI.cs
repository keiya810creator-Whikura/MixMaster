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
        private Action<int> selected;
        public int Index { get; private set; } = -1;
        public RectTransform Rect => (RectTransform)transform;

        private void Awake() { if (button != null) button.onClick.AddListener(Select); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Select); }
        private void Select() { if (button != null && button.interactable) selected?.Invoke(Index); }

        public void Bind(int index, Sprite sprite, string name, bool locked, Action<int> onSelected)
        {
            Index = index;
            selected = onSelected;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (caption != null) caption.text = locked ? "？？？" : name;
            if (unknownOverlay != null) unknownOverlay.gameObject.SetActive(locked);
            if (button != null) button.interactable = !locked;
        }
    }
}
