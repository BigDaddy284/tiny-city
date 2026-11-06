using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Owner, 6 November ("everyone wants to be a farmer"): each role has a most number of holders.</summary>
    public class RoleLimitTests
    {
        private static List<DayPlanEntry> Plan() => new List<DayPlanEntry> { new DayPlanEntry(9, "Find food") };

        private static TownSimulation NewGame()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static void Choose(TownSimulation sim, Resident r, string role) =>
            Assert.That(sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), Plan(), role), Is.EqualTo(DecisionOutcome.Applied));

        [Test]
        public void TheShippedRoles_HaveLimits_ThatLeaveRoomForEveryone()
        {
            var shipped = WorldBrief.Default;
            var limits = shipped.Roles.ToDictionary(o => o.Name, o => o.Max);
            Assert.That(limits["farmer"], Is.EqualTo(3));
            Assert.That(limits["cook"], Is.EqualTo(2));
            Assert.That(limits["mayor"], Is.EqualTo(1));
            Assert.That(limits.Values.All(m => m > 0), Is.True);
            Assert.That(limits.Values.Sum(), Is.GreaterThanOrEqualTo(FlatLand.CreateNewGame().Residents.Count), "a place for everyone");

            var dir = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Configuration", "residents.json")))
            {
                dir = dir.Parent;
            }

            Assert.That(dir, Is.Not.Null, "residents.json not found above the test folder");
            var file = ResidentProfiles.ParseBrief(System.IO.File.ReadAllText(System.IO.Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Configuration", "residents.json")), out var problems);
            Assert.That(problems, Is.Empty);
            Assert.That(file.Roles.Select(o => (o.Name, o.Max)), Is.EqualTo(shipped.Roles.Select(o => (o.Name, o.Max))), "the shipped file says the same");
        }

        [Test]
        public void Max_IsReadFromResidentsJson_Written_AndSaved()
        {
            var brief = ResidentProfiles.ParseBrief("{\"roles\":[{\"name\":\"farmer\",\"max\":2},{\"name\":\"fisher\",\"max\":-1},{\"name\":\"cook\"}]}", out var problems);
            Assert.That(brief.Roles.Select(o => o.Max), Is.EqualTo(new[] { 2, 0, 0 }));
            Assert.That(problems.Count, Is.EqualTo(1), "a max below 1");

            var back = ResidentProfiles.ParseBrief(ResidentProfiles.Write(StarterResidents.DefaultProfiles(), brief), out _);
            Assert.That(back.Roles.Select(o => o.Max), Is.EqualTo(new[] { 2, 0, 0 }));

            var sim = NewGame();
            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            Assert.That(loaded.Brief.Find("farmer").Max, Is.EqualTo(3));

            var older = new WorldBrief(sim.Brief.Text, sim.Brief.Roles.Select(o => new RoleOption(o.Name, o.About, o.CanTax, o.Skills)));
            Assert.That(older.Find("farmer").Max, Is.EqualTo(0));
            Assert.That(older.UpgradedFromOlderSave().Find("farmer").Max, Is.EqualTo(3), "an older town gets the shipped limits");
        }

        [Test]
        public void AFullRole_CannotBeTaken_AndThePromptsSaySo()
        {
            var sim = NewGame();
            var farmers = sim.Residents.Take(3).ToList();
            var late = sim.Residents[3];
            foreach (var r in farmers)
            {
                Choose(sim, r, "farmer");
            }

            Assert.That(farmers.All(r => r.Role == "farmer"), Is.True);
            Assert.That(sim.HoldersOf("farmer"), Is.EqualTo(3));
            Assert.That(sim.RoleIsFull(sim.Brief.Find("farmer")), Is.True);
            Assert.That(sim.RoleIsFull(sim.Brief.Find("farmer"), farmers[0]), Is.False, "not full for someone already in it");

            var plan = DayPlanPrompt.UserText(sim, late, DayPlan.DayFor(sim.Time));
            Assert.That(plan, Does.Contain("(full, 3 of 3 taken)").And.Contain("(2 of 2 places left)"));
            var roles = (List<object>)((Dictionary<string, object>)((Dictionary<string, object>)DayPlanPrompt.Schema(sim, late)["properties"])["role"])["enum"];
            Assert.That(roles.Contains("farmer"), Is.False, "a full role isn't offered");
            var held = (List<object>)((Dictionary<string, object>)((Dictionary<string, object>)DayPlanPrompt.Schema(sim, farmers[0])["properties"])["role"])["enum"];
            Assert.That(held.Contains("farmer"), Is.True, "a holder can keep it");
            var s = new System.Text.StringBuilder();
            PromptText.AppendRoleAndMeeting(s, sim, late);
            Assert.That(s.ToString(), Does.Contain("Full, so nobody else can take them up until a place comes free: farmer (3 of 3)"));

            Assert.That(sim.ApplyDayPlan(late.Id, DayPlan.DayFor(sim.Time), Plan(), "farmer"), Is.EqualTo(DecisionOutcome.RoleTaken), "asked again");
            Assert.That(late.Role, Is.Null, "the fourth farmer is turned away");
            Assert.That(late.DayPlan, Is.Null, "the plan waits for a role with room");
            Assert.That(sim.ApplyDayPlan(late.Id, DayPlan.DayFor(sim.Time), Plan()), Is.EqualTo(DecisionOutcome.RoleTaken), "no role at all is asked again too");
            Choose(sim, late, "fisher");
            Assert.That(late.Role, Is.EqualTo("fisher"), "another role with room is fine");
        }

        [Test]
        public void AVote_ForAFullRole_DoesNotChangeAnything()
        {
            var sim = NewGame();
            var asker = sim.Residents[0];
            var voter = sim.Residents[1];
            Choose(sim, asker, "farmer");
            Choose(sim, sim.Residents[2], "cook");
            Choose(sim, sim.Residents[3], "cook");
            sim.SetRoleRequest(asker.Id, "cook");
            sim.FirstMeetingDay = sim.Time.Day;
            sim.Advance(TownSimulation.MeetingStartMinute - sim.Time.MinuteOfDay);
            foreach (var r in new[] { asker, voter })
            {
                for (var i = 0; i < 10 && r.State != ResidentState.Deciding; i++)
                {
                    sim.Tick();
                }

                var option = sim.Planner.FreeTimeOptions(r).FirstOrDefault(o => o.Activity == ActivityCatalog.AttendMeeting);
                Assert.That(option, Is.Not.Null);
                Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Meeting"), Is.EqualTo(DecisionOutcome.Applied));
            }

            sim.Advance(TownSimulation.MeetingTurnsFromMinute - sim.Time.MinuteOfDay);
            Assert.That(sim.ApplyMeetingTurn(voter.Id, sim.MeetingTurnOf(voter.Id).Id, "Fine by me.", new Dictionary<string, bool> { [asker.Id] = true }), Is.EqualTo(DecisionOutcome.Applied));
            sim.Advance(TownSimulation.MeetingCloseMinute - sim.Time.MinuteOfDay + 1);
            Assert.That(asker.Role, Is.EqualTo("farmer"), "both cook places are taken");
            Assert.That(sim.RoleRequests.ContainsKey(asker.Id), Is.False);
            Assert.That(sim.History.Events.Last(e => e.Kind == TownEventKind.Meeting).Summary, Does.Contain("every place in that role is taken"));
        }
    
        [Test]
        public void SomeoneWithARole_WhoWantsAFullOne_KeepsTheirPlanAndTheirRole()
        {
            var sim = NewGame();
            Choose(sim, sim.Residents[0], "toolmaker");
            var r = sim.Residents[1];
            Choose(sim, r, "farmer");
            sim.Advance(24 * 60);
            Choose(sim, r, "toolmaker");
            Assert.That(r.Role, Is.EqualTo("farmer"));
            Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.Contains("there can only be 1")), Is.True);
        }

        [Test]
        public void EveryonePlanningAtOnce_AllGetARole_WithinTheLimits()
        {
            // From the owner's log of 6 November: twenty plans were asked for at 07:00, each listing every role as
            // free, most answered toolmaker or lumberjack and fifteen were left with no role and nothing to do but chat.
            var sim = NewGame();
            var favourites = new[] { "toolmaker", "lumberjack", "miner", "cook", "farmer", "fisher", "builder", "water purifier", "merchant", "tax man", "security enforcer", "mayor" };
            var transport = FakeAiTransport.WorkingOllama(body =>
            {
                if (!body.Contains("Plan the rest of your day"))
                {
                    return null;
                }

                var role = favourites.First(f => body.Contains("\"" + f + "\""));
                return "{\"role\":\"" + role + "\",\"plan\":[{\"hour\":9,\"plan\":\"Work\"}]}";
            });
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            sim.Tick();
            for (var i = 0; i < 2000 && sim.Residents.Any(r => r.Role == null); i++)
            {
                ai.Update(i * 0.5);
            }

            Assert.That(sim.Residents.All(r => r.Role != null), Is.True, string.Join(", ", sim.Residents.Where(r => r.Role == null).Select(r => r.FirstName)));
            Assert.That(sim.HoldersOf("toolmaker"), Is.EqualTo(1));
            Assert.That(sim.Brief.Roles.All(o => sim.HoldersOf(o.Name) <= o.Max), Is.True);
        }

        [Test]
        public void ThePrompts_SayWhatNobodyCanHave_WhileItsRoleIsEmpty()
        {
            var sim = NewGame();
            var s = new System.Text.StringBuilder();
            PromptText.AppendRoleAndMeeting(s, sim, sim.Residents[0]);
            Assert.That(s.ToString(), Does.Contain("With no water purifier, nobody can get clean water").And.Contain("With no cook, nobody can eat").And.Contain("With no farmer or fisher, there is no food"));

            Choose(sim, sim.Residents[1], "water purifier");
            Choose(sim, sim.Residents[2], "cook");
            Choose(sim, sim.Residents[3], "fisher");
            s.Clear();
            PromptText.AppendRoleAndMeeting(s, sim, sim.Residents[0]);
            Assert.That(s.ToString(), Does.Not.Contain("With no"));
        }
    }
}
