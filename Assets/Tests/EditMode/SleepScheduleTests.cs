using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Everyone has their own bedtime and getting-up time, and work still fits around sleep.</summary>
    public class SleepScheduleTests
    {
        [Test]
        public void StarterTown_DoesNotAllGoToBedOrGetUpAtOnce()
        {
            var residents = StarterTown.CreateResidents(StarterTown.CreateWorld());
            var bedtimes = residents.Select(SleepSchedule.BedtimeOf).Distinct().Count();
            var wakes = residents.Select(SleepSchedule.WakeOf).Distinct().Count();

            Assert.That(bedtimes, Is.GreaterThanOrEqualTo(5));
            Assert.That(wakes, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void EveryoneSleepsEightHours_AndIsUpInTimeForAMorningShift()
        {
            foreach (var r in StarterTown.CreateResidents(StarterTown.CreateWorld()))
            {
                var bed = SleepSchedule.BedtimeOf(r);
                var wake = SleepSchedule.WakeOf(r);
                Assert.That((wake - bed + GameTime.MinutesPerDay) % GameTime.MinutesPerDay, Is.EqualTo(SleepSchedule.SleepMinutes), r.Id);

                if (r.Job != null && r.Job.StartMinuteOfDay < GameTime.MinuteOfDayFor(14, 0) && r.Job.StartMinuteOfDay > wake)
                {
                    var leave = r.Job.StartMinuteOfDay - ActivityPlanner.CommuteLeadMinutes;
                    Assert.That(leave - wake, Is.GreaterThanOrEqualTo(SleepSchedule.GetReadyMinutes), $"{r.Id} wakes at {wake}, leaves at {leave}");
                }
            }
        }

        [Test]
        public void SomeoneOnALateShift_GoesToBedAfterWork()
        {
            var r = StarterTown.CreateResidents(StarterTown.CreateWorld()).First(x => x.Job == null || x.Job.StartMinuteOfDay >= GameTime.MinuteOfDayFor(14, 0));
            r.Job = new Job("Night porter", "obj-anything", GameTime.MinuteOfDayFor(18, 0), GameTime.MinuteOfDayFor(23, 30), Job.EveryDay, 1000);

            Assert.That(SleepSchedule.BedtimeOf(r), Is.EqualTo(GameTime.MinuteOfDayFor(0, 15)));
            Assert.That(SleepSchedule.IsSleepTime(r, GameTime.FromDayAndTime(1, 23, 0)), Is.False);
        }

        [Test]
        public void Bedtimes_NeverChangeBetweenRuns()
        {
            var a = StarterTown.CreateResidents(StarterTown.CreateWorld()).Select(SleepSchedule.BedtimeOf).ToList();
            var b = StarterTown.CreateResidents(StarterTown.CreateWorld()).Select(SleepSchedule.BedtimeOf).ToList();
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
