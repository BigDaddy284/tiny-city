using System;
using System.Linq;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Residents
{
    /// <summary>
    /// Core traits from 0 to 1. They change how quickly some needs fall and which leisure a resident prefers.
    /// Only game logic changes them; the AI may never rewrite them directly.
    /// </summary>
    public sealed class Personality
    {
        public Personality(double active, double sociable, double tidy, double bookish)
        {
            Active = Clamp01(active);
            Sociable = Clamp01(sociable);
            Tidy = Clamp01(tidy);
            Bookish = Clamp01(bookish);
        }

        public double Active { get; }

        public double Sociable { get; }

        public double Tidy { get; }

        public double Bookish { get; }

        public string Describe()
        {
            return $"{Word(Active, "Energetic", "Easy-going")}, {Word(Sociable, "outgoing", "private")}, " +
                $"{Word(Tidy, "tidy", "relaxed about mess")}, {Word(Bookish, "loves books", "not much of a reader")}";
        }

        private static string Word(double value, string high, string low) => value >= 0.5 ? high : low;

        private static double Clamp01(double v) => double.IsNaN(v) ? 0.5 : Math.Max(0, Math.Min(1, v));
    }

    public sealed class Job
    {
        /// <summary>Monday to Friday, as a <see cref="WorkDays"/> mask.</summary>
        public const int Weekdays = 0b0011111;

        public const int EveryDay = 0b1111111;

        public Job(string title, string workObjectId, int startMinuteOfDay, int endMinuteOfDay, bool worksWeekends, long hourlyWagePence = 0)
            : this(title, workObjectId, startMinuteOfDay, endMinuteOfDay, worksWeekends ? EveryDay : Weekdays, hourlyWagePence)
        {
        }

        /// <param name="workDays">Bit 0 is Monday, bit 6 Sunday.</param>
        public Job(string title, string workObjectId, int startMinuteOfDay, int endMinuteOfDay, int workDays, long hourlyWagePence)
        {
            if (startMinuteOfDay >= endMinuteOfDay)
            {
                throw new ArgumentException("A shift must end after it starts on the same day.");
            }

            Title = title;
            WorkObjectId = workObjectId;
            StartMinuteOfDay = startMinuteOfDay;
            EndMinuteOfDay = endMinuteOfDay;
            WorkDays = workDays & EveryDay;
            HourlyWagePence = Math.Max(0, hourlyWagePence);
        }

        public string Title { get; }

        public string WorkObjectId { get; }

        public int StartMinuteOfDay { get; }

        public int EndMinuteOfDay { get; }

        /// <summary>Which days the shift runs; bit 0 is Monday.</summary>
        public int WorkDays { get; }

        public bool WorksWeekends => WorksOn(Weekday.Saturday) || WorksOn(Weekday.Sunday);

        /// <summary>Pay per hour actually worked, in pence.</summary>
        public long HourlyWagePence { get; }

        public bool WorksOn(Weekday day) => (WorkDays & (1 << (int)day)) != 0;

        /// <summary>Short text such as "Mon–Fri" or "Tue–Sun".</summary>
        public string DaysText()
        {
            if (WorkDays == EveryDay)
            {
                return "every day";
            }

            var names = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var days = Enumerable.Range(0, 7).Where(d => WorksOn((Weekday)d)).ToList();
            var consecutive = days.Count > 1 && days.Last() - days.First() == days.Count - 1;
            return consecutive ? $"{names[days.First()]}–{names[days.Last()]}" : string.Join(", ", days.Select(d => names[d]));
        }
    }
}
