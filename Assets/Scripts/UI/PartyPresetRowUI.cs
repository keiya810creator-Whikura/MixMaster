using System;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class PartyPresetRowUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text numberLabel, nameLabel;
        [SerializeField] private Image[] memberIcons;
        [SerializeField] private Color selectedColor = new Color(.12f, .42f, .48f, 1f);
        private Color normalColor;
        private int slot;
        private Action<int> selected;
        private void Awake()
        {
            normalColor = background != null ? background.color : Color.white;
            if (button != null) button.onClick.AddListener(Select);
        }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Select); }
        private void Select() => selected?.Invoke(slot);
        public void Bind(int index, PartyPresetRecord preset, bool isSelected, MonsterCatalogSO catalog, Action<int> onSelected)
        {
            slot = index; selected = onSelected;
            if (numberLabel != null) numberLabel.text = (index + 1).ToString("00");
            if (nameLabel != null) nameLabel.text = preset != null ? preset.name : "未保存";
            if (background != null) background.color = isSelected ? selectedColor : normalColor;
            if (memberIcons == null) return;
            for (int i = 0; i < memberIcons.Length; i++)
            {
                Image image = memberIcons[i]; if (image == null) continue;
                PartyPresetActor member = preset?.members != null && i < preset.members.Count ? preset.members[i] : null;
                MonsterSO monster = member != null && catalog != null ? catalog.GetMonster(member.monsterId) : null;
                image.sprite = monster != null ? monster.icon != null ? monster.icon : monster.sprite : null;
                image.enabled = image.sprite != null;
            }
        }
    }
}
