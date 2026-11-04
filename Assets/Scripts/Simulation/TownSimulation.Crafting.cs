using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation
{
    /// <summary>Tiny City 2's gathering and crafting: wood and stone from the resource area, tools made at a workbench.</summary>
    public sealed partial class TownSimulation
    {
        private void FinishCrafting(Resident r, ActivityDefinition activity)
        {
            if (!FoodMustBeMade || !CraftingRules.IsCrafting(activity))
            {
                return;
            }

            if (activity == ActivityCatalog.GatherWood)
            {
                var axe = r.Belongings.Has(ToolKind.Axe);
                var kept = r.Belongings.Add(MaterialKind.Wood, axe ? CraftingRules.WoodWithAxe : CraftingRules.WoodByHand);
                r.AddLog(Time, $"Gathered {kept} wood" + (axe ? FoodRules.WearNote(r, ToolKind.Axe) : " by hand"));
                return;
            }

            if (activity == ActivityCatalog.GatherStone)
            {
                if (!r.Belongings.Has(ToolKind.Pickaxe))
                {
                    r.AddLog(Time, "Couldn't break any stone without a pickaxe");
                    return;
                }

                var kept = r.Belongings.Add(MaterialKind.Stone, CraftingRules.StoneWithPickaxe);
                r.AddLog(Time, $"Broke off {kept} stone" + FoodRules.WearNote(r, ToolKind.Pickaxe));
                return;
            }

            var tool = CraftingRules.ToolMadeBy(activity).Value;
            if (r.Belongings.Has(tool) || !r.Belongings.Take(MaterialKind.Wood, CraftingRules.WoodFor(tool)))
            {
                r.AddLog(Time, $"Couldn't make {CraftingRules.WithArticle(tool)}: not enough wood");
                return;
            }

            r.Belongings.SetTool(tool, CraftingRules.ToolUses);
            r.AddLog(Time, $"Made {CraftingRules.WithArticle(tool)}");
            Memories.Add(r.Id, Time, MemoryType.Event, 4, $"I made {CraftingRules.WithArticle(tool)} at the workbench.", tags: new[] { "crafting", tool.ToString() });
        }
    }
}
