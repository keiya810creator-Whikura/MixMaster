using UnityEngine;

namespace MixMaster.Core
{
    // Future equip-slot restrictions can use this category.
    public enum EquipmentCategory
    {
        Any = 0,
        Weapon = 1,
        Armor = 2,
        Accessory = 3
    }

    /// <summary>
    /// Immutable definition shared by all instances of the same equipment ID.
    /// Instance level and original baseValue remain in EquipmentRecord.
    /// Uses the same percentage modifier conventions as TitleSO.
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment_",
        menuName = "MixMaster/Data/Equipment")]
    public sealed class EquipmentSO : ScriptableObject
    {
        [Header("Identity")]
        public string equipmentId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public EquipmentCategory category = EquipmentCategory.Any;

        [Header("Percentage Stat Modifiers")]
        [Tooltip("Same as TitleSO: 0.20 means +20%; -0.10 means -10%. " +
                 "Resistance and drop rate are additive rate values.")]
        public StatPercentageModifiers modifiers =
            new StatPercentageModifiers();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (modifiers == null)
                modifiers = new StatPercentageModifiers();

            if (modifiers.resistanceBonus == null)
                modifiers.resistanceBonus = new ElementResistanceSet();

            modifiers.resistanceBonus.ClampAll();
        }
#endif
    }
}
