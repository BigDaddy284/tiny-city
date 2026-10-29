using System;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The whole AI loop against a scripted Ollama: connection, decisions, fallbacks and reflections.</summary>
    public class AiServiceTests
    {
        private const string ReadAtArmchair = "{\"action\":\"read\",\"target_id\":\"obj-cottage1-armchair\",\"reason\":\"A good book is calling\",\"say\":\"Chapter three, here I come!\"}";

        private double now;

        [SetUp]
        public void SetUp() => now = 0;

        private static TownSimulation FreeAfternoon() =>
            TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);

        /// <summary>Lets the service find Ollama and load the model before the town starts moving.</summary>
        private AiService Connected(TownSimulation sim, FakeAiTransport transport, OllamaSettings settings = null)
        {
            var ai = new AiService(settings ?? new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 3; i++)
            {
                ai.Update(now);
                now += 0.1;
            }

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));
            return ai;
        }

        /// <summary>One game minute per real second, like the game at 1x speed.</summary>
        private void Run(AiService ai, TownSimulation sim, int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                ai.Update(now);
                sim.Tick();
                now += 1;
            }

            ai.Update(now);
        }

        [Test]
        public void WorkingOllama_ResidentFollowsTheAiChoice()
        {
            var sim = FreeAfternoon();
            var transport = FakeAiTransport.WorkingOllama(_ => ReadAtArmchair);
            var ai = Connected(sim, transport, new OllamaSettings());

            Run(ai, sim, 5);
            var r = sim.Residents[0];

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));
            Assert.That(r.ActivityId, Is.EqualTo("read"));
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Ai));
            Assert.That(r.Goal, Is.EqualTo("A good book is calling"));
            Assert.That(sim.Conversations.LastLineBy(r.Id).Text, Is.EqualTo("Chapter three, here I come!"));
            Assert.That(ai.Queue.Stats.Accepted, Is.EqualTo(1));

            var prompt = transport.Calls.Last(c => c.Body.Contains("free time")).Body;
            Assert.That(prompt, Does.Contain("Mabel Thornbury"));
            Assert.That(prompt, Does.Contain("obj-cottage1-armchair"));
            Assert.That(prompt, Does.Contain("\"stream\": false"));
            Assert.That(prompt, Does.Contain("\"format\""));
        }

        [TestCase("I think I'll read a book!")]
        [TestCase("{\"action\":")]
        [TestCase("{\"target_id\":\"obj-cottage1-armchair\"}")]
        [TestCase("{\"action\":\"work\",\"target_id\":\"obj-village-garden-plot\",\"reason\":\"x\",\"say\":\"x\"}")]
        [TestCase("{\"action\":\"read\",\"target_id\":\"obj-cottage1-bed\",\"reason\":\"x\",\"say\":\"x\"}")]
        [TestCase("")]
        public void UnusableAnswers_AreRetriedOnce_ThenTheRoutineTakesOver(string answer)
        {
            var sim = FreeAfternoon();
            var ai = Connected(sim, FakeAiTransport.WorkingOllama(_ => answer), new OllamaSettings { maxRetries = 1 });

            Run(ai, sim, 10);
            var r = sim.Residents[0];

            Assert.That(ai.Queue.Stats.InvalidReplies, Is.EqualTo(2));
            Assert.That(ai.Queue.Stats.GaveUp, Is.EqualTo(1));
            Assert.That(r.State, Is.Not.EqualTo(ResidentState.Deciding));
            Assert.That(r.ActivityId, Is.Not.Null);
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Routine));
            Assert.That(r.Log.Any(e => e.Text.Contains("usual routine")), Is.True);
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected), "bad answers are not a lost connection");
        }

        [Test]
        public void AnswerWrappedInProseAndCodeFences_IsStillUnderstood()
        {
            var sim = FreeAfternoon();
            var ai = Connected(sim, FakeAiTransport.WorkingOllama(_ => "Sure!\n```json\n" + ReadAtArmchair + "\n```"), new OllamaSettings());

            Run(ai, sim, 5);

            Assert.That(sim.Residents[0].GoalSource, Is.EqualTo(DecisionSource.Ai));
        }

        [Test]
        public void SlowAi_TheRoutineDecidesAtTheDeadline_AndTheRequestIsDropped()
        {
            var sim = FreeAfternoon();
            var transport = FakeAiTransport.WorkingOllama(_ => null);
            var ai = Connected(sim, transport, new OllamaSettings { decisionWaitMinutes = 10 });

            Run(ai, sim, 15);
            var r = sim.Residents[0];

            Assert.That(r.Log.Any(e => e.Text.Contains("no AI answer in time")), Is.True);
            Assert.That(r.GoalSource, Is.EqualTo(DecisionSource.Routine));
            Assert.That(transport.Calls.Single(c => c.Body.Contains("free time")).Cancelled, Is.True);
        }

        [Test]
        public void OllamaNotRunning_TheTownLivesAFullDayOnItsRoutine()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(2, 6, 0));
            var transport = new FakeAiTransport { Responder = call => call.Fail("Cannot connect to destination host (HTTP 0)") };
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);

            for (var i = 0; i < 24 * 60; i++)
            {
                Run(ai, sim, 1);
                Assert.That(sim.Residents[0].State, Is.Not.EqualTo(ResidentState.Deciding));
            }

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Offline));
            Assert.That(ai.StatusDetail, Does.Contain("Cannot connect"));
            Assert.That(sim.Residents[0].Log.Any(e => e.Text.Contains("work")), Is.True);
            Assert.That(transport.Calls.Count, Is.LessThanOrEqualTo((24 * 60 / (int)AiService.RecheckSeconds) + 2), "rechecks are spaced out");
        }

        [Test]
        public void ModelNotInstalled_ReportsHowToFixIt_AndUsesTheRoutine()
        {
            var sim = FreeAfternoon();
            var transport = new FakeAiTransport { Responder = call => call.Succeed("{\"models\":[{\"name\":\"llama3:latest\"}]}") };
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);

            Run(ai, sim, 60);

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.ModelMissing));
            Assert.That(ai.StatusDetail, Does.Contain("ollama pull qwen2.5:7b"));
            Assert.That(sim.Residents[0].ActivityId ?? sim.Residents[0].Goal, Is.Not.Empty);
            Assert.That(transport.Calls.All(c => c.Url.EndsWith("/api/tags")), Is.True);
        }

        [Test]
        public void ConnectionLostMidGame_GoesOffline_ThenRecovers()
        {
            var sim = FreeAfternoon();
            var up = true;
            var working = FakeAiTransport.WorkingOllama(_ => "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Resting\",\"say\":\"\"}");
            var transport = new FakeAiTransport { Responder = call => up ? working.Responder(call) : call.Fail("Connection refused") };
            var ai = Connected(sim, transport, new OllamaSettings());

            Run(ai, sim, 5);
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));

            up = false;
            Run(ai, sim, 120);
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Offline));
            Assert.That(sim.AiDecisionsEnabled, Is.False);
            Assert.That(sim.Residents[0].State, Is.Not.EqualTo(ResidentState.Deciding));

            up = true;
            Run(ai, sim, (int)AiService.RecheckSeconds + 5);
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));
        }

        [Test]
        public void TurnedOffInSettings_NeverContactsOllama()
        {
            var sim = FreeAfternoon();
            var transport = FakeAiTransport.WorkingOllama(_ => ReadAtArmchair);
            var ai = new AiService(new OllamaSettings { enabled = false }, transport);
            ai.Attach(sim);

            Run(ai, sim, 120);

            Assert.That(transport.Calls, Is.Empty);
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Disabled));
            Assert.That(sim.Residents[0].GoalSource, Is.EqualTo(DecisionSource.Routine));
        }

        [Test]
        public void AtBedtime_TheResidentReflectsOnce_AndImportanceIsCapped()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(3, 21, 50));
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("diary-style")
                ? "{\"summary\":\"I planted the last of the tulips and felt proud.\",\"importance\":9}"
                : "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"Winding down\",\"say\":\"What a day.\"}");
            var ai = Connected(sim, transport, new OllamaSettings());

            Run(ai, sim, 3 * 60);
            var r = sim.Residents[0];
            var reflections = sim.Memories.ForResident(r.Id).Where(m => m.Type == MemoryType.Reflection).ToList();

            Assert.That(reflections.Count, Is.EqualTo(1));
            Assert.That(reflections[0].Summary, Is.EqualTo("I planted the last of the tulips and felt proud."));
            Assert.That(reflections[0].Importance, Is.EqualTo(TownSimulation.MaxReflectionImportance));
            Assert.That(r.LastReflectionDay, Is.EqualTo(3));
            Assert.That(transport.Calls.Count(c => c.Body.Contains("diary-style")), Is.EqualTo(1));
        }

        [Test]
        public void ReflectionsAreNotRepeated_ForADayAlreadyReflectedOn()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(3, 23, 0));
            var r = sim.Residents[0];

            Assert.That(sim.ApplyReflection(r.Id, 3, "   ", 2), Is.EqualTo(DecisionOutcome.InvalidText));
            Assert.That(sim.ApplyReflection(r.Id, 3, "A quiet day.", 2), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(sim.ApplyReflection(r.Id, 3, "Again?", 2), Is.EqualTo(DecisionOutcome.Stale));
            Assert.That(sim.ApplyReflection(r.Id, 9, "From the future", 2), Is.EqualTo(DecisionOutcome.Stale));
        }

        [Test]
        public void AttachingANewTown_DropsRequestsForTheOldOne()
        {
            var sim = FreeAfternoon();
            var transport = FakeAiTransport.WorkingOllama(_ => null);
            var ai = Connected(sim, transport, new OllamaSettings());
            Run(ai, sim, 5);
            Assert.That(ai.Queue.Active, Is.Not.Null);

            var other = FreeAfternoon();
            ai.Attach(other);

            Assert.That(ai.Queue.Active, Is.Null);
            Assert.That(ai.Queue.PendingCount, Is.EqualTo(0));
            Assert.That(other.AiDecisionsEnabled, Is.True);
        }
    
        [Test]
        public void ModelList_IsReadEvenWhileTheAiIsOff()
        {
            var transport = FakeAiTransport.WorkingOllama(_ => ReadAtArmchair);
            var ai = new AiService(new OllamaSettings { enabled = false }, transport);

            ai.RefreshModelList();
            Assert.That(ai.ListingModels, Is.True);
            ai.Update(now);
            ai.Update(now + 0.1);

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Disabled));
            Assert.That(ai.ListingModels, Is.False);
            Assert.That(ai.ModelListError, Is.Null);
            Assert.That(ai.InstalledModels.ToList(), Is.EqualTo(new[] { "llama3:latest", FakeAiTransport.Model }));
            Assert.That(transport.Calls.Count(c => c.Body.Contains("messages")), Is.EqualTo(0));
        }

        [Test]
        public void ModelList_ReportsWhenOllamaIsNotRunning()
        {
            var transport = new FakeAiTransport { Responder = call => call.Fail("connection refused") };
            var ai = new AiService(new OllamaSettings { enabled = false }, transport);

            ai.RefreshModelList();
            ai.Update(now);
            ai.Update(now + 0.1);

            Assert.That(ai.InstalledModels, Is.Empty);
            Assert.That(ai.ModelListError, Does.Contain("connection refused"));
        }

        [Test]
        public void TurningTheAiOff_DropsRequests_AndTurningItOnReconnects()
        {
            var sim = FreeAfternoon();
            var transport = FakeAiTransport.WorkingOllama(_ => null);
            var ai = Connected(sim, transport);
            Run(ai, sim, 2);
            Assert.That(sim.Residents[0].State, Is.EqualTo(ResidentState.Deciding));

            ai.SetEnabled(false);
            Run(ai, sim, 1);

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Disabled));
            Assert.That(sim.AiDecisionsEnabled, Is.False);
            Assert.That(ai.Queue.Active, Is.Null);
            Assert.That(ai.Queue.PendingCount, Is.EqualTo(0));
            Assert.That(ai.Summary(), Does.Contain("Settings"));

            ai.SetEnabled(true);
            Run(ai, sim, 1);
            ai.Update(now + 0.1);

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));
        }

        [Test]
        public void UsingAnotherModel_ChecksAndLoadsIt()
        {
            var sim = FreeAfternoon();
            var transport = FakeAiTransport.WorkingOllama(_ => ReadAtArmchair);
            var ai = Connected(sim, transport);
            var loadsBefore = transport.Calls.Count(c => c.Body.Contains("\"messages\": []"));

            ai.UseModel("llama3:latest");
            Assert.That(sim.AiDecisionsEnabled, Is.False);
            for (var i = 0; i < 3; i++)
            {
                ai.Update(now);
                now += 0.1;
            }

            Assert.That(ai.Model, Is.EqualTo("llama3:latest"));
            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.Connected));
            Assert.That(transport.Calls.Count(c => c.Body.Contains("\"messages\": []")), Is.EqualTo(loadsBefore + 1));
            Assert.That(transport.Calls.Last(c => c.Body.Contains("\"messages\": []")).Body, Does.Contain("llama3:latest"));
        }

        [Test]
        public void UsingAModelThatIsNotInstalled_SaysSo()
        {
            var sim = FreeAfternoon();
            var ai = Connected(sim, FakeAiTransport.WorkingOllama(_ => ReadAtArmchair));

            ai.UseModel("not-a-model:1b");
            ai.Update(now);
            ai.Update(now + 0.1);

            Assert.That(ai.Status, Is.EqualTo(AiConnectionStatus.ModelMissing));
            Assert.That(sim.AiDecisionsEnabled, Is.False);
        }
}
}
