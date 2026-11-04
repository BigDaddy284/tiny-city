using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2's gathering and crafting: wood by hand or with an axe, stone with a pickaxe, tools that wear out.</summary>
    public class CraftingTests
    {
        private static TownSimulation Town()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static FreeTimeOption Option(TownSimulation sim, ActivityDefinition activity) =>
            sim.Planner.FreeTimeOptions(sim.Residents[0], everything: true).FirstOrDefault(o => o.Activity == activity);

        private static void Do(TownSimulation sim, ActivityDefinition activity)
        {
            var r = sim.Residents[0];
            for (var i = 0; i < 5 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            var option = Option(sim, activity);
            Assert.That(option, Is.Not.Null, $"{activity.Id} should be an option");
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Crafting", "Right then"), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 600 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), $"{activity.Id} should have finished");
        }

        [Test]
        public void TheOldTown_HasNoGatheringOrCrafting()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            sim.AiRunsEverything = true;
            var options = sim.Planner.FreeTimeOptions(sim.Residents[0], everything: true);
            Assert.That(options.Any(o => o.Activity != null && CraftingRules.IsCrafting(o.Activity)), Is.False);
        }

        [Test]
        public void WoodIsGatheredByHand_ThenAnAxeIsMade_AndGathersMore()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Assert.That(Option(sim, ActivityCatalog.CraftAxe), Is.Null, "no wood yet");
            Assert.That(Option(sim, ActivityCatalog.GatherStone), Is.Null, "stone needs a pickaxe");

            for (var i = 0; i < CraftingRules.WoodFor(ToolKind.Axe); i++)
            {
                Do(sim, ActivityCatalog.GatherWood);
            }

            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(CraftingRules.WoodFor(ToolKind.Axe) * CraftingRules.WoodByHand));
            Do(sim, ActivityCatalog.CraftAxe);
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(0));
            Assert.That(r.Belongings.UsesLeft(ToolKind.Axe), Is.EqualTo(CraftingRules.ToolUses));
            Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.Contains("made an axe")), Is.True);

            r.Belongings.Add(MaterialKind.Wood, 10);
            Assert.That(Option(sim, ActivityCatalog.CraftAxe), Is.Null, "they already have one");
            r.Belongings.Take(MaterialKind.Wood, 10);

            Do(sim, ActivityCatalog.GatherWood);
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(CraftingRules.WoodWithAxe));
            Assert.That(r.Belongings.UsesLeft(ToolKind.Axe), Is.EqualTo(CraftingRules.ToolUses - 1));
        }

        [Test]
        public void StoneNeedsAPickaxe_AndToolsBreakWhenWornOut()
        {
            var sim = Town();
            var r = sim.Residents[0];
            r.Belongings.SetTool(ToolKind.Pickaxe, 1);
            Do(sim, ActivityCatalog.GatherStone);
            Assert.That(r.Belongings.Count(MaterialKind.Stone), Is.EqualTo(CraftingRules.StoneWithPickaxe));
            Assert.That(r.Belongings.Has(ToolKind.Pickaxe), Is.False, "its last use broke it");
            Assert.That(Option(sim, ActivityCatalog.GatherStone), Is.Null);
        }

        [Test]
        public void ARodCatchesMoreFish_AndAHoePicksMoreVegetables()
        {
            var sim = Town();
            var r = sim.Residents[0];
            sim.Tick();
            r.Food.Clear();
            r.Belongings.SetTool(ToolKind.FishingRod, CraftingRules.ToolUses);
            Do(sim, ActivityCatalog.CatchFish);
            Assert.That(r.Food.Count(FoodKind.Fish), Is.EqualTo(FoodRules.CatchPortions + CraftingRules.RodExtraPortions));
            Assert.That(r.Belongings.UsesLeft(ToolKind.FishingRod), Is.EqualTo(CraftingRules.ToolUses - 1));

            r.Belongings.SetTool(ToolKind.Hoe, CraftingRules.ToolUses);
            Do(sim, ActivityCatalog.PlantCrops);
            Do(sim, ActivityCatalog.WaterCrops);
            sim.Advance(24 * 60);
            Do(sim, ActivityCatalog.WaterCrops);
            r.Food.Clear();
            Do(sim, ActivityCatalog.Harvest);
            Assert.That(r.Food.Count(FoodKind.Vegetables), Is.EqualTo(FoodRules.HarvestPortions + CraftingRules.HoeExtraPortions));
        }

        [Test]
        public void Belongings_SurviveASave_AndThePromptMentionsThem()
        {
            var sim = Town();
            var r = sim.Residents[0];
            r.Belongings.Add(MaterialKind.Wood, 5);
            r.Belongings.Add(MaterialKind.Stone, 2);
            r.Belongings.SetTool(ToolKind.Axe, 7);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World).Simulation;
            var lr = loaded.Residents[0];
            Assert.That(lr.Belongings.Count(MaterialKind.Wood), Is.EqualTo(5));
            Assert.That(lr.Belongings.Count(MaterialKind.Stone), Is.EqualTo(2));
            Assert.That(lr.Belongings.Tools.ToList(), Is.EqualTo(r.Belongings.Tools.ToList()));
            Assert.That(r.Belongings.Describe(), Does.Contain("5 wood").And.Contain("axe (7 uses left)"));

            sim.Tick();
            var prompt = DecisionPrompt.UserText(sim, r, sim.Planner.FreeTimeOptions(r, everything: true), new MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("Your things: 5 wood"));
            Assert.That(prompt, Does.Contain("gather-wood"));
        }
    }
}
