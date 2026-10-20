using UnityEngine;

namespace MixMaster.UI
{
    /// <summary>
    /// Keeps authored UI inside the visible screen. Put controls under this
    /// RectTransform and leave full-screen backdrops on its parent.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        [Tooltip("Minimum logical space for the authored UI. Shrinks uniformly on smaller screens. Set an axis to 0 to disable fitting that axis.")]
        [SerializeField] private Vector2 minimumContentSize = new Vector2(1080f, 1920f);

        private RectTransform target;
        private bool applying;

        private void OnEnable()
        {
            Canvas.willRenderCanvases += Apply;
            Apply();
        }

        private void OnDisable() => Canvas.willRenderCanvases -= Apply;
        private void LateUpdate() => Apply();
        private void OnTransformParentChanged() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        public void Apply()
        {
            if (!Application.isPlaying || applying || !isActiveAndEnabled || Screen.width <= 0 || Screen.height <= 0) return;
            if (target == null) target = transform as RectTransform;
            RectTransform parent = target != null ? target.parent as RectTransform : null;
            if (parent == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return;
            Canvas root = canvas.rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

            // Some devices report an empty safe area during startup. Keep the
            // last valid layout and try again before the next Canvas render.
            Rect safe = Screen.safeArea;
            if (!IsValidRect(safe)) return;
            safe = Rect.MinMaxRect(Mathf.Max(0f, safe.xMin), Mathf.Max(0f, safe.yMin),
                Mathf.Min(Screen.width, safe.xMax), Mathf.Min(Screen.height, safe.yMax));
            if (!IsValidRect(safe)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.min, camera, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safe.max, camera, out Vector2 max)) return;

            // Intersect in the actual parent's coordinates. A panel already
            // inside another safe-area container must not receive two insets.
            Rect localSafe = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            if (!TryCalculate(parent.rect, localSafe, minimumContentSize,
                out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 size, out float scale)) return;

            applying = true;
            try
            {
                if (target.pivot != new Vector2(0.5f, 0.5f)) target.pivot = new Vector2(0.5f, 0.5f);
                if (target.anchorMin != anchorMin) target.anchorMin = anchorMin;
                if (target.anchorMax != anchorMax) target.anchorMax = anchorMax;
                if (target.anchoredPosition != Vector2.zero) target.anchoredPosition = Vector2.zero;
                if (target.sizeDelta != size) target.sizeDelta = size;
                Vector3 desiredScale = new Vector3(scale, scale, 1f);
                if (target.localScale != desiredScale) target.localScale = desiredScale;
            }
            finally { applying = false; }
        }

        /// <summary>
        /// Computes an inset frame while preserving the authored reference
        /// space. Expanding its logical rect compensates for the uniform
        /// scale, so edge-anchored controls still reach the safe frame edges.
        /// </summary>
        public static bool TryCalculate(Rect parent, Rect safe, Vector2 minimum,
            out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 sizeDelta, out float scale)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;
            sizeDelta = Vector2.zero;
            scale = 1f;
            if (!IsValidRect(parent) || !IsValidRect(safe) ||
                !Finite(minimum.x) || !Finite(minimum.y)) return false;
            float left = Mathf.Max(parent.xMin, safe.xMin), bottom = Mathf.Max(parent.yMin, safe.yMin);
            float right = Mathf.Min(parent.xMax, safe.xMax), top = Mathf.Min(parent.yMax, safe.yMax);
            float width = right - left, height = top - bottom;
            if (width <= 0f || height <= 0f) return false;
            if (minimum.x > 0f) scale = Mathf.Min(scale, width / minimum.x);
            if (minimum.y > 0f) scale = Mathf.Min(scale, height / minimum.y);
            if (scale <= 0f || !Finite(scale)) return false;
            anchorMin = new Vector2((left - parent.xMin) / parent.width, (bottom - parent.yMin) / parent.height);
            anchorMax = new Vector2((right - parent.xMin) / parent.width, (top - parent.yMin) / parent.height);
            sizeDelta = new Vector2(Mathf.Max(0f, width / scale - width), Mathf.Max(0f, height / scale - height));
            return Finite(sizeDelta.x) && Finite(sizeDelta.y);
        }

        private static bool IsValidRect(Rect rect) => Finite(rect.x) && Finite(rect.y) &&
            Finite(rect.width) && Finite(rect.height) && Finite(rect.xMax) && Finite(rect.yMax) && rect.width > 0f && rect.height > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // Existing inventory UI creates a few controls lazily and searches
        // authored children by path. Keep both old and wrapped panels usable.
        public static Transform ContentRoot(Transform parent)
        {
            if (parent == null) return null;
            Transform content = parent.Find("SafeArea");
            return content != null && content.GetComponent<SafeAreaLayout>() != null ? content : parent;
        }

        /// <summary>For the existing code-built logs and equipment detail.</summary>
        public static RectTransform CreateContent(Transform parent)
        {
            GameObject content = new GameObject("SafeArea", typeof(RectTransform));
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            content.AddComponent<SafeAreaLayout>();
            return rect;
        }
    }
}
