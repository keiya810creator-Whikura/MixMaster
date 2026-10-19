using System;
using System.Collections.Generic;
using System.Text;
using MixMaster.Core;
using MixMaster.Player;
using MixMaster.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class ReincarnationPanelUI : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button humanButton;
        [SerializeField] private Button sortButton;
        [SerializeField] private Button directionButton;
        [SerializeField] private Button favoritesFilterButton;
        [SerializeField] private Button reincarnateButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text currentText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField] private TMP_Text sortText;
        [SerializeField] private TMP_Text directionText;
        [SerializeField] private TMP_Text favoritesFilterText;
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private ReincarnationBookEntryUI entryPrefab;
        [SerializeField] private Sprite humanSprite;
        private readonly List<ReincarnationBookEntryUI> entries = new List<ReincarnationBookEntryUI>();
        private ReincarnationManager manager;
        private PlayerManager player;
        private bool subscribed;
        private bool favoritesOnly;
        private bool descending;
        private bool executing;
        private ReincarnationBookSort sort;
        private string selectedId;
        private string pendingId;
        private float retryAt;
        private float lastWidth;
        private int visibleCount;
        private static readonly string[] SortNames =
            { "入手順", "名前", "基礎HP", "基礎MP", "基礎攻撃", "基礎魔力", "基礎防御", "基礎魔防" };
        public event Action Closed;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (humanButton != null) humanButton.onClick.AddListener(SelectHuman);
            if (sortButton != null) sortButton.onClick.AddListener(CycleSort);
            if (directionButton != null) directionButton.onClick.AddListener(ReverseSort);
            if (favoritesFilterButton != null) favoritesFilterButton.onClick.AddListener(ToggleFavoritesFilter);
            if (reincarnateButton != null) reincarnateButton.onClick.AddListener(RequestReincarnation);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmReincarnation);
            if (cancelButton != null) cancelButton.onClick.AddListener(CancelConfirmation);
        }
        private void OnEnable()
        {
            selectedId = null;
            EnsureBindings();
            Refresh();
        }
        private void OnDisable()
        {
            if (subscribed)
            {
                if (manager != null) manager.BooksChanged -= Refresh;
                if (player != null) player.StatsChanged -= RefreshDetails;
            }
            subscribed = false;
            CancelConfirmation();
            Closed?.Invoke();
        }
        private void Update()
        {
            if (!subscribed && Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 0.5f;
                EnsureBindings();
                Refresh();
            }
            UpdateGridSize();
        }
        private void EnsureBindings()
        {
            if (manager == null) manager = FindFirstObjectByType<ReincarnationManager>();
            if (player == null) player = FindFirstObjectByType<PlayerManager>();
            if (manager == null || player == null || subscribed) return;
            manager.BooksChanged += Refresh;
            player.StatsChanged += RefreshDetails;
            subscribed = true;
            if (humanSprite == null)
            {
                PlayerController controller = FindFirstObjectByType<PlayerController>();
                SpriteRenderer renderer = controller != null ? controller.GetComponentInChildren<SpriteRenderer>() : null;
                if (renderer != null) humanSprite = renderer.sprite;
            }
        }

        private void SelectHuman() => SelectBook(ReincarnationRules.HumanBookId);
        private void SelectBook(string id)
        {
            if (executing || manager == null || manager.FindBook(id) == null) return;
            selectedId = id;
            Refresh();
        }
        private void ToggleFavorite(string id)
        {
            if (!executing && manager != null) manager.ToggleFavorite(id);
        }
        private void CycleSort()
        {
            sort = (ReincarnationBookSort)(((int)sort + 1) % SortNames.Length);
            descending = sort != ReincarnationBookSort.Name && sort != ReincarnationBookSort.Acquisition;
            Refresh();
        }
        private void ReverseSort() { descending = !descending; Refresh(); }
        private void ToggleFavoritesFilter() { favoritesOnly = !favoritesOnly; Refresh(); }

        private void Refresh()
        {
            CancelConfirmation();
            if (manager == null || player == null) return;
            if (manager.FindBook(selectedId) == null)
                selectedId = manager.FindBook(player.ReincarnationBookId) != null
                    ? player.ReincarnationBookId : ReincarnationRules.HumanBookId;

            List<ReincarnationBookRecord> visible = manager.GetSortedBooks(sort, descending, favoritesOnly);
            visibleCount = visible.Count;
            for (int i = 0; i < visible.Count; i++)
            {
                if (i >= entries.Count)
                {
                    if (entryPrefab == null || content == null) break;
                    entries.Add(Instantiate(entryPrefab, content));
                }
                ReincarnationBookRecord book = visible[i];
                MonsterSO species = manager.GetSpecies(book.bookId);
                Sprite image = book.bookId == ReincarnationRules.HumanBookId ? humanSprite :
                    species != null ? (species.sprite != null ? species.sprite : species.icon) : null;
                entries[i].gameObject.SetActive(true);
                entries[i].Bind(book.bookId, manager.GetBookName(book.bookId), image,
                    book.isFavorite, book.bookId == selectedId, SelectBook, ToggleFavorite);
            }
            for (int i = visible.Count; i < entries.Count; i++) entries[i].gameObject.SetActive(false);
            if (countText != null) countText.text = "転生の書 " + manager.Books.Count + "種　／　表示 " + visible.Count + "種";
            if (sortText != null) sortText.text = "並び順: " + SortNames[(int)sort];
            if (directionText != null) directionText.text = descending ? "降順 ▼" : "昇順 ▲";
            if (favoritesFilterText != null) favoritesFilterText.text = favoritesOnly ? "★のみ: ON" : "★のみ: OFF";
            if (noticeText != null) noticeText.text = visible.Count == 0
                ? "お気に入りの書はありません。「人間の書」は上のボタンからも選べます。"
                : "信頼度5で、その種族の転生の書を取得できます。";
            lastWidth = -1f;
            UpdateGridSize();
            RefreshDetails();
            if (detailsScroll != null) detailsScroll.verticalNormalizedPosition = 1f;
        }

        private void UpdateGridSize()
        {
            if (grid == null || content == null || viewport == null) return;
            int columns = Mathf.Max(1, grid.constraintCount);
            float width = viewport.rect.width;
            if (width <= 0f || Mathf.Abs(lastWidth - width) < 0.5f) return;
            lastWidth = width;
            float cellWidth = (width - grid.padding.left - grid.padding.right - grid.spacing.x * (columns - 1)) / columns;
            grid.cellSize = new Vector2(Mathf.Max(40f, cellWidth), grid.cellSize.y);
            int rows = Mathf.CeilToInt((float)visibleCount / columns);
            content.sizeDelta = new Vector2(content.sizeDelta.x,
                grid.padding.top + grid.padding.bottom + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y);
            UpdateDetailHeight();
        }

        private void RefreshDetails()
        {
            if (manager == null || player == null) return;
            if (currentText != null) currentText.text = "現在: " + manager.GetBookName(player.ReincarnationBookId) +
                "　Lv." + player.Level + "\nスキルポイント: " + player.SkillPoints.ToString("N0") +
                "　使用済み: " + player.SpentSkillPoints.ToString("N0");
            CharacterStats stats = manager.GetBookBaseStats(selectedId);
            if (detailText != null)
            {
                if (stats == null) detailText.text = "転生先のデータがありません。";
                else
                {
                    // Preview the actual Lv1 values before equipment, including the
                    // shared movement multiplier and normal caps on criticals/resistances.
                    CharacterStats preview = StatCalculator.CalculateStats(stats, 1, new MonsterIndividualValues());
                    var text = new StringBuilder(manager.GetBookName(selectedId));
                    text.Append("\n基礎値　HP ").Append(stats.maxHp.ToString("N0"))
                        .Append(" ／ MP ").Append(stats.maxMp.ToString("N0"))
                        .Append("\n攻撃 ").Append(stats.attack.ToString("N0"))
                        .Append(" ／ 魔力 ").Append(stats.magic.ToString("N0"))
                        .Append(" ／ 防御 ").Append(stats.defense.ToString("N0"))
                        .Append(" ／ 魔防 ").Append(stats.magicDefense.ToString("N0"));
                    text.Append("\n移動速度 ").Append(preview.moveSpeed.ToString("0.##"))
                        .Append(" ／ 攻撃速度 ").Append(preview.attackSpeed.ToString("0.##")).Append("回/秒")
                        .Append("\n攻撃範囲 ").Append(preview.attackRange.ToString("0.##"))
                        .Append("\nクリティカル率 ").Append(Percent(preview.criticalRate))
                        .Append(" ／ 倍率 ×").Append(preview.criticalMultiplier.ToString("0.##"))
                        .Append("\n属性 ").Append(ElementName(preview.element))
                        .Append(" ／ ドロップ率補正 +").Append(Percent(preview.dropRateBonus));
                    ElementResistanceSet r = preview.resistances;
                    text.Append("\n属性耐性　火 ").Append(Percent(r.fire))
                        .Append(" ／ 水 ").Append(Percent(r.water)).Append(" ／ 風 ").Append(Percent(r.wind))
                        .Append("\n雷 ").Append(Percent(r.lightning)).Append(" ／ 光 ").Append(Percent(r.light))
                        .Append(" ／ 闇 ").Append(Percent(r.dark));
                    detailText.text = text.ToString();
                }
            }
            UpdateDetailHeight();
            if (reincarnateButton != null) reincarnateButton.interactable = !executing && stats != null;
        }

        private void UpdateDetailHeight()
        {
            if (detailsScroll == null || detailsScroll.content == null ||
                detailsScroll.viewport == null || detailText == null) return;
            RectTransform detailContent = detailsScroll.content;
            detailContent.sizeDelta = new Vector2(detailContent.sizeDelta.x,
                Mathf.Max(detailsScroll.viewport.rect.height, detailText.preferredHeight + 24f));
        }

        private static string Percent(float value) => (value * 100f).ToString("0.##") + "%";

        private static string ElementName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "火";
                case ElementType.Water: return "水";
                case ElementType.Wind: return "風";
                case ElementType.Lightning: return "雷";
                case ElementType.Light: return "光";
                case ElementType.Dark: return "闇";
                default: return "無";
            }
        }

        private void RequestReincarnation()
        {
            if (executing || manager == null || player == null || manager.FindBook(selectedId) == null) return;
            pendingId = selectedId;
            if (noticeText != null) noticeText.text = manager.GetBookName(pendingId) +
                "で転生しますか？\nLv1・経験値0に戻ります。スキルポイント " +
                ReincarnationRules.TotalSkillPoints(player.SkillPoints, player.SpentSkillPoints).ToString("N0") +
                " を引き継ぎます。書は消費しません。";
            SetConfirmationVisible(true);
        }
        private void ConfirmReincarnation()
        {
            if (executing || string.IsNullOrEmpty(pendingId) || manager == null || !isActiveAndEnabled) return;
            string id = pendingId;
            executing = true;
            try
            {
                bool success = manager.TryReincarnate(id, out string error);
                Refresh();
                if (noticeText != null) noticeText.text = success
                    ? manager.GetBookName(id) + "で転生しました。Lv1から成長できます。" : error;
            }
            finally { executing = false; RefreshDetails(); }
        }
        private void CancelConfirmation() { pendingId = null; SetConfirmationVisible(false); }
        private void SetConfirmationVisible(bool visible)
        {
            if (confirmButton != null) confirmButton.gameObject.SetActive(visible);
            if (cancelButton != null) cancelButton.gameObject.SetActive(visible);
            if (reincarnateButton != null) reincarnateButton.gameObject.SetActive(!visible);
        }
        public void Close()
        {
            MapBuildingUIRouter router = MapBuildingUIRouter.Instance;
            if (router != null && router.CurrentBuilding == MapBuildingType.Reincarnation) router.CloseAll();
            else gameObject.SetActive(false);
            MapBuildingAccess.SuppressOpenFor(0.5f);
        }
    }
}
