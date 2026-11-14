using System;
using System.Collections.Generic;
using System.Text;
using MixMaster.Core;

namespace MixMaster.UI
{
    internal static class BreedingGoalContent
    {
        internal static List<DiscoveredBreedingRecipe> ForChild(GameJournalManager journal, string childId)
        {
            var result = new List<DiscoveredBreedingRecipe>();
            if (journal != null)
                foreach (var recipe in journal.DiscoveredRecipes)
                    if (SameId(recipe.childId, childId))
                        result.Add(recipe);
            return result;
        }

        internal static string Name(MonsterCatalogSO catalog, string id)
        {
            MonsterSO monster = catalog != null ? catalog.GetMonster(id) : null;
            return monster != null ? EncyclopediaContent.Name(monster.displayName, id) : id;
        }

        internal static string KindName(BreedingRecipeKind kind)
        {
            switch (kind)
            {
                case BreedingRecipeKind.UpperVariant: return "上位種変異配合";
                case BreedingRecipeKind.UltraSpecial: return "超特殊配合";
                default: return "特殊配合";
            }
        }

        internal static string Describe(GameJournalManager journal, string childId,
            MonsterManager monsters, MonsterCatalogSO catalog, DiscoveredBreedingRecipe selected = null)
        {
            var text = new StringBuilder();
            var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { childId };
            var recipes = selected != null
                ? new List<DiscoveredBreedingRecipe> { selected } : ForChild(journal, childId);
            var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var recipe in recipes)
            {
                if (text.Length > 0) text.AppendLine();
                text.AppendLine(KindName(recipe.kind));
                AppendParent(text, journal, recipe.parentAId, monsters, catalog, path, states, 1);
                AppendParent(text, journal, recipe.parentBId, monsters, catalog, path, states, 1);
            }
            return text.ToString().TrimEnd();
        }

        private static void AppendParent(StringBuilder text, GameJournalManager journal, string id,
            MonsterManager monsters, MonsterCatalogSO catalog, HashSet<string> path,
            Dictionary<string, string> states, int depth)
        {
            if (!states.TryGetValue(id, out string state))
            {
                state = ParentState(id, monsters);
                states.Add(id, state);
            }
            text.Append('　', depth).Append(Name(catalog, id)).Append("　").AppendLine(state);

            // Keep the expanded tree bounded. Every recorded recipe is also
            // directly accessible from its child's encyclopedia entry.
            if (depth >= 4 || !path.Add(id)) return;
            var routes = ForChild(journal, id);
            foreach (var route in routes)
            {
                text.Append('　', depth + 1).AppendLine(KindName(route.kind));
                AppendParent(text, journal, route.parentAId, monsters, catalog, path, states, depth + 1);
                AppendParent(text, journal, route.parentBId, monsters, catalog, path, states, depth + 1);
            }
            path.Remove(id);
        }

        private static string ParentState(string id, MonsterManager monsters)
        {
            int bestLevel = 0;
            bool ready = false;
            if (monsters != null)
            {
                foreach (var record in monsters.OwnedMonsters)
                {
                    if (record == null || !SameId(record.monsterId, id)) continue;
                    bestLevel = Math.Max(bestLevel, record.level);
                    if (record.level >= BreedingManager.MinimumParentLevel &&
                        !monsters.IsProtectedFromConsumption(record)) ready = true;
                }
            }

            int level = BreedingManager.MinimumParentLevel;
            return bestLevel == 0 ? "未所持" : bestLevel < level
                ? $"育成中（Lv.{bestLevel}）"
                : ready ? $"Lv.{level}到達" : $"Lv.{level}到達・編成または保護中";
        }

        private static bool SameId(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
