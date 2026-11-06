using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Everyone has their own usual bedtime and getting-up time.</summary>
    public class SleepScheduleTests
    {
        [Test]
        public void TheResidents_DoNotAllGoToBedOrGetUpAtOnce()
        {
            var residents = FlatLand.CreateResidents();
            var bedtimes = residents.Select(SleepSchedule.BedtimeOf).Distinct().Count();
            var wakes = residents.Select(SleepSchedule.WakeOf).Distinct().Count();

            Assert.That(bedtimes, Is.GreaterThanOrEqualTo(5));
            Assert.That(wakes, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void EveryoneSleepsEightHours()
        {
            foreach (var r in FlatLand.CreateResidents())
            {
                var bed = SleepSchedule.BedtimeOf(r);
                var wake = SleepSchedule.WakeOf(r);
                Assert.That((wake - bed + GameTime.MinutesPerDay) % GameTime.MinutesPerDay, Is.EqualTo(SleepSchedule.SleepMinutes), r.Id);
            }
        }

        [Test]
        public void Bedtimes_NeverChangeBetweenRuns()
        {
            var a = FlatLand.CreateResidents().Select(SleepSchedule.BedtimeOf).ToList();
            var b = FlatLand.CreateResidents().Select(SleepSchedule.BedtimeOf).ToList();
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
