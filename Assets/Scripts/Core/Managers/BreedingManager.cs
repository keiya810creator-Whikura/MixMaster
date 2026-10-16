using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Deterministic breeding: same species, then special recipe, then the
    /// closest breeding value among species up to one stage beyond both parents.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreedingManager : MonoBehaviour
    {
        [Header("References (optional: auto-loaded when empty)")]
        [SerializeField] private MonsterManager monsterManager;
        [SerializeField] private MonsterCatalogSO monsterCatalog;
        [SerializeField] private BreedingRecipeCatalogSO specialRecipes;

        [Header("Breeding")]
        [SerializeField] private int averageCorrection;

        public event Action<OwnedMonsterRecord> MonsterBred;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void EnsureDependencies()
        {
            if (monsterManager == null)
                monsterManager = GetComponent<MonsterManager>() ??
                    FindFirstObjectByType<MonsterManager>();
            if (monsterCatalog == null)
                monsterCatalog = MonsterCatalogSO.Load();
            if (specialRecipes == null)
                specialRecipes = BreedingRecipeCatalogSO.Load();
        }

        /// <summary>
        /// Existing API kept for scripts that already use it.
        /// Calculation uses long to avoid integer overflow.
        /// </summary>
        public int CalculateChildBreedingValue(
            int parentAValue, int parentBValue, int correction)
        {
            long average = ((long)parentAValue + parentBValue) / 2L;
            long result = average + correction;
            if (result > int.MaxValue) return int.MaxValue;
            if (result < int.MinValue) return int.MinValue;
            return (int)result;
        }

        public int CalculateChildBreedingValue(MonsterSO parentA, MonsterSO parentB)
        {
            if (parentA == null || parentB == null)
                return 0;
            return CalculateChildBreedingValue(
                parentA.breedingValue, parentB.breedingValue, averageCorrection);
        }

        public bool TryResolveChild(
            MonsterSO parentA, MonsterSO parentB,
            out MonsterSO child, out bool isSpecialRecipe)
        {
            child = null;
            isSpecialRecipe = false;
            if (parentA == null || parentB == null ||
                string.IsNullOrWhiteSpace(parentA.monsterId) ||
                string.IsNullOrWhiteSpace(parentB.monsterId))
                return false;

            // Same species always breeds back into itself.
            if (string.Equals(parentA.monsterId, parentB.monsterId,
                    StringComparison.OrdinalIgnoreCase))
            {
                child = parentA;
                return true;
            }

            EnsureDependencies();

            if (specialRecipes != null &&
                specialRecipes.TryFindRecipe(
                    parentA.monsterId, parentB.monsterId, out string childId))
            {
                if (monsterCatalog == null)
                    return false;
                child = monsterCatalog.GetMonster(childId);
                isSpecialRecipe = child != null;
                return child != null;
            }

            if (monsterCatalog == null)
                return false;

            int maxParentStage = Mathf.Max(
                Mathf.Max(1, parentA.appearanceStage),
                Mathf.Max(1, parentB.appearanceStage));
            int maximumStage = maxParentStage < int.MaxValue
                ? maxParentStage + 1 : int.MaxValue;
            long target = CalculateChildBreedingValue(parentA, parentB);
            long closest = long.MaxValue;
            var tiedCandidates = new List<MonsterSO>();

            IReadOnlyList<MonsterSO> monsters = monsterCatalog.Monsters;
            if (monsters == null)
                return false;

            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterSO candidate = monsters[i];
                if (candidate == null ||
                    string.IsNullOrWhiteSpace(candidate.monsterId) ||
                    candidate.appearanceStage < 1 ||
                    candidate.appearanceStage > maximumStage ||
                    candidate.specialBreedingOnly)
                    continue;

                long distance = Math.Abs((long)candidate.breedingValue - target);
                if (distance > closest)
                    continue;

                if (distance < closest)
                {
                    tiedCandidates.Clear();
                    closest = distance;
                }
                tiedCandidates.Add(candidate);
            }

            if (tiedCandidates.Count == 0)
                return false;

            // A pair produces one stable species even if several monsters share
            // a breeding value. Parent ordering never changes the result.
            tiedCandidates.Sort((a, b) =>
                string.Compare(a.monsterId, b.monsterId, StringComparison.OrdinalIgnoreCase));
            uint hash = StablePairHash(parentA.monsterId, parentB.monsterId);
            child = tiedCandidates[(int)(hash % (uint)tiedCandidates.Count)];
            return true;
        }

        public bool TryPreviewOwned(
            string parentUniqueIdA, string parentUniqueIdB,
            out MonsterSO child, out bool special, out string error)
        {
            child = null;
            special = false;
            error = string.Empty;
            EnsureDependencies();

            if (monsterManager == null || monsterCatalog == null)
            {
                error = "MonsterManager または MonsterCatalog がありません。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(parentUniqueIdA) ||
                string.IsNullOrWhiteSpace(parentUniqueIdB) ||
                string.Equals(parentUniqueIdA, parentUniqueIdB, StringComparison.Ordinal))
            {
                error = "異なる所持モンスター2体を選択してください。";
                return false;
            }

            OwnedMonsterRecord ownedA = monsterManager.FindOwnedMonster(parentUniqueIdA);
            OwnedMonsterRecord ownedB = monsterManager.FindOwnedMonster(parentUniqueIdB);
            if (ownedA == null || ownedB == null)
            {
                error = "親モンスターが所持一覧にありません。";
                return false;
            }
            if (monsterManager.IsProtectedFromConsumption(ownedA) ||
                monsterManager.IsProtectedFromConsumption(ownedB))
            {
                error = "パーティ編成中・ロック・お気に入りの親は配合できません。";
                return false;
            }

            MonsterSO speciesA = monsterCatalog.GetMonster(ownedA.monsterId);
            MonsterSO speciesB = monsterCatalog.GetMonster(ownedB.monsterId);
            if (!TryResolveChild(speciesA, speciesB, out child, out special))
            {
                error = "配合結果を決定できません。MonsterCatalogと配合レシピを確認してください。";
                return false;
            }
            return true;
        }

        public bool TryBreed(
            string parentUniqueIdA, string parentUniqueIdB,
            out OwnedMonsterRecord newborn, out string error)
        {
            newborn = null;
            if (!TryPreviewOwned(parentUniqueIdA, parentUniqueIdB,
                    out MonsterSO child, out _, out error))
                return false;

            if (!monsterManager.TryCompleteBreeding(
                    parentUniqueIdA, parentUniqueIdB, child, out newborn))
            {
                error = "所持モンスターの保護状態が変わったため、配合を中止しました。";
                return false;
            }

            MonsterBred?.Invoke(newborn);
            SaveManager save = GetComponent<SaveManager>() ??
                FindFirstObjectByType<SaveManager>();
            if (save != null && !save.SaveGame())
                Debug.LogWarning("[BreedingManager] 配合は成功しましたが保存に失敗しました。", this);
            return true;
        }

        private static uint StablePairHash(string a, string b)
        {
            if (string.Compare(a, b, StringComparison.OrdinalIgnoreCase) > 0)
            {
                string tmp = a;
                a = b;
                b = tmp;
            }

            string canonical = a.ToUpperInvariant() + "|" + b.ToUpperInvariant();
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < canonical.Length; i++)
                {
                    hash ^= canonical[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
