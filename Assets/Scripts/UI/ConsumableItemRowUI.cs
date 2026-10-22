using System;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class ConsumableItemRowUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText, descriptionText, quantityText, priceText, actionText;
        [SerializeField] private Button actionButton, shortcutButton;
        private Action action, select;
        private void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(Act);
            if (shortcutButton != null) shortcutButton.onClick.AddListener(Select);
        }
        private void OnDestroy()
        {
            if (actionButton != null) actionButton.onClick.RemoveListener(Act);
            if (shortcutButton != null) shortcutButton.onClick.RemoveListener(Select);
        }
        private void Act() => action?.Invoke();
        private void Select() => select?.Invoke();
        public void Bind(ConsumableDefinition item, long owned, bool shop, int amount, bool canBuy, Action onAction, Action onSelect)
        {
            action = onAction; select = onSelect;
            if (icon != null) { icon.sprite = item.icon; icon.enabled = item.icon != null; }
            if (nameText != null) nameText.text = item.displayName;
            if (descriptionText != null) descriptionText.text = item.Description;
            if (quantityText != null) quantityText.text = "所持 " + owned.ToString("N0") + "個";
            if (priceText != null) priceText.text = shop
                ? item.purchasePrice < 0 ? "価格未設定" : item.purchasePrice.ToString("N0") + " G / 個" : "";
            if (actionText != null) actionText.text = shop ? amount + "個購入" : "使用";
            if (actionButton != null) actionButton.interactable = shop ? canBuy : owned > 0;
            if (shortcutButton != null) shortcutButton.gameObject.SetActive(!shop);
        }
    }
}
