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
        public void AStallHolder_NamesTheirOwnPrice_WhenTheyChooseToRunAStall()
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
            var option = sim.Planner.FreeTimeOptions(r, everything: true).First(o => o.ActionId == ActivityCatalog.RunStall.Id);
            var prompt = DecisionPrompt.UserText(sim, r, sim.Planner.FreeTimeOptions(r, everything: true), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("\"price\""));
            Assert.That(sim.ApplyDecision(r.Id, 41, option.ActionId, option.TargetId, "sell", "", pricePence: 12345), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.StallPricePence, Is.EqualTo(12345), "no fixed prices: whatever they ask");

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World).Simulation;
            Assert.That(loaded.GetResident(r.Id).StallPricePence, Is.EqualTo(12345));
            var options = loaded.Planner.FreeTimeOptions(loaded.GetResident(sim.Residents[1].Id), everything: true);
            var stallOption = options.First(o => o.ActionId == ActivityCatalog.RunStall.Id || o.Activity != null);
            Assert.That(DecisionPrompt.TryParse($"{{\"action\":\"{stallOption.ActionId}\",\"target_id\":\"{stallOption.TargetId}\",\"price\":2.5}}", options, out var reply, out var parseError), Is.True, parseError);
            Assert.That(reply.PricePence, Is.EqualTo(250));
        }

        [Test]
        public void ASeller_AtAStall_SellsAPortionToABuyer_AndRemembersTheDay()
        {
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon);
            sim.FoodMustBeMade = true;
            sim.Tick();
            var seller = sim.Residents[0];
            var buyer = sim.Residents[1];
            seller.Food.Clear();
            seller.Food.Add(FoodKind.Meal, 2, sim.Time.Day + 3);
            var stall = sim.World.Objects.First(o => o.Type == TinyCity.Simulation.World.ObjectType.MarketStall);

            Assert.That(sim.Planner.CanDo(buyer, ActivityCatalog.BuyFood, stall, sim.Time), Is.False, "nobody is selling yet");
            Assert.That(sim.Planner.CanDo(buyer, ActivityCatalog.RunStall, stall, sim.Time), Is.EqualTo(buyer.Food.Total > 0));

            seller.ResetActivity();
            seller.StallPricePence = 750;
            seller.TargetObjectId = stall.Id;
            seller.ActivityId = ActivityCatalog.RunStall.Id;
            seller.State = ResidentState.Performing;
            seller.MinutesRemaining = 30;
            seller.Location = stall.Location;
            Assert.That(sim.Planner.CanDo(buyer, ActivityCatalog.BuyFood, stall, sim.Time), Is.True);
            Assert.That(sim.Planner.CanDo(seller, ActivityCatalog.BuyFood, stall, sim.Time), Is.False, "not from yourself");

            var money = (seller.MoneyPence, buyer.MoneyPence);
            var meals = buyer.Food.Count(FoodKind.Meal);
            buyer.ResetActivity();
            buyer.TargetObjectId = stall.Id;
            buyer.ActivityId = ActivityCatalog.BuyFood.Id;
            buyer.State = ResidentState.Travelling;
            buyer.Location = stall.Location;
            sim.Tick();

            Assert.That(buyer.Food.Count(FoodKind.Meal), Is.EqualTo(meals + 1));
            Assert.That(seller.Food.Count(FoodKind.Meal), Is.EqualTo(1));
            Assert.That(buyer.MoneyPence, Is.EqualTo(money.Item2 - 750), "the seller's own price");
            Assert.That(seller.MoneyPence, Is.EqualTo(money.Item1 + 750));
            Assert.That(sim.Economy.Ledger.All.Any(t => t.ResidentId == seller.Id && t.Kind == TransactionKind.Sale), Is.True);

            for (var i = 0; i < 40 && seller.ActivityId == ActivityCatalog.RunStall.Id; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.Memories.ForResident(seller.Id).Any(m => m.Summary.StartsWith("I sold 1 portion of food at a market stall")), Is.True);
        }

        [Test]
        public void FoodOfferedInAChat_MovesOnlyIfTheOtherAccepts_AndIsPaidFor()
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
            second.MoneyPence = 1000;

            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("give_food").And.Contain("Your food: 3 fish"));
            Assert.That(sim.ApplyConversationLine(session.Id, session.LineRequestId, first.Id, "Fancy some fish?", "warm", "fish", false, giveFood: 2, pricePence: 300), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(first.Food.Total, Is.EqualTo(3), "nothing moves until they accept");

            WaitForLine(sim, session);
            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("offered you 2 portions of food for £3.00"));
            Assert.That(sim.ApplyConversationLine(session.Id, session.LineRequestId, second.Id, "Go on then.", "warm", "fish", false, acceptOffer: true), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(second.Food.Count(FoodKind.Fish), Is.EqualTo(2));
            Assert.That(first.Food.Count(FoodKind.Fish), Is.EqualTo(1));
            Assert.That(second.MoneyPence, Is.EqualTo(700));
            Assert.That(sim.Memories.ForResident(second.Id).Any(m => m.Summary.Contains("sold me 2 portions of food")), Is.True);
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
        public void MoneyHandedOverInAChat_Moves_AndATaxMan_CanAskForAShare_WhichTheOtherMayRefuse()
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
            taxer.MoneyPence = 2000;
            var payerMoney = payer.MoneyPence;

            Assert.That(DialoguePrompt.UserText(sim, session, new TinyCity.Simulation.Memory.MemoryRecord[0]), Does.Contain("\"tax\"").And.Contain("give_money"));
            sim.ApplyConversationLine(session.Id, session.LineRequestId, taxer.Id, "Here's your wage, and I'll have my tax.", "neutral", "tax", false, giveMoneyPence: 500, taxPercent: 20);
            Assert.That((taxer.MoneyPence, payer.MoneyPence), Is.EqualTo((1500L, payerMoney + 500)), "money moves at once");
            Assert.That(sim.Economy.Ledger.All.Any(t => t.ResidentId == payer.Id && t.Kind == TransactionKind.Gift), Is.True);
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

            WaitForLine(sim, session);
            payer.Role = "farmer";
            sim.ApplyConversationLine(session.Id, session.LineRequestId, payer.Id, "Now you pay me.", "playful", "tax", false, taxPercent: 50);
            Assert.That(session.TaxDemand, Is.Null, "only a role that can tax may ask");
        }

        [Test]
        public void NobodyFallsIll_InTinyCity2()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon);
            sim.AiRunsEverything = true;
            foreach (var r in sim.Residents)
            {
                r.Health = 30;
            }

            sim.Advance(24 * 60);
            Assert.That(sim.Residents.All(r => r.IllnessId == null), Is.True);
        }

        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_food\":2,\"price\":1.5,\"accept_food\":true}", 2, 150, true)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_food\":99,\"price\":-4}", 99, 0, false)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false,\"give_food\":40,\"price\":250}", 40, 25000, false)]
        [TestCase("{\"say\":\"Hi\",\"tone\":\"warm\",\"topic\":\"x\",\"end\":false}", 0, 0, false)]
        public void DialogueReplies_ReadFoodOffersWithinLimits(string content, int give, long price, bool accept)
        {
            Assert.That(DialoguePrompt.TryParse(content, out var reply, out _), Is.True);
            Assert.That((reply.GiveFood, reply.PricePence, reply.AcceptOffer), Is.EqualTo((give, price, accept)));
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
