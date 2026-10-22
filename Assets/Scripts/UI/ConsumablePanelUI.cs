using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class ConsumablePanelUI : MonoBehaviour
    {
        [SerializeField] private bool shopMode;
        [SerializeField] private Transform rowsRoot;
        [SerializeField] private ConsumableItemRowUI rowPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text goldText, statusText, selectedText;
        [SerializeField] private TMP_Dropdown purchaseQuantityDropdown;
        [SerializeField] private Button[] shortcutAssignmentButtons = new Button[5];
        [SerializeField] private TMP_Text[] shortcutAssignmentTexts = new TMP_Text[5];
        [SerializeField] private ConsumableAutoUseControls hpAutoUse, mpAutoUse;
        private readonly List<ConsumableItemRowUI> rows = new List<ConsumableItemRowUI>();
        private readonly List<UnityAction> assignmentActions = new List<UnityAction>();
        private ConsumableManager manager;
        private GameManager game;
        private string selectedItemId;
        public bool IsShop => shopMode;
        public event Action Closed;
        public static ConsumablePanelUI Find(bool shop)
        {
            foreach (var panel in FindObjectsByType<ConsumablePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.shopMode == shop && panel.gameObject.scene.IsValid()) return panel;
            return null;
        }
        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (purchaseQuantityDropdown != null)
            {
                purchaseQuantityDropdown.ClearOptions();
                purchaseQuantityDropdown.AddOptions(new List<string> { "1個ずつ購入", "10個ずつ購入", "100個ずつ購入" });
                purchaseQuantityDropdown.onValueChanged.AddListener(QuantityChanged);
            }
            for (int i = 0; i < shortcutAssignmentButtons.Length; i++)
            {
                int slot = i; UnityAction action = () => Assign(slot); assignmentActions.Add(action);
                if (shortcutAssignmentButtons[i] != null) shortcutAssignmentButtons[i].onClick.AddListener(action);
            }
        }
        private void OnEnable() { Connect(); Refresh(); }
        private void Update() { if (manager == null) { Connect(); Refresh(); } }
        private void OnDisable()
        {
            if (manager != null) manager.Changed -= Refresh;
            if (game != null) game.GoldChanged -= GoldChanged;
            manager = null; game = null; Closed?.Invoke();
        }
        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (purchaseQuantityDropdown != null) purchaseQuantityDropdown.onValueChanged.RemoveListener(QuantityChanged);
            for (int i = 0; i < assignmentActions.Count; i++)
                if (shortcutAssignmentButtons[i] != null) shortcutAssignmentButtons[i].onClick.RemoveListener(assignmentActions[i]);
        }
        private void Connect()
        {
            if (manager == null)
            {
                manager = ConsumableManager.Instance;
                if (manager != null) manager.Changed += Refresh;
            }
            if (game == null) { game = GameManager.Instance; if (game != null) game.GoldChanged += GoldChanged; }
        }
        public void Show() { gameObject.SetActive(true); transform.SetAsLastSibling(); Connect(); Refresh(); }
        public void Close() => gameObject.SetActive(false);
        private int PurchaseQuantity => purchaseQuantityDropdown == null || purchaseQuantityDropdown.value == 0
            ? 1 : purchaseQuantityDropdown.value == 1 ? 10 : 100;
        private void QuantityChanged(int value) => Refresh();
        private void GoldChanged(long value) => Refresh();
        public void Refresh()
        {
            if (goldText != null) goldText.text = "所持ゴールド " + (game != null ? game.Gold : 0L).ToString("N0") + " G";
            if (manager == null || manager.Catalog == null || manager.Catalog.items == null || rowsRoot == null || rowPrefab == null) return;
            int index = 0;
            foreach (ConsumableDefinition item in manager.Catalog.items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                if (rows.Count <= index) rows.Add(Instantiate(rowPrefab, rowsRoot));
                var row = rows[index++]; row.gameObject.SetActive(true);
                string id = item.itemId; int amount = PurchaseQuantity;
                bool canBuy = game != null && item.purchasePrice >= 0 && item.purchasePrice <= long.MaxValue / amount &&
                    game.Gold >= item.purchasePrice * amount && manager.Quantity(id) <= long.MaxValue - amount;
                row.Bind(item, manager.Quantity(id), shopMode, amount, canBuy,
                    () => Act(id), () => Select(id));
            }
            for (int i = index; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            if (hpAutoUse != null) hpAutoUse.Bind(manager);
            if (mpAutoUse != null) mpAutoUse.Bind(manager);
            ConsumableDefinition selected = manager.Catalog.Get(selectedItemId);
            if (selectedText != null) selectedText.text = selected == null ? "アイテムの「登録」を押して枠を選択" : "登録するアイテム：" + selected.displayName + "（同じ枠で解除）";
            for (int i = 0; i < shortcutAssignmentTexts.Length && i < ConsumableManager.ShortcutCount; i++)
            {
                ConsumableDefinition assigned = manager.Catalog.Get(manager.Shortcut(i));
                if (shortcutAssignmentTexts[i] != null) shortcutAssignmentTexts[i].text = (i + 1) + "：" + (assigned != null ? assigned.displayName : "未登録");
            }
        }
        private void Act(string id)
        {
            if (manager == null) return;
            string message;
            if (shopMode) manager.TryPurchase(id, PurchaseQuantity, out message);
            else manager.TryUse(id, out message);
            if (statusText != null) statusText.text = message;
            Refresh();
        }
        private void Select(string id) { selectedItemId = id; Refresh(); }
        private void Assign(int slot)
        {
            if (manager == null || string.IsNullOrEmpty(selectedItemId)) return;
            manager.SetShortcut(slot, manager.Shortcut(slot) == selectedItemId ? null : selectedItemId);
        }
    }
}
