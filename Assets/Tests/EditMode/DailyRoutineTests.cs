using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Phase 1 acceptance: the resident lives a basic day without any AI.</summary>
    public class DailyRoutineTests
    {
        private static int MinutesDoing(TownSimulation sim, string activityId, int minutes, out double lowestNeed)
        {
            var r = sim.Residents[0];
            var count = 0;
            lowestNeed = double.MaxValue;
            for (var i = 0; i < minutes; i++)
            {
                sim.Tick();
                if (r.State == ResidentState.Performing && r.ActivityId == activityId)
                {
                    count++;
                }

                foreach (NeedType need in new[] { NeedType.Hunger, NeedType.Thirst, NeedType.Energy, NeedType.Toilet })
                {
                    lowestNeed = System.Math.Min(lowestNeed, r.Needs[need]);
                }
            }

            return count;
        }

        [Test]
        public void NewGame_FirstWeekday_EatsWorksAndSleeps()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            var done = new System.Collections.Generic.HashSet<string>();
            var log = new System.Collections.Generic.List<string>();

            for (var i = 0; i < 24 * 60; i++)
            {
                sim.Tick();
                if (r.State == ResidentState.Performing)
                {
                    done.Add(r.ActivityId);
                }

                var last = r.Log.LastOrDefault();
                if (last != null && last.Time.TotalMinutes == sim.Time.TotalMinutes - 1)
                {
                    log.Add($"{last.Time} {last.Text}");
                }
            }

            var text = string.Join("\n", log);
            Assert.That(done.Contains(ActivityCatalog.Eat.Id) || done.Contains(ActivityCatalog.CafeMeal.Id), Is.True, text);
            Assert.That(done.Contains(ActivityCatalog.Work.Id), Is.True, text);
            Assert.That(done.Contains(ActivityCatalog.Sleep.Id), Is.True, text);
        }

        [Test]
        public void OverAWeek_BasicNeedsNeverBottomOut()
        {
            var sim = StarterTown.CreateNewGame();

            MinutesDoing(sim, ActivityCatalog.Sleep.Id, 7 * 24 * 60, out var lowest);

            Assert.That(lowest, Is.GreaterThan(5), "Hunger, thirst, energy or toilet nearly ran out during a normal week.");
        }

        [Test]
        public void OnAWeekday_WorksMostOfTheShift()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(1, 8, 0));

            var working = MinutesDoing(sim, ActivityCatalog.Work.Id, 10 * 60, out _);

            Assert.That(working, Is.GreaterThan(6 * 60));
        }

        [Test]
        public void AtNight_SleepsMostOfTheNight()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(1, 22, 0), needsLevel: 60);

            var sleeping = MinutesDoing(sim, ActivityCatalog.Sleep.Id, 9 * 60, out _);

            Assert.That(sleeping, Is.GreaterThan(7 * 60));
        }

        [Test]
        public void GoingToWork_WalksOutOfTheCottage_ThroughTheDoor()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(1, 8, 30));
            var r = sim.Residents[0];
            var maps = new System.Collections.Generic.List<string> { r.Location.MapId };

            for (var i = 0; i < 200 && !(r.State == ResidentState.Performing && r.ActivityId == ActivityCatalog.Work.Id); i++)
            {
                var before = r.Location;
                sim.Tick();
                if (r.Location.MapId != before.MapId)
                {
                    maps.Add(r.Location.MapId);
                    Assert.That(sim.World.Portals.Any(p => p.From == before && p.To == r.Location), "Changed map without using the door");
                }
                else
                {
                    var moved = System.Math.Abs(r.Location.Position.X - before.Position.X) + System.Math.Abs(r.Location.Position.Y - before.Position.Y);
                    Assert.That(moved, Is.LessThanOrEqualTo(1), "Moved more than one tile in a minute");
                }
            }

            Assert.That(maps, Is.EqualTo(new[] { StarterTown.CottageMapId, StarterTown.VillageMapId }));
            Assert.That(r.Location, Is.EqualTo(TestTown.Object(sim, StarterTown.GardenPlotId).Location));
        }

        [Test]
        public void SameStart_GivesSameDay()
        {
            var a = StarterTown.CreateNewGame();
            var b = StarterTown.CreateNewGame();

            a.Advance(2 * 24 * 60);
            b.Advance(2 * 24 * 60);

            Assert.That(TinyCity.Simulation.Persistence.SaveSerializer.Save(a), Is.EqualTo(TinyCity.Simulation.Persistence.SaveSerializer.Save(b)));
        }
    }
}
