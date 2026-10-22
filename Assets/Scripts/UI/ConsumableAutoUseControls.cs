using System.Collections.Generic;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class ConsumableAutoUseControls : MonoBehaviour
    {
        [SerializeField] private ConsumableEffect effect;
        [SerializeField] private Toggle enabledToggle;
        [SerializeField] private TMP_Dropdown potionDropdown;
        [SerializeField] private Slider thresholdSlider;
        [SerializeField] private TMP_Text thresholdText;
        private ConsumableManager manager;
        private readonly List<string> ids = new List<string>();
        private void Awake()
        {
            if (enabledToggle != null) enabledToggle.onValueChanged.AddListener(ToggleChanged);
            if (potionDropdown != null) potionDropdown.onValueChanged.AddListener(PotionChanged);
            if (thresholdSlider != null)
            {
                thresholdSlider.minValue = 0; thresholdSlider.maxValue = 100; thresholdSlider.wholeNumbers = true;
                thresholdSlider.onValueChanged.AddListener(ThresholdChanged);
            }
        }
        private void OnDestroy()
        {
            if (enabledToggle != null) enabledToggle.onValueChanged.RemoveListener(ToggleChanged);
            if (potionDropdown != null) potionDropdown.onValueChanged.RemoveListener(PotionChanged);
            if (thresholdSlider != null) thresholdSlider.onValueChanged.RemoveListener(ThresholdChanged);
        }
        public void Bind(ConsumableManager value)
        {
            if (manager != value || ids.Count == 0)
            {
                manager = value; ids.Clear(); var names = new List<string>();
                if (manager != null && manager.Catalog != null && manager.Catalog.items != null)
                    foreach (var item in manager.Catalog.items)
                        if (item != null && item.effect == effect) { ids.Add(item.itemId); names.Add(item.displayName); }
                if (potionDropdown != null) { potionDropdown.ClearOptions(); potionDropdown.AddOptions(names); }
            }
            if (manager == null) return;
            ConsumableAutoUseRule rule = manager.AutoUseRule(effect);
            if (enabledToggle != null) enabledToggle.SetIsOnWithoutNotify(rule.enabled);
            if (potionDropdown != null) potionDropdown.SetValueWithoutNotify(Mathf.Max(0, ids.IndexOf(rule.itemId)));
            if (thresholdSlider != null) thresholdSlider.SetValueWithoutNotify(rule.thresholdPercent);
            if (thresholdText != null) thresholdText.text = (effect == ConsumableEffect.RestoreHp ? "HP" : "MP") +
                " " + rule.thresholdPercent + "％以下で自動使用";
        }
        private void ToggleChanged(bool value) => Commit();
        private void PotionChanged(int value) => Commit();
        private void ThresholdChanged(float value) => Commit();
        private void Commit()
        {
            int index = potionDropdown != null ? potionDropdown.value : -1;
            if (manager == null || index < 0 || index >= ids.Count) return;
            manager.SetAutoUse(effect, enabledToggle != null && enabledToggle.isOn, ids[index],
                thresholdSlider != null ? Mathf.RoundToInt(thresholdSlider.value) : 50);
            Bind(manager);
        }
    }
}
