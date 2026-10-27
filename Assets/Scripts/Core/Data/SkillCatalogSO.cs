using System.Collections.Generic;
using UnityEngine;
namespace MixMaster.Core
{
    [CreateAssetMenu(menuName = "MixMaster/Data/スキル一覧")]
    public sealed class SkillCatalogSO : ScriptableObject
    {
        public List<SkillSO> skills = new List<SkillSO>();
        public List<SkillSO> humanSkills = new List<SkillSO>();
        private readonly Dictionary<string, SkillSO> byId = new Dictionary<string, SkillSO>();
        private int indexedFrame = -1, indexedCount = -1;
        private List<SkillSO> indexedSkills;
        public SkillSO Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int count = skills != null ? skills.Count : 0;
            if (indexedFrame != Time.frameCount || indexedSkills != skills || indexedCount != count)
            {
                byId.Clear(); indexedFrame = Time.frameCount; indexedSkills = skills; indexedCount = count;
                if (skills != null) foreach (var skill in skills)
                    if (skill != null && !string.IsNullOrEmpty(skill.skillId) && !byId.ContainsKey(skill.skillId))
                        byId.Add(skill.skillId, skill);
            }
            return byId.TryGetValue(id, out var result) ? result : null;
        }
        public static SkillCatalogSO Load() => Resources.Load<SkillCatalogSO>("Skills/SkillCatalog");
    }
}
