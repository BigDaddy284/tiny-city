using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>
    /// Tiny City 2: roles decide who can do what (owner, 5 November), the water purifier's clean water, and the
    /// weekly town meeting where role changes are voted on.
    /// </summary>
    public class RoleWorkTests
    {
        private static TownSimulation NewGame()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static List<DayPlanEntry> Plan() => new List<DayPlanEntry> { new DayPlanEntry(9, "Get on") };

        private static void TakeRole(TownSimulation sim, Resident r, string role) =>
            Assert.That(sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), Plan(), role), Is.EqualTo(DecisionOutcome.Applied));

        private static HashSet<string> Options(TownSimulation sim, Resident r) =>
            new HashSet<string>(sim.Planner.FreeTimeOptions(r).Select(o => o.ActionId));

        private static void WaitUntilDeciding(TownSimulation sim, Resident r)
        {
            for (var i = 0; i < 10 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding));
        }

        /// <summary>Chooses this activity for them (the nearest place for it), and runs until they're deciding again.</summary>
        private static void Do(TownSimulation sim, Resident r, ActivityDefinition activity)
        {
            WaitUntilDeciding(sim, r);
            var option = sim.Planner.FreeTimeOptions(r).FirstOrDefault(o => o.Activity == activity);
            Assert.That(option, Is.Not.Null, $"{activity.Id} should be an option");
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Work"), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 600 && (r.State != ResidentState.Deciding || i == 0); i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), $"{activity.Id} should have finished");
        }

        [Test]
        public void WithoutARole_NoWorkIsOpen_AndARoleOpensItsOwnWork()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            var basics = Options(sim, r);
            foreach (var id in new[] { "wash-pond", "catch-fish", "plant-crops", "plan-building", "purify-water", "craft-axe", "cook", "gather-sticks", "gather-loose-stones" })
            {
                Assert.That(basics.Contains(id), Is.False, id + " belongs to a role");
            }

            TakeRole(sim, r, "farmer");
            var farmer = Options(sim, r);
            Assert.That(farmer.Contains("plant-crops"), Is.True);
            Assert.That(farmer.Contains("plan-building") || farmer.Contains("cook") || farmer.Contains("craft-axe"), Is.False, "a farmer can't build, cook or make tools");
            Assert.That(farmer.Contains("catch-fish"), Is.False, "only a fisher fishes (owner, 6 November)");

            var miner = sim.Residents[1];
            TakeRole(sim, miner, "miner");
            Assert.That(Options(sim, miner).Contains("gather-loose-stones"), Is.True, "only a miner picks up stones (owner, 6 November)");
            Assert.That(Options(sim, miner).Contains("gather-sticks"), Is.False, "branches are the lumberjack's");

            var s = new System.Text.StringBuilder();
            PromptText.AppendFood(s, sim, r);
            Assert.That(s.ToString(), Does.Contain("As the farmer you can plant crops").And.Not.Contain("farmer: " + r.FirstName), "they are not listed among the others");
            Assert.That(s.ToString(), Does.Contain("The town meeting is held at the meeting circle on Monday (day 8) at 10:00"));
        }

        [Test]
        public void HandGathering_NeedsARole_EvenWhenNoRoleListsIt()
        {
            var sim = NewGame();
            var shipped = WorldBrief.Default;
            sim.Brief = new WorldBrief(shipped.Text, shipped.Roles.Select(role => new RoleOption(role.Name, role.About, role.CanTax, role.Skills.Where(id => id != "gather-sticks" && id != "gather-loose-stones"))));
            var r = sim.Residents[0];
            Assert.That(Options(sim, r).Contains("gather-sticks") || Options(sim, r).Contains("gather-loose-stones"), Is.False, "nobody without a role picks up branches or stones (owner, 6 November)");
            TakeRole(sim, r, "lumberjack");
            Assert.That(Options(sim, r).Contains("gather-sticks"), Is.False, "and no role lists them here, so nobody may");
        }

        [Test]
        public void ATownSavedBeforeBranchesWentToRoles_IsBroughtUpToDateOnLoading()
        {
            var sim = NewGame();
            var shipped = WorldBrief.Default;
            // What towns started before 6 November saved: the lumberjack and miner without the hand-gathering, and words saying anyone could.
            const string cookBefore = "Only you can cook. You turn raw vegetables and fish into proper meals at a stove, which fill people up far better than raw food and don't make them ill. You keep an eye on what is running low and make sure nobody goes hungry.";
            sim.Brief = new WorldBrief(
                shipped.Text.Replace(
                    "without one you can do nothing for yourself: you can't fish, grow or cook food or touch the pond's dirty water, so you need each other.",
                    "without one you can only drink and wash in the dirty pond and eat raw fish and pick up fallen branches and loose stones, so you need each other."),
                shipped.Roles.Select(role => new RoleOption(role.Name, role.Name == "cook" ? cookBefore : role.About, role.CanTax, role.Skills.Where(id => id != "gather-sticks" && id != "gather-loose-stones" && id != "wash-pond"))));
            Assert.That(sim.Brief.Text, Does.Contain("pick up fallen branches"));

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            loaded.AiDecisionsEnabled = true;
            Assert.That(loaded.Brief.Find("lumberjack").Skills, Does.Contain("gather-sticks"));
            Assert.That(loaded.Brief.Find("miner").Skills, Does.Contain("gather-loose-stones"));
            Assert.That(loaded.Brief.Text, Does.Not.Contain("pick up fallen branches").And.Not.Contain("eat raw").And.Contain("you can't fish, grow or cook food"));
            Assert.That(loaded.Brief.Find("cook").About, Is.EqualTo(shipped.Find("cook").About), "the cook's old words about raw food are replaced");
            Assert.That(loaded.Brief.Find("water purifier").Skills, Does.Contain("wash-pond"));
            var r = loaded.Residents[0];
            Assert.That(Options(loaded, r).Contains("gather-sticks"), Is.False);
            TakeRole(loaded, r, "lumberjack");
            Assert.That(Options(loaded, r).Contains("gather-sticks"), Is.True, "the lumberjack can again");
        }

        [Test]
        public void Brief_ListsWhatEachRoleCanDo_AndRejectsUnknownWork()
        {
            Assert.That(WorldBrief.Default.Find("lumberjack").Skills, Does.Contain("gather-wood"));
            Assert.That(WorldBrief.Default.Find("water purifier").Skills, Is.EqualTo(new[] { "purify-water", "wash-pond" }));
            Assert.That(WorldBrief.Default.Text, Does.Contain("majority vote at the weekly meeting"));
            var brief = ResidentProfiles.ParseBrief("{\"roles\":[{\"name\":\"digger\",\"about\":\"digs\",\"can\":[\"gather-stone\",\"juggling\"]}]}", out var problems);
            Assert.That(brief.Find("digger").Skills, Is.EqualTo(new[] { "gather-stone" }));
            Assert.That(problems.Count, Is.EqualTo(1), "juggling isn't work in the game");
            Assert.That(brief.MayDo(null, "gather-stone"), Is.False);
            Assert.That(brief.MayDo(null, "gather-wood"), Is.False, "gathering always needs a role, even one no role lists (owner, 6 November)");
            Assert.That(brief.MayDo("digger", "gather-wood"), Is.False);
            Assert.That(brief.MayDo(null, "drink-clean"), Is.True, "everyday things are open to everyone");
            Assert.That(brief.MayDo(null, "wash-clean"), Is.True);
            Assert.That(brief.MayDo(null, "catch-fish"), Is.False, "owner, 6 November: only a fisher catches fish");
            Assert.That(brief.MayDo(null, "wash-pond"), Is.False, "and only the water purifier touches the pond");
        }

        [Test]
        public void OnlyTheFisher_CatchesFish()
        {
            var sim = NewGame();
            var anyone = sim.Residents[0];
            var fisher = sim.Residents[1];
            TakeRole(sim, fisher, "fisher");
            WaitUntilDeciding(sim, anyone);
            Assert.That(Options(sim, anyone).Contains("catch-fish"), Is.False, "no fishing by hand");

            var before = fisher.Food.Count(FoodKind.Fish);
            Do(sim, fisher, ActivityCatalog.CatchFish);
            Assert.That(fisher.Food.Count(FoodKind.Fish) - before, Is.EqualTo(FoodRules.CatchPortions));
        }

        [Test]
        public void OnlyTheWaterPurifierTouchesThePond_AndMakesCleanWater_ThatAnyoneCanDrinkAndWashWith()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            WaitUntilDeciding(sim, r);
            Assert.That(ActivityCatalog.TryGet("drink-pond", out _), Is.False, "owner, 6 November: pond water can't be drunk");
            Assert.That(Options(sim, r).Contains("wash-pond"), Is.False, "only the water purifier touches the pond");
            Assert.That(Options(sim, r).Contains("drink-clean"), Is.False, "no clean water yet");

            TakeRole(sim, r, "water purifier");
            Do(sim, r, ActivityCatalog.PurifyWater);
            Assert.That(r.CleanWater, Is.EqualTo(FoodRules.PurifyPortions));
            var health = r.Health;
            Do(sim, r, ActivityCatalog.DrinkCleanWater);
            Assert.That(r.CleanWater, Is.EqualTo(FoodRules.PurifyPortions - 1));
            r.Needs[NeedType.Hygiene] = 20;
            Do(sim, r, ActivityCatalog.WashWithCleanWater);
            Assert.That(r.CleanWater, Is.EqualTo(FoodRules.PurifyPortions - 2), "washing uses a portion too");
            Assert.That(r.Needs[NeedType.Hygiene], Is.GreaterThan(60));
            Assert.That(r.Health, Is.GreaterThan(health - 0.5), "clean water does no harm");
            Assert.That(CraftingRules.ItemNames, Does.Contain(CraftingRules.CleanWaterName), "clean water can be traded");

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            Assert.That(loaded.GetResident(r.Id).CleanWater, Is.EqualTo(r.CleanWater));
        }

        [Test]
        public void AFirstRoleIsFree_ButAChange_WaitsForTheMeeting_AndIsSaved()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            Assert.That(sim.FirstMeetingDay, Is.EqualTo(FlatLand.StartTime.Day + 7));
            Assert.That(sim.NextMeetingDay(1), Is.EqualTo(8));
            Assert.That(sim.NextMeetingDay(9), Is.EqualTo(15));
            Assert.That(sim.IsMeetingDay(1), Is.False, "there is no first-day meeting");
            TakeRole(sim, r, "farmer");
            Assert.That(r.Role, Is.EqualTo("farmer"));

            sim.Advance(24 * 60);
            TakeRole(sim, r, "cook");
            Assert.That(r.Role, Is.EqualTo("farmer"), "a change needs a vote");
            Assert.That(sim.RoleRequests[r.Id], Is.EqualTo("cook"));
            var s = new System.Text.StringBuilder();
            PromptText.AppendFood(s, sim, r);
            Assert.That(s.ToString(), Does.Contain("You have asked to become the cook"));

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());
            Assert.That(loaded.Warnings, Is.Empty);
            Assert.That(loaded.Simulation.RoleRequests[r.Id], Is.EqualTo("cook"));
            Assert.That(loaded.Simulation.FirstMeetingDay, Is.EqualTo(sim.FirstMeetingDay));
        }

        [Test]
        public void TheMeeting_VotesOnRoleChanges_AMajorityOfThoseThereIsNeeded()
        {
            var sim = NewGame();
            var asker = sim.Residents[0];
            var second = sim.Residents[1];
            var third = sim.Residents[2];
            TakeRole(sim, asker, "farmer");
            TakeRole(sim, second, "fisher");
            sim.SetRoleRequest(asker.Id, "cook");
            sim.SetRoleRequest(second.Id, TownSimulation.RoleNone);
            sim.FirstMeetingDay = sim.Time.Day;
            Assert.That(Options(sim, third).Contains("attend-meeting"), Is.False, "not before ten");

            sim.Advance(TownSimulation.MeetingStartMinute - sim.Time.MinuteOfDay);
            foreach (var r in new[] { asker, second, third })
            {
                WaitUntilDeciding(sim, r);
                var option = sim.Planner.FreeTimeOptions(r).FirstOrDefault(o => o.Activity == ActivityCatalog.AttendMeeting);
                Assert.That(option, Is.Not.Null, "the meeting is on");
                Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Meeting"), Is.EqualTo(DecisionOutcome.Applied));
            }

            sim.Advance(TownSimulation.MeetingTurnsFromMinute - sim.Time.MinuteOfDay);
            Assert.That(sim.Meeting.Attendees, Is.EquivalentTo(new[] { asker.Id, second.Id, third.Id }));
            var text = MeetingPrompt.UserText(sim, third);
            Assert.That(text, Does.Contain($"{asker.FirstName} asked to stop being the farmer and become the cook").And.Contain($"{second.FirstName} asked to give up being the fisher"));

            Assert.That(MeetingPrompt.TryParse($"{{\"say\":\"We need a cook.\",\"votes\":[{{\"person\":\"{asker.FirstName}\",\"yes\":true}},{{\"person\":\"{second.FirstName}\",\"yes\":false}},{{\"person\":\"Nobody\",\"yes\":true}}]}}", sim, out var reply, out var error), Is.True, error);
            Assert.That(reply.Votes.Count, Is.EqualTo(2));
            Assert.That(sim.ApplyMeetingTurn(third.Id, sim.MeetingTurnOf(third.Id).Id, reply.Say, reply.Votes), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(sim.ApplyMeetingTurn(asker.Id, sim.MeetingTurnOf(asker.Id).Id, "I'd cook for us all.", new Dictionary<string, bool> { [second.Id] = false }), Is.EqualTo(DecisionOutcome.Applied));
            var secondTurn = sim.MeetingTurnOf(second.Id).Id;
            sim.DropMeetingTurn(second.Id, secondTurn);
            Assert.That(sim.ApplyMeetingTurn(second.Id, secondTurn, "Too late", null), Is.EqualTo(DecisionOutcome.Stale));

            sim.Advance(TownSimulation.MeetingCloseMinute - sim.Time.MinuteOfDay + 1);
            Assert.That(asker.Role, Is.EqualTo("cook"), "two of three voted for it");
            Assert.That(second.Role, Is.EqualTo("fisher"), "nobody voted for it");
            Assert.That(sim.RoleRequests, Is.Empty);
            var meeting = sim.History.Events.Last(e => e.Kind == TownEventKind.Meeting);
            Assert.That(meeting.Summary, Does.Contain("3 came").And.Contain("2 for, 0 against, so it passed").And.Contain("0 for, 2 against, so it failed"));
            Assert.That(Journal.IsNotable(meeting.Kind), Is.True);
            Assert.That(sim.Memories.ForResident(third.Id).Any(m => m.Summary.StartsWith("At the town meeting")), Is.True);
            Assert.That(sim.Meeting.Attendees, Is.Empty, "the meeting is over");
        }

        [Test]
        public void ARequest_FromSomeoneWhoStaysAway_WaitsForTheNextMeeting()
        {
            var sim = NewGame();
            var asker = sim.Residents[0];
            var other = sim.Residents[1];
            TakeRole(sim, asker, "farmer");
            sim.SetRoleRequest(asker.Id, "cook");
            sim.FirstMeetingDay = sim.Time.Day;
            sim.Advance(TownSimulation.MeetingStartMinute - sim.Time.MinuteOfDay);
            WaitUntilDeciding(sim, other);
            var option = sim.Planner.FreeTimeOptions(other).First(o => o.Activity == ActivityCatalog.AttendMeeting);
            sim.ApplyDecision(other.Id, other.PendingDecisionId, option.ActionId, option.TargetId, "Meeting");
            sim.Advance(TownSimulation.MeetingCloseMinute - sim.Time.MinuteOfDay + 1);
            Assert.That(asker.Role, Is.EqualTo("farmer"));
            Assert.That(sim.RoleRequests[asker.Id], Is.EqualTo("cook"));
            Assert.That(sim.NextMeetingDay(sim.Time.Day + 1), Is.EqualTo(sim.Time.Day + 7));
        }

        [Test]
        public void Feelings_AreWords_WorstFirst()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            r.Needs[NeedType.Thirst] = 5;
            sim.Tick();
            var feelings = PromptText.Feelings(r, sim);
            Assert.That(feelings.Contains("toilet"), Is.False, feelings);
            Assert.That(feelings.Contains("parched with thirst"), Is.True, feelings);
            Assert.That(feelings.Contains("getting worse every hour"), Is.True, feelings);
            Assert.That(ActivityCatalog.All.Any(a => a.Id == "toilet-outdoors"), Is.False);
        }

        [Test]
        public void NobodyStartsWithFood()
        {
            var sim = NewGame();
            sim.Tick();
            Assert.That(sim.Residents.All(o => o.Food.IsEmpty && o.Food.Count(FoodKind.Meal) == 0), Is.True);
        }

        [Test]
        public void PersonalityIsKeptPrivate()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            var other = sim.Residents[1];
            Assert.That(PromptText.Persona(r, sim), Does.Contain(PromptText.PrivatePersonality));
            Assert.That(PromptText.Persona(r, sim), Does.Contain(r.Outlook));
            var s = new System.Text.StringBuilder();
            PromptText.AppendFood(s, sim, r);
            Assert.That(s.ToString().Contains(other.Outlook), Is.False, "nobody is told someone else's personality");
        }
    }
}
