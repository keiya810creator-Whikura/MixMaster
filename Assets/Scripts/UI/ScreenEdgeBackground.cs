using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Extends a decorative background from its safe-area frame to the screen
    /// edges. Its parent still positions the buttons and text in the safe area.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public sealed class ScreenEdgeBackground : MonoBehaviour
    {
        [SerializeField] private bool extendTop;
        [SerializeField] private bool extendBottom;
        [SerializeField] private bool extendLeft = true;
        [SerializeField] private bool extendRight = true;

        private RectTransform target;
        private bool applying;

        private void OnEnable()
        {
            Canvas.willRenderCanvases += Apply;
            Apply();
        }

        private void OnDisable() => Canvas.willRenderCanvases -= Apply;
        private void LateUpdate() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        public void Apply()
        {
            if (!Application.isPlaying || applying || !isActiveAndEnabled ||
                Screen.width <= 0 || Screen.height <= 0) return;

            applying = true;
            try
            {
                if (target == null) target = transform as RectTransform;
                RectTransform parent = target != null ? target.parent as RectTransform : null;
                Canvas canvas = GetComponentInParent<Canvas>();
                if (parent == null || canvas == null ||
                    canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return;

                // Apply the safe frame first, regardless of callback order.
                // Converting to this parent also compensates for UI scaling.
                SafeAreaLayout safeLayout = GetComponentInParent<SafeAreaLayout>();
                if (safeLayout != null) safeLayout.Apply();
                Canvas root = canvas.rootCanvas;
                Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out Vector2 min) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                        new Vector2(Screen.width, Screen.height), camera, out Vector2 max)) return;

                Rect frame = parent.rect;
                if (frame.width <= 0f || frame.height <= 0f || !Finite(min) || !Finite(max)) return;
                float left = extendLeft ? Mathf.Min(frame.xMin, Mathf.Min(min.x, max.x)) : frame.xMin;
                float right = extendRight ? Mathf.Max(frame.xMax, Mathf.Max(min.x, max.x)) : frame.xMax;
                float bottom = extendBottom ? Mathf.Min(frame.yMin, Mathf.Min(min.y, max.y)) : frame.yMin;
                float top = extendTop ? Mathf.Max(frame.yMax, Mathf.Max(min.y, max.y)) : frame.yMax;
                Vector2 size = new Vector2(right - left - frame.width, top - bottom - frame.height);
                Vector2 position = new Vector2(
                    (left + right - frame.xMin - frame.xMax) * 0.5f,
                    (bottom + top - frame.yMin - frame.yMax) * 0.5f);

                if (target.anchorMin != Vector2.zero) target.anchorMin = Vector2.zero;
                if (target.anchorMax != Vector2.one) target.anchorMax = Vector2.one;
                if (target.pivot != new Vector2(0.5f, 0.5f)) target.pivot = new Vector2(0.5f, 0.5f);
                if (target.sizeDelta != size) target.sizeDelta = size;
                if (target.anchoredPosition != position) target.anchoredPosition = position;
            }
            finally { applying = false; }
        }

        private static bool Finite(Vector2 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
