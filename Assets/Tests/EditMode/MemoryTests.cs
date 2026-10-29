using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    public class MemoryTests
    {
        private const string Mabel = "res-0001";

        [Test]
        public void Add_CleansText_ClampsImportance_AndGivesStableIds()
        {
            var store = new MemoryStore();
            var a = store.Add(Mabel, new GameTime(10), MemoryType.Event, 99, "  \"Found a\n\tfour-leaf clover\"  ");
            var b = store.Add(Mabel, new GameTime(20), MemoryType.Event, -4, "Rain all day");

            Assert.That(a.Summary, Is.EqualTo("Found a four-leaf clover"));
            Assert.That(a.Importance, Is.EqualTo(MemoryRecord.MaxImportance));
            Assert.That(b.Importance, Is.EqualTo(MemoryRecord.MinImportance));
            Assert.That(a.Id, Is.EqualTo("mem-000001"));
            Assert.That(b.Id, Is.EqualTo("mem-000002"));
            Assert.That(a.Participants, Is.EqualTo(new[] { Mabel }));
        }

        [Test]
        public void Add_RejectsEmptySummaries()
        {
            var store = new MemoryStore();

            Assert.That(store.Add(Mabel, new GameTime(0), MemoryType.Reflection, 3, " \n "), Is.Null);
            Assert.That(store.All, Is.Empty);
        }

        [Test]
        public void Recall_PrefersRelevantImportantAndRecentMemories()
        {
            var store = new MemoryStore();
            var now = GameTime.FromDayAndTime(10, 12, 0);
            store.Add(Mabel, GameTime.FromDayAndTime(1, 9, 0), MemoryType.Event, 2, "An old minor thing");
            var relevant = store.Add(Mabel, GameTime.FromDayAndTime(2, 9, 0), MemoryType.Event, 4, "Fell asleep in the armchair", tags: new[] { "obj-cottage1-armchair" });
            var important = store.Add(Mabel, GameTime.FromDayAndTime(1, 9, 0), MemoryType.LifeHistory, 9, "Moved to the village");
            var recent = store.Add(Mabel, GameTime.FromDayAndTime(10, 11, 0), MemoryType.Event, 2, "Just had lunch");
            store.Add("res-0002", now, MemoryType.Event, 10, "Someone else's memory");

            var recalled = store.Recall(Mabel, now, new[] { "obj-cottage1-armchair" }, 3);

            Assert.That(recalled, Is.EqualTo(new[] { important, relevant, recent }));
            Assert.That(recalled.All(m => m.ResidentId == Mabel), Is.True);
        }

        [Test]
        public void EachResidentsMemories_AreCapped_ForgettingTheLeastImportantOldest()
        {
            var store = new MemoryStore();
            var keep = store.Add(Mabel, new GameTime(0), MemoryType.LifeHistory, 9, "Very important");
            for (var i = 0; i < MemoryStore.MaxPerResident + 50; i++)
            {
                store.Add(Mabel, new GameTime(i + 1), MemoryType.Event, 1 + (i % 3), $"Thing {i}");
            }

            var mine = store.ForResident(Mabel).ToList();
            Assert.That(mine.Count, Is.EqualTo(MemoryStore.MaxPerResident));
            Assert.That(mine.Contains(keep), Is.True);
            Assert.That(mine.Any(m => m.Summary == "Thing 0"), Is.False, "the oldest least important memory goes first");
            Assert.That(store.NextNumber, Is.EqualTo(MemoryStore.MaxPerResident + 52), "ids are never reused");
        }

        [Test]
        public void RunningDesperatelyLow_IsRememberedOncePerDay()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Hunger] = 10);
            sim.Advance(60);
            sim.Residents[0].Needs[NeedType.Hunger] = 5;
            sim.Advance(60);

            var hungry = sim.Memories.ForResident(Mabel).Where(m => m.Tags.Contains("urgent-hunger")).ToList();
            Assert.That(hungry.Count, Is.EqualTo(1));
            Assert.That(hungry[0].Type, Is.EqualTo(MemoryType.Event));
        }

        [Test]
        public void NewGame_StartsWithLifeHistory()
        {
            var sim = StarterTown.CreateNewGame();

            Assert.That(sim.Memories.ForResident(Mabel).Count(m => m.Type == MemoryType.LifeHistory), Is.EqualTo(2));
        }

        [Test]
        public void Remarks_AreKeptInOrder_AndBounded()
        {
            var history = new ConversationHistory();
            for (var i = 0; i < ConversationHistory.MaxRecords + 10; i++)
            {
                history.AddRemark(Mabel, new GameTime(i), $"Line {i}", "read");
            }

            Assert.That(history.All.Count, Is.EqualTo(ConversationHistory.MaxRecords));
            Assert.That(history.All[0].Lines[0].Text, Is.EqualTo("Line 10"));
            Assert.That(history.LastLineBy(Mabel).Text, Is.EqualTo($"Line {ConversationHistory.MaxRecords + 9}"));
            Assert.That(history.AddRemark(Mabel, new GameTime(0), "   ", "read"), Is.Null);
        }

        [TestCase("Hello", 10, "Hello")]
        [TestCase("  “Quoted”  ", 20, "Quoted")]
        [TestCase("tab\there\u0007bell", 30, "tab here bell")]
        [TestCase("The quick brown fox jumps over the lazy dog", 20, "The quick brown fox…")]
        [TestCase(null, 10, "")]
        public void TextRules_CleanAndShorten(string input, int max, string expected)
        {
            Assert.That(TextRules.Clean(input, max), Is.EqualTo(expected));
        }
    }
}
