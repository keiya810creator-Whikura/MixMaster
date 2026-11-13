using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Add to the user's Button. No extra HUD button is generated.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class EncyclopediaButton : MonoBehaviour
    {
        private Button button;
        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Open);
        }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Open); }
        public void Open()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace)
            {
                Debug.LogWarning("[Encyclopedia] 図鑑ボタンを画面UIのCanvas内へ配置してください。", this);
                return;
            }
            EncyclopediaMenuUI.OpenDefault(canvas.rootCanvas.transform);
        }
    }
}
