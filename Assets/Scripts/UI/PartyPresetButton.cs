using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PartyPresetButton : MonoBehaviour
    {
        private Button button;
        private void Awake() { button = GetComponent<Button>(); if (button != null) button.onClick.AddListener(Open); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Open); }
        public void Open()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace)
            { Debug.LogWarning("[PartyPresets] ボタンを画面UIのCanvas内へ配置してください。", this); return; }
            PartyPresetScreenUI.OpenDefault(canvas.rootCanvas.transform);
        }
    }
}
