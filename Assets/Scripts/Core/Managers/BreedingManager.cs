using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Immutable snapshot of a breeding attempt's resource requirements.
    /// A step spends 10,000 of the corresponding parent's unique material.
    /// </summary>
    public sealed class BreedingMaterialOffer
    {
        public MonsterSO child;
        public bool isSpecial;
        public MaterialSO materialA;
        public MaterialSO materialB;
        public int stepsA;
        public int stepsB;
        public long requiredA;
        public long requiredB;
        public long ownedA;
        public long ownedB;
        public int successPercent;
    }

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
        [SerializeField] private MaterialInventoryManager materialInventory;

        [Header("Breeding")]
        [SerializeField] private int averageCorrection;

        public const int BaseSuccessPercent = 80;
        public const int BonusPercentPerStep = 2;
        public const int MaxMaterialStepsPerParent = 5;
        public const long MaterialCostPerStep = 10000L;

        // The future no-parent-loss purchase can call SetProtectParentsOnFailure.
        // Defaults to false and must be initialized by the future purchase system.
        private bool protectParentsOnFailure;

        public event Action<OwnedMonsterRecord> MonsterBred;
        public event Action<bool, OwnedMonsterRecord> BreedingAttemptCompleted;

        public void SetProtectParentsOnFailure(bool enabled)
        {
            protectParentsOnFailure = enabled;
        }

        public bool ProtectParentsOnFailure => protectParentsOnFailure;

        public static int GetSuccessPercent(int stepsA, int stepsB)
        {
            return BaseSuccessPercent +
                BonusPercentPerStep * (
                    Mathf.Clamp(stepsA, 0, MaxMaterialStepsPerParent) +
                    Mathf.Clamp(stepsB, 0, MaxMaterialStepsPerParent));
        }

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
            if (materialInventory == null)
                materialInventory = GetComponent<MaterialInventoryManager>() ??
                    FindFirstObjectByType<MaterialInventoryManager>();
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
            if (ownedA.level < 10 || ownedB.level < 10)
            {
                error = "配合の親にはLv.10以上のモンスターが必要です。";
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

        /// <summary>
        /// Previews child, material names, owned quantities, and success rate.
        /// Two parents sharing a material must jointly have enough stock.
        /// </summary>
        public bool TryGetOffer(
            string parentUniqueIdA, string parentUniqueIdB,
            int stepsA, int stepsB,
            out BreedingMaterialOffer offer, out string error)
        {
            offer = null;
            error = string.Empty;
            if (stepsA < 0 || stepsB < 0 ||
                stepsA > MaxMaterialStepsPerParent ||
                stepsB > MaxMaterialStepsPerParent)
            {
                error = "素材の投入は親ごとに0～5回です。";
                return false;
            }

            if (!TryPreviewOwned(parentUniqueIdA, parentUniqueIdB,
                    out MonsterSO child, out bool special, out error))
                return false;

            OwnedMonsterRecord a = monsterManager.FindOwnedMonster(parentUniqueIdA);
            OwnedMonsterRecord b = monsterManager.FindOwnedMonster(parentUniqueIdB);
            MonsterSO speciesA = monsterCatalog.GetMonster(a.monsterId);
            MonsterSO speciesB = monsterCatalog.GetMonster(b.monsterId);
            MaterialSO materialA = speciesA != null ? speciesA.uniqueMaterial : null;
            MaterialSO materialB = speciesB != null ? speciesB.uniqueMaterial : null;
            long requiredA = stepsA * MaterialCostPerStep;
            long requiredB = stepsB * MaterialCostPerStep;
            string materialAId = materialA != null ? materialA.materialId : null;
            string materialBId = materialB != null ? materialB.materialId : null;

            if ((requiredA > 0L && string.IsNullOrWhiteSpace(materialAId)) ||
                (requiredB > 0L && string.IsNullOrWhiteSpace(materialBId)))
            {
                error = "親の固有ドロップ素材が未設定なので投入できません。";
                return false;
            }
            if (materialInventory == null && requiredA + requiredB > 0L)
            {
                error = "MaterialInventoryManagerがありません。";
                return false;
            }
            long ownedA = materialInventory != null
                ? materialInventory.GetAmount(materialAId) : 0L;
            long ownedB = materialInventory != null
                ? materialInventory.GetAmount(materialBId) : 0L;
            bool sameMaterial = requiredA > 0L && requiredB > 0L &&
                string.Equals(materialAId, materialBId,
                    StringComparison.OrdinalIgnoreCase);
            if ((sameMaterial && ownedA < requiredA + requiredB) ||
                (!sameMaterial &&
                 (ownedA < requiredA || ownedB < requiredB)))
            {
                error = "固有素材の所持数が不足しています。";
                return false;
            }

            offer = new BreedingMaterialOffer
            {
                child = child,
                isSpecial = special,
                materialA = materialA,
                materialB = materialB,
                stepsA = stepsA,
                stepsB = stepsB,
                requiredA = requiredA,
                requiredB = requiredB,
                ownedA = ownedA,
                ownedB = ownedB,
                successPercent = (int)Math.Min(100, GetSuccessPercent(stepsA, stepsB) + SkillProgressionManager.PartyBonus(SkillPassiveType.BreedingChance))
            };
            return true;
        }

        /// <summary>
        /// Returns true if an attempt was made, even if the roll failed.
        /// Parents and invested materials are spent on failure by default.
        /// </summary>
        public bool TryBreed(
            string parentUniqueIdA, string parentUniqueIdB,
            int stepsA, int stepsB,
            out OwnedMonsterRecord newborn, out bool succeeded, out string error)
        {
            newborn = null;
            succeeded = false;
            if (!TryGetOffer(parentUniqueIdA, parentUniqueIdB, stepsA, stepsB,
                    out BreedingMaterialOffer offer, out error))
                return false;

            string idA = offer.materialA != null ? offer.materialA.materialId : null;
            string idB = offer.materialB != null ? offer.materialB.materialId : null;
            if (offer.requiredA > 0L || offer.requiredB > 0L)
            {
                if (materialInventory == null ||
                    !materialInventory.TryConsumeBreedingMaterials(
                        idA, offer.requiredA, idB, offer.requiredB))
                {
                    error = "素材の消費に失敗しました。所持数を確認してください。";
                    return false;
                }
            }

            bool successRoll = offer.successPercent >= 100 ||
                UnityEngine.Random.value < offer.successPercent / 100f;
            if (!monsterManager.TryCompleteBreedingAttempt(
                    parentUniqueIdA, parentUniqueIdB, offer.child,
                    successRoll, protectParentsOnFailure, out newborn))
            {
                // Parent validation may have changed in a MaterialChanged
                // subscriber. Refund all committed materials in that case.
                if (offer.requiredA > 0L)
                    materialInventory.AddMaterial(idA, offer.requiredA);
                if (offer.requiredB > 0L)
                    materialInventory.AddMaterial(idB, offer.requiredB);
                error = "親の保護状態が変わったため配合を中止し、素材を返却しました。";
                return false;
            }

            succeeded = successRoll;
            if (succeeded)
                MonsterBred?.Invoke(newborn);
            BreedingAttemptCompleted?.Invoke(succeeded, newborn);

            SaveManager save = GetComponent<SaveManager>() ??
                FindFirstObjectByType<SaveManager>();
            if (save != null && !save.SaveGame())
                Debug.LogWarning("[BreedingManager] 配合結果の保存に失敗しました。", this);

            return true;
        }

        /// <summary>
        /// Compatibility entry: 0 materials. Returns false on a failed roll.
        /// </summary>
        public bool TryBreed(
            string parentUniqueIdA, string parentUniqueIdB,
            out OwnedMonsterRecord newborn, out string error)
        {
            bool attempted = TryBreed(parentUniqueIdA, parentUniqueIdB,
                0, 0, out newborn, out bool succeeded, out error);
            if (attempted && !succeeded)
                error = "配合に失敗しました。親2体は消費されました。";
            return attempted && succeeded;
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
