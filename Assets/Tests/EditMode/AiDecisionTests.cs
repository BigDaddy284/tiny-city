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
        private const string Fishing = "obj-flat-fishing-1";

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
        public void ValidSuggestion_IsApplied_WithItsReason_AndNothingSaid()
        {
            var sim = DecidingTown(out var r);

            var outcome = sim.ApplyDecision(r.Id, r.PendingDecisionId, "catch-fish", Fishing, "  Fish for supper\n");

            Assert.That(outcome, Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.ActivityId, Is.EqualTo("catch-fish"));
            Assert.That(r.TargetObjectId, Is.EqualTo(Fishing));
            Assert.That(r.Goal, Is.EqualTo("Fish for supper"));
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Ai));
            Assert.That(sim.Conversations.All, Is.Empty, "a choice says nothing out loud: bubbles are only for chats");
            Assert.That(r.Log.Last().Text, Does.EndWith("(AI)"));
        }

        [Test]
        public void RelaxSuggestion_TakesItEasy()
        {
            var sim = DecidingTown(out var r);

            var outcome = sim.ApplyDecision(r.Id, r.PendingDecisionId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "Just resting my feet");

            Assert.That(outcome, Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.State, Is.EqualTo(ResidentState.Waiting));
            Assert.That(r.MinutesRemaining, Is.EqualTo(TownSimulation.RelaxMinutes));
            Assert.That(sim.Conversations.All, Is.Empty, "an empty line is not recorded");
        }

        [Test]
        public void WithoutAi_ResidentsWaitToBeAsked_AndTheWaitHasNoEnd()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            sim.Advance(24 * 60);

            Assert.That(sim.Residents[0].State, Is.EqualTo(ResidentState.Deciding), "nothing decides for them");
        }

        [TestCase("fly-to-the-moon", "obj-does-not-exist")]
        public void SuggestionsNothingCanMend_AreRejected(string action, string target)
        {
            var sim = DecidingTown(out var r);

            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, action, target, "why not"), Is.Not.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), "a rejected suggestion changes nothing");
            Assert.That(sim.Conversations.All, Is.Empty);
        }

        [Test]
        public void AnswerForAnOldDecision_IsStale()
        {
            var sim = DecidingTown(out var r);
            var oldId = r.PendingDecisionId;
            sim.ApplyDecision(r.Id, oldId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "first answer");

            Assert.That(sim.ApplyDecision(r.Id, oldId, "catch-fish", Fishing, "late"), Is.EqualTo(DecisionOutcome.Stale));
            Assert.That(sim.ApplyDecision("res-nobody", 1, "catch-fish", Fishing, "?"), Is.EqualTo(DecisionOutcome.UnknownResident));
        }

        [Test]
        public void DeceasedResident_CannotBeDirected()
        {
            var sim = DecidingTown(out var r);
            var id = r.PendingDecisionId;
            r.IsAlive = false;

            Assert.That(sim.ApplyDecision(r.Id, id, "catch-fish", Fishing, "?"), Is.EqualTo(DecisionOutcome.NotAlive));
        }

        [Test]
        public void LongReasons_AreShortened()
        {
            var sim = DecidingTown(out var r);
            sim.ApplyDecision(r.Id, r.PendingDecisionId, "catch-fish", Fishing, string.Concat(Enumerable.Repeat("very ", 100)));

            Assert.That(r.Goal.Length, Is.LessThanOrEqualTo(TownSimulation.MaxReasonLength));
        }
    }
}
