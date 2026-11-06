using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The Phase 5 journal: what goes into the town history, searching it, timelines and exported text.</summary>
    public class JournalTests
    {
        private const string Mia = StarterTown.ResidentId;
        private const string Ivy = StarterTown.IvyId;
        private const string Felix = StarterTown.FelixId;

        [Test]
        public void AYearOfTownLife_RecordsFirstMeetings_AndFriendships_EachOnce()
        {
            var sim = StarterTown.CreateNewGame();
            for (var i = 0; i < Seasons.DaysPerYear * GameTime.MinutesPerDay; i++)
            {
                sim.Tick();
            }

            var met = sim.History.Events.Where(e => e.Kind == TownEventKind.Met).ToList();
            Assert.That(met, Is.Not.Empty, "neighbours meet in a year");
            var pairs = met.Select(e => string.Join("+", e.ResidentIds.OrderBy(id => id))).ToList();
            Assert.That(pairs.Distinct().Count(), Is.EqualTo(pairs.Count), "each pair meets for the first time only once");
            foreach (var e in met)
            {
                Assert.That(e.ResidentIds.Count, Is.EqualTo(2));
                Assert.That(sim.Relationships.Get(e.ResidentIds[0], e.ResidentIds[1]).HaveMet, Is.True);
            }

            var friends = sim.History.Events.Where(e => e.Kind == TownEventKind.Friends)
                .Select(e => string.Join("+", e.ResidentIds.OrderBy(id => id))).ToList();
            Assert.That(friends.Distinct().Count(), Is.EqualTo(friends.Count), "becoming good friends is recorded once per pair");
            TestContext.WriteLine($"In a year: {sim.History.Events.Count} history events, {met.Count} first meetings, {friends.Count} friendships.");
        }

        private static TownSimulation TownWithHistory()
        {
            var sim = StarterTown.CreateNewGame();
            sim.History.Add(GameTime.FromDayAndTime(1, 9, 0), TownEventKind.Met, "Mia Hart and Ivy Brook met for the first time at the café.", new[] { Mia, Ivy });
            sim.History.Add(GameTime.FromDayAndTime(2, 5, 0), TownEventKind.Built, "Felix finished building a hut.", new[] { Felix });
            sim.History.Add(GameTime.FromDayAndTime(3, 18, 0), TownEventKind.Meeting, "The town meeting by the quiz board.", new[] { Mia, Felix });
            sim.History.Add(GameTime.FromDayAndTime(9, 12, 0), TownEventKind.Friends, "Ivy and Felix became good friends.", new[] { Ivy, Felix });
            return sim;
        }

        [Test]
        public void Search_IsNewestFirst_AndFiltersByTopicResidentAndWords()
        {
            var sim = TownWithHistory();
            var all = Journal.Search(sim.History, sim.GetResident);
            Assert.That(all.Select(e => e.Kind).ToList(), Is.EqualTo(new List<TownEventKind> { TownEventKind.Friends, TownEventKind.Meeting, TownEventKind.Built, TownEventKind.Met }));

            Assert.That(Journal.Search(sim.History, sim.GetResident, JournalTopic.Life).Single().Kind, Is.EqualTo(TownEventKind.Built));
            Assert.That(Journal.Search(sim.History, sim.GetResident, JournalTopic.Friends).Count, Is.EqualTo(2));
            Assert.That(Journal.Search(sim.History, sim.GetResident, JournalTopic.Meetings).Single().Kind, Is.EqualTo(TownEventKind.Meeting));

            var felix = Journal.Search(sim.History, sim.GetResident, residentId: Felix);
            Assert.That(felix.Count, Is.EqualTo(3));
            Assert.That(Journal.Search(sim.History, sim.GetResident, JournalTopic.Life, Mia), Is.Empty);

            Assert.That(Journal.Search(sim.History, sim.GetResident, text: "QUIZ").Single().Kind, Is.EqualTo(TownEventKind.Meeting), "case doesn't matter");
            Assert.That(Journal.Search(sim.History, sim.GetResident, text: "café first").Single().Kind, Is.EqualTo(TownEventKind.Met), "every word must match");
            Assert.That(Journal.Search(sim.History, sim.GetResident, text: "day 9").Single().Kind, Is.EqualTo(TownEventKind.Friends), "dates can be searched");
            var felixName = sim.GetResident(Felix).FullName.Split(' ').Last();
            Assert.That(Journal.Search(sim.History, sim.GetResident, text: felixName).Count, Is.EqualTo(3), "a surname finds events that only say a first name");
            Assert.That(Journal.Search(sim.History, sim.GetResident, text: "dragons"), Is.Empty);
        }

        [Test]
        public void Search_ReturnsAtMostTheLimit()
        {
            var sim = StarterTown.CreateNewGame();
            for (var i = 0; i < Journal.MaxResults + 50; i++)
            {
                sim.History.Add(new GameTime(i), TownEventKind.Meeting, $"Meeting {i}", new[] { Mia });
            }

            var found = Journal.Search(sim.History, sim.GetResident);
            Assert.That(found.Count, Is.EqualTo(Journal.MaxResults));
            Assert.That(found[0].Summary, Is.EqualTo($"Meeting {Journal.MaxResults + 49}"));
        }

        [Test]
        public void Since_GivesWhatIsNew_ForNotifications()
        {
            var sim = TownWithHistory();
            var mark = sim.History.NextNumber;
            Assert.That(sim.History.Since(mark), Is.Empty);
            sim.Die(sim.GetResident(Felix), "old age");
            var fresh = sim.History.Since(mark).ToList();
            Assert.That(fresh.Count, Is.EqualTo(1));
            Assert.That(fresh[0].Kind, Is.EqualTo(TownEventKind.Death));
            Assert.That(Journal.IsNotable(fresh[0].Kind), Is.True);
            Assert.That(sim.History.Since(1).Count(), Is.EqualTo(sim.History.Events.Count));
        }

        [Test]
        public void Timeline_MixesRecordsAndOwnMemories_InOrder_MarkingAiReflections()
        {
            var sim = TownWithHistory();
            sim.Memories.Add(Mia, GameTime.FromDayAndTime(2, 22, 0), MemoryType.Reflection, 5, "A lovely, slow day.");
            sim.Memories.Add(Mia, GameTime.FromDayAndTime(4, 8, 0), MemoryType.Event, 8, "I couldn't cover this week's rent.", tags: new[] { "money" });
            sim.Memories.Add(Mia, GameTime.FromDayAndTime(6, 0, 0), MemoryType.Event, 2, "I had a sandwich.");

            var timeline = LifeStory.Timeline(sim, Mia);
            var texts = timeline.Select(e => e.Text).ToList();
            Assert.That(timeline.Select(e => e.Time.TotalMinutes).ToList(), Is.Ordered);
            Assert.That(texts.Any(t => t.Contains("met for the first time")), Is.True);
            Assert.That(texts.Any(t => t.Contains("quiz board")), Is.True);
            Assert.That(texts.Any(t => t.Contains("became good friends")), Is.False, "not about Mia");
            Assert.That(timeline.Single(e => e.Text == "A lovely, slow day.").Source, Is.EqualTo(TimelineSource.AiReflection));
            Assert.That(timeline.Single(e => e.Text.Contains("rent")).Source, Is.EqualTo(TimelineSource.Memory));
            Assert.That(texts.Any(t => t.Contains("sandwich")), Is.False, "trivial memories stay out");
        }

        [Test]
        public void LifeStory_DescribesTheResident_AndMarksWhatTheAiWrote()
        {
            var sim = TownWithHistory();
            sim.Memories.Add(Ivy, GameTime.FromDayAndTime(2, 22, 0), MemoryType.Reflection, 5, "Felix made me laugh today.");
            var ivy = sim.GetResident(Ivy);
            var story = LifeStory.Write(sim, Ivy);
            Assert.That(story, Does.StartWith($"The life of {ivy.FullName}"));
            Assert.That(story, Does.Contain($"{ivy.FullName} is {ivy.AgeYears}"));
            Assert.That(story, Does.Contain("Day 1, Monday 09:00: Mia Hart and Ivy Brook met"));
            Assert.That(story, Does.Contain("[AI]: Felix made me laugh today."));
            Assert.That(LifeStory.Write(sim, "res-9999"), Is.Empty);

            sim.Die(ivy, "pneumonia");
            Assert.That(LifeStory.Write(sim, Ivy), Does.Contain($"{ivy.FullName} died on Day 1"));
        }

        [Test]
        public void TownHistoryText_ListsResidents_AndEventsByDay()
        {
            var sim = TownWithHistory();
            var text = LifeStory.WriteTownHistory(sim);
            Assert.That(text, Does.StartWith("A history of Tiny City"));
            foreach (var r in sim.Residents)
            {
                Assert.That(text, Does.Contain(r.FullName));
            }

            Assert.That(text, Does.Contain("Day 3, Wednesday"));
            Assert.That(text, Does.Contain("  18:00  The town meeting by the quiz board."));
            Assert.That(text.IndexOf("met for the first time"), Is.LessThan(text.IndexOf("became good friends")), "oldest first");
            Assert.That(LifeStory.WriteTownHistory(StarterTown.CreateNewGame()), Does.Contain("Nothing has happened yet."));
        }

        [Test]
        public void EveryKindOfEvent_SurvivesSaveAndLoad()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Tick();
            foreach (TownEventKind kind in System.Enum.GetValues(typeof(TownEventKind)))
            {
                sim.History.Add(sim.Time, kind, $"A {kind} happened.", new[] { Mia });
            }

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            Assert.That(result.Simulation.History.Events.Select(e => e.Kind).ToList(), Is.EqualTo(sim.History.Events.Select(e => e.Kind).ToList()));
        }
    }
}
