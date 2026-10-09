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

        private MonsterManager manager;
        private MonsterCatalogSO catalog;
        private OwnedMonsterListUI listUI;
        private bool isSubscribed;

        private void Awake()
        {
            listUI = GetComponentInParent<OwnedMonsterListUI>();
            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i] == null)
                    continue;

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

                    slotNames[i].text = (i + 1) + ". " + displayName +
                        (record != null ? " Lv." + record.level : "");
                }

                if (slotButtons != null && i < slotButtons.Length &&
                    slotButtons[i] != null)
                {
                    slotButtons[i].interactable = record != null;
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
