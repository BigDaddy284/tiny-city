using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Phase 5 save slots: summaries for the save list, slot ids and the names index.</summary>
    public class SaveSlotTests
    {
        [Test]
        public void Summary_ReadsTheDateAndResidents_OfARealSave()
        {
            var sim = StarterTown.CreateNewGame();
            for (var i = 0; i < 90; i++)
            {
                sim.Tick();
            }

            sim.Die(sim.GetResident(StarterTown.FelixId), "test");
            var summary = SaveSummary.Read(SaveSerializer.Save(sim));
            Assert.That(summary.IsReadable, Is.True, summary.Error);
            Assert.That(summary.Version, Is.EqualTo(SaveSerializer.CurrentVersion));
            Assert.That(summary.Time.TotalMinutes, Is.EqualTo(sim.Time.TotalMinutes));
            Assert.That(summary.Residents, Is.EqualTo(sim.Residents.Count));
            Assert.That(summary.Living, Is.EqualTo(sim.Residents.Count - 1));
            Assert.That(summary.Describe(), Does.StartWith($"Day {sim.Time.Day}, {sim.Time.Weekday} {sim.Time.ClockText}"));
            Assert.That(summary.Describe(), Does.Contain($"{sim.Residents.Count - 1} of {sim.Residents.Count} residents alive"));
        }

        [Test]
        public void Summary_ExplainsSavesThatCannotBeRead()
        {
            var good = SaveSerializer.Save(StarterTown.CreateNewGame());
            var cases = new Dictionary<string, string>
            {
                ["the file is damaged"] = good.Substring(0, good.Length / 2),
                ["isn't a Tiny City save"] = "{\"hello\": 1}",
                ["newer version"] = good.Replace($"\"version\": {SaveSerializer.CurrentVersion}", "\"version\": 99"),
                ["no residents"] = "{\"format\": \"tiny-town-save\", \"version\": 6, \"gameMinutes\": 10, \"residents\": []}",
                ["no valid date"] = "{\"format\": \"tiny-town-save\", \"version\": 6, \"residents\": [{}]}",
            };
            foreach (var pair in cases)
            {
                var summary = SaveSummary.Read(pair.Value);
                Assert.That(summary.IsReadable, Is.False, pair.Key);
                Assert.That(summary.Error, Does.Contain(pair.Key));
                Assert.That(summary.Describe(), Does.StartWith("Can't be read"));
            }

            Assert.That(SaveSummary.Read(null).IsReadable, Is.False);
            Assert.That(good, Does.Contain($"\"version\": {SaveSerializer.CurrentVersion}"), "the version test above edits the real text");
        }

        [Test]
        public void Summary_NotesAnOlderVersion()
        {
            var old = "{\"format\": \"tiny-town-save\", \"version\": 2, \"gameMinutes\": 600, \"residents\": [{\"id\": \"res-0001\"}]}";
            var summary = SaveSummary.Read(old);
            Assert.That(summary.IsReadable, Is.True, summary.Error);
            Assert.That(summary.Living, Is.EqualTo(1), "saves from before deaths count everyone as alive");
            Assert.That(summary.Describe(), Does.Contain("older version"));
        }

        [Test]
        public void SlotIds_AreTownThenNumbered_AndOnlyThoseAreValid()
        {
            Assert.That(SaveSlotIndex.NewSlotId(new string[0]), Is.EqualTo("town"));
            Assert.That(SaveSlotIndex.NewSlotId(new[] { "town" }), Is.EqualTo("town-2"));
            Assert.That(SaveSlotIndex.NewSlotId(new[] { "town", "town-2", "town-4" }), Is.EqualTo("town-3"), "gaps are reused");
            Assert.That(SaveSlotIndex.NewSlotId(new[] { "TOWN" }), Is.EqualTo("town-2"), "Windows file names ignore case");

            foreach (var good in new[] { "town", "town-2", "town-15" })
            {
                Assert.That(SaveSlotIndex.IsValidSlotId(good), Is.True, good);
            }

            foreach (var bad in new[] { null, "", "slots", "town-1", "town-0", "town-02", "town-x", "../town", "town-2.json", "town-" })
            {
                Assert.That(SaveSlotIndex.IsValidSlotId(bad), Is.False, bad ?? "null");
            }
        }

        [Test]
        public void Names_HaveDefaults_AreCleaned_AndSurviveTheIndexFile()
        {
            var index = new SaveSlotIndex();
            Assert.That(index.NameOf("town"), Is.EqualTo(SaveSlotIndex.DefaultName));
            Assert.That(index.NameOf("town-3"), Is.EqualTo("Town 3"));

            index.SetName("town", "  Riverside\n ");
            index.SetName("town-2", new string('x', 100));
            index.SetName("not-a-slot", "Nope");
            Assert.That(index.NameOf("town"), Is.EqualTo("Riverside"));
            Assert.That(index.NameOf("town-2").Length, Is.EqualTo(SaveSlotIndex.MaxNameLength));
            Assert.That(index.Names.ContainsKey("not-a-slot"), Is.False);

            var loaded = SaveSlotIndex.FromJson(index.ToJson());
            Assert.That(loaded.NameOf("town"), Is.EqualTo("Riverside"));
            Assert.That(loaded.NameOf("town-2"), Is.EqualTo(index.NameOf("town-2")));

            index.SetName("town", "   ");
            Assert.That(index.NameOf("town"), Is.EqualTo(SaveSlotIndex.DefaultName), "a blank name goes back to the default");
            index.Forget("town-2");
            Assert.That(index.NameOf("town-2"), Is.EqualTo("Town 2"));

            Assert.That(SaveSlotIndex.FromJson("{ broken").Names, Is.Empty, "a damaged index only loses names");
            Assert.That(SaveSlotIndex.FromJson(null).Names, Is.Empty);
            Assert.That(SaveSlotIndex.FromJson("{\"names\": {\"town\": 5, \"town-2\": \"Hill\"}}").Names.Keys.ToList(), Is.EqualTo(new List<string> { "town-2" }));
        }

        [Test]
        public void CopyNames_NeverClash()
        {
            var index = new SaveSlotIndex();
            index.SetName("town", "Riverside");
            Assert.That(index.CopyName("town", new[] { "town" }), Is.EqualTo("Copy of Riverside"));
            index.SetName("town-2", "Copy of Riverside");
            Assert.That(index.CopyName("town", new[] { "town", "town-2" }), Is.EqualTo("Copy of Riverside (2)"));
            index.SetName("town-3", "Copy of Riverside (2)");
            Assert.That(index.CopyName("town", new[] { "town", "town-2", "town-3" }), Is.EqualTo("Copy of Riverside (3)"));

            index.SetName("town-4", new string('y', SaveSlotIndex.MaxNameLength));
            var copy = index.CopyName("town-4", new[] { "town-4" });
            Assert.That(copy.Length, Is.LessThanOrEqualTo(SaveSlotIndex.MaxNameLength));
            Assert.That(copy, Does.StartWith("Copy of"));
        }
    }
}
