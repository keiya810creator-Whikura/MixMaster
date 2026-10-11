using System;
using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class PartyFormationBarUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text partyCountText;
        [SerializeField] private Button[] slotButtons = new Button[3];
        [SerializeField] private Image[] slotIcons = new Image[3];
        [SerializeField] private TMP_Text[] slotNames = new TMP_Text[3];
        [SerializeField] private Button clearAllButton;

        private readonly Color[] emptySlotColors = new Color[3];

        private MonsterManager manager;
        private MonsterCatalogSO catalog;
        private OwnedMonsterListUI listUI;
        private bool isSubscribed;

        private void Awake()
        {
            listUI = GetComponentInParent<OwnedMonsterListUI>();
            EnsureClearAllButton();
            if (clearAllButton != null)
                clearAllButton.onClick.AddListener(ClearAllParty);

            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null)
                    continue;

                if (i < emptySlotColors.Length &&
                    slotButtons[i].targetGraphic is Image background)
                    emptySlotColors[i] = background.color;

                int index = i;
                slotButtons[i].onClick.AddListener(
                    () => OpenMemberAt(index));
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (clearAllButton != null)
                clearAllButton.onClick.RemoveListener(ClearAllParty);
        }

        private void ClearAllParty()
        {
            if (manager == null)
                manager = FindFirstObjectByType<MonsterManager>();

            manager?.ClearParty();
        }

        private void EnsureClearAllButton()
        {
            if (clearAllButton != null)
                return;

            Transform existing = transform.Find("ClearAllPartyButton");
            if (existing != null)
                clearAllButton = existing.GetComponent<Button>();

            if (clearAllButton != null)
                return;

            GameObject go = new GameObject(
                "ClearAllPartyButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-12f, -7f);
            rect.sizeDelta = new Vector2(165f, 38f);

            Image background = go.GetComponent<Image>();
            background.color = new Color(0.36f, 0.23f, 0.24f, 1f);

            GameObject labelObject = new GameObject(
                "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(go.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "編成一括解除";
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            clearAllButton = go.GetComponent<Button>();
        }

        public void SetReferences(
            TMP_Text count,
            Button[] buttons,
            Image[] icons,
            TMP_Text[] names)
        {
            partyCountText = count;
            slotButtons = buttons;
            slotIcons = icons;
            slotNames = names;
        }

        private void Subscribe()
        {
            if (manager == null)
                manager = FindFirstObjectByType<MonsterManager>();

            if (manager == null || isSubscribed)
                return;

            manager.PartyChanged += Refresh;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
                return;

            if (manager != null)
                manager.PartyChanged -= Refresh;

            isSubscribed = false;
        }

        public void Refresh()
        {
            Subscribe();

            if (catalog == null)
                catalog = MonsterCatalogSO.Load();

            if (manager == null)
                return;

            IReadOnlyList<string> ids = manager.PartyMonsterUniqueIds;
            if (clearAllButton != null)
                clearAllButton.interactable = ids.Count > 0;

            if (partyCountText != null)
                partyCountText.text =
                    "パーティ " + ids.Count + "/" +
                    manager.MaxPartySize +
                    "　（仲間をタップして編成）";

            for (int i = 0; i < 3; i++)
            {
                OwnedMonsterRecord record =
                    i < ids.Count
                        ? manager.FindOwnedMonster(ids[i])
                        : null;

                MonsterSO monster = record != null && catalog != null
                    ? catalog.GetMonster(record.monsterId)
                    : null;

                Sprite sprite = monster != null
                    ? (monster.icon != null
                        ? monster.icon
                        : monster.sprite)
                    : null;

                if (slotIcons != null && i < slotIcons.Length &&
                    slotIcons[i] != null)
                {
                    slotIcons[i].sprite = sprite;
                    slotIcons[i].enabled = sprite != null;
                }

                if (slotNames != null && i < slotNames.Length &&
                    slotNames[i] != null)
                {
                    string displayName = monster != null
                        ? (!string.IsNullOrWhiteSpace(monster.displayName)
                            ? monster.displayName
                            : record.monsterId)
                        : (record != null ? record.monsterId : "空き枠");

                    TitleSO title = record != null && catalog != null
                        ? catalog.GetTitle(record.titleId)
                        : null;

                    if (title != null)
                    {
                        string titleName = !string.IsNullOrWhiteSpace(
                            title.displayName) ? title.displayName : title.titleId;

                        displayName = "《" + titleName + "》" + displayName;
                    }

                    slotNames[i].text = (i + 1) + ". " + displayName +
                        (record != null ? " Lv." + record.level : "");
                }

                if (slotButtons != null && i < slotButtons.Length &&
                    slotButtons[i] != null)
                {
                    Button button = slotButtons[i];
                    button.interactable = record != null;

                    // Same colors as party drop/damage text (red/blue/green).
                    Color slotColor = i < emptySlotColors.Length
                        ? emptySlotColors[i] : Color.white;
                    if (record != null)
                    {
                        MaterialDropSourceType source = i == 0
                            ? MaterialDropSourceType.Party1
                            : i == 1
                                ? MaterialDropSourceType.Party2
                                : MaterialDropSourceType.Party3;
                        slotColor = MaterialDropLogUI.GetSourceColor(source);
                    }

                    if (button.targetGraphic is Image background)
                        background.color = slotColor;
                }
            }
        }

        private void OpenMemberAt(int index)
        {
            if (manager == null || index < 0 ||
                index >= manager.PartyMonsterUniqueIds.Count)
                return;

            OwnedMonsterRecord record =
                manager.FindOwnedMonster(
                    manager.PartyMonsterUniqueIds[index]);

            if (record == null)
                return;

            if (listUI == null)
                listUI = GetComponentInParent<OwnedMonsterListUI>();

            if (listUI != null)
                listUI.OpenDetail(record);
        }
    }
}
