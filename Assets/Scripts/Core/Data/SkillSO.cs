using UnityEngine;

namespace MixMaster.Core
{
    public enum SkillCategory
    {
        Active,
        Passive
    }

    public enum SkillPowerSource
    {
        None,
        Attack,
        Magic
    }

    public enum SkillTargetType
    {
        Self,
        SingleEnemy,
        AreaAroundSelf,
        AreaAroundTarget
    }

    [CreateAssetMenu(
        fileName = "Skill_",
        menuName = "MixMaster/Data/Skill")]
    public sealed class SkillSO : ScriptableObject
    {
        [Header("Identity")]
        public string skillId;
        public string displayName;
        [TextArea(2, 6)] public string description;
        public Sprite icon;

        [Header("Acquisition")]
        [Min(0)] public int skillPointCost = 1;
        [Min(1)] public int requiredLevel = 1;

        [Header("Use")]
        public SkillCategory category = SkillCategory.Active;
        public SkillPowerSource powerSource = SkillPowerSource.Attack;
        public SkillTargetType targetType = SkillTargetType.SingleEnemy;
        [Min(0)] public long mpCost = 0;
        [Min(0f)] public float powerMultiplier = 1f;

        [Header("Element")]
        public bool useOwnerElement = true;
        public ElementType overrideElement = ElementType.None;

#if UNITY_EDITOR
        private void OnValidate()
        {
            skillPointCost = Mathf.Max(0, skillPointCost);
            requiredLevel = Mathf.Max(1, requiredLevel);
            mpCost = System.Math.Max(0L, mpCost);
            powerMultiplier = Mathf.Max(0f, powerMultiplier);
        }
#endif
    }
}
