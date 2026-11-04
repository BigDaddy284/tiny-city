using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2's food: nothing free, so it is grown, watered, picked, caught, cooked, eaten and goes off.</summary>
    public class FoodTests
    {
        private static TownSimulation Town(double hunger = 90)
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Hunger] = hunger);
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static FreeTimeOption Option(TownSimulation sim, ActivityDefinition activity) =>
            sim.Planner.FreeTimeOptions(sim.Residents[0], everything: true).FirstOrDefault(o => o.Activity == activity);

        /// <summary>Waits for the resident to decide, chooses this activity, and runs until they are deciding again.</summary>
        private static void Do(TownSimulation sim, ActivityDefinition activity)
        {
            var r = sim.Residents[0];
            for (var i = 0; i < 5 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            var option = Option(sim, activity);
            Assert.That(option, Is.Not.Null, $"{activity.Id} should be an option");
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Food", "Right then"), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 600 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), $"{activity.Id} should have finished");
        }

        [Test]
        public void TheOldTown_KeepsItsFreeFood_AndHasNoneOfTheNew()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Hunger] = 30);
            sim.AiRunsEverything = true;
            var options = sim.Planner.FreeTimeOptions(sim.Residents[0], everything: true);
            Assert.That(options.Any(o => o.Activity == ActivityCatalog.Snack), Is.True);
            Assert.That(options.Any(o => o.Activity != null && FoodRules.IsMadeFood(o.Activity)), Is.False);
        }

        [Test]
        public void NothingIsFree_AndTheCupboardBecomesCookedMeals()
        {
            var sim = Town(hunger: 30);
            var r = sim.Residents[0];
            var groceries = r.Groceries;
            Assert.That(groceries, Is.GreaterThan(0));
            sim.Tick();

            Assert.That(r.Groceries, Is.EqualTo(0));
            Assert.That(r.Food.Count(FoodKind.Meal), Is.EqualTo(groceries));
            var options = sim.Planner.FreeTimeOptions(r, everything: true);
            Assert.That(options.Any(o => o.Activity != null && FoodRules.IsFreeFood(o.Activity)), Is.False);
            Assert.That(options.Any(o => o.Activity == ActivityCatalog.EatMeal), Is.True);
            Assert.That(options.Any(o => o.Activity == ActivityCatalog.EatRaw), Is.False, "nothing raw yet");
            Assert.That(options.Any(o => o.Activity == ActivityCatalog.PlantCrops), Is.True);
            Assert.That(options.Any(o => o.Activity == ActivityCatalog.CatchFish), Is.True);

            Do(sim, ActivityCatalog.EatMeal);
            Assert.That(r.Food.Count(FoodKind.Meal), Is.EqualTo(groceries - 1));
        }

        [Test]
        public void ACrop_IsPlanted_WateredOnTwoDays_AndPicked()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlantCrops);
            var plot = StarterTown.GardenPlotId;
            Assert.That(sim.Food.CropAt(plot), Is.Not.Null);
            Assert.That(Option(sim, ActivityCatalog.PlantCrops), Is.Null, "the plot is taken");
            Assert.That(Option(sim, ActivityCatalog.Harvest), Is.Null, "not ready yet");

            Do(sim, ActivityCatalog.WaterCrops);
            Assert.That(sim.Food.CropAt(plot).WateredDays, Is.EqualTo(1));
            Assert.That(Option(sim, ActivityCatalog.WaterCrops), Is.Null, "once a day");

            sim.Advance(24 * 60);
            Do(sim, ActivityCatalog.WaterCrops);
            Assert.That(sim.Food.CropAt(plot).IsReady, Is.True);

            var before = r.Food.Count(FoodKind.Vegetables);
            Do(sim, ActivityCatalog.Harvest);
            Assert.That(r.Food.Count(FoodKind.Vegetables), Is.EqualTo(before + FoodRules.HarvestPortions));
            Assert.That(sim.Food.CropAt(plot), Is.Null);
            Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.StartsWith("I picked")), Is.True);
        }

        [Test]
        public void FishAreCaught_CookedIntoMeals_AndRawFoodCanBeEaten()
        {
            var sim = Town();
            var r = sim.Residents[0];
            sim.Tick();
            r.Food.Clear();
            Do(sim, ActivityCatalog.CatchFish);
            Assert.That(r.Food.Count(FoodKind.Fish), Is.EqualTo(FoodRules.CatchPortions));

            Do(sim, ActivityCatalog.EatRaw);
            Assert.That(r.Food.Count(FoodKind.Fish), Is.EqualTo(FoodRules.CatchPortions - 1));

            Do(sim, ActivityCatalog.Cook);
            Assert.That(r.Food.Raw, Is.EqualTo(0));
            Assert.That(r.Food.Count(FoodKind.Meal), Is.EqualTo(FoodRules.CatchPortions - 1));
        }

        [Test]
        public void FoodGoesOff_AtFourInTheMorning()
        {
            var sim = Town();
            var r = sim.Residents[0];
            sim.Tick();
            r.Food.Clear();
            var tomorrow = sim.Time.Day + 1;
            r.Food.Add(FoodKind.Fish, 2, tomorrow);
            r.Food.Add(FoodKind.Vegetables, 3, sim.Time.Day + 5);
            while (!(sim.Time.Day == tomorrow && sim.Time.MinuteOfDay == FoodRules.SpoilMinute + 1))
            {
                sim.Tick();
            }

            Assert.That(r.Food.Count(FoodKind.Fish), Is.EqualTo(0));
            Assert.That(r.Food.Count(FoodKind.Vegetables), Is.EqualTo(3));
        }

        [Test]
        public void Stock_IsUsedSoonestToGoOffFirst_AndHasALimit()
        {
            var stock = new FoodStock();
            stock.Add(FoodKind.Vegetables, 2, 9);
            stock.Add(FoodKind.Fish, 2, 4);
            stock.Add(FoodKind.Meal, 1, 6);
            Assert.That(stock.TakeRaw(3), Is.EqualTo(3));
            Assert.That(stock.Count(FoodKind.Fish), Is.EqualTo(0));
            Assert.That(stock.Count(FoodKind.Vegetables), Is.EqualTo(1));
            Assert.That(stock.Count(FoodKind.Meal), Is.EqualTo(1));
            Assert.That(stock.Describe(5), Does.Contain("1 cooked meal").And.Contain("1 portion of vegetables"));
            Assert.That(stock.Add(FoodKind.Fish, 1000, 7), Is.EqualTo(FoodStock.MaxPortions - 2));
        }

        [Test]
        public void FoodAndCrops_SurviveASave_AndThePromptsMentionThem()
        {
            var sim = Town();
            Do(sim, ActivityCatalog.PlantCrops);
            Do(sim, ActivityCatalog.WaterCrops);
            var r = sim.Residents[0];
            r.Food.Add(FoodKind.Fish, 2, sim.Time.Day + 2);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World).Simulation;
            loaded.FoodMustBeMade = true;
            var lr = loaded.Residents[0];
            Assert.That(lr.Food.Batches.Select(b => (b.Kind, b.Count, b.GoesOffDay)), Is.EqualTo(r.Food.Batches.Select(b => (b.Kind, b.Count, b.GoesOffDay))));
            var crop = loaded.Food.CropAt(StarterTown.GardenPlotId);
            Assert.That(crop, Is.Not.Null);
            Assert.That((crop.PlantedDay, crop.WateredDays, crop.LastWateredDay), Is.EqualTo((sim.Food.CropAt(StarterTown.GardenPlotId).PlantedDay, 1, sim.Time.Day)));

            var prompt = DecisionPrompt.UserText(loaded, lr, loaded.Planner.FreeTimeOptions(lr, everything: true), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("Your food:").And.Contain("2 fish").And.Contain("Garden:").And.Contain("watered today"));
            Assert.That(prompt, Does.Not.Contain("food at home for"));
        }
    }
}
