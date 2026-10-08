using UnityEngine;

namespace MixMaster.Core
{
    [CreateAssetMenu(
        fileName = "Title_",
        menuName = "MixMaster/Data/Title")]
    public sealed class TitleSO : ScriptableObject
    {
        [Header("Identity")]
        public string titleId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;

        [Header("Roll")]
        [Min(0.0001f)]
        public float selectionWeight = 1f;

        [Header("Percentage Stat Modifiers")]
        [Tooltip("0.20 = +20%, -0.10 = -10%. Resistance and drop rate use additive rate values.")]
        public StatPercentageModifiers modifiers =
            new StatPercentageModifiers();

#if UNITY_EDITOR
        private void OnValidate()
        {
            selectionWeight = Mathf.Max(0.0001f, selectionWeight);

            if (modifiers == null)
                modifiers = new StatPercentageModifiers();

            if (modifiers.resistanceBonus == null)
                modifiers.resistanceBonus = new ElementResistanceSet();

            modifiers.resistanceBonus.ClampAll();
        }
#endif
    }
}
