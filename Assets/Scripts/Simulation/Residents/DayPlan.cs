using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Residents
{
    /// <summary>One line of a resident's day plan: what they mean to be doing from an hour of the day.</summary>
    public sealed class DayPlanEntry
    {
        public DayPlanEntry(int hour, string text)
        {
            Hour = hour;
            Text = text;
        }

        /// <summary>The hour of the day, 0 to 23, that this part of the plan starts.</summary>
        public int Hour { get; }

        public string Text { get; }

        public string ClockText => $"{Hour:00}:00";
    }

    /// <summary>
    /// Tiny City 2: the broad strokes of a resident's day, written by the AI when they start their day, as the
    /// generative agents do. It only guides their choices (it goes into each decision and their bedtime
    /// reflection); every action is still chosen and checked one at a time. Like an unanswered decision it is not
    /// saved: after loading, the resident plans the rest of the day again.
    /// </summary>
    public sealed class DayPlan
    {
        /// <summary>A plan day runs from 04:00 to 04:00, so staying up past midnight is still the same day.</summary>
        public const int DayStartHour = 4;

        public const int MaxEntries = 10;
        public const int MaxTextLength = 80;

        public DayPlan(int day, GameTime madeAt, IReadOnlyList<DayPlanEntry> entries)
        {
            Day = day;
            MadeAt = madeAt;
            Entries = entries;
        }

        /// <summary>The plan day it is for (see <see cref="DayFor"/>).</summary>
        public int Day { get; }

        public GameTime MadeAt { get; }

        /// <summary>In the order they happen through the day, starting at <see cref="DayStartHour"/>.</summary>
        public IReadOnlyList<DayPlanEntry> Entries { get; }

        public static int DayFor(GameTime time) =>
            (int)((time.TotalMinutes - (DayStartHour * GameTime.MinutesPerHour)) / GameTime.MinutesPerDay) + 1;

        /// <summary>How far an hour is into a plan day, so 23:00 comes before 01:00.</summary>
        public static int Order(int hour) => (hour - DayStartHour + 24) % 24;

        /// <summary>The part of the plan for this hour: the latest entry that has started, or null before the first.</summary>
        public DayPlanEntry Now(GameTime time) =>
            Entries.LastOrDefault(e => Order(e.Hour) <= Order(time.Hour));

        public string Describe() => string.Join("; ", Entries.Select(e => $"{e.ClockText} {e.Text}"));

        /// <summary>
        /// Builds a plan from the AI's entries: hours must be whole hours 0 to 23 (the first entry for an hour
        /// wins), text is cleaned and shortened, and at most <see cref="MaxEntries"/> are kept. Returns null if
        /// nothing usable is left.
        /// </summary>
        public static DayPlan Make(int day, GameTime madeAt, IEnumerable<DayPlanEntry> entries)
        {
            var kept = new List<DayPlanEntry>();
            foreach (var e in entries ?? Enumerable.Empty<DayPlanEntry>())
            {
                if (e == null || e.Hour < 0 || e.Hour > 23 || kept.Any(k => k.Hour == e.Hour))
                {
                    continue;
                }

                var text = TextRules.Clean(e.Text, MaxTextLength);
                if (text.Length > 0)
                {
                    kept.Add(new DayPlanEntry(e.Hour, text));
                }
            }

            if (kept.Count == 0)
            {
                return null;
            }

            return new DayPlan(day, madeAt, kept.OrderBy(e => Order(e.Hour)).Take(MaxEntries).ToList());
        }
    }
}
