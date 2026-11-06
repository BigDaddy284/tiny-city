using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2: when everyone has died, a log is written for the next world, which starts out remembering its lessons.</summary>
    public class LegacyTests
    {
        private static TownSimulation NewGame()
        {
            var sim = FlatLand.CreateNewGame();
            return sim;
        }

        [Test]
        public void WhenTheLastOneDies_TheWorldEnds_AndTheLogHoldsLessonsForTheNextWorld()
        {
            var sim = NewGame();
            sim.Memories.Add(sim.Residents[2].Id, sim.Time, MemoryType.Insight, 8, "Food has to come first, before any building.", tags: new[] { "insight" });
            foreach (var r in sim.Residents.Take(sim.Residents.Count - 1))
            {
                sim.Die(r, "hunger");
                Assert.That(Legacy.EveryoneHasDied(sim), Is.False);
            }

            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.WorldEnded), Is.False);
            sim.Die(sim.Residents.Last(), "old age");
            Assert.That(Legacy.EveryoneHasDied(sim), Is.True);
            var ended = sim.History.Events.Single(e => e.Kind == TownEventKind.WorldEnded);
            Assert.That(ended.Summary, Does.Contain("lasted 1 day"));
            Assert.That(Journal.IsNotable(TownEventKind.WorldEnded), Is.True);

            var log = Legacy.Write(sim);
            Assert.That(log, Does.Contain(Legacy.LessonsHeading).And.Contain("How each of them died").And.Contain("A history of Tiny City"));
            var lessons = Legacy.ReadLessons(log);
            Assert.That(lessons.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(lessons[0], Does.Contain($"{sim.Residents.Count} people lived on this land for 1 day,"));
            Assert.That(lessons[1], Does.StartWith($"What they died of: hunger ({sim.Residents.Count - 1}), old age (1)."));
            Assert.That(lessons.Any(l => l.EndsWith("came to believe: Food has to come first, before any building.")), Is.True);

            var next = FlatLand.CreateNewGame(lessons: lessons);
            foreach (var r in next.Residents)
            {
                var remembered = next.Memories.ForResident(r.Id).Where(m => m.Tags.Contains("legacy")).ToList();
                Assert.That(remembered.Count, Is.EqualTo(lessons.Count), r.Id);
                Assert.That(remembered[0].Summary, Does.StartWith("Left by those who lived here before us: Before us,"));
            }
        }

        [Test]
        public void ReadLessons_FindsNothing_InAnythingElse()
        {
            Assert.That(Legacy.ReadLessons(null), Is.Empty);
            Assert.That(Legacy.ReadLessons("just some text\n- a line"), Is.Empty);
            Assert.That(Legacy.ReadLessons($"x\n{Legacy.LessonsHeading}\n---\n- one\n- two\n\nmore"), Is.EqualTo(new[] { "one", "two" }));
        }
    }
}
