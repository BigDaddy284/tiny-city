using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Persistence;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;

namespace TinyTown.Tests.EditMode
{
    public class PersistenceTests
    {
        [Test]
        public void Json_RoundTripsValues()
        {
            var value = new Dictionary<string, object>
            {
                ["text"] = "Quote \" backslash \\ newline \n tab \t unicode é",
                ["number"] = 0.1 + 0.2,
                ["big"] = 1234567890123.0,
                ["flag"] = true,
                ["nothing"] = null,
                ["list"] = new List<object> { 1.0, "two", false, new Dictionary<string, object>() },
            };

            var parsed = (Dictionary<string, object>)Json.Parse(Json.Write(value));

            Assert.That(parsed["text"], Is.EqualTo(value["text"]));
            Assert.That(parsed["number"], Is.EqualTo(0.1 + 0.2));
            Assert.That(parsed["big"], Is.EqualTo(1234567890123.0));
            Assert.That(parsed["flag"], Is.EqualTo(true));
            Assert.That(parsed["nothing"], Is.Null);
            Assert.That(((List<object>)parsed["list"]).Count, Is.EqualTo(4));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("[1,2")]
        [TestCase("\"unterminated")]
        [TestCase("{\"a\":1} extra")]
        [TestCase("nul")]
        public void Json_RejectsMalformedText(string text)
        {
            Assert.That(() => Json.Parse(text), Throws.TypeOf<System.FormatException>());
        }

        [Test]
        public void Json_RejectsExcessiveNesting()
        {
            Assert.That(() => Json.Parse(new string('[', Json.MaxDepth + 2)), Throws.TypeOf<System.FormatException>());
        }

        [Test]
        public void SaveThenLoad_RestoresEverything()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance((26 * 60) + 17);
            var saved = SaveSerializer.Save(sim);

            var result = SaveSerializer.Load(saved, StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Simulation.Time, Is.EqualTo(sim.Time));
            var before = sim.Residents[0];
            var after = result.Simulation.Residents[0];
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.FullName, Is.EqualTo(before.FullName));
            Assert.That(after.Location, Is.EqualTo(before.Location));
            Assert.That(after.State, Is.EqualTo(before.State));
            Assert.That(after.ActivityId, Is.EqualTo(before.ActivityId));
            Assert.That(after.Log.Count, Is.EqualTo(before.Log.Count));
            for (var i = 0; i < Needs.Count; i++)
            {
                Assert.That(after.Needs[(NeedType)i], Is.EqualTo(before.Needs[(NeedType)i]));
            }

