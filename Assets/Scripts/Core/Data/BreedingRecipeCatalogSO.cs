using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    [Serializable]
    public sealed class SpecialBreedingRecipe
    {
        public string parentAId;
        public string parentBId;
        public string childId;
    }

    [CreateAssetMenu(fileName = "BreedingRecipes",
        menuName = "MixMaster/Data/Breeding Recipes")]
    public sealed class BreedingRecipeCatalogSO : ScriptableObject
    {
        public const string ResourcesPath = "Data/BreedingRecipes";
        [SerializeField] private List<SpecialBreedingRecipe> recipes =
            new List<SpecialBreedingRecipe>();

        private static BreedingRecipeCatalogSO cached;
        public IReadOnlyList<SpecialBreedingRecipe> Recipes => recipes;

        public static BreedingRecipeCatalogSO Load()
        {
            if (cached == null)
                cached = Resources.Load<BreedingRecipeCatalogSO>(ResourcesPath);
            return cached;
        }

        public bool TryFindRecipe(string parentAId, string parentBId,
            out string childId)
        {
            childId = null;
            if (string.IsNullOrWhiteSpace(parentAId) ||
                string.IsNullOrWhiteSpace(parentBId))
                return false;

            for (int i = 0; i < recipes.Count; i++)
            {
                SpecialBreedingRecipe recipe = recipes[i];
                if (recipe == null)
                    continue;
                bool direct =
                    string.Equals(recipe.parentAId, parentAId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(recipe.parentBId, parentBId, StringComparison.OrdinalIgnoreCase);
                bool reversed =
                    string.Equals(recipe.parentAId, parentBId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(recipe.parentBId, parentAId, StringComparison.OrdinalIgnoreCase);
                if (!direct && !reversed)
                    continue;
                childId = recipe.childId;
                return !string.IsNullOrWhiteSpace(childId);
            }

            return false;
        }

#if UNITY_EDITOR
        public void SetRecipes(List<SpecialBreedingRecipe> newRecipes)
        {
            recipes = newRecipes ?? new List<SpecialBreedingRecipe>();
        }
#endif
    }
}
