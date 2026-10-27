using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Persistence;
using TinyTown.Simulation.Residents;

namespace TinyTown.Tests.EditMode
{
    /// <summary>Save version 2: memories, conversation history and AI decision details.</summary>
    public class MemoryPersistenceTests
    {
        private static TownSimulation TownWithMemoriesAndSpeech()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance(9 * 60);
            sim.Memories.Add(StarterTown.ResidentId, sim.Time, MemoryType.Event, 6, "The tulips came up at last.", new[] { "res-0002" }, new[] { StarterTown.GardenPlotId });
            sim.Conversations.AddRemark(StarterTown.ResidentId, sim.Time, "What a glorious morning!", "work");
            sim.Advance(14 * 60);
            Assert.That(sim.ApplyReflection(StarterTown.ResidentId, TownSimulation.ReflectionDayFor(sim.Time), "A good, honest day's work.", 3), Is.EqualTo(DecisionOutcome.Applied));
            return sim;
        }

        [Test]
        public void MemoriesAndHistory_SurviveSaveAndLoad()
        {
            var sim = TownWithMemoriesAndSpeech();
            var saved = SaveSerializer.Save(sim);

            var result = SaveSerializer.Load(saved, StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var loaded = result.Simulation;
            Assert.That(loaded.Memories.All.Count, Is.EqualTo(sim.Memories.All.Count));
            for (var i = 0; i < sim.Memories.All.Count; i++)
            {
                var a = sim.Memories.All[i];
                var b = loaded.Memories.All[i];
                Assert.That(b.Id, Is.EqualTo(a.Id));
                Assert.That(b.ResidentId, Is.EqualTo(a.ResidentId));
                Assert.That(b.Time, Is.EqualTo(a.Time));
                Assert.That(b.Type, Is.EqualTo(a.Type));
                Assert.That(b.Importance, Is.EqualTo(a.Importance));
                Assert.That(b.Summary, Is.EqualTo(a.Summary));
                Assert.That(b.Participants, Is.EqualTo(a.Participants));
                Assert.That(b.Tags, Is.EqualTo(a.Tags));
            }

            Assert.That(loaded.Memories.NextNumber, Is.EqualTo(sim.Memories.NextNumber));
            Assert.That(loaded.Conversations.LastLineBy(StarterTown.ResidentId).Text, Is.EqualTo("What a glorious morning!"));
            Assert.That(loaded.Conversations.NextNumber, Is.EqualTo(sim.Conversations.NextNumber));
            Assert.That(loaded.Residents[0].LastReflectionDay, Is.EqualTo(sim.Residents[0].LastReflectionDay));
            Assert.That(SaveSerializer.Save(loaded), Is.EqualTo(saved));
        }

        [Test]
        public void AiChosenActivity_KeepsItsExplanationAfterLoading()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            var r = sim.Residents[0];
            sim.ApplyDecision(r.Id, r.PendingDecisionId, "read", "obj-cottage1-armchair", "A good book is calling", "Tea and a book!");

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation.Residents[0];

            Assert.That(loaded.GoalSource, Is.EqualTo(DecisionSource.Ai));
            Assert.That(loaded.Goal, Is.EqualTo("A good book is calling"));
            Assert.That(loaded.ActivityId, Is.EqualTo("read"));
        }

        [Test]
        public void ResidentWaitingForTheAi_IsSavedAsIdle_SoNoUnfinishedRequestIsKept()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Fun] = 30);
            sim.AiDecisionsEnabled = true;
            sim.Tick();
            Assert.That(sim.Residents[0].State, Is.EqualTo(ResidentState.Deciding));

            var json = SaveSerializer.Save(sim);
            var loaded = SaveSerializer.Load(json, StarterTown.CreateWorld()).Simulation;

            Assert.That(json, Does.Not.Contain("Deciding"));
            Assert.That(loaded.Residents[0].State, Is.EqualTo(ResidentState.Idle));
            Assert.That(loaded.AiDecisionsEnabled, Is.False, "AI availability is checked again after loading");
            loaded.Tick();
            Assert.That(loaded.Residents[0].ActivityId, Is.Not.Null);
        }

        [Test]
        public void Version1Save_IsUpgraded_WithEmptyMemories()
        {
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(StarterTown.CreateNewGame()));
            root["version"] = 1.0;
            root.Remove("memories");
            root.Remove("conversations");
            var resident = (Dictionary<string, object>)((List<object>)root["residents"])[0];
            resident.Remove("goalSource");
            resident.Remove("lastReflectionDay");

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            Assert.That(result.UpgradedFromVersion, Is.EqualTo(1));
            Assert.That(result.Simulation.Memories.All, Is.Empty);
            Assert.That(result.Simulation.Residents[0].FullName, Is.EqualTo("Mabel Thornbury"));

            StarterTown.AddLifeHistory(result.Simulation);
            var resaved = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(result.Simulation));
            Assert.That(resaved["version"], Is.EqualTo((double)SaveSerializer.CurrentVersion));
            Assert.That(SaveSerializer.Load(SaveSerializer.Save(result.Simulation), StarterTown.CreateWorld()).Simulation.Memories.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void DamagedMemoryRecords_AreSkipped_WithAWarning()
        {
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(StarterTown.CreateNewGame()));
            var records = (List<object>)((Dictionary<string, object>)root["memories"])["records"];
            var good = (Dictionary<string, object>)records[0];
            records.Add(new Dictionary<string, object>(good) { ["id"] = "mem-000099", ["residentId"] = "res-ghost" });
            records.Add(new Dictionary<string, object>(good));
            records.Add(new Dictionary<string, object>(good) { ["id"] = "mem-000100", ["summary"] = "  " });
            records.Add(new Dictionary<string, object>(good) { ["id"] = "mem-000101", ["type"] = "Prophecy" });
            records.Add("not a record");

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True);
            Assert.That(result.Simulation.Memories.All.Count, Is.EqualTo(2));
            Assert.That(result.Warnings.Any(w => w.Contains("5 damaged memory")), Is.True, string.Join("; ", result.Warnings));
        }

        [Test]
        public void NewIds_NeverCollideWithLoadedOnes_EvenIfTheCounterWasDamaged()
        {
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(StarterTown.CreateNewGame()));
            var memories = (Dictionary<string, object>)root["memories"];
            memories["nextNumber"] = 1.0;
            ((Dictionary<string, object>)((List<object>)memories["records"])[1])["id"] = "mem-000041";

            var sim = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld()).Simulation;
            var added = sim.Memories.Add(StarterTown.ResidentId, sim.Time, MemoryType.Event, 2, "Something new");

            Assert.That(added.Id, Is.EqualTo("mem-000042"));
        }

        [Test]
        public void MissingMemorySection_LoadsWithAWarning()
        {
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(TownWithMemoriesAndSpeech()));
            root.Remove("memories");

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True);
            Assert.That(result.Simulation.Memories.All, Is.Empty);
            Assert.That(result.Warnings.Any(w => w.Contains("Memories were missing")), Is.True);
        }
    }
}
