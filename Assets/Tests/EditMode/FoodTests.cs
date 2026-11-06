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
            Assert.That(sim.Planner.FreeTimeOptions(r, everything: true).Any(o => o.Activity == ActivityCatalog.PlantCrops && o.TargetId == plot), Is.False, "the plot is taken");
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
        public void Crops_ArePlanted_WhereverTheyChoose_AsWhateverTheyLike()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            var r = sim.Residents[0];
            sim.Tick();
            Assert.That(sim.World.ObjectsOfType(TinyCity.Simulation.World.ObjectType.GardenPlot).Any(), Is.False, "no garden plots: they plant where they like");
            var ground = sim.FreeTimeOptionsFor(r).Single(o => o.Activity == ActivityCatalog.PlantCrops);
            Assert.That(ground.IsGround, Is.True);
            Assert.That(ground.TargetId, Is.EqualTo(FreeTimeOption.GroundTargetId));

            Assert.That(sim.GroundProblem(r, new TinyCity.Simulation.World.Location(r.Location.MapId, 22, 31)), Does.Contain("isn't open ground"), "not in the pond");
            var bench = sim.World.Objects.First(o => o.Type == TinyCity.Simulation.World.ObjectType.Bench);
            Assert.That(sim.GroundProblem(r, bench.Location), Is.Not.Null, "not on a bench");

            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, "plant-crops", "ground", "Food", "Here", null, 40, 10, "Purple Potatoes"), Is.EqualTo(DecisionOutcome.Applied), "anywhere on open ground, not just the soil");
            var patch = sim.Food.Patches.Single();
            Assert.That((patch.What, patch.PlanterId, patch.Location.Position.X, patch.Location.Position.Y), Is.EqualTo(("purple potatoes", r.Id, 40, 10)));
            Assert.That(r.TargetObjectId, Is.EqualTo(patch.Id));
            for (var i = 0; i < 300 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.Food.CropAt(patch.Id), Is.Not.Null, "planted");
            Assert.That(r.Log.Any(e => e.Text.Contains("Planted purple potatoes")), Is.True);
            Assert.That(sim.GroundProblem(r, patch.Location), Is.Not.Null, "nobody plants on top of it");

            var crop = sim.Food.CropAt(patch.Id);
            crop.WateredDays = FoodRules.WateredDaysToGrow;
            var option = sim.FreeTimeOptionsFor(r).Single(o => o.Activity == ActivityCatalog.Harvest);
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Food", "Ready"), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 300 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.Food.Count(FoodKind.Vegetables), Is.EqualTo(FoodRules.HarvestPortions));
            Assert.That(sim.Food.Patches, Is.Empty, "the patch is gone once picked");
            Assert.That(sim.World.TryGetObject(patch.Id, out _), Is.False);
            Assert.That(sim.GroundProblem(r, patch.Location), Is.Null, "and the ground is free again");
        }

        [Test]
        public void CropPatches_SurviveASave()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            var r = sim.Residents[0];
            sim.Tick();
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, "plant-crops", "ground", "Food", "Here", null, 20, 23, "beans"), Is.EqualTo(DecisionOutcome.Applied));
            sim.Advance(120);
            Assert.That(sim.Food.Patches.Count(), Is.EqualTo(1));

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty);
            var loaded = result.Simulation;
            var patch = loaded.Food.Patches.Single();
            Assert.That((patch.Id, patch.What, patch.PlanterId), Is.EqualTo((sim.Food.Patches.Single().Id, "beans", r.Id)));
            Assert.That(loaded.Food.CropAt(patch.Id), Is.Not.Null);
            Assert.That(loaded.World.TryGetObject(patch.Id, out var obj) && obj.Location == patch.Location, Is.True);
            Assert.That(loaded.Food.NextPatchNumber, Is.EqualTo(sim.Food.NextPatchNumber));
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
            Assert.That(prompt, Does.Contain("Your food:").And.Contain("2 fish").And.Contain("Crops:").And.Contain("watered today"));
            Assert.That(prompt, Does.Not.Contain("food at home for"));
        }
    
        [Test]
        public void ThereAreNoPrices_ForAStall_OrAnywhereInTinyCity2()
        {
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon);
            sim.FoodMustBeMade = true;
            sim.AiRunsEverything = true;
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];
            r.Food.Add(FoodKind.Fish, 5, sim.Time.Day + 2);
            r.ResetActivity();
            r.State = ResidentState.Deciding;
            r.PendingDecisionId = 41;
            var prompt = DecisionPrompt.UserText(sim, r, sim.Planner.FreeTimeOptions(r, everything: true), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Not.Contain("\"price\"").And.Not.Contain("Money:").And.Contain(PromptText.NoMoney));
            Assert.That(OllamaProtocol.BuildChatBody("m", "s", "u", DecisionPrompt.Schema(sim.Planner.FreeTimeOptions(r, everything: true), 3), 0.5, 100), Does.Not.Contain("price"));
            Assert.That(DialoguePrompt.Schema().ContainsKey("properties") && !((System.Collections.Generic.Dictionary<string, object>)DialoguePrompt.Schema()["properties"]).ContainsKey("give_money"), Is.True);
        }

        [Test]
        public void AStallHolder_HandsOutFood_AndRemembersTheDay()
        {
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon);
            sim.FoodMustBeMade = true;
            sim.Tick();
            var seller = sim.Residents[0];
            var buyer = sim.Residents[1];
            seller.Food.Clear();
            seller.Food.Add(FoodKind.Meal, 2, sim.Time.Day + 3);
            var stall = sim.World.Objects.First(o => o.Type == TinyCity.Simulation.World.ObjectType.MarketStall);

            Assert.That(sim.Planner.CanDo(buyer, ActivityCatalog.BuyFood, stall, sim.Time), Is.False, "nobody is at the stall yet");
            Assert.That(sim.Planner.CanDo(buyer, ActivityCatalog.RunStall, stall, sim.Time), Is.EqualTo(buyer.Food.Total > 0));

            seller.ResetActivity();
            seller.TargetObjectId = stall.Id;
            seller.ActivityId = ActivityCatalog.RunStall.Id;
            seller.State = ResidentState.Performing;
            seller.MinutesRemaining = 30;
            seller.Location = stall.Location;
            buyer.MoneyPence = 0;
            Assert.That(sim.Planner.CanDo(buyer, ActivityCatalog.BuyFood, stall, sim.Time), Is.True, "no money needed");
            Assert.That(sim.Planner.CanDo(seller, ActivityCatalog.BuyFood, stall, sim.Time), Is.False, "not from yourself");

            var meals = buyer.Food.Count(FoodKind.Meal);
            buyer.ResetActivity();
            buyer.TargetObjectId = stall.Id;
            buyer.ActivityId = ActivityCatalog.BuyFood.Id;
            buyer.State = ResidentState.Travelling;
            buyer.Location = stall.Location;
            sim.Tick();

            Assert.That(buyer.Food.Count(FoodKind.Meal), Is.EqualTo(meals + 1));
            Assert.That(seller.Food.Count(FoodKind.Meal), Is.EqualTo(1));
            Assert.That(buyer.MoneyPence, Is.EqualTo(0));
            Assert.That(sim.Economy.Ledger.All.Any(), Is.False, "nothing is paid");

            for (var i = 0; i < 40 && seller.ActivityId == ActivityCatalog.RunStall.Id; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.Memories.ForResident(seller.Id).Any(m => m.Summary.StartsWith("I handed out 1 portion of food at a market stall")), Is.True);
        }

        [Test]
        public void FoodOfferedInAChat_MovesOnlyIfTheOtherAccepts()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            sim.FoodMustBeMade = true;
            ConversationSessionFor(sim, out var session);
            var first = sim.GetResident(session.SpeakerId);
            var second = sim.GetResident(session.ListenerId);
            first.Food.Clear();
            second.Food.Clear();
            first.Food.Add(FoodKind.Fish, 3, sim.Time.Day + 2);

            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("give_food").And.Contain("Your food: 3 fish"));
            Assert.That(sim.ApplyConversationLine(session.Id, session.LineRequestId, first.Id, "Fancy some fish?", "warm", "fish", false, giveFood: 2), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(first.Food.Total, Is.EqualTo(3), "nothing moves until they accept");

            WaitForLine(sim, session);
            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("offered you 2 portions of food."));
            Assert.That(sim.ApplyConversationLine(session.Id, session.LineRequestId, second.Id, "Go on then.", "warm", "fish", false, acceptOffer: true), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(second.Food.Count(FoodKind.Fish), Is.EqualTo(2));
            Assert.That(first.Food.Count(FoodKind.Fish), Is.EqualTo(1));
            Assert.That(sim.Memories.ForResident(second.Id).Any(m => m.Summary.Contains("gave me 2 portions of food")), Is.True);
            Assert.That(session.FoodOffer, Is.Null);

            WaitForLine(sim, session);
            sim.ApplyConversationLine(session.Id, session.LineRequestId, first.Id, "Here, have the last.", "warm", "fish", false, giveFood: 1);
            WaitForLine(sim, session);
            sim.ApplyConversationLine(session.Id, session.LineRequestId, second.Id, "No thanks.", "neutral", "fish", false, acceptOffer: false);
            Assert.That(first.Food.Count(FoodKind.Fish), Is.EqualTo(1), "a refused offer moves nothing");
        }

        [Test]
        public void ASmallModelsSlips_AreMended_InsteadOfAskingAgain()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            var r = sim.Residents[0];
            sim.Tick();
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, "plant-crops", "ground", "Food", "Here", null, 22, 31, "rice"), Is.EqualTo(DecisionOutcome.Applied), "in the pond");
            var patch = sim.Food.Patches.Single();
            Assert.That(sim.Pathfinder.Distance(r.Location, patch.Location) <= TinyCity.Simulation.AI.PromptText.FreeSpotSearch * 2, Is.True, "planted on free ground nearby instead");
            Assert.That(r.Log.Any(e => e.Text.Contains("so chose")), Is.True);

            var other = sim.Residents[1];
            Assert.That(sim.ApplyDecision(other.Id, other.PendingDecisionId, "plant-crops", "ground", "Food", "Here"), Is.EqualTo(DecisionOutcome.Applied), "no spot given");

            var third = sim.Residents[2];
            Assert.That(sim.ApplyDecision(third.Id, third.PendingDecisionId, "sleep", "obj-nowhere", "Tired", "Yawn"), Is.EqualTo(DecisionOutcome.Applied), "the wrong target: the nearest place to sleep");

            var fourth = sim.Residents[3];
            var only = sim.FreeTimeOptionsFor(fourth).GroupBy(o => o.TargetId).First(g => g.Count() == 1).Single();
            Assert.That(sim.ApplyDecision(fourth.Id, fourth.PendingDecisionId, "do-the-thing", only.TargetId, "Why not", "Hm"), Is.EqualTo(DecisionOutcome.Applied), "the wrong action for a target only one option uses");
            Assert.That(fourth.ActivityId, Is.EqualTo(only.ActionId));
        }

        [Test]
        public void ATaxMan_CanAskForAShare_WhichTheOtherMayRefuse()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            sim.FoodMustBeMade = true;
            sim.Brief = WorldBrief.Default;
            ConversationSessionFor(sim, out var session);
            var taxer = sim.GetResident(session.SpeakerId);
            var payer = sim.GetResident(session.ListenerId);
            taxer.Role = "tax man";
            payer.Food.Clear();
            payer.Food.Add(FoodKind.Fish, 10, sim.Time.Day + 2);
            payer.Belongings.Add(MaterialKind.Wood, 5);
            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("\"tax\"").And.Not.Contain("give_money"));
            sim.ApplyConversationLine(session.Id, session.LineRequestId, taxer.Id, "I'll have my tax.", "neutral", "tax", false, taxPercent: 20);
            Assert.That(payer.Food.Total, Is.EqualTo(10), "nothing is taxed until they pay");

            WaitForLine(sim, session);
            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("asked you to hand over 20%").And.Contain("pay_tax"));
            sim.ApplyConversationLine(session.Id, session.LineRequestId, payer.Id, "Fair enough.", "neutral", "tax", false, payTax: true);
            Assert.That((payer.Food.Total, taxer.Food.Count(FoodKind.Fish)), Is.EqualTo((8, 2)));
            Assert.That((payer.Belongings.Count(MaterialKind.Wood), taxer.Belongings.Count(MaterialKind.Wood)), Is.EqualTo((4, 1)), "20% of 5, rounded down");
            Assert.That(session.TaxDemand, Is.Null);

            WaitForLine(sim, session);
            sim.ApplyConversationLine(session.Id, session.LineRequestId, taxer.Id, "Same again.", "neutral", "tax", false, taxPercent: 50);
            WaitForLine(sim, session);
            sim.ApplyConversationLine(session.Id, session.LineRequestId, payer.Id, "No chance.", "disagree", "tax", false, payTax: false);
            Assert.That(payer.Food.Total, Is.EqualTo(8), "refused");
            Assert.That(sim.Memories.ForResident(taxer.Id).Any(m => m.Summary.Contains("they refused")), Is.True);
            var trades = sim.TradesOf(taxer.Id).ToList();
            Assert.That(trades.Count, Is.EqualTo(2), "the tax paid and the tax refused are the taxer's trades");
            Assert.That(trades[0].Summary, Does.Contain("they refused"), "newest first");

            WaitForLine(sim, session);
            payer.Role = "farmer";
            sim.ApplyConversationLine(session.Id, session.LineRequestId, payer.Id, "Now you pay me.", "playful", "tax", false, taxPercent: 50);
            Assert.That(session.TaxDemand, Is.Null, "only a role that can tax may ask");
        }

        [Test]
        public void MaterialsAndTools_CanBeHandedOverInAChat_AndPromptsHaveNoMoney()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiDecisionsEnabled = true;
            sim.FoodMustBeMade = true;
            sim.Brief = WorldBrief.Default;
            ConversationSessionFor(sim, out var session);
            var giver = sim.GetResident(session.SpeakerId);
            var taker = sim.GetResident(session.ListenerId);
            giver.Belongings.Add(MaterialKind.Wood, 5);
            giver.Belongings.SetTool(ToolKind.Axe, 7, 2);
            var prompt = DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("give_item").And.Not.Contain("give_money").And.Not.Contain("Your money"));

            sim.ApplyConversationLine(session.Id, session.LineRequestId, giver.Id, "Have some wood.", "warm", "wood", false, giveItem: "wood", giveAmount: 9);
            Assert.That((giver.Belongings.Count(MaterialKind.Wood), taker.Belongings.Count(MaterialKind.Wood)), Is.EqualTo((0, 5)), "no more than they have");
            WaitForLine(sim, session);
            sim.ApplyConversationLine(session.Id, session.LineRequestId, taker.Id, "Thanks.", "warm", "wood", false);
            WaitForLine(sim, session);
            sim.ApplyConversationLine(session.Id, session.LineRequestId, giver.Id, "And my axe.", "warm", "wood", false, giveItem: "stone axe");
            Assert.That((giver.Belongings.Has(ToolKind.Axe), taker.Belongings.TierOf(ToolKind.Axe), taker.Belongings.UsesLeft(ToolKind.Axe)), Is.EqualTo((false, 2, 7)));
            Assert.That(sim.TradesOf(taker.Id).Count(), Is.EqualTo(2));

            Assert.That(DialoguePrompt.TryParse("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_item\":\"iron ore\",\"give_amount\":3}", out var reply, out _), Is.True);
            Assert.That((reply.GiveItem, reply.GiveAmount), Is.EqualTo(("iron ore", 3)));
            Assert.That(CraftingRules.TryMaterial(reply.GiveItem, out var kind) && kind == MaterialKind.Iron, Is.True);
            Assert.That(CraftingRules.TryTool("iron pickaxe", out var tool) && tool == ToolKind.Pickaxe, Is.True);
        }

        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_food\":2,\"accept_food\":true}", 2, true)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_food\":99,\"price\":-4}", 99, false)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_food\":-3}", 0, false)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false}", 0, false)]
        public void DialogueReplies_ReadFoodOffers(string content, int give, bool accept)
        {
            Assert.That(DialoguePrompt.TryParse(content, out var reply, out _), Is.True);
            Assert.That((reply.GiveFood, reply.AcceptOffer), Is.EqualTo((give, accept)));
        }

        private static void ConversationSessionFor(TownSimulation sim, out TinyCity.Simulation.Social.ConversationSession session)
        {
            foreach (var r in sim.Residents)
            {
                r.Needs[NeedType.Social] = 10;
            }

            for (var i = 0; i < 10 && sim.ActiveConversations.Count == 0; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.ActiveConversations.Count, Is.EqualTo(1));
            session = sim.ActiveConversations[0];
            WaitForLine(sim, session);
        }

        private static void WaitForLine(TownSimulation sim, TinyCity.Simulation.Social.ConversationSession session)
        {
            for (var i = 0; i < 30 && !session.AwaitingLine; i++)
            {
                sim.Tick();
            }

            Assert.That(session.AwaitingLine, Is.True);
        }
}
}
