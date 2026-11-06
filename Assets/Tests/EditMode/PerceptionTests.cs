using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2: residents notice each other and the AI decides whether they go over and talk.</summary>
    public class PerceptionTests
    {
        /// <summary>Mabel and Arthur side by side on the green, each told by the "AI" to take it easy where they are.</summary>
        private static TownSimulation TwoRelaxing(double social = 10)
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon, social);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            foreach (var r in sim.Residents.Where(x => x.State == ResidentState.Deciding))
            {
                Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "Rest", "Ahh"), Is.EqualTo(DecisionOutcome.Applied));
            }

            return sim;
        }

        [Test]
        public void NoticingSomeone_AsksTheAi_AndIsRemembered_ButStartsNoChatByItself()
        {
            var sim = TwoRelaxing();
            var mabel = sim.Residents[0];
            var arthur = sim.Residents[1];
            sim.Tick();

            Assert.That(mabel.Reaction, Is.Not.Null);
            Assert.That(mabel.Reaction.OtherId, Is.EqualTo(arthur.Id));
            Assert.That(mabel.Reaction.Seen, Does.Contain(arthur.FullName));
            var seen = sim.Memories.ForResident(mabel.Id).Single(m => m.Summary.StartsWith("Saw " + arthur.FullName));
            Assert.That(seen.ImportanceRated, Is.True, "observations keep the game's figure, so the AI isn't asked to rate them");

            for (var i = 0; i < 10; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.ActiveConversations.Any(), Is.False, "v1's chance meetings are off; only the AI starts chats");
        }

        [Test]
        public void TheSamePerson_IsNotAskedAboutAgainSoon()
        {
            var sim = TwoRelaxing();
            var mabel = sim.Residents[0];
            sim.Tick();
            var first = mabel.Reaction;
            Assert.That(sim.ApplyReaction(mabel.Id, first.Id, false, "Busy resting"), Is.EqualTo(DecisionOutcome.Applied));

            for (var i = 0; i < PerceptionRules.ReactCooldownMinutes - 5; i++)
            {
                sim.Tick();
                Assert.That(mabel.Reaction == null || mabel.Reaction.OtherId != first.OtherId, Is.True, $"minute {i}");
                if (mabel.State == ResidentState.Deciding)
                {
                    sim.ApplyDecision(mabel.Id, mabel.PendingDecisionId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "Rest", "Ahh");
                }

                var arthur = sim.Residents[1];
                if (arthur.State == ResidentState.Deciding)
                {
                    sim.ApplyDecision(arthur.Id, arthur.PendingDecisionId, FreeTimeOption.RelaxActionId, FreeTimeOption.NoTargetId, "Rest", "Ahh");
                }

                if (arthur.Reaction != null)
                {
                    sim.ApplyReaction(arthur.Id, arthur.Reaction.Id, false, "No");
                }
            }
        }

        [Test]
        public void GoingOver_WalksToThemAndStartsAChat()
        {
            var sim = TwoRelaxing();
            var mabel = sim.Residents[0];
            var arthur = sim.Residents[1];
            sim.Tick();

            Assert.That(sim.ApplyReaction(mabel.Id, mabel.Reaction.Id, true, "Haven't caught up in ages"), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(mabel.TargetResidentId, Is.EqualTo(arthur.Id));
            Assert.That(mabel.GoalSource, Is.EqualTo(DecisionSource.Ai));
            for (var i = 0; i < 5 && !sim.ActiveConversations.Any(); i++)
            {
                sim.Tick();
            }

            Assert.That(sim.ActiveConversations.Any(c => c.ParticipantIds.Contains(mabel.Id) && c.ParticipantIds.Contains(arthur.Id)), Is.True);
        }

        [Test]
        public void OldOrWrongAnswers_AreStale()
        {
            var sim = TwoRelaxing();
            var mabel = sim.Residents[0];
            sim.Tick();
            var reaction = mabel.Reaction;

            Assert.That(sim.ApplyReaction(mabel.Id, reaction.Id + 99, true, "x"), Is.EqualTo(DecisionOutcome.Stale));
            for (var i = 0; i <= PerceptionRules.ReactWaitMinutes + 1; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.ApplyReaction(mabel.Id, reaction.Id, true, "too late"), Is.EqualTo(DecisionOutcome.Stale));
            Assert.That(mabel.TargetResidentId, Is.Null);
        }

        [Test]
        public void NobodyAsleepOrAtWork_IsNoticedOrNotices()
        {
            var sim = TwoRelaxing();
            var arthur = sim.Residents[1];
            arthur.ActivityId = ActivityCatalog.Sleep.Id;

            Assert.That(TownSimulation.FreeToTalk(arthur), Is.False);
            Assert.That(TownSimulation.CanReact(arthur), Is.False);
            sim.Tick();
            Assert.That(sim.Residents[0].Reaction, Is.Null);
        }

        [Test]
        public void TheOldTown_HasNoReactions()
        {
            var sim = TestTown.TwoSideBySide(TestTown.WeekendAfternoon, 10);
            for (var i = 0; i < 30; i++)
            {
                sim.Tick();
            }

            Assert.That(sim.Residents.All(r => r.Reaction == null), Is.True);
        }

        [Test]
        public void AiService_AsksReactAndAppliesTheAnswer()
        {
            var sim = TwoRelaxing();
            sim.Tick();
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("go over to talk") ? "{\"react\":true,\"reason\":\"Fancy a natter\"}" : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 40 && sim.Residents.All(r => r.TargetResidentId == null && r.State != ResidentState.Talking); i++)
            {
                ai.Update(i * 0.1);
            }

            Assert.That(transport.Calls.Any(c => c.Body != null && c.Body.Contains("You notice")), Is.True);
            Assert.That(sim.Residents.Any(r => r.TargetResidentId != null), Is.True);
        }

        [TestCase("{\"react\":true,\"reason\":\"Hi\"}", true)]
        [TestCase("{\"react\":false}", true)]
        [TestCase("{\"react\":\"maybe\"}", false)]
        [TestCase("nope", false)]
        public void ReactionPrompt_NeedsATrueOrFalse(string content, bool ok)
        {
            Assert.That(ReactionPrompt.TryParse(content, out _, out _), Is.EqualTo(ok));
        }
    }
}
