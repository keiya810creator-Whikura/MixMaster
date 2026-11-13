using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class EquipmentInventoryButton : MonoBehaviour
    {
        [Tooltip("空欄なら配置済み画面を検索し、なければ専用Prefabから開きます。")]
        [SerializeField] private EquipmentInventoryUI inventory;
        private Button button;

        private void Awake() { button = GetComponent<Button>(); if (button != null) button.onClick.AddListener(Open); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Open); }

        public void Open()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace)
            { Debug.LogWarning("[EquipmentInventory] ボタンを画面UIのCanvas内へ配置してください。", this); return; }
            if (inventory != null) inventory.Open();
            else inventory = EquipmentInventoryUI.OpenDefault(canvas.rootCanvas.transform);
        }
    }
}
