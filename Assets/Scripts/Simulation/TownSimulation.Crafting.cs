using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation
{
    /// <summary>Gathering and crafting: three tiers of wood and stone from the resource area, tools in three tiers made at a workbench.</summary>
    public sealed partial class TownSimulation
    {
        private void FinishCrafting(Resident r, ActivityDefinition activity)
        {
            if (!CraftingRules.IsCrafting(activity))
            {
                return;
            }

            var gathering = CraftingRules.GatheringFor(activity);
            if (gathering != null)
            {
                var tier = r.Belongings.TierOf(gathering.Tool);
                var amount = gathering.YieldWith(tier);
                var material = CraftingRules.Name(gathering.Material);
                if (amount <= 0)
                {
                    r.AddLog(Time, $"Couldn't get any {material} without a better {CraftingRules.Name(gathering.Tool)}");
                    return;
                }

                var kept = r.Belongings.Add(gathering.Material, amount);
                r.AddLog(Time, $"Gathered {kept} {material}" + (gathering.MinTier > 0 ? FoodRules.WearNote(r, gathering.Tool) : " by hand"));
                if (CraftingRules.TierOf(gathering.Material) == CraftingRules.MaxTier && kept > 0)
                {
                    Memories.Add(r.Id, Time, MemoryType.Event, 4, $"I gathered {kept} {material}, the rarest kind.", tags: new[] { "crafting", gathering.Material.ToString() });
                }

                return;
            }

            var recipe = CraftingRules.RecipeFor(activity);
            if (r.Belongings.TierOf(recipe.Tool) >= recipe.Tier || !r.Belongings.Pay(recipe))
            {
                r.AddLog(Time, $"Couldn't make {CraftingRules.WithArticle(recipe.Tool, recipe.Tier)}: not enough materials");
                return;
            }

            r.Belongings.SetTool(recipe.Tool, CraftingRules.UsesFor(recipe.Tier), recipe.Tier);
            var made = CraftingRules.WithArticle(recipe.Tool, recipe.Tier);
            r.AddLog(Time, $"Made {made}");
            Memories.Add(r.Id, Time, MemoryType.Event, 3 + recipe.Tier, $"I made {made} at the workbench.", tags: new[] { "crafting", recipe.Tool.ToString() });
        }
    }
}
