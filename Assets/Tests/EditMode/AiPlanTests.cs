using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>One AI answer planning a few free-time activities, each checked again by the game when it is due.</summary>
    public class AiPlanTests
    {
        private const string Armchair = "obj-cottage1-armchair";
        private static readonly PlanStep RelaxStep = new PlanStep(FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId);

        private static TownSimulation DecidingTown(out Resident r)
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            r = sim.Residents[0];
            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding));
            return sim;
        }

        private static void Relax(TownSimulation sim, Resident r, params PlanStep[] then) =>
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "Feet up", string.Empty, then), Is.EqualTo(DecisionOutcome.Applied));

        [Test]
        public void LaterSteps_StartWithoutAskingAgain()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, new PlanStep("read", Armchair));
            Assert.That(r.PlannedSteps.Count, Is.EqualTo(1));

            sim.Advance(TownSimulation.RelaxMinutes + 1);

            Assert.That(r.ActivityId, Is.EqualTo("read"));
            Assert.That(r.TargetObjectId, Is.EqualTo(Armchair));
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Ai));
            Assert.That(r.PlanStepsTaken, Is.EqualTo(1));
            Assert.That(r.PlannedSteps.Count, Is.EqualTo(0));
            Assert.That(r.Log.Any(e => e.Text.Contains("(AI plan)")), Is.True);
        }

        [Test]
        public void StepsThatAreNotOptionsNow_AreNotKept()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, new PlanStep("work", "obj-village-garden-plot"), new PlanStep("fly", "none"), null);

            Assert.That(r.PlannedSteps.Count, Is.EqualTo(0));
        }

        [Test]
        public void APlan_HasAtMostTheStepLimit()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, RelaxStep, RelaxStep, RelaxStep, RelaxStep);

            Assert.That(r.PlannedSteps.Count, Is.EqualTo(TownSimulation.MaxPlanSteps - 1));
        }

        [Test]
        public void AStepThatNoLongerFits_IsSkipped()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, new PlanStep("read", Armchair), RelaxStep);
            r.Needs[NeedType.Fun] = 100;

            sim.Advance(TownSimulation.RelaxMinutes + 1);

            Assert.That(r.State, Is.EqualTo(ResidentState.Waiting), "reading no longer helps, so the plan moves on to taking it easy");
            Assert.That(r.PlanStepsTaken, Is.EqualTo(1));
            Assert.That(r.Goal, Does.Contain("Planned"));
        }

        [Test]
        public void AGoneTarget_FallsBackToTheNearestForTheSameActivity()
        {
            var sim = DecidingTown(out var r);
            var bench = sim.FreeTimeOptionsFor(r).First(o => o.ActionId == "watch-town");
            Relax(sim, r, new PlanStep("watch-town", "obj-somewhere-else"));

            sim.Advance(TownSimulation.RelaxMinutes + 1);

            Assert.That(r.ActivityId, Is.EqualTo("watch-town"));
            Assert.That(r.TargetObjectId, Is.EqualTo(bench.TargetId));
        }

        [Test]
        public void AnOldPlan_IsDropped_AndTheyAskAgain()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, new PlanStep("read", Armchair));
            r.PlanExpires = sim.Time.TotalMinutes;

            sim.Advance(TownSimulation.RelaxMinutes + 1);

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding));
            Assert.That(r.PlanStepsTaken, Is.EqualTo(0));
            Assert.That(r.PlannedSteps.Count, Is.EqualTo(0));
        }

        [Test]
        public void TurningTheAiOff_DropsThePlan()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, new PlanStep("read", Armchair));
            sim.AiDecisionsEnabled = false;

            sim.Advance(TownSimulation.RelaxMinutes + 1);

            Assert.That(r.PlanStepsTaken, Is.EqualTo(0));
            Assert.That(r.PlannedSteps.Count, Is.EqualTo(0));
        }

        [Test]
        public void Bedtime_DropsThePlan()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, RelaxStep, RelaxStep);

            sim.Advance(SleepSchedule.MinutesToBed(r, sim.Time) + 30);

            Assert.That(r.ActivityId, Is.EqualTo(ActivityCatalog.Sleep.Id));
            Assert.That(r.PlannedSteps.Count, Is.EqualTo(0));
        }

        [Test]
        public void APlan_IsNotSaved()
        {
            var sim = DecidingTown(out var r);
            Relax(sim, r, new PlanStep("read", Armchair));

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation;

            Assert.That(loaded.Residents[0].PlannedSteps.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reply_ReadsTheThenList()
        {
            var sim = DecidingTown(out var r);
            var options = sim.FreeTimeOptionsFor(r);
            var content = "{\"action\":\"read\",\"target_id\":\"" + Armchair + "\",\"reason\":\"x\",\"say\":\"y\",\"then\":["
                + "{\"action\":\"watch-town\",\"target_id\":\"obj-village-bench-pond\"},{\"target_id\":\"no-action\"},{\"action\":\"relax\"},{\"action\":\"paint\"}]}";

            Assert.That(DecisionPrompt.TryParse(content, options, out var reply, out var error), Is.True, error);
            var steps = reply.Then.Select(s => s.ActionId + "@" + (s.TargetId ?? "-")).ToList();
            Assert.That(steps, Is.EqualTo(new List<string> { "watch-town@obj-village-bench-pond", "relax@-" }));
        }

        [TestCase("{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"x\",\"say\":\"y\"}")]
        [TestCase("{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"x\",\"say\":\"y\",\"then\":\"read\"}")]
        public void Reply_WithoutAUsableThenList_StillCounts(string content)
        {
            var sim = DecidingTown(out var r);

            Assert.That(DecisionPrompt.TryParse(content, sim.FreeTimeOptionsFor(r), out var reply, out var error), Is.True, error);
            Assert.That(reply.Then.Count, Is.EqualTo(0));
        }

        [Test]
        public void Schema_AsksForUpToTwoMoreSteps()
        {
            var sim = DecidingTown(out var r);
            var schema = DecisionPrompt.Schema(sim.FreeTimeOptionsFor(r));
            var then = (Dictionary<string, object>)((Dictionary<string, object>)schema["properties"])["then"];

            Assert.That(then["type"], Is.EqualTo("array"));
            Assert.That(then["maxItems"], Is.EqualTo(TownSimulation.MaxPlanSteps - 1));
            Assert.That(((List<object>)schema["required"]).Contains("then"), Is.True);
        }
    }
}
