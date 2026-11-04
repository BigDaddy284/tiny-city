using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2: the AI makes every choice, nothing falls back on the routine, and the town waits for it.</summary>
    public class AiRunsEverythingTests
    {
        private static TownSimulation Town(System.Action<Needs> adjust = null)
        {
            var sim = TestTown.WithResident(TestTown.WorkHour, adjust: adjust);
            sim.AiRunsEverything = true;
            return sim;
        }

        [Test]
        public void WithoutTheAi_NobodyActsOnTheRoutine_TheyWait()
        {
            var sim = Town(n => n[NeedType.Hunger] = 5);
            for (var i = 0; i < 120; i++)
            {
                sim.Tick();
            }

            var r = sim.Residents[0];
            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding));
            Assert.That(r.ActivityId, Is.Null.Or.Empty);
            Assert.That(sim.WaitingForAi, Is.True);
        }

        [Test]
        public void Options_IncludeEatingSleepingAndTheirOwnJob_WhateverTheirNeeds()
        {
            var sim = Town();
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];
            var actions = sim.FreeTimeOptionsFor(r).Select(o => o.ActionId).ToList();

            Assert.That(actions, Does.Contain(ActivityCatalog.Work.Id));
            Assert.That(actions, Does.Contain(ActivityCatalog.Sleep.Id));
            Assert.That(actions, Does.Contain(ActivityCatalog.Snack.Id));
            Assert.That(sim.FreeTimeOptionsFor(r).Where(o => o.ActionId == ActivityCatalog.Work.Id).All(o => o.TargetId == r.Job.WorkObjectId), Is.True);
        }

        [Test]
        public void TheAiCanChooseToGoToWork()
        {
            var sim = Town();
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];

            var outcome = sim.ApplyDecision(r.Id, r.PendingDecisionId, ActivityCatalog.Work.Id, r.Job.WorkObjectId, "Shift starts", "Off to work");

            Assert.That(outcome, Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.ActivityId, Is.EqualTo(ActivityCatalog.Work.Id));
        }

        [Test]
        public void GivingUpOnAnAnswer_AsksAgain_InsteadOfUsingTheRoutine()
        {
            var sim = Town();
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];
            var first = r.PendingDecisionId;

            Assert.That(sim.AbandonDecision(r.Id, first, "answer unusable"), Is.True);
            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding));
            Assert.That(r.PendingDecisionId, Is.GreaterThan(first));
        }

        [Test]
        public void NothingInterruptsWhatTheAiChose_EvenAnUrgentNeed()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 20);
            sim.AiRunsEverything = true;
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];
            var read = sim.FreeTimeOptionsFor(r).First(o => o.ActionId == ActivityCatalog.Read.Id);
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, read.ActionId, read.TargetId, "Reading", "Book time"), Is.EqualTo(DecisionOutcome.Applied));
            r.Needs[NeedType.Hunger] = 1;

            for (var i = 0; i < 10; i++)
            {
                sim.Tick();
            }

            Assert.That(r.ActivityId, Is.EqualTo(ActivityCatalog.Read.Id));
        }

        [Test]
        public void Conversations_WaitForTheAi_AndNeverUseSmallTalk()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon, social: 10);
            sim.AiRunsEverything = true;
            sim.AiDecisionsEnabled = true;
            var mabel = sim.Residents[0];
            var arthur = sim.Residents[1];
            for (var i = 0; i < 3 && !sim.ActiveConversations.Any(); i++)
            {
                sim.Tick();
                foreach (var r in sim.Residents.Where(x => x.State == ResidentState.Deciding))
                {
                    var other = r == mabel ? arthur : mabel;
                    var chat = sim.FreeTimeOptionsFor(r).FirstOrDefault(o => o.Partner == other);
                    if (chat != null)
                    {
                        sim.ApplyDecision(r.Id, r.PendingDecisionId, chat.ActionId, chat.TargetId, "Chat", "Hello");
                    }
                }
            }

            for (var i = 0; i < 30 && !sim.ActiveConversations.Any(c => c.AwaitingLine); i++)
            {
                sim.Tick();
            }

            var session = sim.ActiveConversations.FirstOrDefault(c => c.AwaitingLine);
            Assert.That(session, Is.Not.Null, "a chat should start and wait for its first line");
            var lineId = session.LineRequestId;
            for (var i = 0; i < 60; i++)
            {
                sim.Tick();
            }

            Assert.That(session.Lines.Count, Is.EqualTo(0));
            Assert.That(session.AwaitingLine, Is.True);
            Assert.That(sim.UseSmallTalk(session.Id, lineId), Is.False);
            Assert.That(session.LineRequestId, Is.GreaterThan(lineId), "a failed line is asked for again");
        }

        [Test]
        public void MemoryWork_WaitsWhileAChoiceIsPending()
        {
            var sim = Town();
            sim.Memories.Add(sim.Residents[0].Id, sim.Time, TinyCity.Simulation.Memory.MemoryType.Event, 3, "Saw a fox");
            sim.Tick();
            var transport = FakeAiTransport.WorkingOllama(_ => null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 15; i++)
            {
                ai.Update(i * 0.1);
            }

            Assert.That(sim.WaitingForAi, Is.True);
            Assert.That(transport.Calls.Any(c => c.Body.Contains("\"ratings\"")), Is.False);
            Assert.That(transport.Calls.Any(c => c.Body.Contains("Nobody decides for you")), Is.True);
        }
    }
}
