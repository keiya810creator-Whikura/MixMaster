using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class ReincarnationBookEntryUI : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Button favoriteButton;
        [SerializeField] private Image monsterBackground;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text favoriteText;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.21f, 0.29f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.27f, 0.47f, 0.60f, 1f);
        private string bookId;
        private Action<string> select;
        private Action<string> favorite;

        private void Awake()
        {
            if (selectButton != null) selectButton.onClick.AddListener(Select);
            if (favoriteButton != null) favoriteButton.onClick.AddListener(ToggleFavorite);
        }
        private void Select() => select?.Invoke(bookId);
        private void ToggleFavorite() => favorite?.Invoke(bookId);

        public void Bind(string id, string title, Sprite background, bool isFavorite,
            bool isSelected, Action<string> onSelect, Action<string> onFavorite)
        {
            bookId = id;
            select = onSelect;
            favorite = onFavorite;
            if (nameText != null) nameText.text = title;
            if (favoriteText != null) favoriteText.text = isFavorite ? "★" : "☆";
            if (monsterBackground != null)
            {
                monsterBackground.sprite = background;
                monsterBackground.enabled = background != null;
            }
            if (selectionFrame != null)
                selectionFrame.color = isSelected ? selectedColor : normalColor;
        }
    }
}
