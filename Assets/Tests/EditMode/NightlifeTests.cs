using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The neon bar: Felix's evening job, a drink or a dance, club night, and older saves.</summary>
    public class NightlifeTests
    {
        private static TinyCity.Simulation.Economy.Business Bar(TownSimulation sim) => sim.World.Businesses.First(b => b.Id == StarterTown.BarId);

        private static void AdvanceTo(TownSimulation sim, int day, int hour)
        {
            var target = GameTime.FromDayAndTime(day, hour, 0).TotalMinutes;
            sim.Advance((int)(target - sim.Time.TotalMinutes));
        }

        [Test]
        public void TheBar_OpensOnAWednesdayEvening_WithFelixBehindTheCounter()
        {
            var sim = StarterTown.CreateNewGame();
            var felix = sim.GetResident(StarterTown.FelixId);
            Assert.That(felix.Job.WorkObjectId, Is.EqualTo(StarterTown.BarCounterId));

            AdvanceTo(sim, 3, 12);
            Assert.That(sim.Economy.IsOpen(Bar(sim), sim.Time), Is.False, "the bar is shut in the daytime");

            AdvanceTo(sim, 3, 19);
            Assert.That(sim.Time.Weekday, Is.EqualTo(Weekday.Wednesday));
            Assert.That(felix.Location.MapId, Is.EqualTo(StarterTown.BarMapId), string.Join("\n", felix.Log.Select(e => e.Text)));
            Assert.That(sim.Economy.IsOpen(Bar(sim), sim.Time), Is.True);
        }

        [Test]
        public void OnMondaysAndTuesdays_TheBarStaysShut()
        {
            var sim = StarterTown.CreateNewGame();
            AdvanceTo(sim, 1, 20);
            Assert.That(sim.Economy.IsOpen(Bar(sim), sim.Time), Is.False);
            AdvanceTo(sim, 2, 20);
            Assert.That(sim.Economy.IsOpen(Bar(sim), sim.Time), Is.False);
        }

        [Test]
        public void OverAWeek_NeighboursGoForADrinkOrADance()
        {
            var sim = StarterTown.CreateNewGame();
            var drinks = 0;
            var dances = 0;
            for (var minute = 0; minute < 7 * GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                drinks += sim.Residents.Count(r => r.State == ResidentState.Performing && r.ActivityId == ActivityCatalog.BarDrink.Id);
                dances += sim.Residents.Count(r => r.State == ResidentState.Performing && r.ActivityId == ActivityCatalog.Dance.Id);
            }

            Assert.That(drinks + dances, Is.GreaterThan(0), "nobody went to the bar all week");
            Assert.That(sim.Economy.Ledger.All.Any(t => t.BusinessId == StarterTown.BarId), Is.True, "the bar took no money");
        }

        [Test]
        public void ClubNight_IsOnTheCalendarOnFridays()
        {
            var world = StarterTown.CreateWorld();
            var friday = GameTime.FromDayAndTime(5, 21, 0);
            Assert.That(world.Calendar.OnAt(friday) is var evt && evt != null && new[] { evt }.Any(e => e.Id == "club-night" && e.PlaceObjectId == StarterTown.ClubCornerId), Is.True);
        }

        [Test]
        public void AnOlderSave_GivesFelixTheBarJob_Once()
        {
            var sim = StarterTown.CreateNewGame();
            var felix = sim.GetResident(StarterTown.FelixId);
            felix.Job = null;
            felix.WeeklyIncomePence = 9500;
            felix.IncomeLabel = "Jobseeker's allowance";

            Assert.That(StarterTown.AddBarJobToOlderSave(sim), Is.True);
            Assert.That(felix.Job.WorkObjectId, Is.EqualTo(StarterTown.BarCounterId));
            Assert.That(felix.WeeklyIncomePence, Is.EqualTo(0));
            Assert.That(StarterTown.AddBarJobToOlderSave(sim), Is.False, "nothing changes the second time");
        }
    }
}
