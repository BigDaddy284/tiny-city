using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2: residents plan their day with the AI before their first choice, and the plan guides them.</summary>
    public class DayPlanTests
    {
        private const string PlanJson = "{\"plan\":[{\"hour\":9,\"plan\":\"Gather wood\"},{\"hour\":13,\"plan\":\"Cook lunch\"},{\"hour\":19,\"plan\":\"Sit by the pond\"}]}";

        private static TownSimulation Town()
        {
            var sim = TestTown.WithResident(TinyCity.Simulation.Time.GameTime.FromDayAndTime(1, 10, 0));
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            return sim;
        }

        [Test]
        public void PlanDay_RunsFromFourInTheMorning()
        {
            Assert.That(DayPlan.DayFor(GameTime.FromDayAndTime(2, 3, 59)), Is.EqualTo(1));
            Assert.That(DayPlan.DayFor(GameTime.FromDayAndTime(2, 4, 0)), Is.EqualTo(2));
            Assert.That(DayPlan.DayFor(GameTime.FromDayAndTime(2, 23, 0)), Is.EqualTo(2));
        }

        [Test]
        public void Make_KeepsValidHoursOnce_InDayOrder()
        {
            var plan = DayPlan.Make(3, new GameTime(0), new[]
            {
                new DayPlanEntry(23, "Late film"),
                new DayPlanEntry(1, "Bed at last"),
                new DayPlanEntry(9, "Work"),
                new DayPlanEntry(9, "Something else at nine"),
                new DayPlanEntry(24, "No such hour"),
                new DayPlanEntry(-1, "Nor this"),
                new DayPlanEntry(12, "   "),
                null,
            });

            Assert.That(plan.Entries.Select(e => e.Hour).ToList(), Is.EqualTo(new List<int> { 9, 23, 1 }));
            Assert.That(plan.Entries[0].Text, Is.EqualTo("Work"));
            Assert.That(DayPlan.Make(3, new GameTime(0), new[] { new DayPlanEntry(30, "x") }), Is.Null);
            Assert.That(DayPlan.Make(3, new GameTime(0), Enumerable.Range(0, 24).Select(h => new DayPlanEntry(h, "hour " + h))).Entries.Count, Is.EqualTo(DayPlan.MaxEntries));
        }

        [Test]
        public void Now_IsTheLatestPartThatHasStarted()
        {
            var plan = DayPlan.Make(1, new GameTime(0), new[] { new DayPlanEntry(9, "Work"), new DayPlanEntry(18, "Dinner"), new DayPlanEntry(1, "Sleep") });

            Assert.That(plan.Now(GameTime.FromDayAndTime(1, 8, 0)), Is.Null);
            Assert.That(plan.Now(GameTime.FromDayAndTime(1, 12, 30)).Text, Is.EqualTo("Work"));
            Assert.That(plan.Now(GameTime.FromDayAndTime(1, 22, 0)).Text, Is.EqualTo("Dinner"));
            Assert.That(plan.Now(GameTime.FromDayAndTime(2, 2, 0)).Text, Is.EqualTo("Sleep"));
        }

        [TestCase(PlanJson, true, 3)]
        [TestCase("{\"plan\":[{\"hour\":\"nine\",\"plan\":\"Work\"},{\"hour\":10,\"plan\":\"Shop\"}]}", true, 1)]
        [TestCase("{\"plan\":[{\"hour\":40,\"plan\":\"Work\"}]}", false, 0)]
        [TestCase("{\"plan\":[]}", false, 0)]
        [TestCase("{\"steps\":[]}", false, 0)]
        [TestCase("not json", false, 0)]
        public void TryParse_KeepsOnlyUsableEntries(string content, bool ok, int count)
        {
            Assert.That(DayPlanPrompt.TryParse(content, out var entries, out var error), Is.EqualTo(ok), error);
            Assert.That(entries.Count, Is.EqualTo(count));
        }

        [Test]
        public void ApplyDayPlan_AcceptsOnePlanForToday_AndRejectsOthers()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var today = DayPlan.DayFor(sim.Time);
            DayPlanPrompt.TryParse(PlanJson, out var entries, out _);

            Assert.That(sim.NeedsDayPlan(r), Is.True);
            Assert.That(sim.ApplyDayPlan(r.Id, today + 1, entries), Is.EqualTo(DecisionOutcome.Stale));
            Assert.That(sim.ApplyDayPlan(r.Id, today, new List<DayPlanEntry>()), Is.EqualTo(DecisionOutcome.InvalidText));
            Assert.That(sim.ApplyDayPlan("res-9999", today, entries), Is.EqualTo(DecisionOutcome.UnknownResident));
            Assert.That(sim.ApplyDayPlan(r.Id, today, entries), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(sim.ApplyDayPlan(r.Id, today, entries), Is.EqualTo(DecisionOutcome.Stale));
            Assert.That(sim.NeedsDayPlan(r), Is.False);
            Assert.That(r.DayPlan.Entries.Count, Is.EqualTo(3));
        }

        [Test]
        public void ThePlan_GoesIntoTheirChoicesAndTheirReflection()
        {
            var sim = Town();
            var r = sim.Residents[0];
            DayPlanPrompt.TryParse(PlanJson, out var entries, out _);
            sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), entries);

            var decision = DecisionPrompt.UserText(sim, r, sim.FreeTimeOptionsFor(r), new List<TinyCity.Simulation.Memory.MemoryRecord>());
            Assert.That(decision, Does.Contain("Your plan for today: 09:00 Gather wood; 13:00 Cook lunch; 19:00 Sit by the pond."));
            Assert.That(decision, Does.Contain("Right now your plan says: Gather wood."));
            Assert.That(ReflectionPrompt.BuildBody("m", sim, r, sim.Time.Day), Does.Contain("What you planned this morning"));
        }

        [Test]
        public void AiService_AsksForThePlanFirst_ThenTheChoiceWithThePlanInIt()
        {
            var sim = Town();
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("Plan the rest of your day") ? PlanJson : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 30; i++)
            {
                ai.Update(i * 0.1);
            }

            var chats = transport.Calls.Where(c => c.Url.EndsWith("/api/chat") && !c.Body.Contains("\"messages\": []")).ToList();
            Assert.That(chats.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(chats[0].Body, Does.Contain("Plan the rest of your day"));
            Assert.That(sim.Residents[0].DayPlan, Is.Not.Null);
            Assert.That(chats.Skip(1).Any(c => c.Body.Contains("Your plan for today") && c.Body.Contains("Choose one option")), Is.True);
        }

        [Test]
        public void AiService_WhenThePlanCantBeMade_TheyChooseWithoutOne()
        {
            var sim = Town();
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("Plan the rest of your day") ? "not json" : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 60; i++)
            {
                ai.Update(i * 0.5);
            }

            Assert.That(sim.Residents[0].DayPlan, Is.Null);
            Assert.That(transport.Calls.Count(c => c.Body != null && c.Body.Contains("Plan the rest of your day")), Is.LessThanOrEqualTo(new OllamaSettings().maxRetries + 1));
            Assert.That(transport.Calls.Any(c => c.Body != null && c.Body.Contains("Choose one option")), Is.True);
        }
    
        [Test]
        public void AiService_AsksForAGivenUpPlanAgain_ALittleLater()
        {
            var sim = Town();
            var working = false;
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("Plan the rest of your day") ? (working ? PlanJson : "not json") : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 60; i++)
            {
                ai.Update(i * 0.5);
            }

            Assert.That(sim.Residents[0].DayPlan, Is.Null, "given up on for now");
            working = true;
            for (var i = 60; i < 60 + (int)(AiService.DayPlanRetrySeconds * 2) + 20; i++)
            {
                ai.Update(i * 0.5);
                if (sim.Residents[0].State != ResidentState.Deciding)
                {
                    sim.Tick();
                }
            }

            Assert.That(sim.Residents[0].DayPlan, Is.Not.Null, "they are asked again, not left without a plan all day");
        }

        [Test]
        public void SomeoneWithoutARole_MustNameOneWithAPlaceLeft()
        {
            var sim = TinyCity.Simulation.Content.FlatLand.CreateNewGame();
            var r = sim.Residents[0];
            var schema = DayPlanPrompt.Schema(sim, r);
            Assert.That((List<object>)schema["required"], Does.Contain("role"));
            Assert.That(DayPlanPrompt.RoleChoices(sim, r), Does.Not.Contain(TownSimulation.RoleNone));
            Assert.That(DayPlanPrompt.UserText(sim, r, DayPlan.DayFor(sim.Time)), Does.Contain("Your first role is yours straight away"));

            Assert.That(sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), new List<DayPlanEntry> { new DayPlanEntry(9, "Fish") }, "fisher"), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(DayPlanPrompt.RoleChoices(sim, r), Does.Contain(TownSimulation.RoleNone), "once they have one they may give it up");
            Assert.That((List<object>)DayPlanPrompt.Schema(sim, r)["required"], Does.Not.Contain("role"));
        }

        [Test]
        public void ImportanceRatings_MayOnlyNameTheMemoriesAskedAbout()
        {
            var sim = TinyCity.Simulation.Content.FlatLand.CreateNewGame();
            var r = sim.Residents[0];
            var memories = sim.Memories.ForResident(r.Id).Take(3).ToList();
            var body = ImportancePrompt.BuildBody("m", sim, r, memories);
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(body, "\"maxItems\":\\s*" + memories.Count), Is.True, body);
            foreach (var m in memories)
            {
                Assert.That(body, Does.Contain($"\"{m.Id}\""));
            }
        }
    }
}
