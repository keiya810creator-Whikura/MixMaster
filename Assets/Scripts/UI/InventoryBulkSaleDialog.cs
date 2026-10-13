using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Shared confirmation overlay for bulk sales. It never sells anything
    /// on its own: the caller supplies the verified snapshot/action.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryBulkSaleDialog : MonoBehaviour
    {
        private TMP_Text message;
        private Action onConfirmed;
        private Button confirmButton;

        public static InventoryBulkSaleDialog Ensure(Transform parent)
        {
            InventoryBulkSaleDialog dialog =
                parent.GetComponentInChildren<InventoryBulkSaleDialog>(true);
            if (dialog != null) return dialog;

            GameObject backdrop = new GameObject(
                "BulkSaleConfirmation", typeof(RectTransform),
                typeof(Image), typeof(InventoryBulkSaleDialog));
            backdrop.transform.SetParent(parent, false);
            RectTransform overlay = backdrop.GetComponent<RectTransform>();
            InventoryUiFactory.Stretch(overlay);
            Image overlayBg = backdrop.GetComponent<Image>();
            overlayBg.color = new Color(0.01f, 0.02f, 0.04f, 0.97f);
            overlayBg.raycastTarget = true;

            GameObject card = new GameObject(
                "ConfirmBox", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(backdrop.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.07f, 0.30f);
            cardRect.anchorMax = new Vector2(0.93f, 0.72f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color =
                new Color(0.105f, 0.17f, 0.25f, 1f);

            InventoryBulkSaleDialog view =
                backdrop.GetComponent<InventoryBulkSaleDialog>();
            view.message = InventoryUiFactory.Text(
                card.transform, "Message", "",
                new Vector2(24f, -28f), new Vector2(800f, 230f), 30f);
            view.message.enableWordWrapping = true;

            Button cancel = InventoryUiFactory.Button(
                card.transform, "Cancel", "キャンセル",
                new Vector2(27f, -278f), new Vector2(315f, 72f));
            view.confirmButton = InventoryUiFactory.Button(
                card.transform, "Confirm", "まとめて売却",
                new Vector2(388f, -278f), new Vector2(315f, 72f));
            view.confirmButton.GetComponent<Image>().color =
                new Color(0.53f, 0.23f, 0.20f, 1f);
            cancel.onClick.AddListener(view.Close);
            view.confirmButton.onClick.AddListener(view.Confirm);
            backdrop.SetActive(false);
            return view;
        }

        public void Show(string messageText, Action action)
        {
            onConfirmed = action;
            if (message != null) message.text = messageText;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Confirm()
        {
            Action callback = onConfirmed;
            onConfirmed = null;
            gameObject.SetActive(false);
            callback?.Invoke();
        }

        public void Close()
        {
            onConfirmed = null;
            gameObject.SetActive(false);
        }
    }
}
