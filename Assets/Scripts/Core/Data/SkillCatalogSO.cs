using System.Collections.Generic;
using UnityEngine;
namespace MixMaster.Core
{
    [CreateAssetMenu(menuName = "MixMaster/Data/スキル一覧")]
    public sealed class SkillCatalogSO : ScriptableObject
    {
        public List<SkillSO> skills = new List<SkillSO>();
        public List<SkillSO> humanSkills = new List<SkillSO>();
        public SkillSO Get(string id) => skills.Find(s => s != null && s.skillId == id);
        public static SkillCatalogSO Load() => Resources.Load<SkillCatalogSO>("Skills/SkillCatalog");
    }
}
