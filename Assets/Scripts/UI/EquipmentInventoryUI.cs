using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class EquipmentInventoryUI : MonoBehaviour
    {
        [Header("Inventory")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button closeButton;

        private EquipmentManager manager;
        private TMP_Dropdown sortDropdown;
        private TMP_Dropdown statFilter;
        private TMP_Dropdown rarityFilter;
        private Button directionButton;
        private TMP_Text directionLabel;
        private bool ascending;
        private bool subscribed;
        private string inspectedId;

        private readonly List<GameObject> activeRows = new List<GameObject>();

        private static readonly string[] SortChoices = BuildSortChoices();
        private static readonly string[] StatChoices = InventoryStatFilter.Labels;
        private static readonly string[] RarityChoices =
        {
            "全レア度", "ノーマル", "レア", "スーパーレア",
            "エピックレア", "レジェンダリーレア"
        };

        public void SetReferences(RectTransform content,
            TMP_Text count, TMP_Text detail, Button close)
        {
            contentRoot = content;
            countText = count;
            detailText = detail;
            closeButton = close;
        }

        private void Awake()
        {
            EnsureControls();
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            BindManager();
            Rebuild();
        }

        private void OnDisable()
        {
            if (subscribed && manager != null)
                manager.InventoryChanged -= Rebuild;
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            else
            {
                BindManager();
                Rebuild();
            }
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void SortBy(int selected)
        {
            if (sortDropdown != null)
                sortDropdown.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, SortChoices.Length - 1));
            Rebuild();
        }

        public void FilterByStat(int selected)
        {
            if (statFilter != null)
                statFilter.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, StatChoices.Length - 1));
            Rebuild();
        }

        public void FilterByRarity(int selected)
        {
            if (rarityFilter != null)
                rarityFilter.SetValueWithoutNotify(
                    Mathf.Clamp(selected, 0, RarityChoices.Length - 1));
            Rebuild();
        }

        private void BindManager()
        {
            if (manager == null)
                manager = FindFirstObjectByType<EquipmentManager>();
            if (manager == null || subscribed)
                return;

            manager.InventoryChanged += Rebuild;
            subscribed = true;
        }

        public void Rebuild()
        {
            if (manager == null) BindManager();

            foreach (GameObject row in activeRows)
                if (row != null) Destroy(row);
            activeRows.Clear();

            if (manager == null || contentRoot == null)
                return;

            IReadOnlyList<EquipmentRecord> source = manager.Inventory;
            var matches = new List<int>();
            int statIndex = statFilter != null ? statFilter.value : 0;
            int rarityIndex = rarityFilter != null ? rarityFilter.value : 0;
            int sortIndex = sortDropdown != null ? sortDropdown.value : 0;

            for (int i = 0; i < source.Count; i++)
            {
                EquipmentRecord record = source[i];
                if (record == null) continue;

                if (rarityIndex != 0 &&
                    (int)EquipmentRarityUtility.GetRarity(record.level) !=
                    rarityIndex - 1)
                    continue;

                if (statIndex != 0)
                {
                    EquipmentSO definition =
                        manager.GetEquipmentDefinition(record.equipmentId);
                    if (definition == null ||
                        Math.Abs(InventoryStatFilter.GetValue(
                            definition.modifiers, (InventoryStat)statIndex)) < 0.000001d)
                        continue;
                }

                matches.Add(i);
            }

            matches.Sort((a, b) =>
            {
                EquipmentRecord left = source[a], right = source[b];
                int compare;
                if (sortIndex == 0)
                    compare = b.CompareTo(a); // newest first
                else if (sortIndex == 1)
                    compare = right.level.CompareTo(left.level);
                else
                {
                    InventoryStat stat = (InventoryStat)(sortIndex - 1);
                    EquipmentSO first =
                        manager.GetEquipmentDefinition(left.equipmentId);
                    EquipmentSO second =
                        manager.GetEquipmentDefinition(right.equipmentId);

                    double x = first != null
                        ? InventoryStatFilter.GetValue(first.modifiers, stat) : 0d;
                    double y = second != null
                        ? InventoryStatFilter.GetValue(second.modifiers, stat) : 0d;
                    compare = y.CompareTo(x);
                }

                if (compare == 0)
                    compare = b.CompareTo(a);
                return ascending ? -compare : compare;
            });

            if (countText != null)
                countText.text = "装備 " + matches.Count.ToString("N0") +
                    " / " + source.Count.ToString("N0");

            foreach (int index in matches)
                BuildEntry(source[index], index);

            if (matches.Count == 0 && detailText != null)
                detailText.text = "条件に一致する装備がありません。";
        }

        private void BuildEntry(EquipmentRecord record, int index)
        {
            EquipmentSO definition =
                manager.GetEquipmentDefinition(record.equipmentId);
            GameObject row = new GameObject(
                "Equipment_" + record.uniqueId, typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(contentRoot, false);
            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.preferredHeight = 120f;
            layout.minHeight = 120f;

            Image bg = row.GetComponent<Image>();
            bg.color = RarityColor(EquipmentRarityUtility.GetRarity(record.level));
            Button button = row.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => Inspect(record));

            if (definition != null && definition.icon != null)
            {
                GameObject icon = new GameObject(
                    "Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(row.transform, false);
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                InventoryUiFactory.TopLeft(iconRect,
                    new Vector2(10f, -17f), new Vector2(84f, 84f));
                Image iconImage = icon.GetComponent<Image>();
                iconImage.sprite = definition.icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            string name = definition != null &&
                !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName : record.equipmentId;

            var rarity = EquipmentRarityUtility.GetRarity(record.level);
            string label = name + "   Lv." + record.level +
                " 《" + EquipmentRarityUtility.Labels[(int)rarity] + "》" +
                "\n" + (definition != null
                    ? InventoryStatFilter.FormatModifiers(definition.modifiers)
                    : "装備定義が見つかりません");
            TMP_Text text = InventoryUiFactory.Text(
                row.transform, "Summary", label,
                new Vector2(112f, -10f), new Vector2(790f, 104f), 23f);
            text.enableWordWrapping = true;

            activeRows.Add(row);
        }

        private void Inspect(EquipmentRecord record)
        {
            inspectedId = record.uniqueId;
            EquipmentSO definition =
                manager.GetEquipmentDefinition(record.equipmentId);
            string name = definition != null
                ? definition.displayName : record.equipmentId;
            var rarity = EquipmentRarityUtility.GetRarity(record.level);
            if (detailText != null)
            {
                detailText.text = name + "　Lv." + record.level +
                    "　[" + EquipmentRarityUtility.Labels[(int)rarity] + "]" +
                    "\n" + (definition != null
                        ? InventoryStatFilter.FormatModifiers(definition.modifiers) +
                            "\n" + definition.description
                        : "装備定義が未登録です。") +
                    "\n※装備の付け替えは次回実装予定";
            }
        }

        private void EnsureControls()
        {
            Transform parent = transform.Find("FilterControls");
            if (parent == null)
            {
                GameObject area = new GameObject(
                    "FilterControls", typeof(RectTransform));
                area.transform.SetParent(transform, false);
                RectTransform rect = area.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -110f);
                rect.sizeDelta = new Vector2(0f, 122f);
                parent = rect;
            }

            var control = (RectTransform)parent;
            sortDropdown = InventoryUiFactory.Dropdown(control,
                "SortDropdown", new Vector2(20f, -6f),
                new Vector2(240f, 54f), SortChoices);
            statFilter = InventoryUiFactory.Dropdown(control,
                "StatFilter", new Vector2(270f, -6f),
                new Vector2(245f, 54f), StatChoices);
            rarityFilter = InventoryUiFactory.Dropdown(control,
                "RarityFilter", new Vector2(525f, -6f),
                new Vector2(240f, 54f), RarityChoices);
            directionButton = InventoryUiFactory.Button(control,
                "Direction", "降順", new Vector2(775f, -6f),
                new Vector2(135f, 54f));
            directionLabel = directionButton.GetComponentInChildren<TMP_Text>();

            InventoryUiFactory.Text(control, "SortHint", "並び替え",
                new Vector2(24f, -70f), new Vector2(230f, 28f), 18f);
            InventoryUiFactory.Text(control, "StatHint", "ステータス絞り込み",
                new Vector2(273f, -70f), new Vector2(270f, 28f), 18f);
            InventoryUiFactory.Text(control, "RarityHint", "レア度絞り込み",
                new Vector2(528f, -70f), new Vector2(240f, 28f), 18f);

            sortDropdown.onValueChanged.AddListener(SortBy);
            statFilter.onValueChanged.AddListener(FilterByStat);
            rarityFilter.onValueChanged.AddListener(FilterByRarity);
            directionButton.onClick.AddListener(() =>
            {
                ascending = !ascending;
                if (directionLabel != null)
                    directionLabel.text = ascending ? "昇順" : "降順";
                Rebuild();
            });
        }

        private static string[] BuildSortChoices()
        {
            var options = new List<string> { "入手順", "レベル" };
            for (int i = 1; i < InventoryStatFilter.Labels.Length; i++)
                options.Add(InventoryStatFilter.Labels[i]);
            return options.ToArray();
        }

        private static Color RarityColor(EquipmentRarity rarity)
        {
            switch (rarity)
            {
                case EquipmentRarity.Rare:
                    return new Color(0.13f, 0.27f, 0.43f, 1f);
                case EquipmentRarity.SuperRare:
                    return new Color(0.20f, 0.29f, 0.52f, 1f);
                case EquipmentRarity.EpicRare:
                    return new Color(0.36f, 0.20f, 0.48f, 1f);
                case EquipmentRarity.LegendaryRare:
                    return new Color(0.47f, 0.31f, 0.11f, 1f);
                default:
                    return new Color(0.15f, 0.19f, 0.25f, 1f);
            }
        }
    }
}
