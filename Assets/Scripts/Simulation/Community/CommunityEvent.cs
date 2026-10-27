using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Health;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Community
{
    /// <summary>
    /// A regular gathering on the village calendar, held at one gathering spot. It happens either on certain
    /// weekdays (<see cref="Days"/>, bit = <see cref="Weekday"/>) or once a year on <see cref="YearDay"/>.
    /// </summary>
    public sealed class CommunityEvent
    {
        public CommunityEvent(string id, string name, string placeObjectId, int startMinute, int endMinute, int days = 0, int yearDay = 0, double turnout = 0.5)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(placeObjectId))
            {
                throw new ArgumentException("A community event needs an id and a place.");
            }

            if (startMinute < 0 || endMinute > GameTime.MinutesPerDay || endMinute <= startMinute)
            {
                throw new ArgumentException($"Event '{id}' must start and end on the same day.");
            }

            if ((days == 0) == (yearDay == 0) || yearDay < 0 || yearDay > HealthRules.DaysPerYear)
            {
                throw new ArgumentException($"Event '{id}' needs either weekdays or a day of the year.");
            }

            Id = id;
            Name = name ?? id;
            PlaceObjectId = placeObjectId;
            StartMinute = startMinute;
            EndMinute = endMinute;
            Days = days;
            YearDay = yearDay;
            Turnout = Math.Max(0, Math.Min(1, turnout));
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>The gathering spot where it is held.</summary>
        public string PlaceObjectId { get; }

        public int StartMinute { get; }

        public int EndMinute { get; }

        /// <summary>Weekday mask, or 0 for a yearly event.</summary>
        public int Days { get; }

        /// <summary>Day of the year (1 to <see cref="HealthRules.DaysPerYear"/>), or 0 for a weekly event.</summary>
        public int YearDay { get; }

        /// <summary>The share of an average resident's free time spent going, before personality.</summary>
        public double Turnout { get; }

        public bool IsYearly => YearDay > 0;

        public bool OccursOn(int day) =>
            IsYearly ? HealthRules.CycleDay(day) == YearDay : (Days & (1 << (int)new GameTime((long)(day - 1) * GameTime.MinutesPerDay).Weekday)) != 0;

        /// <summary>The same event on a given day, e.g. "quiz-night-day10". Used for memories and the town history.</summary>
        public string OccurrenceId(int day) => $"{Id}-day{day}";
    }

    /// <summary>The village calendar. Yearly events take precedence over a weekly one held the same day.</summary>
    public sealed class CommunityCalendar
    {
        public CommunityCalendar(IEnumerable<CommunityEvent> events)
        {
            Events = (events ?? Enumerable.Empty<CommunityEvent>()).OrderBy(e => e.IsYearly ? 0 : 1).ToList();
            if (Events.Select(e => e.Id).Distinct().Count() != Events.Count)
            {
                throw new ArgumentException("Community event ids must be unique.");
            }
        }

        public IReadOnlyList<CommunityEvent> Events { get; }

        /// <summary>The event happening at this time, or null. On a yearly event's day, weekly ones are cancelled.</summary>
        public CommunityEvent OnAt(GameTime time) => OnDay(time.Day).FirstOrDefault(e => time.MinuteOfDay >= e.StartMinute && time.MinuteOfDay < e.EndMinute);

        /// <summary>The events held on a day: its yearly event if any, otherwise its weekly ones.</summary>
        public IEnumerable<CommunityEvent> OnDay(int day)
        {
            var yearly = Events.Where(e => e.IsYearly && e.OccursOn(day)).ToList();
            return yearly.Count > 0 ? yearly : Events.Where(e => !e.IsYearly && e.OccursOn(day));
        }

        /// <summary>The next event to start at or after this time, within a year, with its start time.</summary>
        public (CommunityEvent evt, GameTime start)? Next(GameTime now)
        {
            for (var day = now.Day; day <= now.Day + HealthRules.DaysPerYear; day++)
            {
                foreach (var e in OnDay(day).OrderBy(e => e.StartMinute))
                {
                    var start = GameTime.FromDayAndTime(day, e.StartMinute / 60, e.StartMinute % 60);
                    if (start.TotalMinutes + (e.EndMinute - e.StartMinute) > now.TotalMinutes)
                    {
                        return (e, start);
                    }
                }
            }

            return null;
        }
    }
}
