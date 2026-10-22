using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class ConsumableShortcutsHUD : MonoBehaviour
    {
        [SerializeField] private Button itemButton;
        [SerializeField] private ConsumablePanelUI inventoryPanel;
        [SerializeField] private TMP_Text lureTimeText;
        private int lastSeconds = -1;
        private void Awake() { if (itemButton != null) itemButton.onClick.AddListener(OpenInventory); }
        private void OnDestroy() { if (itemButton != null) itemButton.onClick.RemoveListener(OpenInventory); }
        private void Update()
        {
            var items = ConsumableManager.Instance;
            int seconds = items != null ? Mathf.CeilToInt(items.LureSecondsRemaining) : 0;
            if (seconds == lastSeconds) return;
            lastSeconds = seconds;
            if (lureTimeText != null) lureTimeText.text = seconds > 0 ? "鈴の効果 あと" + seconds + "秒" : "";
        }
        private void OpenInventory()
        {
            if (inventoryPanel == null) inventoryPanel = ConsumablePanelUI.Find(false);
            if (inventoryPanel == null) return;
            if (inventoryPanel.gameObject.activeSelf) inventoryPanel.Close();
            else
            {
                if (MapBuildingUIRouter.Instance != null) MapBuildingUIRouter.Instance.CloseAll();
                inventoryPanel.Show();
            }
        }
    }
}
