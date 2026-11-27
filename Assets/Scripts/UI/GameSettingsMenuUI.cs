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
        private Slider musicVolumeSlider, effectVolumeSlider;
        private TMP_Text musicVolumeText, effectVolumeText;
        [SerializeField] private Button recordsButton, goalsButton, helpButton;
        private GameGuidePanelUI guide;
        private bool opened;
        [SerializeField] private Button purchaseButton;
        private GameObject purchasePage;
        private TMP_Text purchaseStatus;
        private readonly Button[] productButtons = new Button[PurchaseManager.ProductIds.Length];
        private static readonly string[] ProductNames =
        {
            "広告削除", "常鳴りの魔寄せ鈴 ＋ 悠久の帰還翼",
            "配合のお守り ×5", "配合のお守り ×50"
        };

        private void Awake()
        {
            EnsureGuideButtons();
            EnsurePurchaseEntry();
            if (recordsButton != null) recordsButton.onClick.AddListener(ShowRecords);
            if (goalsButton != null) goalsButton.onClick.AddListener(ShowGoals);
            if (helpButton != null) helpButton.onClick.AddListener(ShowHelp);
            if (volumeButton != null) volumeButton.onClick.AddListener(ShowVolume);
            if (otherButton != null) otherButton.onClick.AddListener(ShowOther);
            if (creditsButton != null) creditsButton.onClick.AddListener(ShowCredits);
            if (contactButton != null) contactButton.onClick.AddListener(ShowContact);
            if (backButton != null) backButton.onClick.AddListener(Back);
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
        private void OnDisable() { GameAudio.SaveVolume(); if (PurchaseManager.Instance != null) PurchaseManager.Instance.Changed -= RefreshPurchases; Unsubscribe(); opened = false; if (guide != null) guide.gameObject.SetActive(false); }

        private void OnDestroy()
        {
            Unsubscribe();
            if (purchaseButton != null) purchaseButton.onClick.RemoveListener(ShowPurchases);
            if (recordsButton != null) recordsButton.onClick.RemoveListener(ShowRecords);
            if (goalsButton != null) goalsButton.onClick.RemoveListener(ShowGoals);
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowHelp);
            if (volumeButton != null) volumeButton.onClick.RemoveListener(ShowVolume);
            if (otherButton != null) otherButton.onClick.RemoveListener(ShowOther);
            if (creditsButton != null) creditsButton.onClick.RemoveListener(ShowCredits);
            if (contactButton != null) contactButton.onClick.RemoveListener(ShowContact);
            if (backButton != null) backButton.onClick.RemoveListener(Back);
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
            if (PurchaseManager.Instance != null)
            {
                PurchaseManager.Instance.Changed -= RefreshPurchases;
                PurchaseManager.Instance.Changed += RefreshPurchases;
            }
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
        private void EnsurePurchaseEntry()
        {
            if (selectionRoot == null) return;
            if (purchaseButton == null)
                purchaseButton = InventoryUiFactory.Button(selectionRoot.transform, "Purchases", "ショップ・購入の復元",
                    new Vector2(28, -1250), new Vector2(944, 64));
            MatchPurchaseFont(purchaseButton.GetComponentInChildren<TMP_Text>(true));
            purchaseButton.onClick.AddListener(ShowPurchases);
        }

        private void ShowPurchases()
        {
            if (purchasePage == null)
            {
                purchasePage = new GameObject("PurchasePage", typeof(RectTransform));
                purchasePage.transform.SetParent(selectionRoot.transform.parent, false);
                InventoryUiFactory.Stretch((RectTransform)purchasePage.transform);
                string[] descriptions =
                {
                    "広告なしで復活・無料帰還・配合＋15ポイント・放置報酬2倍",
                    "鈴は敵の引き寄せをオン・オフ。翼は消費せず街へ帰還。",
                    "使用を選んだ配合が失敗したとき、1個で両親を保護。",
                    "同じお守りのまとめ買い。"
                };
                for (int i = 0; i < productButtons.Length; i++)
                {
                    string productId = PurchaseManager.ProductIds[i];
                    float top = -225f - i * 212f;
                    var description = InventoryUiFactory.Text(purchasePage.transform, productId + "Description",
                        descriptions[i], new Vector2(28, top), new Vector2(944, 64), 33f);
                    MatchPurchaseFont(description);
                    var button = InventoryUiFactory.Button(purchasePage.transform, productId, "",
                        new Vector2(28, top - 72f), new Vector2(944, 106));
                    button.onClick.AddListener(() => PurchaseManager.Instance?.Buy(productId));
                    MatchPurchaseFont(button.GetComponentInChildren<TMP_Text>(true));
                    productButtons[i] = button;
                }
                var restore = InventoryUiFactory.Button(purchasePage.transform, "RestorePurchases", "購入を復元",
                    new Vector2(28, -1100), new Vector2(944, 104));
                restore.onClick.AddListener(() => PurchaseManager.Instance?.RestorePurchases());
                MatchPurchaseFont(restore.GetComponentInChildren<TMP_Text>(true));
                purchaseStatus = InventoryUiFactory.Text(purchasePage.transform, "PurchaseStatus", "",
                    new Vector2(28, -1220), new Vector2(944, 130), 30f);
                MatchPurchaseFont(purchaseStatus);
            }

            ShowPage("ショップ", purchasePage);
            RefreshPurchases();
        }

        private void MatchPurchaseFont(TMP_Text label)
        {
            if (label != null && titleText != null) label.font = titleText.font;
        }

        private void RefreshPurchases()
        {
            if (purchasePage == null || !purchasePage.activeInHierarchy) return;
            var purchases = PurchaseManager.Instance;
            for (int i = 0; i < productButtons.Length; i++)
            {
                string id = PurchaseManager.ProductIds[i];
                bool owned = purchases?.Owns(id) == true;
                string price = owned ? "購入済み" : purchases?.LocalPrice(id);
                var button = productButtons[i];
                button.interactable = purchases?.CanBuy(id) == true;
                button.GetComponentInChildren<TMP_Text>(true).text = ProductNames[i] +
                    (string.IsNullOrEmpty(price) ? "" : "　" + price);
            }
            purchaseStatus.text = (purchases?.Status ?? "ストアを利用できません。") +
                "\n配合のお守り " + (purchases?.Charms ?? 0L).ToString("N0") + "個";
        }

        public void ShowOther()
        {
            EnsureVolumeSliders();
            ShowPage("その他設定", otherSettingsRoot);
            Refresh();
            if (musicVolumeSlider == null || effectVolumeSlider == null) return;
            musicVolumeSlider.SetValueWithoutNotify(GameAudio.MusicVolume);
            effectVolumeSlider.SetValueWithoutNotify(GameAudio.EffectVolume);
            musicVolumeText.text = $"BGM  {GameAudio.MusicVolume}%";
            effectVolumeText.text = $"SE  {GameAudio.EffectVolume}%";
        }

        public void ShowVolume() => ShowOther();

        private void Back()
        {
            GameAudio.Play(SoundCue.Back);
            ShowSelection();
        }

        private void EnsureVolumeSliders()
        {
            if (otherSettingsRoot == null || musicVolumeSlider != null) return;
            Transform parent = otherSettingsRoot.transform;
            musicVolumeSlider = CreateVolumeSlider(parent, "BgmVolume", 28f, out musicVolumeText);
            effectVolumeSlider = CreateVolumeSlider(parent, "SeVolume", 512f, out effectVolumeText);

            musicVolumeSlider.onValueChanged.AddListener(value =>
            {
                GameAudio.SetMusicVolume(Mathf.RoundToInt(value));
                musicVolumeText.text = $"BGM  {GameAudio.MusicVolume}%";
            });
            effectVolumeSlider.onValueChanged.AddListener(value =>
            {
                GameAudio.SetEffectVolume(Mathf.RoundToInt(value));
                effectVolumeText.text = $"SE  {GameAudio.EffectVolume}%";
            });

            if (saveStatusText != null)
                InventoryUiFactory.TopLeft(saveStatusText.rectTransform, new Vector2(28f, -1390f), new Vector2(944f, 34f));
        }

        private Slider CreateVolumeSlider(Transform parent, string name, float x, out TMP_Text label)
        {
            label = InventoryUiFactory.Text(parent, name + "Label", string.Empty,
                new Vector2(x, -1290f), new Vector2(460f, 34f), 25f);
            if (titleText != null) label.font = titleText.font;

            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider));
            root.transform.SetParent(parent, false);
            var track = root.GetComponent<Image>();
            InventoryUiFactory.TopLeft(track.rectTransform, new Vector2(x, -1330f), new Vector2(460f, 46f));
            track.color = new Color(.11f, .15f, .21f);

            var fillArea = new GameObject("TrackArea", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var trackArea = (RectTransform)fillArea.transform;
            InventoryUiFactory.Stretch(trackArea);
            trackArea.offsetMin = new Vector2(18f, 9f);
            trackArea.offsetMax = new Vector2(-18f, -9f);

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(trackArea, false);
            var fill = fillObject.GetComponent<Image>();
            InventoryUiFactory.Stretch(fill.rectTransform);
            fill.color = new Color(.32f, .68f, .87f);
            fill.raycastTarget = false;

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(trackArea, false);
            var handle = handleObject.GetComponent<Image>();
            InventoryUiFactory.Stretch(handle.rectTransform);
            handle.rectTransform.sizeDelta = new Vector2(30f, 8f);
            handle.color = new Color(.9f, .96f, 1f);
            handle.raycastTarget = false;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            return slider;
        }
        public void ShowCredits() => ShowPage("クレジット（未実装）", placeholderRoot);
        public void ShowContact() => ShowPage("お問合せ（未実装）", placeholderRoot);

        private void ShowPage(string title, GameObject page)
        {
            if (otherSettingsRoot != null && otherSettingsRoot.activeSelf && page != otherSettingsRoot)
                GameAudio.SaveVolume();
            if (purchasePage != null) purchasePage.SetActive(page == purchasePage);
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
        public void Close()
        {
            GameAudio.Play(SoundCue.Back);
            gameObject.SetActive(false);
        }
    }
}
