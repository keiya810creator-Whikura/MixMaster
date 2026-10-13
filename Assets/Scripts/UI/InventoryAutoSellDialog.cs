using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    /// <summary>
    /// Auto-sale rule editor. It applies only to newly acquired records;
    /// changing a rule never scans or sells existing inventory.
    /// Built once per inventory, like InventoryBulkSaleDialog.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryAutoSellDialog : MonoBehaviour
    {
        private TMP_Text heading;
        private TMP_Text ruleText;
        private TMP_Text valueText;
        private TMP_Text requiredCountText;
        private TMP_Text noticeText;
        private Toggle enabledToggle;
        private Toggle untitledToggle;
        private GameObject untitledRow;
        private GameObject requiredCountRow;
        private EquipmentManager equipment;
        private MonsterManager monsters;
        private bool monsterMode;
        private bool dirty;

        public static InventoryAutoSellDialog Ensure(Transform parent)
        {
            InventoryAutoSellDialog existing =
                parent.GetComponentInChildren<InventoryAutoSellDialog>(true);
            if (existing != null) return existing;

            GameObject backdrop = new GameObject(
                "AutoSellSettingsOverlay", typeof(RectTransform),
                typeof(Image), typeof(InventoryAutoSellDialog));
            backdrop.transform.SetParent(parent, false);
            InventoryUiFactory.Stretch(backdrop.GetComponent<RectTransform>());
            Image shade = backdrop.GetComponent<Image>();
            shade.color = new Color(0.01f, 0.02f, 0.045f, 0.97f);
            shade.raycastTarget = true;

            GameObject card = new GameObject(
                "SettingsCard", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup));
            card.transform.SetParent(backdrop.transform, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.07f, 0.19f);
            cardRect.anchorMax = new Vector2(0.93f, 0.81f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            card.GetComponent<Image>().color =
                new Color(0.095f, 0.155f, 0.235f, 1f);
            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 20, 20);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            InventoryAutoSellDialog view =
                backdrop.GetComponent<InventoryAutoSellDialog>();
            view.heading = AddLabel(card.transform, "Heading",
                "自動売却設定", 32f, 58f);
            view.heading.alignment = TextAlignmentOptions.Center;
            view.enabledToggle = AddToggle(card.transform, "EnabledToggle",
                "自動売却を利用する");
            view.enabledToggle.onValueChanged.AddListener(view.SetEnabled);
            view.untitledToggle = AddToggle(card.transform, "UntitledToggle",
                "称号なしモンスターを無条件で自動売却");
            view.untitledRow = view.untitledToggle.gameObject;
            view.untitledToggle.onValueChanged.AddListener(view.SetUntitled);

            view.ruleText = AddLabel(card.transform, "RuleLabel",
                "", 23f, 52f);
            view.ruleText.alignment = TextAlignmentOptions.Center;
            AddCounter(card.transform, "ThresholdCounter", true,
                out view.valueText, view.ChangeThreshold);

            view.requiredCountRow = AddCounter(
                card.transform, "RequiredCountCounter", false,
                out view.requiredCountText, view.ChangeCount);

            view.noticeText = AddLabel(card.transform, "Notice",
                "", 20f, 85f);
            view.noticeText.alignment = TextAlignmentOptions.Center;
            view.noticeText.enableWordWrapping = true;

            Button close = InventoryUiFactory.Button(
                card.transform, "CloseAutoSell", "設定を閉じる",
                Vector2.zero, new Vector2(400f, 64f));
            AddLayout(close.gameObject, 64f, 0f);
            close.onClick.AddListener(view.Close);

            backdrop.SetActive(false);
            return view;
        }

        public void OpenEquipment()
        {
            equipment = FindFirstObjectByType<EquipmentManager>();
            if (equipment == null)
            {
                Debug.LogWarning("[AutoSell] EquipmentManager がありません。", this);
                return;
            }
            monsterMode = false;
            dirty = false;
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void OpenMonsters()
        {
            monsters = FindFirstObjectByType<MonsterManager>();
            if (monsters == null)
            {
                Debug.LogWarning("[AutoSell] MonsterManager がありません。", this);
                return;
            }
            monsterMode = true;
            dirty = false;
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Refresh()
        {
            if (monsterMode && monsters != null)
            {
                heading.text = "モンスター自動売却設定";
                enabledToggle.SetIsOnWithoutNotify(monsters.AutoSellEnabled);
                untitledRow.SetActive(true);
                untitledToggle.SetIsOnWithoutNotify(monsters.AutoSellUntitled);
                requiredCountRow.SetActive(true);
                valueText.text = monsters.AutoSellIvThreshold.ToString();
                requiredCountText.text =
                    monsters.AutoSellRequiredStats.ToString();
                ruleText.text = "個体値 " + monsters.AutoSellIvThreshold +
                    " 以上のステータスが";
                noticeText.text = monsters.AutoSellRequiredStats +
                    " か所以上ある個体を残します。\n" +
                    "称号なしをONにすると個体値に関係なく売却します。\n" +
                    "※ 新規入手のみ。編成・お気に入り・ロックは売却しません。";
            }
            else if (!monsterMode && equipment != null)
            {
                heading.text = "装備自動売却設定";
                enabledToggle.SetIsOnWithoutNotify(equipment.AutoSellEnabled);
                untitledRow.SetActive(false);
                requiredCountRow.SetActive(false);
                valueText.text = equipment.AutoSellKeepLevel.ToString();
                ruleText.text = equipment.AutoSellKeepLevel +
                    " Lv以上の装備以外を自動売却";
                noticeText.text =
                    "設定したレベル以上の装備を残します。\n" +
                    "※ 新規入手のみ。装備中・お気に入り・ロックは売却しません。";
            }
        }

        private void SetEnabled(bool enabled)
        {
            if (monsterMode && monsters != null)
                monsters.SetAutoSellSettings(enabled, monsters.AutoSellUntitled,
                    monsters.AutoSellIvThreshold, monsters.AutoSellRequiredStats);
            else if (!monsterMode && equipment != null)
                equipment.SetAutoSellSettings(enabled, equipment.AutoSellKeepLevel);
            dirty = true;
            Refresh();
        }

        private void SetUntitled(bool enabled)
        {
            if (!monsterMode || monsters == null) return;
            monsters.SetAutoSellSettings(monsters.AutoSellEnabled, enabled,
                monsters.AutoSellIvThreshold, monsters.AutoSellRequiredStats);
            dirty = true;
            Refresh();
        }

        private void ChangeThreshold(int delta)
        {
            if (monsterMode && monsters != null)
            {
                monsters.SetAutoSellSettings(monsters.AutoSellEnabled,
                    monsters.AutoSellUntitled,
                    Mathf.Clamp(monsters.AutoSellIvThreshold + delta, 0, 100),
                    monsters.AutoSellRequiredStats);
            }
            else if (!monsterMode && equipment != null)
            {
                equipment.SetAutoSellSettings(equipment.AutoSellEnabled,
                    Mathf.Clamp(equipment.AutoSellKeepLevel + delta,
                        EquipmentManager.MinEquipmentLevel,
                        EquipmentManager.MaxEquipmentLevel));
            }
            dirty = true;
            Refresh();
        }

        private void ChangeCount(int delta)
        {
            if (!monsterMode || monsters == null) return;
            monsters.SetAutoSellSettings(monsters.AutoSellEnabled,
                monsters.AutoSellUntitled, monsters.AutoSellIvThreshold,
                Mathf.Clamp(monsters.AutoSellRequiredStats + delta, 1,
                    MonsterManager.IndividualValueCount));
            dirty = true;
            Refresh();
        }

        public void Close()
        {
            FlushSave();
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // Also save when the whole inventory is closed without using
            // this dialog's own Close button.
            FlushSave();
        }

        private void FlushSave()
        {
            if (!dirty) return;
            dirty = false;
            SaveManager save = FindFirstObjectByType<SaveManager>();
            if (save != null) save.SaveGame();
        }

        private static TMP_Text AddLabel(Transform parent, string name,
            string caption, float size, float height)
        {
            TMP_Text text = InventoryUiFactory.Text(parent, name, caption,
                Vector2.zero, new Vector2(700f, height), size);
            AddLayout(text.gameObject, height, 1f);
            return text;
        }

        private static LayoutElement AddLayout(GameObject obj,
            float height, float flexWidth)
        {
            LayoutElement element = obj.GetComponent<LayoutElement>() ??
                obj.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = flexWidth;
            return element;
        }

        private static Toggle AddToggle(Transform parent, string name,
            string caption)
        {
            GameObject row = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(Toggle),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            Image bg = row.GetComponent<Image>();
            bg.color = new Color(0.16f, 0.24f, 0.32f, 1f);
            Toggle toggle = row.GetComponent<Toggle>();
            toggle.targetGraphic = bg;
            AddLayout(row, 62f, 1f);

            GameObject check = new GameObject("Checkmark",
                typeof(RectTransform), typeof(Image));
            check.transform.SetParent(row.transform, false);
            RectTransform markRect = check.GetComponent<RectTransform>();
            InventoryUiFactory.TopLeft(markRect,
                new Vector2(17f, -14f), new Vector2(34f, 34f));
            Image mark = check.GetComponent<Image>();
            mark.color = new Color(0.40f, 0.9f, 0.55f, 1f);
            mark.raycastTarget = false;
            toggle.graphic = mark;

            TMP_Text label = InventoryUiFactory.Text(
                row.transform, "Label", caption,
                new Vector2(64f, -4f), new Vector2(590f, 55f), 23f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(64f, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }

        private static GameObject AddCounter(Transform parent, string name,
            bool tens, out TMP_Text number, System.Action<int> changed)
        {
            GameObject row = new GameObject(name,
                typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            AddLayout(row, 60f, 1f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            if (tens) AddStepButton(row.transform, "-10", -10, changed);
            AddStepButton(row.transform, "-1", -1, changed);
            number = InventoryUiFactory.Text(
                row.transform, "Value", "0",
                Vector2.zero, new Vector2(120f, 60f), 28f);
            number.alignment = TextAlignmentOptions.Center;
            LayoutElement valueLayout = AddLayout(number.gameObject, 60f, 1f);
            valueLayout.preferredWidth = 120f;
            AddStepButton(row.transform, "+1", 1, changed);
            if (tens) AddStepButton(row.transform, "+10", 10, changed);
            return row;
        }

        private static void AddStepButton(Transform parent,
            string caption, int delta, System.Action<int> changed)
        {
            Button button = InventoryUiFactory.Button(
                parent, "Step" + caption, caption,
                Vector2.zero, new Vector2(100f, 60f));
            LayoutElement element = AddLayout(button.gameObject, 60f, 0f);
            element.preferredWidth = 100f;
            button.onClick.AddListener(() => changed(delta));
        }
    }
}
