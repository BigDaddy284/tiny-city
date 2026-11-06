using System.Collections.Generic;
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
    /// <summary>Tiny City 2's gathering and crafting: branches and stones by hand, trees with an axe, rocks with a pickaxe, tools that wear out.</summary>
    public class CraftingTests
    {
        private static TownSimulation Town()
        {
            var sim = TinyCity.Simulation.Content.FlatLand.CreateNewGame();
            // Anyone may do anything here: these tests are about the work, not who may do it.
            sim.Brief = TinyCity.Simulation.Residents.WorldBrief.Empty;
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static FreeTimeOption Option(TownSimulation sim, ActivityDefinition activity) =>
            sim.Planner.FreeTimeOptions(sim.Residents[0]).FirstOrDefault(o => o.Activity == activity);

        private static void Do(TownSimulation sim, ActivityDefinition activity)
        {
            var r = sim.Residents[0];
            for (var i = 0; i < 5 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            var option = Option(sim, activity);
            Assert.That(option, Is.Not.Null, $"{activity.Id} should be an option");
            // Planting is done on open ground: the crop field by the cross path.
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Crafting", null, 20, 23), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 600 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), $"{activity.Id} should have finished");
        }

        [Test]
        public void WoodIsGatheredByHand_ThenAnAxeIsMade_AndGathersMore()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Assert.That(Option(sim, ActivityCatalog.CraftAxe), Is.Null, "no wood yet");
            Assert.That(Option(sim, ActivityCatalog.GatherStone), Is.Null, "stone needs a pickaxe");
            Assert.That(Option(sim, ActivityCatalog.GatherHardwood), Is.Null, "hardwood needs a stone axe");
            Assert.That(Option(sim, ActivityCatalog.GatherWood), Is.Null, "chopping trees needs an axe");
            Assert.That(Option(sim, ActivityCatalog.GatherLooseStones), Is.Not.Null, "loose stones are picked up by hand");

            for (var i = 0; i < 3; i++)
            {
                Do(sim, ActivityCatalog.GatherSticks);
            }

            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(3 * CraftingRules.ByHand));
            Assert.That(r.Log.Any(e => e.Text.EndsWith("by hand")), Is.True);
            Do(sim, ActivityCatalog.CraftAxe);
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(0));
            Assert.That(r.Belongings.TierOf(ToolKind.Axe), Is.EqualTo(1));
            Assert.That(r.Belongings.UsesLeft(ToolKind.Axe), Is.EqualTo(CraftingRules.UsesFor(1)));
            Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.Contains("made a wooden axe")), Is.True);

            r.Belongings.Add(MaterialKind.Wood, 10);
            Assert.That(Option(sim, ActivityCatalog.CraftAxe), Is.Null, "they already have one");
            r.Belongings.Take(MaterialKind.Wood, 10);

            Do(sim, ActivityCatalog.GatherWood);
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(4));
            Assert.That(r.Belongings.UsesLeft(ToolKind.Axe), Is.EqualTo(CraftingRules.UsesFor(1) - 1));
        }

        [Test]
        public void HigherTiers_NeedBetterTools_AndYieldLess()
        {
            var wood = CraftingRules.GatheringFor(ActivityCatalog.GatherWood);
            var hardwood = CraftingRules.GatheringFor(ActivityCatalog.GatherHardwood);
            var heartwood = CraftingRules.GatheringFor(ActivityCatalog.GatherHeartwood);
            var stone = CraftingRules.GatheringFor(ActivityCatalog.GatherStone);
            var iron = CraftingRules.GatheringFor(ActivityCatalog.GatherIron);
            var crystal = CraftingRules.GatheringFor(ActivityCatalog.GatherCrystal);

            Assert.That(new[] { 0, 1, 2, 3 }.Select(CraftingRules.GatheringFor(ActivityCatalog.GatherSticks).YieldWith).ToList(), Is.EqualTo(new List<int> { 1, 1, 1, 1 }), "by hand, whatever they hold");
            Assert.That(new[] { 0, 1, 2, 3 }.Select(CraftingRules.GatheringFor(ActivityCatalog.GatherLooseStones).YieldWith).ToList(), Is.EqualTo(new List<int> { 1, 1, 1, 1 }));
            Assert.That(new[] { 0, 1, 2, 3 }.Select(wood.YieldWith).ToList(), Is.EqualTo(new List<int> { 0, 4, 5, 6 }));
            Assert.That(new[] { 0, 1, 2, 3 }.Select(hardwood.YieldWith).ToList(), Is.EqualTo(new List<int> { 0, 0, 2, 3 }));
            Assert.That(new[] { 0, 1, 2, 3 }.Select(heartwood.YieldWith).ToList(), Is.EqualTo(new List<int> { 0, 0, 0, 1 }));
            Assert.That(new[] { 0, 1, 2, 3 }.Select(stone.YieldWith).ToList(), Is.EqualTo(new List<int> { 0, 3, 4, 5 }));
            Assert.That(new[] { 0, 1, 2, 3 }.Select(iron.YieldWith).ToList(), Is.EqualTo(new List<int> { 0, 0, 2, 3 }));
            Assert.That(new[] { 0, 1, 2, 3 }.Select(crystal.YieldWith).ToList(), Is.EqualTo(new List<int> { 0, 0, 0, 1 }));
            foreach (var g in CraftingRules.Gatherings.Values)
            {
                Assert.That(CraftingRules.TierOf(g.Material), Is.EqualTo(System.Math.Max(1, g.MinTier)), g.Material.ToString());
            }
        }

        [Test]
        public void ToolsAreUpgraded_StoneThenIron_UpToCrystal()
        {
            var sim = Town();
            var r = sim.Residents[0];
            r.Belongings.SetTool(ToolKind.Pickaxe, 10, 1);
            r.Belongings.Add(MaterialKind.Wood, 2);
            Do(sim, ActivityCatalog.GatherStone);
            Assert.That(r.Belongings.Count(MaterialKind.Stone), Is.EqualTo(3));
            Assert.That(Option(sim, ActivityCatalog.GatherIron), Is.Null, "iron needs a stone pickaxe");

            Do(sim, ActivityCatalog.CraftStonePickaxe);
            Assert.That(r.Belongings.TierOf(ToolKind.Pickaxe), Is.EqualTo(2));
            Assert.That(r.Belongings.UsesLeft(ToolKind.Pickaxe), Is.EqualTo(CraftingRules.UsesFor(2)));
            Assert.That(r.Belongings.Count(MaterialKind.Stone), Is.EqualTo(0));
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(0));
            r.Belongings.Add(MaterialKind.Wood, 3);
            Assert.That(Option(sim, ActivityCatalog.CraftPickaxe), Is.Null, "no going back to a worse one");
            r.Belongings.Take(MaterialKind.Wood, 3);

            Do(sim, ActivityCatalog.GatherIron);
            Assert.That(r.Belongings.Count(MaterialKind.Iron), Is.EqualTo(2));
            Assert.That(Option(sim, ActivityCatalog.CraftIronPickaxe), Is.Null, "needs 3 iron and 2 hardwood");
            r.Belongings.Add(MaterialKind.Iron, 1);
            r.Belongings.Add(MaterialKind.Hardwood, 2);
            Do(sim, ActivityCatalog.CraftIronPickaxe);
            Assert.That(r.Belongings.TierOf(ToolKind.Pickaxe), Is.EqualTo(3));

            Do(sim, ActivityCatalog.GatherCrystal);
            Assert.That(r.Belongings.Count(MaterialKind.Crystal), Is.EqualTo(1));
            Do(sim, ActivityCatalog.GatherStone);
            Assert.That(r.Belongings.Count(MaterialKind.Stone), Is.EqualTo(5), "an iron pickaxe breaks more stone");
        }

        [Test]
        public void StoneNeedsAPickaxe_AndToolsBreakWhenWornOut()
        {
            var sim = Town();
            var r = sim.Residents[0];
            r.Belongings.SetTool(ToolKind.Pickaxe, 1);
            Do(sim, ActivityCatalog.GatherStone);
            Assert.That(r.Belongings.Count(MaterialKind.Stone), Is.EqualTo(3));
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
            r.Belongings.SetTool(ToolKind.FishingRod, CraftingRules.UsesFor(1));
            Do(sim, ActivityCatalog.CatchFish);
            Assert.That(r.Food.Count(FoodKind.Fish), Is.EqualTo(FoodRules.CatchPortions + CraftingRules.RodExtraPortions));
            Assert.That(r.Belongings.UsesLeft(ToolKind.FishingRod), Is.EqualTo(CraftingRules.UsesFor(1) - 1));

            r.Belongings.SetTool(ToolKind.Hoe, CraftingRules.UsesFor(1));
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
            r.Belongings.SetTool(ToolKind.Axe, 7, 2);
            r.Belongings.SetTool(ToolKind.Pickaxe, 25, 3);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World).Simulation;
            var lr = loaded.Residents[0];
            Assert.That(lr.Belongings.Count(MaterialKind.Wood), Is.EqualTo(5));
            Assert.That(lr.Belongings.Count(MaterialKind.Stone), Is.EqualTo(2));
            Assert.That(lr.Belongings.Tools.ToList(), Is.EqualTo(r.Belongings.Tools.ToList()));
            Assert.That(r.Belongings.Describe(), Does.Contain("5 wood").And.Contain("stone axe (7 uses left)").And.Contain("iron pickaxe (25 uses left)"));

            sim.Tick();
            var prompt = DecisionPrompt.UserText(sim, r, sim.Planner.FreeTimeOptions(r), new MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("Your things: 5 wood"));
            Assert.That(prompt, Does.Contain("gather-crystal"));
        }
    }
}
