using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Editable row shared by offline and dungeon reward lists.</summary>
    [DisallowMultipleComponent]
    public sealed class RewardItemRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Button inspectButton;
        private Action inspect;

        private void Awake()
        {
            if (inspectButton != null) inspectButton.onClick.AddListener(Inspect);
        }
        private void OnDestroy()
        {
            if (inspectButton != null) inspectButton.onClick.RemoveListener(Inspect);
        }
        private void Inspect() => inspect?.Invoke();

        public void SetData(Sprite sprite, string label, string amount, Action onInspect = null)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (nameText != null) nameText.text = label;
            if (amountText != null) amountText.text = amount;
            inspect = onInspect;
            if (inspectButton != null) inspectButton.interactable = inspect != null;
        }
    }
}
