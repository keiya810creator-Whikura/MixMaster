using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public enum GameGuidePage { Records, Goals, Help }

    /// <summary>Reusable settings child screen. UI is built once, lists use eight pooled rows.</summary>
    [DisallowMultipleComponent]
    public sealed class GameGuidePanelUI : MonoBehaviour
    {
        private const int PageSize = 8;
        private RectTransform card;
        private GameObject recordsRoot, goalsRoot, helpRoot, detailRoot;
        private TMP_Text title, stats, pageLabel, status, detailText, helpText;
        private ScrollRect helpScroll, detailScroll;
        private TMP_Dropdown kindPicker, targetPicker, helpPicker;
        private TMP_InputField amountInput;
        private Button previous, next, deleteButton, addButton;
        private readonly Button[] rows = new Button[PageSize];
        private readonly TMP_Text[] rowLabels = new TMP_Text[PageSize];
        private List<GameGuideContent.Target> choices = new List<GameGuideContent.Target>();
        private GameGuideContent content;
        private GameJournalManager journal;
        private MonsterManager monsters;
        private EquipmentManager equipment;
        private MaterialInventoryManager materials;
        private PlayerManager player;
        private GameGuidePage page;
        private int offset;
        private bool history;
        private float refreshAt;
        private string selectedGoal;
        private Action back;

        public static GameGuidePanelUI OpenDefault(Transform parent, GameGuidePage page, Action onBack)
        {
            var panel = parent.GetComponentInChildren<GameGuidePanelUI>(true);
            if (panel == null)
            {
                var go = new GameObject("GameGuidePanel", typeof(RectTransform), typeof(Image), typeof(GameGuidePanelUI));
                go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
                InventoryUiFactory.Stretch((RectTransform)go.transform);
                go.GetComponent<Image>().color = new Color(0, 0, 0, .92f);
                panel = go.GetComponent<GameGuidePanelUI>();
            }
            panel.Open(page, onBack); return panel;
        }
        public void Open(GameGuidePage requested, Action onBack)
        {
            if (card == null) Build();
            journal = GameJournalManager.Instance;
            monsters = journal != null ? journal.GetComponent<MonsterManager>() : null;
            equipment = journal != null ? journal.GetComponent<EquipmentManager>() : null;
            materials = journal != null ? journal.GetComponent<MaterialInventoryManager>() : null;
            player = journal != null ? journal.GetComponent<PlayerManager>() : null;
            content = content ?? new GameGuideContent(MonsterCatalogSO.Load(), EquipmentCatalogSO.Load(), MapCatalogSO.Load());
            page = requested; back = onBack; offset = 0; history = false; selectedGoal = null;
            detailRoot.SetActive(false);
            recordsRoot.SetActive(page == GameGuidePage.Records);
            goalsRoot.SetActive(page == GameGuidePage.Goals);
            helpRoot.SetActive(page == GameGuidePage.Help);
            title.text = page == GameGuidePage.Records ? "プレイ記録・配合履歴" : page == GameGuidePage.Goals ? "目標リスト" : "ヘルプ";
            if (page == GameGuidePage.Goals) UpdateChoices();
            if (page == GameGuidePage.Help) ShowHelp(0);
            gameObject.SetActive(true); transform.SetAsLastSibling(); refreshAt = 0; Refresh();
        }
        private void Update()
        {
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 1f; Refresh();
        }
        private void OnDisable() { if (detailRoot != null) detailRoot.SetActive(false); selectedGoal = null; }
        private RectTransform Root(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; InventoryUiFactory.Stretch(rect); return rect;
        }
        private TMP_Text Text(Transform parent, string name, string value, float x, float y, float w, float h, float size = 30)
        {
            var label = InventoryUiFactory.Text(parent, name, value, new Vector2(x, y), new Vector2(w, h), size);
            label.richText = false; return label;
        }
        private Button Button(Transform parent, string name, string caption, float x, float y, float w, float h, Action click)
        {
            var button = InventoryUiFactory.Button(parent, name, caption, new Vector2(x, y), new Vector2(w, h));
            var label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 30; label.fontSizeMax = 30; label.fontSizeMin = 24; label.richText = false;
            button.onClick.AddListener(() => click()); return button;
        }
        private ScrollRect Scroll(Transform parent, string name, float y, float height, out TMP_Text label)
        {
            var view = Root(parent, name);
            InventoryUiFactory.TopLeft(view, new Vector2(28, y), new Vector2(944, height));
            view.gameObject.AddComponent<RectMask2D>();
            var image = view.gameObject.AddComponent<Image>(); image.color = new Color(.08f, .12f, .18f, 1);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var body = Root(view, "Content");
            body.anchorMin = new Vector2(0, 1); body.anchorMax = Vector2.one; body.pivot = new Vector2(.5f, 1);
            body.sizeDelta = Vector2.zero;
            label = body.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 30; label.color = Color.white; label.richText = false; label.raycastTarget = false;
            label.margin = new Vector4(16, 16, 16, 16);
            scroll.viewport = view; scroll.content = body; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
            return scroll;
        }
        private static void ScrollText(ScrollRect scroll, TMP_Text label, string value)
        {
            label.text = value; Canvas.ForceUpdateCanvases();
            label.rectTransform.sizeDelta = new Vector2(0, Math.Max(scroll.viewport.rect.height, label.preferredHeight + 40));
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
        }
        private void Build()
        {
            var safe = Root(transform, "SafeArea"); safe.gameObject.AddComponent<SafeAreaLayout>();
            card = Root(safe, "Card"); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(1000, 1560);
            card.gameObject.AddComponent<Image>().color = new Color(.08f, .12f, .18f, 1);
            title = Text(card, "Title", "", 28, -24, 944, 72, 42);
            Button(card, "Back", "設定メニューへ戻る", 28, -1410, 944, 100, Back);
            recordsRoot = Root(card, "Records").gameObject;
            goalsRoot = Root(card, "Goals").gameObject;
            helpRoot = Root(card, "Help").gameObject;
            Button(recordsRoot.transform, "StatsTab", "プレイ記録", 28, -120, 460, 90, () => { history = false; offset = 0; Refresh(); });
            Button(recordsRoot.transform, "HistoryTab", "配合履歴", 512, -120, 460, 90, () => { history = true; offset = 0; Refresh(); });
            stats = Text(recordsRoot.transform, "Statistics", "", 28, -250, 944, 1000, 32);
            kindPicker = InventoryUiFactory.Dropdown(goalsRoot.transform, "Kind", new Vector2(28, -120), new Vector2(944, 80), new[] { "モンスター", "装備", "素材" });
            targetPicker = InventoryUiFactory.Dropdown(goalsRoot.transform, "Target", new Vector2(28, -220), new Vector2(944, 80), new[] { "対象を読み込み中" });
            kindPicker.onValueChanged.AddListener(_ => UpdateChoices());
            var input = Root(goalsRoot.transform, "TargetCount");
            InventoryUiFactory.TopLeft(input, new Vector2(28, -320), new Vector2(460, 80));
            input.gameObject.AddComponent<Image>().color = new Color(.19f, .25f, .34f, 1);
            amountInput = input.gameObject.AddComponent<TMP_InputField>();
            var area = Root(input, "Text Area"); area.offsetMin = new Vector2(12, 8); area.offsetMax = new Vector2(-12, -8); area.gameObject.AddComponent<RectMask2D>();
            var inputText = Text(area, "Text", "1", 0, 0, 430, 64, 32); InventoryUiFactory.Stretch(inputText.rectTransform);
            amountInput.textViewport = area; amountInput.textComponent = inputText; amountInput.contentType = TMP_InputField.ContentType.IntegerNumber; amountInput.characterLimit = 6; amountInput.text = "1";
            addButton = Button(goalsRoot.transform, "AddGoal", "登録・必要数を更新", 512, -320, 460, 80, AddGoal);
            Text(goalsRoot.transform, "GoalHint", "必要数 1～999,999 ／ 同じ対象は更新 ／ タップで入手先・削除", 28, -410, 944, 64, 26);
            // One row pool shared by records and goals. Only rebind visible eight.
            for (int i = 0; i < PageSize; i++)
            {
                int index = i;
                rows[i] = Button(card, "Row" + i, "", 28, -490 - i * 92, 944, 82, () => SelectRow(index));
                rowLabels[i] = rows[i].GetComponentInChildren<TMP_Text>(); rowLabels[i].alignment = TextAlignmentOptions.MidlineLeft;
            }
            previous = Button(card, "Previous", "前へ", 28, -1240, 220, 80, () => { offset = Math.Max(0, offset - PageSize); Refresh(); });
            next = Button(card, "Next", "次へ", 752, -1240, 220, 80, () => { offset += PageSize; Refresh(); });
            pageLabel = Text(card, "Page", "", 268, -1240, 464, 80);
            status = Text(card, "Status", "", 28, -1330, 944, 64, 26);
            helpPicker = InventoryUiFactory.Dropdown(helpRoot.transform, "Topic", new Vector2(28, -120), new Vector2(944, 80), GameHelpContent.Titles);
            helpScroll = Scroll(helpRoot.transform, "HelpText", -230, 1090, out helpText);
            helpPicker.onValueChanged.AddListener(ShowHelp);
            detailRoot = Root(card, "Detail").gameObject;
            detailRoot.AddComponent<Image>().color = new Color(.08f, .12f, .18f, 1);
            detailScroll = Scroll(detailRoot.transform, "DetailText", -120, 1050, out detailText);
            deleteButton = Button(detailRoot.transform, "DeleteGoal", "この目標を削除", 28, -1200, 944, 80, DeleteGoal);
            Button(detailRoot.transform, "CloseDetail", "一覧へ戻る", 28, -1320, 944, 100, () => { selectedGoal = null; detailRoot.SetActive(false); Refresh(); });
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
            // Match the configured Japanese font on the existing settings prefab.
            var parentLabel = transform.parent.GetComponentInChildren<TMP_Text>(true);
            if (parentLabel != null && parentLabel.font != null)
                foreach (var label in GetComponentsInChildren<TMP_Text>(true)) label.font = parentLabel.font;
        }
        private void UpdateChoices()
        {
            if (content == null) return;
            choices = content.Targets.FindAll(x => x.kind == (GoalKind)kindPicker.value);
            var names = choices.ConvertAll(x => x.name);
            targetPicker.ClearOptions(); targetPicker.AddOptions(names.Count == 0 ? new List<string> { "登録対象がありません" } : names);
            targetPicker.SetValueWithoutNotify(0); targetPicker.RefreshShownValue();
            targetPicker.interactable = choices.Count > 0; addButton.interactable = journal != null && choices.Count > 0;
        }
        private void AddGoal()
        {
            int i = targetPicker.value;
            if (journal == null || i < 0 || i >= choices.Count || !long.TryParse(amountInput.text, out long count) || count < 1 || count > GameJournalManager.MaximumTarget)
            { status.text = "必要数は1～999,999で入力してください。"; return; }
            var x = choices[i];
            if (!journal.SetGoal(x.kind, x.id, x.name, count)) { status.text = "目標は最大50件です。不要な目標を削除してください。"; return; }
            offset = 0; Refresh(); status.text = "目標を登録・更新しました。自動保存されます。";
        }
        private void DeleteGoal()
        {
            if (journal != null && selectedGoal != null) journal.DeleteGoal(selectedGoal);
            selectedGoal = null; detailRoot.SetActive(false); Refresh();
        }
        private void ShowHelp(int index)
        {
            index = Mathf.Clamp(index, 0, GameHelpContent.Bodies.Length - 1); helpPicker.SetValueWithoutNotify(index);
            ScrollText(helpScroll, helpText, GameHelpContent.Titles[index] + "\n\n" + GameHelpContent.Bodies[index]);
        }
        private void Refresh()
        {
            if (card == null) return;
            bool list = page == GameGuidePage.Goals || page == GameGuidePage.Records && history;
            int count = journal == null ? 0 : page == GameGuidePage.Goals ? journal.Goals.Count : journal.History.Count;
            offset = Math.Min(offset, Math.Max(0, (count - 1) / PageSize * PageSize));
            if (page == GameGuidePage.Records && !history)
            {
                stats.gameObject.SetActive(true);
                double seconds = journal != null ? journal.PlaySeconds : 0;
                stats.text = journal == null ? "記録を読み込めません。" :
                    "記録開始　" + GameGuideContent.Date(journal.StartedUtc) + "\n\nプレイ時間　" + ((long)seconds / 3600).ToString("N0") + "時間 " + ((long)seconds / 60 % 60) + "分\n\n討伐数　" + GameGuideContent.Number(journal.DefeatedEnemies) +
                    "\n\n最大ダメージ　" + GameGuideContent.Number(journal.MaximumDamage) + "\n\n配合回数　" + GameGuideContent.Number(journal.BreedingAttempts) + "\n成功 " + GameGuideContent.Number(journal.BreedingSuccesses) + " ／ 失敗 " + GameGuideContent.Number(journal.BreedingFailures) +
                    "\n\n転生回数　" + GameGuideContent.Number(player != null ? player.ReincarnationCount : 0) + "\n\n※転生回数以外は、この機能の追加後からの記録です。\nプレイ時間は前面でゲームが動いている時間を計測します。";
            }
            else stats.gameObject.SetActive(false);
            for (int i = 0; i < PageSize; i++)
            {
                bool visible = list && offset + i < count; rows[i].gameObject.SetActive(visible);
                if (!visible) continue;
                if (page == GameGuidePage.Goals)
                {
                    var goal = journal.Goals[offset + i]; long owned = GameGuideContent.Owned(goal, monsters, equipment, materials);
                    rowLabels[i].text = (owned >= goal.targetCount ? "達成　" : "未達成　") + content.Resolve(goal).name + "\n" + GameGuideContent.Number(owned) + " / " + GameGuideContent.Number(goal.targetCount);
                }
                else
                {
                    var entry = journal.History[offset + i];
                    rowLabels[i].text = GameGuideContent.Date(entry.utc) + (entry.succeeded ? "　成功" : "　失敗") + "\n" + GameGuideContent.Monster(entry.parentA) + " × " + GameGuideContent.Monster(entry.parentB);
                }
            }
            previous.gameObject.SetActive(list); next.gameObject.SetActive(list); pageLabel.gameObject.SetActive(list);
            previous.interactable = offset > 0; next.interactable = offset + PageSize < count;
            pageLabel.text = count == 0 ? "まだ登録がありません" : (offset / PageSize + 1) + " / " + ((count - 1) / PageSize + 1) + "　（" + count + "件）";
            status.text = journal == null ? "記録を読み込めません。" : journal.SavePending ? "変更は反映済みです。保存を待機・再試行しています。" : page == GameGuidePage.Goals ? "現在の所持数で判定 ／ 最大50件" : page == GameGuidePage.Records && history ? "成功・失敗の最新200件 ／ タップで詳細" : "";
            if (detailRoot.activeSelf && selectedGoal != null) ShowGoalDetail(false);
        }
        private void SelectRow(int row)
        {
            int index = offset + row;
            if (journal == null || index < 0) return;
            if (page == GameGuidePage.Goals)
            {
                if (index >= journal.Goals.Count) return;
                selectedGoal = journal.Goals[index].goalId; ShowGoalDetail(true);
            }
            else
            {
                if (index >= journal.History.Count) return;
                selectedGoal = null; detailRoot.SetActive(true); detailRoot.transform.SetAsLastSibling(); deleteButton.gameObject.SetActive(false);
                ScrollText(detailScroll, detailText, GameGuideContent.History(journal.History[index]));
            }
        }
        private void ShowGoalDetail(bool reset)
        {
            GoalRecord goal = null;
            foreach (var x in journal.Goals) if (x.goalId == selectedGoal) { goal = x; break; }
            if (goal == null) { selectedGoal = null; detailRoot.SetActive(false); return; }
            var target = content.Resolve(goal); long owned = GameGuideContent.Owned(goal, monsters, equipment, materials);
            string value = target.name + "\n\n" + (owned >= goal.targetCount ? "達成" : "未達成") + "　" + GameGuideContent.Number(owned) + " / " + GameGuideContent.Number(goal.targetCount) + "\n\n" + target.sources + "\n\n現在の所持数で判定します。消費や売却で進捗が減る場合があります。";
            detailRoot.SetActive(true); detailRoot.transform.SetAsLastSibling(); deleteButton.gameObject.SetActive(true);
            if (reset) ScrollText(detailScroll, detailText, value); else if (detailText.text != value)
            {
                float position = detailScroll.verticalNormalizedPosition; ScrollText(detailScroll, detailText, value); detailScroll.verticalNormalizedPosition = position;
            }
        }
        private void Back() { gameObject.SetActive(false); back?.Invoke(); }
    }
}
