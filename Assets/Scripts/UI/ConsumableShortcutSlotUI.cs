using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class ConsumableShortcutSlotUI : MonoBehaviour
    {
        [SerializeField, Range(0, 4)] private int slotIndex;
        [SerializeField] private Button useButton;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text quantityText, nameText;
        private ConsumableManager manager;
        private int lastCooldown = -1;
        private void Awake() { if (useButton != null) useButton.onClick.AddListener(Use); }
        private void OnEnable() { Connect(); Refresh(); }
        private void Update()
        {
            if (manager == null) { Connect(); Refresh(); return; }
            int seconds = Mathf.CeilToInt(manager.CooldownRemaining(manager.Shortcut(slotIndex)));
            if (seconds != lastCooldown) Refresh();
        }
        private void OnDisable() { if (manager != null) manager.Changed -= Refresh; manager = null; }
        private void OnDestroy() { if (useButton != null) useButton.onClick.RemoveListener(Use); }
        private void Connect() { manager = ConsumableManager.Instance; if (manager != null) manager.Changed += Refresh; }
        private void Use() { if (manager != null) manager.TryUse(manager.Shortcut(slotIndex), out _); }
        public void Refresh()
        {
            string id = manager != null ? manager.Shortcut(slotIndex) : null;
            ConsumableDefinition item = manager != null && manager.Catalog != null ? manager.Catalog.Get(id) : null;
            long count = manager != null ? manager.Quantity(id) : 0;
            float cooldown = manager != null ? manager.CooldownRemaining(id) : 0f;
            lastCooldown = Mathf.CeilToInt(cooldown);
            if (icon != null) { icon.sprite = item != null ? item.icon : null; icon.enabled = icon.sprite != null; }
            if (quantityText != null) quantityText.text = item != null ? count.ToString("N0") : "";
            if (nameText != null) nameText.text = item == null ? "未登録" : item.displayName.Replace("回復ポーション", "") + (lastCooldown > 0 ? "\nあと" + lastCooldown + "秒" : "");
            if (useButton != null) useButton.interactable = item != null && count > 0 && cooldown <= 0f;
        }
    }
}
