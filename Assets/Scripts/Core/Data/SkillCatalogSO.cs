using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MixMaster.Core
{
    [CreateAssetMenu(menuName = "MixMaster/Data/スキル一覧")]
    public sealed class SkillCatalogSO : ScriptableObject
    {
        public List<SkillSO> skills = new List<SkillSO>();
        public List<SkillSO> humanSkills = new List<SkillSO>();
        private readonly Dictionary<string, SkillSO> byId = new Dictionary<string, SkillSO>(StringComparer.Ordinal);
        private readonly List<SkillSO> genericMonsterSkills = new List<SkillSO>();
        private List<SkillSO> indexedSkills;
        private int indexedCount = -1;
        private bool hasSignatures;

        public void InvalidateIndex() => indexedCount = -1;

        internal bool UsesMonsterSkillDistribution
        {
            get
            {
                EnsureIndex();
                return hasSignatures && genericMonsterSkills.Count >= 2;
            }
        }

        internal IReadOnlyList<SkillSO> GenericMonsterSkills
        {
            get
            {
                EnsureIndex();
                return genericMonsterSkills;
            }
        }

        public SkillSO Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndex();
            return byId.TryGetValue(id, out var result) ? result : null;
        }

        internal SkillSO GetMonsterSignature(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId) || !monsterId.StartsWith("MON_", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(monsterId.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                return null;

            var skill = Get(number.ToString(CultureInfo.InvariantCulture));
            return skill != null && skill.executable && skill.distribution == SkillDistribution.MonsterSignature
                ? skill : null;
        }

        private void EnsureIndex()
        {
            int count = skills?.Count ?? 0;
            if (indexedSkills == skills && indexedCount == count) return;
            indexedSkills = skills;
            indexedCount = count;
            byId.Clear();
            genericMonsterSkills.Clear();
            hasSignatures = false;
            if (skills == null) return;
            foreach (var skill in skills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.skillId) || !byId.TryAdd(skill.skillId, skill)) continue;
                if (skill.executable && skill.distribution == SkillDistribution.MonsterGeneric)
                    genericMonsterSkills.Add(skill);
                hasSignatures |= skill.executable && skill.distribution == SkillDistribution.MonsterSignature;
            }
        }

#if UNITY_EDITOR
        private void OnValidate() => InvalidateIndex();
#endif

        public static SkillCatalogSO Load() => Resources.Load<SkillCatalogSO>("Skills/SkillCatalog");
    }
}
