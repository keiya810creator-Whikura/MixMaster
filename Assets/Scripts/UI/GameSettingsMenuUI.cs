using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class GameSettingsMenuUI : MonoBehaviour
    {
        public const string ResourcePath = "UI/Settings/GameSettingsMenu";
        [SerializeField] private GameObject selectionRoot, otherSettingsRoot, placeholderRoot;
        [SerializeField] private TMP_Text titleText, saveStatusText;
        [SerializeField] private Button volumeButton, otherButton, creditsButton, contactButton, backButton, closeButton;
        [SerializeField] private Toggle materialLogToggle, damageTextToggle, healingTextToggle, buffTextToggle,
            debuffTextToggle, skillEffectsToggle, attackRangeToggle;
        private DisplaySettingsManager settings;
        [SerializeField] private Button recordsButton, goalsButton, helpButton;
        private GameGuidePanelUI guide;
        private bool opened;

        private void Awake()
        {
            EnsureGuideButtons();
            if (recordsButton != null) recordsButton.onClick.AddListener(ShowRecords);
            if (goalsButton != null) goalsButton.onClick.AddListener(ShowGoals);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (volumeButton != null) volumeButton.onClick.AddListener(ShowVolume);
            if (otherButton != null) otherButton.onClick.AddListener(ShowOther);
            if (creditsButton != null) creditsButton.onClick.AddListener(ShowCredits);
            if (contactButton != null) contactButton.onClick.AddListener(ShowContact);
            if (backButton != null) backButton.onClick.AddListener(ShowSelection);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (materialLogToggle != null) materialLogToggle.onValueChanged.AddListener(SetMaterialLog);
            if (damageTextToggle != null) damageTextToggle.onValueChanged.AddListener(SetDamageText);
            if (healingTextToggle != null) healingTextToggle.onValueChanged.AddListener(SetHealingText);
            if (buffTextToggle != null) buffTextToggle.onValueChanged.AddListener(SetBuffText);
            if (debuffTextToggle != null) debuffTextToggle.onValueChanged.AddListener(SetDebuffText);
            if (skillEffectsToggle != null) skillEffectsToggle.onValueChanged.AddListener(SetSkillEffects);
            if (attackRangeToggle != null) attackRangeToggle.onValueChanged.AddListener(SetAttackRange);
        }

        private void Start() { if (!opened) gameObject.SetActive(false); }
        private void OnDisable() { Unsubscribe(); opened = false; if (guide != null) guide.gameObject.SetActive(false); }

        private void OnDestroy()
        {
            Unsubscribe();
            if (recordsButton != null) recordsButton.onClick.RemoveListener(ShowRecords);
            if (goalsButton != null) goalsButton.onClick.RemoveListener(ShowGoals);
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowHelp);
            if (volumeButton != null) volumeButton.onClick.RemoveListener(ShowVolume);
            if (otherButton != null) otherButton.onClick.RemoveListener(ShowOther);
            if (creditsButton != null) creditsButton.onClick.RemoveListener(ShowCredits);
            if (contactButton != null) contactButton.onClick.RemoveListener(ShowContact);
            if (backButton != null) backButton.onClick.RemoveListener(ShowSelection);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (materialLogToggle != null) materialLogToggle.onValueChanged.RemoveListener(SetMaterialLog);
            if (damageTextToggle != null) damageTextToggle.onValueChanged.RemoveListener(SetDamageText);
            if (healingTextToggle != null) healingTextToggle.onValueChanged.RemoveListener(SetHealingText);
            if (buffTextToggle != null) buffTextToggle.onValueChanged.RemoveListener(SetBuffText);
            if (debuffTextToggle != null) debuffTextToggle.onValueChanged.RemoveListener(SetDebuffText);
            if (skillEffectsToggle != null) skillEffectsToggle.onValueChanged.RemoveListener(SetSkillEffects);
            if (attackRangeToggle != null) attackRangeToggle.onValueChanged.RemoveListener(SetAttackRange);
        }

        public static GameSettingsMenuUI OpenDefault(Transform parent)
        {
            if (parent == null) return null;
            var menu = parent.GetComponentInChildren<GameSettingsMenuUI>(true);
            if (menu == null)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null) { Debug.LogWarning("[GameSettings] GameSettingsMenu.prefabが見つかりません。"); return null; }
                menu = Instantiate(prefab, parent, false).GetComponent<GameSettingsMenuUI>();
            }
            if (menu != null) menu.Open();
            return menu;
        }

        public void Open()
        {
            gameObject.SetActive(true); transform.SetAsLastSibling(); opened = true;
            Unsubscribe(); settings = DisplaySettingsManager.Instance;
            if (settings != null) settings.Changed += Refresh;
            ShowSelection(); Refresh();
        }

        private void Unsubscribe()
        {
            if (settings != null) settings.Changed -= Refresh;
            settings = null;
        }

        public void ShowSelection() { if (guide != null) guide.gameObject.SetActive(false); ShowPage("設定", selectionRoot); }
        private void ShowRecords() => ShowGuide(GameGuidePage.Records);
        private void ShowGoals() => ShowGuide(GameGuidePage.Goals);
        private void ShowHelp() => ShowGuide(GameGuidePage.Help);
        private void ShowGuide(GameGuidePage page)
        {
            guide = GameGuidePanelUI.OpenDefault(transform, page, ShowSelection);
        }
        private void EnsureGuideButtons()
        {
            if (selectionRoot == null || volumeButton == null || recordsButton != null && goalsButton != null && helpButton != null) return;
            Button Create(string name, string caption)
            {
                var existing = selectionRoot.transform.Find(name);
                var button = existing != null ? existing.GetComponent<Button>() : null;
                if (button == null)
                {
                    button = Instantiate(volumeButton, selectionRoot.transform, false); button.name = name;
                    button.onClick = new Button.ButtonClickedEvent();
                }
                var label = button.GetComponentInChildren<TMP_Text>(true); if (label != null) label.text = caption;
                return button;
            }
            if (recordsButton == null) recordsButton = Create("Records", "プレイ記録・配合履歴");
            if (goalsButton == null) goalsButton = Create("Goals", "目標リスト");
            if (helpButton == null) helpButton = Create("Help", "ヘルプ");
            var buttons = new[] { volumeButton, otherButton, recordsButton, goalsButton, helpButton, creditsButton, contactButton };
            for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null)
                InventoryUiFactory.TopLeft((RectTransform)buttons[i].transform, new Vector2(28, -232 - i * 150), new Vector2(944, 112));
            foreach (var label in selectionRoot.GetComponentsInChildren<TMP_Text>(true))
                if (label.transform.parent == selectionRoot.transform && label.rectTransform.anchoredPosition.y < -1000) InventoryUiFactory.TopLeft(label.rectTransform, new Vector2(28, -1320), new Vector2(944, 80));
        }
        public void ShowOther() { ShowPage("その他設定", otherSettingsRoot); Refresh(); }
        public void ShowVolume() => ShowPage("音量設定（未実装）", placeholderRoot);
        public void ShowCredits() => ShowPage("クレジット（未実装）", placeholderRoot);
        public void ShowContact() => ShowPage("お問合せ（未実装）", placeholderRoot);

        private void ShowPage(string title, GameObject page)
        {
            if (selectionRoot != null) selectionRoot.SetActive(page == selectionRoot);
            if (otherSettingsRoot != null) otherSettingsRoot.SetActive(page == otherSettingsRoot);
            if (placeholderRoot != null) placeholderRoot.SetActive(page == placeholderRoot);
            if (titleText != null) titleText.text = title;
            if (backButton != null) backButton.gameObject.SetActive(page != selectionRoot);
        }

        private void Refresh()
        {
            SetToggle(materialLogToggle, DisplayOption.MaterialLog);
            SetToggle(damageTextToggle, DisplayOption.DamageText);
            SetToggle(healingTextToggle, DisplayOption.HealingText);
            SetToggle(buffTextToggle, DisplayOption.BuffText);
            SetToggle(debuffTextToggle, DisplayOption.DebuffText);
            SetToggle(skillEffectsToggle, DisplayOption.SkillEffects);
            SetToggle(attackRangeToggle, DisplayOption.AttackRange);
            if (saveStatusText != null)
                saveStatusText.text = settings == null ? "設定を読み込めません。" : settings.SavePending
                    ? "設定は反映済みです。保存を再試行しています。" : "変更は自動で保存されます。";
        }

        private void SetToggle(Toggle toggle, DisplayOption option)
        {
            if (toggle == null) return;
            toggle.interactable = settings != null;
            toggle.SetIsOnWithoutNotify(DisplaySettingsManager.IsDisabled(option));
        }

        private void Set(DisplayOption option, bool disabled) { if (settings != null) settings.SetDisabled(option, disabled); }
        private void SetMaterialLog(bool value) => Set(DisplayOption.MaterialLog, value);
        private void SetDamageText(bool value) => Set(DisplayOption.DamageText, value);
        private void SetHealingText(bool value) => Set(DisplayOption.HealingText, value);
        private void SetBuffText(bool value) => Set(DisplayOption.BuffText, value);
        private void SetDebuffText(bool value) => Set(DisplayOption.DebuffText, value);
        private void SetSkillEffects(bool value) => Set(DisplayOption.SkillEffects, value);
        private void SetAttackRange(bool value) => Set(DisplayOption.AttackRange, value);
        public void Close() => gameObject.SetActive(false);
    }
}
