using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaLinkUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private Image icon;
        private Action selected;
        private void Awake() { if (button != null) button.onClick.AddListener(Select); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Select); }
        private void Select() => selected?.Invoke();
        public void Bind(string name, Sprite sprite, Action onSelected)
        {
            selected = onSelected;
            if (caption != null) caption.text = name + "  ›";
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            gameObject.SetActive(true);
        }
        public void Clear() { selected = null; gameObject.SetActive(false); }
    }
}
