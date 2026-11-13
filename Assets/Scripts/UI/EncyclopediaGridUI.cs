using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>Only visible rows plus two buffer rows own UI instances.</summary>
    [DisallowMultipleComponent]
    public sealed class EncyclopediaGridUI : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private EncyclopediaCellUI cellPrefab;
        [SerializeField, Min(1)] private int columns = 6;
        [SerializeField] private Vector2 cellSize = new Vector2(136f, 164f);
        [SerializeField] private Vector2 spacing = new Vector2(12f, 14f);
        private readonly List<EncyclopediaCellUI> cells = new List<EncyclopediaCellUI>();
        private Action<EncyclopediaCellUI, int> bindCell;
        private int itemCount;
        private bool dirty;
        private int previousFirstRow = -1, previousNeeded = -1;
        private float previousWidth, previousHeight, previousDx, previousDy;

        private void Awake() { if (scroll != null) scroll.onValueChanged.AddListener(Scrolled); }
        private void OnDestroy() { if (scroll != null) scroll.onValueChanged.RemoveListener(Scrolled); }
        private void OnEnable() => dirty = true;
        private void OnRectTransformDimensionsChange() => dirty = true;
        private void LateUpdate() { if (dirty) { dirty = false; RefreshVisible(); } }
        private void Scrolled(Vector2 _) => Refresh(false);

        public void SetData(int count, Action<EncyclopediaCellUI, int> bind)
        {
            itemCount = Mathf.Max(0, count);
            bindCell = bind;
            if (scroll != null) scroll.StopMovement();
            if (content != null) content.anchoredPosition = Vector2.zero;
            RefreshVisible();
            dirty = true;
        }

        public void RefreshVisible() => Refresh(true);

        private void Refresh(bool force)
        {
            if (scroll == null || scroll.viewport == null || content == null || cellPrefab == null)
                return;
            int cols = Mathf.Max(1, columns);
            float width = Mathf.Max(1f, cellSize.x), height = Mathf.Max(1f, cellSize.y);
            float dx = width + Mathf.Max(0f, spacing.x), dy = height + Mathf.Max(0f, spacing.y);
            int rows = Mathf.CeilToInt((float)itemCount / cols);
            content.sizeDelta = new Vector2(0f, rows == 0 ? 0f : rows * dy - Mathf.Max(0f, spacing.y));
            int visibleRows = Mathf.Max(1, Mathf.CeilToInt(scroll.viewport.rect.height / dy)) + 2;
            int needed = Mathf.Min(itemCount, visibleRows * cols);
            int firstRow = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, content.anchoredPosition.y) / dy) - 1,
                0, Mathf.Max(0, rows - visibleRows));
            if (!force && firstRow == previousFirstRow && needed == previousNeeded &&
                scroll.viewport.rect.width == previousWidth && scroll.viewport.rect.height == previousHeight &&
                dx == previousDx && dy == previousDy) return;
            previousFirstRow = firstRow;
            previousNeeded = needed;
            previousWidth = scroll.viewport.rect.width;
            previousHeight = scroll.viewport.rect.height;
            previousDx = dx;
            previousDy = dy;
            while (cells.Count < needed)
                cells.Add(Instantiate(cellPrefab, content, false));

            float left = Mathf.Max(0f, (scroll.viewport.rect.width - (cols * dx - Mathf.Max(0f, spacing.x))) * .5f);
            for (int i = 0; i < cells.Count; i++)
            {
                EncyclopediaCellUI cell = cells[i];
                int index = firstRow * cols + i;
                bool visible = i < needed && index < itemCount;
                cell.gameObject.SetActive(visible);
                if (!visible) continue;
                cell.Rect.anchorMin = cell.Rect.anchorMax = new Vector2(0f, 1f);
                cell.Rect.pivot = new Vector2(0f, 1f);
                cell.Rect.sizeDelta = new Vector2(width, height);
                cell.Rect.anchoredPosition = new Vector2(left + (index % cols) * dx, -(index / cols) * dy);
                if (force || cell.Index != index) bindCell?.Invoke(cell, index);
            }
        }
    }
}
