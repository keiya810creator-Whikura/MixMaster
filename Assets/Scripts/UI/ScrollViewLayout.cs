using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Includes the final row and a small inset in the scrollable content bounds.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ScrollRect))]
    public sealed class ScrollViewLayout : MonoBehaviour
    {
        public const float DefaultEndPadding = 12f;
        [SerializeField, Min(0f)] private float endPadding = DefaultEndPadding;
        [Tooltip("Disable for virtual grids or content whose height is managed by its screen.")]
        [SerializeField] private bool measureFixedRows = true;
        private readonly Vector3[] corners = new Vector3[4];

        private void Awake() => Apply();
        private void OnEnable() => Apply();

        public static void Prepare(ScrollRect scroll)
        {
            if (scroll == null) return;
            var layout = scroll.GetComponent<ScrollViewLayout>();
            if (layout == null) layout = scroll.gameObject.AddComponent<ScrollViewLayout>();
            layout.Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
            RepairLoadedScenes();
        }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode) => RepairLoadedScenes();
        private static void RepairLoadedScenes()
        {
            // Also repair unpacked copies in older scenes. Prefab instances carry this component themselves.
            foreach (var scroll in FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Prepare(scroll);
        }

        public bool Apply()
        {
            var scroll = GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null) return false;
            var dropdown = scroll.GetComponentInParent<TMP_Dropdown>(true);
            if (dropdown != null && dropdown.template == scroll.transform)
                return PrepareDropdown(dropdown, scroll, Mathf.Max(0f, endPadding));

            float padding = Mathf.Max(0f, endPadding);
            var group = scroll.content.GetComponent<LayoutGroup>();
            if (group != null)
            {
                int bottom = Mathf.CeilToInt(padding);
                if (group.padding.bottom >= bottom) return false;
                group.padding.bottom = bottom;
                LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
                return true;
            }
            var text = scroll.content.GetComponent<TMP_Text>();
            if (text != null)
            {
                Vector4 margin = text.margin;
                if (margin.w >= padding) return false;
                margin.w = padding;
                text.margin = margin;
                return true;
            }
            // Virtual grids compute the height for all rows, including rows outside their cell pool.
            // Goal HUD and reincarnation details also manage their content height explicitly.
            if (!measureFixedRows || scroll.content.GetComponent<EncyclopediaGridUI>() != null ||
                scroll.GetComponentInParent<EncyclopediaGridUI>(true) != null ||
                scroll.GetComponentInParent<GoalTrackerHUD>(true) != null ||
                scroll.content.GetComponent<ContentSizeFitter>() != null) return false;

            float bottomEdge = scroll.content.rect.yMax;
            bool hasRow = false;
            foreach (Transform child in scroll.content)
            {
                if (!(child is RectTransform row) || !row.gameObject.activeSelf ||
                    row.anchorMin.y != 1f || row.anchorMax.y != 1f) continue;
                var element = row.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                row.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                    bottomEdge = Mathf.Min(bottomEdge, scroll.content.InverseTransformPoint(corners[i]).y);
                hasRow = true;
            }
            if (!hasRow) return false;
            float height = scroll.content.rect.yMax - bottomEdge + padding;
            if (height <= scroll.content.rect.height + .01f) return false;
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return true;
        }

        private static bool PrepareDropdown(TMP_Dropdown dropdown, ScrollRect scroll, float padding)
        {
            var toggle = dropdown.template.GetComponentInChildren<Toggle>(true);
            var item = toggle != null ? toggle.transform as RectTransform : null;
            var content = item != null ? item.parent as RectTransform : null;
            if (item == null || content == null) return false;
            float height = Mathf.Max(1f, item.rect.height);
            bool changed = false;
            // TMP positions generated options from the content bottom using the template's edge insets.
            // A top pivot with a half-row offset produces a negative bottom inset and clips the last row.
            if (content.anchorMin.y != 1f) { content.anchorMin = new Vector2(content.anchorMin.x, 1f); changed = true; }
            if (content.anchorMax.y != 1f) { content.anchorMax = new Vector2(content.anchorMax.x, 1f); changed = true; }
            if (content.pivot.y != 1f) { content.pivot = new Vector2(content.pivot.x, 1f); changed = true; }
            if (!Mathf.Approximately(content.sizeDelta.y, height + padding * 2f))
            { content.sizeDelta = new Vector2(content.sizeDelta.x, height + padding * 2f); changed = true; }
            if (item.anchorMin.y != 1f) { item.anchorMin = new Vector2(item.anchorMin.x, 1f); changed = true; }
            if (item.anchorMax.y != 1f) { item.anchorMax = new Vector2(item.anchorMax.x, 1f); changed = true; }
            if (item.pivot.y != .5f) { item.pivot = new Vector2(item.pivot.x, .5f); changed = true; }
            float y = -padding - height * .5f;
            if (!Mathf.Approximately(item.anchoredPosition.y, y))
            { item.anchoredPosition = new Vector2(item.anchoredPosition.x, y); changed = true; }
            if (scroll.content != content) { scroll.content = content; changed = true; }
            if (!scroll.vertical) { scroll.vertical = true; changed = true; }
            return changed;
        }
    }
}