            Assert.That(SaveSerializer.Save(result.Simulation), Is.EqualTo(saved));
        }

        [Test]
        public void LoadedGame_ContinuesExactlyAsIfNeverSaved()
        {
            // Save at many different moments (mid-walk, mid-activity, asleep) and check the future is unchanged.
            var reference = StarterTown.CreateNewGame();
            var world = StarterTown.CreateWorld();
            for (var checkpoint = 0; checkpoint < 24; checkpoint++)
            {
                reference.Advance(97);
                var restored = SaveSerializer.Load(SaveSerializer.Save(reference), world).Simulation;

                var continued = SaveSerializer.Load(SaveSerializer.Save(reference), world).Simulation;
                continued.Advance(300);
                restored.Advance(300);

                Assert.That(SaveSerializer.Save(restored), Is.EqualTo(SaveSerializer.Save(continued)));
            }

            var straight = StarterTown.CreateNewGame();
            straight.Advance((24 * 97) + 300);
            var viaSaves = StarterTown.CreateNewGame();
            for (var i = 0; i < 24; i++)
            {
                viaSaves.Advance(97);
                viaSaves = SaveSerializer.Load(SaveSerializer.Save(viaSaves), world).Simulation;
            }

            viaSaves.Advance(300);
            Assert.That(SaveSerializer.Save(viaSaves), Is.EqualTo(SaveSerializer.Save(straight)));
        }

        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{\"format\":\"something-else\",\"version\":1}")]
        [TestCase("{\"format\":\"tiny-town-save\"}")]
        [TestCase("{\"format\":\"tiny-town-save\",\"version\":99,\"townId\":\"town-tiny-town\",\"gameMinutes\":0,\"residents\":[]}")]
        [TestCase("{\"format\":\"tiny-town-save\",\"version\":1,\"townId\":\"another-town\",\"gameMinutes\":0,\"residents\":[]}")]
        [TestCase("{\"format\":\"tiny-town-save\",\"version\":1,\"townId\":\"town-tiny-town\",\"gameMinutes\":-5,\"residents\":[]}")]
        [TestCase("{\"format\":\"tiny-town-save\",\"version\":1,\"townId\":\"town-tiny-town\",\"gameMinutes\":0,\"residents\":[]}")]
        [TestCase("{\"format\":\"tiny-town-save\",\"version\":1,\"townId\":\"town-tiny-town\",\"gameMinutes\":0,\"residents\":[{\"firstName\":\"No id\"}]}")]
        public void InvalidSaves_AreRejectedWithAReason(string json)
        {
            var result = SaveSerializer.Load(json, StarterTown.CreateWorld());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        [Test]
        public void DuplicateResidentIds_AreRejected()
        {
            const string json = "{\"format\":\"tiny-town-save\",\"version\":1,\"townId\":\"town-tiny-town\",\"gameMinutes\":0," +
                "\"residents\":[{\"id\":\"res-1\"},{\"id\":\"res-1\"}]}";

            Assert.That(SaveSerializer.Load(json, StarterTown.CreateWorld()).Success, Is.False);
        }

        [Test]
        public void DamagedResident_IsRepaired_WithWarnings()
        {
            var sim = StarterTown.CreateNewGame();
            var json = SaveSerializer.Save(sim)
                .Replace("\"mapId\": \"map-cottage-1\"", "\"mapId\": \"map-demolished\"")
                .Replace("\"hunger\":", "\"oldHunger\":")
                .Replace("\"state\": \"Idle\"", "\"state\": \"Performing\", \"activityId\": \"teleport\"");

            var result = SaveSerializer.Load(json, StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            var r = result.Simulation.Residents[0];
            Assert.That(result.Simulation.World.IsWalkable(r.Location), Is.True);
            Assert.That(r.Location, Is.EqualTo(TestTown.Object(result.Simulation, StarterTown.BedId).Location));
            Assert.That(r.State, Is.EqualTo(ResidentState.Idle));
            Assert.That(r.Needs[NeedType.Hunger], Is.InRange(0, 100));
            Assert.That(result.Warnings.Any(w => w.Contains("position")), Is.True, string.Join("; ", result.Warnings));
            Assert.That(result.Warnings.Any(w => w.Contains("Hunger")), Is.True, string.Join("; ", result.Warnings));
            Assert.That(result.Warnings.Any(w => w.Contains("activity")), Is.True, string.Join("; ", result.Warnings));
        }

        [Test]
        public void PerformingAwayFromTarget_IsLoadedAsWalkingThere()
        {
            var sim = TestTown.WithResident(TestTown.WorkHour);
            sim.Tick();
            Assert.That(sim.Residents[0].ActivityId, Is.EqualTo("work"));
            var json = SaveSerializer.Save(sim).Replace("\"state\": \"Travelling\"", "\"state\": \"Performing\"");

            var result = SaveSerializer.Load(json, StarterTown.CreateWorld());

            Assert.That(result.Simulation.Residents[0].State, Is.EqualTo(ResidentState.Travelling));
            Assert.That(result.Warnings, Is.Not.Empty);
        }

        [Test]
        public void OutOfRangeNeeds_AreClampedOnLoad()
        {
            var json = SaveSerializer.Save(StarterTown.CreateNewGame());
            var start = json.IndexOf("\"fun\": ", System.StringComparison.Ordinal);
            var end = json.IndexOf('\n', start);
            json = json.Substring(0, start) + "\"fun\": 5000" + json.Substring(end);

            var result = SaveSerializer.Load(json, StarterTown.CreateWorld());

            Assert.That(result.Simulation.Residents[0].Needs[NeedType.Fun], Is.EqualTo(100));
        }

        [Test]
        public void SaveFile_DoesNotContainRuntimePathData()
        {
            var sim = TestTown.WithResident(TestTown.WorkHour);
            sim.Advance(5);

            var json = SaveSerializer.Save(sim);

            Assert.That(json, Does.Not.Contain("path"));
            Assert.That(json, Does.Contain($"\"version\": {SaveSerializer.CurrentVersion}"));
        }

        [Test]
        public void ResidentLog_IsBounded()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance(7 * 24 * 60);

            Assert.That(sim.Residents[0].Log.Count, Is.LessThanOrEqualTo(Resident.MaxLogEntries));
        }

        [Test]
        public void Location_IsAMapAndTile()
        {
            var loc = new Location(StarterTown.VillageMapId, 3, 4);

            Assert.That(loc.ToString(), Does.Contain(StarterTown.VillageMapId));
        }
    }
}
