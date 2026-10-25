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
        Magic,
        MaxHp
    }

    public enum SkillTargetType
    {
        Self,
        SingleEnemy,
        AreaAroundSelf,
        AreaAroundTarget,
        SingleAlly
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

        [Header("共通スキル実行")]
        public bool executable;
        public SkillBaseEffect effect;
        public SkillApplication application;
        [Min(0)] public long flatPower;
        [Min(0)] public float cooldown = 5f, castTime, globalCooldown = 0.2f, range = 6f, radius;
        [Min(0)] public int hitLimit = 1;
        public bool canCritical = true, projectile, homing;
        [Min(0)] public float projectileSpeed = 8f, projectileLifetime = 3f;
        public GameObject effectPrefab;
        [Header("発射物の共通プレファブとCSV上書き")]
        public GameObject projectilePrefab;
        public bool useProjectileMotionOverrides;
        public ProjectileMotionOverride[] projectileMotionOverrides = new ProjectileMotionOverride[0];
        public SkillAffix[] affixes = new SkillAffix[0];

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
