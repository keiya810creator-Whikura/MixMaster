using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class SkillDetailsPopupUI : MonoBehaviour
    {
        public const string ResourcePath = "UI/SkillDetails/SkillDetailsPopup";
        [SerializeField] private Button closeButton;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text heading, description;
        [SerializeField] private ScrollRect scroll;
        private void Awake() { if (closeButton != null) closeButton.onClick.AddListener(Hide); }
        private void OnDestroy() { if (closeButton != null) closeButton.onClick.RemoveListener(Hide); }
        public static SkillDetailsPopupUI Create(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || parent == null) { Debug.LogWarning("[SkillDetails] スキル詳細Prefabが見つかりません。"); return null; }
            return Instantiate(prefab, parent, false).GetComponent<SkillDetailsPopupUI>();
        }
        public void Show(SkillSO skill)
        {
            if (skill == null) return;
            if (heading != null) heading.text = EncyclopediaContent.Name(skill.displayName, skill.skillId);
            if (description != null) description.text = EncyclopediaContent.SkillDetails(skill);
            if (icon != null) { icon.sprite = skill.icon; icon.enabled = skill.icon != null; }
            gameObject.SetActive(true); transform.SetAsLastSibling();
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
        }
        public void Hide() => gameObject.SetActive(false);
    }
}
