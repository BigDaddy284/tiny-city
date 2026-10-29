using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The simulation's own checks on AI suggestions, independent of Ollama and the queue.</summary>
    public class AiDecisionTests
    {
        private const string Armchair = "obj-cottage1-armchair";

        private static TownSimulation DecidingTown(out Resident r)
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            r = sim.Residents[0];
            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding));
            return sim;
        }

        [Test]
        public void WithoutAi_ResidentsNeverWaitForDecisions()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            for (var i = 0; i < 24 * 60; i++)
            {
                sim.Tick();
                Assert.That(sim.Residents[0].State, Is.Not.EqualTo(ResidentState.Deciding));
            }
        }

        [Test]
        public void ValidSuggestion_IsApplied_WithReasonAndSpeech()
        {
            var sim = DecidingTown(out var r);

            var outcome = sim.ApplyDecision(r.Id, r.PendingDecisionId, "read", Armchair, "  A good book is calling\n", "\"Chapter three, here I come!\"");

            Assert.That(outcome, Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.ActivityId, Is.EqualTo("read"));
            Assert.That(r.TargetObjectId, Is.EqualTo(Armchair));
            Assert.That(r.Goal, Is.EqualTo("A good book is calling"));
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Ai));
            Assert.That(sim.Conversations.LastLineBy(r.Id).Text, Is.EqualTo("Chapter three, here I come!"));
            Assert.That(r.Log.Last().Text, Does.EndWith("(AI)"));
        }

        [Test]
        public void RelaxSuggestion_TakesItEasy()
        {
            var sim = DecidingTown(out var r);

            var outcome = sim.ApplyDecision(r.Id, r.PendingDecisionId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "Just resting my feet", string.Empty);

            Assert.That(outcome, Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.State, Is.EqualTo(ResidentState.Waiting));
            Assert.That(r.MinutesRemaining, Is.EqualTo(TownSimulation.RelaxMinutes));
            Assert.That(sim.Conversations.All, Is.Empty, "an empty line is not recorded");
        }

        [TestCase("work", "obj-village-garden-plot", DecisionOutcome.InvalidAction)]
        [TestCase("sleep", "obj-cottage1-bed", DecisionOutcome.InvalidAction)]
        [TestCase("fly-to-the-moon", "none", DecisionOutcome.InvalidAction)]
        [TestCase("read", "obj-cottage1-bed", DecisionOutcome.InvalidTarget)]
        [TestCase("read", "obj-does-not-exist", DecisionOutcome.InvalidTarget)]
        [TestCase("relax", "obj-cottage1-armchair", DecisionOutcome.InvalidTarget)]
        public void SuggestionsOutsideTheOfferedOptions_AreRejected(string action, string target, DecisionOutcome expected)
        {
            var sim = DecidingTown(out var r);

            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, action, target, "why not", "hi"), Is.EqualTo(expected));
            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), "a rejected suggestion changes nothing");
            Assert.That(sim.Conversations.All, Is.Empty);
        }

        [Test]
        public void CareActivityForAFullNeed_IsNotOffered()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, needsLevel: 100, adjust: n => n[NeedType.Fun] = 30);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];

            Assert.That(sim.Planner.FreeTimeOptions(r).Any(o => o.ActionId == "eat"), Is.False);
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, "eat", "obj-cottage1-kitchen", "Snack!", string.Empty), Is.EqualTo(DecisionOutcome.InvalidAction));
        }

        [Test]
        public void AnswerForAnOldDecision_IsStale()
        {
            var sim = DecidingTown(out var r);
            var oldId = r.PendingDecisionId;
            sim.AiDecisionsEnabled = false;
            sim.Tick();
            sim.AiDecisionsEnabled = true;

            Assert.That(sim.ApplyDecision(r.Id, oldId, "read", Armchair, "late", string.Empty), Is.EqualTo(DecisionOutcome.Stale));
            Assert.That(sim.ApplyDecision("res-nobody", 1, "read", Armchair, "?", string.Empty), Is.EqualTo(DecisionOutcome.UnknownResident));
        }

        [Test]
        public void DeceasedResident_CannotBeDirected()
        {
            var sim = DecidingTown(out var r);
            var id = r.PendingDecisionId;
            r.IsAlive = false;

            Assert.That(sim.ApplyDecision(r.Id, id, "read", Armchair, "?", string.Empty), Is.EqualTo(DecisionOutcome.NotAlive));
        }

        [Test]
        public void UrgentNeedWhileDeciding_IsHandledByTheRoutineAtOnce()
        {
            var sim = DecidingTown(out var r);
            r.Needs[NeedType.Thirst] = 5;
            sim.Tick();

            Assert.That(r.State, Is.Not.EqualTo(ResidentState.Deciding));
            Assert.That(r.ActivityId, Is.EqualTo("drink"));
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Routine));
        }

        [Test]
        public void NoAnswerInTime_TheRoutineDecides()
        {
            var sim = DecidingTown(out var r);
            sim.Advance(sim.DecisionWaitMinutes);

            Assert.That(r.State, Is.Not.EqualTo(ResidentState.Deciding));
            Assert.That(r.ActivityId, Is.Not.Null);
            Assert.That(r.Log.Any(e => e.Text.Contains("no AI answer in time")), Is.True);
        }

        [Test]
        public void LongReasons_AreShortened()
        {
            var sim = DecidingTown(out var r);
            sim.ApplyDecision(r.Id, r.PendingDecisionId, "read", Armchair, string.Concat(Enumerable.Repeat("very ", 100)), new string('a', 500));

            Assert.That(r.Goal.Length, Is.LessThanOrEqualTo(TownSimulation.MaxReasonLength));
            Assert.That(sim.Conversations.LastLineBy(r.Id).Text.Length, Is.LessThanOrEqualTo(160));
        }
    }
}
