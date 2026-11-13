using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class MaterialInventoryPanelUI : MonoBehaviour
    {
        public const string ResourcePath = "UI/MaterialInventory/MaterialInventoryPanel";
        [SerializeField] private EncyclopediaGridUI grid;
        [SerializeField] private GameObject listRoot, detailRoot;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backLabel, countText, emptyText;
        [SerializeField] private ScrollRect detailScroll;
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailName, amountText, sourcesText;
        private MaterialInventoryManager inventory;
        private MaterialInventoryContent definitions;
        private List<MaterialInventoryContent.Entry> items = new List<MaterialInventoryContent.Entry>();
        private string selectedId;
        private bool opened, dirty;
        private float retryAt;

        private void Awake() { if (backButton != null) backButton.onClick.AddListener(Back); }
        private void Start() { if (!opened) gameObject.SetActive(false); }
        private void OnDestroy() { if (backButton != null) backButton.onClick.RemoveListener(Back); }
        private void OnDisable() { Unsubscribe(); opened = false; }
        private void LateUpdate()
        {
            if (!opened) return;
            if (inventory == null && Time.unscaledTime >= retryAt) { retryAt = Time.unscaledTime + .5f; BindInventory(); dirty = true; }
            if (dirty) { dirty = false; Refresh(false); }
        }

        public static MaterialInventoryPanelUI OpenDefault(Transform parent)
        {
            if (parent == null) return null;
            var screen = parent.GetComponentInChildren<MaterialInventoryPanelUI>(true);
            if (screen == null) screen = FindFirstObjectByType<MaterialInventoryPanelUI>(FindObjectsInactive.Include);
            if (screen == null)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null) { Debug.LogWarning("[MaterialInventory] 素材インベントリPrefabが見つかりません。"); return null; }
                screen = Instantiate(prefab, parent, false).GetComponent<MaterialInventoryPanelUI>();
                if (screen != null) InventoryUiFactory.Stretch((RectTransform)screen.transform);
            }
            if (screen != null) screen.Open();
            return screen;
        }

        public void Open()
        {
            gameObject.SetActive(true); transform.SetAsLastSibling(); opened = true;
            BindInventory();
            definitions = new MaterialInventoryContent(MonsterCatalogSO.Load(), MapCatalogSO.Load());
            selectedId = null;
            ShowList(); Refresh(true); dirty = false;
        }

        private void BindInventory()
        {
            Unsubscribe();
            inventory = FindFirstObjectByType<MaterialInventoryManager>();
            if (inventory == null) return;
            inventory.MaterialChanged += MaterialChanged;
            inventory.MaterialsRestored += MarkDirty;
        }
        private void Unsubscribe()
        {
            if (inventory != null) { inventory.MaterialChanged -= MaterialChanged; inventory.MaterialsRestored -= MarkDirty; }
            inventory = null;
        }
        private void MaterialChanged(string _, long delta, long total) => MarkDirty();
        private void MarkDirty() => dirty = true;

        private void Refresh(bool reset)
        {
            if (definitions == null) return;
            var next = definitions.Owned(inventory != null ? inventory.Records : null);
            bool changed = reset || next.Count != items.Count;
            if (!changed) for (int i = 0; i < next.Count; i++) if (next[i].Id != items[i].Id) { changed = true; break; }
            items = next;
            if (countText != null) countText.text = "所持素材 " + items.Count.ToString("N0") + "種（タップで入手元）";
            if (emptyText != null)
            {
                emptyText.text = inventory == null ? "素材データを読み込み中です" : "所持している素材はありません";
                emptyText.gameObject.SetActive(items.Count == 0);
            }
            if (grid != null) { if (changed) grid.SetData(items.Count, BindCell); else grid.RefreshVisible(); }
            if (selectedId != null)
            {
                int index = items.FindIndex(x => x.Id == selectedId);
                if (index < 0) ShowList(); else ShowDetail(index, false);
            }
        }

        private void BindCell(EncyclopediaCellUI cell, int index)
        {
            var entry = items[index];
            cell.Bind(index, entry.Material != null ? entry.Material.icon : null,
                entry.Name + "\n" + entry.Count, false, Select);
        }
        private void Select(int index) => ShowDetail(index, true);
        private void ShowDetail(int index, bool resetScroll)
        {
            if (index < 0 || index >= items.Count) return;
            var entry = items[index]; selectedId = entry.Id;
            if (detailIcon != null) { detailIcon.sprite = entry.Material != null ? entry.Material.icon : null; detailIcon.enabled = detailIcon.sprite != null; }
            if (detailName != null) detailName.text = entry.Name;
            if (amountText != null) amountText.text = "総所持数　" + entry.Count;
            if (sourcesText != null) sourcesText.text = definitions.Sources(entry.Id);
            if (listRoot != null) listRoot.SetActive(false);
            if (detailRoot != null) detailRoot.SetActive(true);
            if (backLabel != null) backLabel.text = "一覧へ";
            if (resetScroll && detailScroll != null) { Canvas.ForceUpdateCanvases(); detailScroll.StopMovement(); detailScroll.verticalNormalizedPosition = 1f; }
        }
        private void ShowList()
        {
            selectedId = null;
            if (listRoot != null) listRoot.SetActive(true);
            if (detailRoot != null) detailRoot.SetActive(false);
            if (backLabel != null) backLabel.text = "閉じる";
        }
        public void Back() { if (selectedId != null) ShowList(); else gameObject.SetActive(false); }
    }
}
