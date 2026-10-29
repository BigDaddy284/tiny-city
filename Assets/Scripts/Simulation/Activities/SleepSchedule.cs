using System;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Activities
{
    /// <summary>
    /// Each resident's own bedtime and getting-up time, so the town doesn't all go to bed and get up at once.
    /// Worked out from who they are, never stored: younger people stay up later, everyone gets a fixed personal
    /// shift of up to half an hour either way (from <see cref="Chance"/>, so it never changes between runs), early
    /// shifts pull the alarm earlier, and late shifts push bedtime until after work.
    /// </summary>
    public static class SleepSchedule
    {
        public const int SleepMinutes = 8 * 60;

        /// <summary>Time between getting up and leaving for work (washing, breakfast).</summary>
        public const int GetReadyMinutes = 45;

        /// <summary>After a late shift, time to get home and wind down before bed.</summary>
        public const int WindDownMinutes = 45;

        /// <summary>The most a resident's bedtime moves from the usual one for their age, either way.</summary>
        public const int PersonalShiftMinutes = 30;

        private const int ShiftSalt = 61;

        public static int BedtimeOf(Resident r)
        {
            var job = r.Job;
            if (job != null && EndsLate(job))
            {
                return (job.EndMinuteOfDay + WindDownMinutes) % GameTime.MinutesPerDay;
            }

            var bedtime = UsualBedtime(r.AgeYears) + PersonalShift(r.Id);
            if (job != null && StartsInTheMorning(job))
            {
                var latestWake = job.StartMinuteOfDay - ActivityPlanner.CommuteLeadMinutes - GetReadyMinutes;
                bedtime = Math.Min(bedtime, latestWake - SleepMinutes + GameTime.MinutesPerDay);
            }

            return ((bedtime % GameTime.MinutesPerDay) + GameTime.MinutesPerDay) % GameTime.MinutesPerDay;
        }

        public static int WakeOf(Resident r) => (BedtimeOf(r) + SleepMinutes) % GameTime.MinutesPerDay;

        public static bool IsSleepTime(Resident r, GameTime time) => GameTime.IsWithin(time.MinuteOfDay, BedtimeOf(r), WakeOf(r));

        /// <summary>Minutes from now until this resident's bedtime (0 to a day).</summary>
        public static int MinutesToBed(Resident r, GameTime time) =>
            ((BedtimeOf(r) - time.MinuteOfDay) + GameTime.MinutesPerDay) % GameTime.MinutesPerDay;

        /// <summary>The usual bedtime, as minutes after midnight of the day before: young adults 23:15, then 22:30, and 21:45 from 60.</summary>
        private static int UsualBedtime(int age) =>
            age < 30 ? GameTime.MinuteOfDayFor(23, 15) : age < 60 ? GameTime.MinuteOfDayFor(22, 30) : GameTime.MinuteOfDayFor(21, 45);

        /// <summary>−30 to +30 minutes, in fives.</summary>
        private static int PersonalShift(string residentId)
        {
            var steps = (2 * PersonalShiftMinutes / 5) + 1;
            return ((int)(Chance.Roll(residentId, 0, ShiftSalt) * steps) * 5) - PersonalShiftMinutes;
        }

        private static bool StartsInTheMorning(Job job) => job.StartMinuteOfDay >= GameTime.MinuteOfDayFor(4, 0) && job.StartMinuteOfDay < GameTime.MinuteOfDayFor(14, 0);

        /// <summary>A shift that ends at 22:00 or later, or runs past midnight.</summary>
        private static bool EndsLate(Job job) => job.EndMinuteOfDay >= GameTime.MinuteOfDayFor(22, 0) || job.EndMinuteOfDay < job.StartMinuteOfDay;
    }
}
