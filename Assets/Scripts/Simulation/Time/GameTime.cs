using System;

namespace TinyCity.Simulation.Time
{
    public enum Weekday
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday,
    }

    /// <summary>
    /// In-game time as whole minutes since 00:00 on Day 1 (a Monday). The simulation advances one minute per tick.
    /// </summary>
    public readonly struct GameTime : IEquatable<GameTime>, IComparable<GameTime>
    {
        public const int MinutesPerHour = 60;
        public const int MinutesPerDay = 24 * MinutesPerHour;
        public const int DaysPerWeek = 7;

        public GameTime(long totalMinutes)
        {
            if (totalMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalMinutes), "Game time cannot be negative.");
            }

            TotalMinutes = totalMinutes;
        }

        public long TotalMinutes { get; }

        /// <summary>1-based day number.</summary>
        public int Day => (int)(TotalMinutes / MinutesPerDay) + 1;

        public int MinuteOfDay => (int)(TotalMinutes % MinutesPerDay);

        public int Hour => MinuteOfDay / MinutesPerHour;

        public int Minute => MinuteOfDay % MinutesPerHour;

        public Weekday Weekday => (Weekday)((Day - 1) % DaysPerWeek);

        public bool IsWeekend => Weekday == Weekday.Saturday || Weekday == Weekday.Sunday;

        public static GameTime FromDayAndTime(int day, int hour, int minute)
        {
            if (day < 1 || hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                throw new ArgumentOutOfRangeException(nameof(day), $"Invalid game time: day {day}, {hour}:{minute}.");
            }

            return new GameTime(((long)(day - 1) * MinutesPerDay) + (hour * MinutesPerHour) + minute);
        }

        public static int MinuteOfDayFor(int hour, int minute) => (hour * MinutesPerHour) + minute;

        /// <summary>True when <paramref name="minuteOfDay"/> is in [start, end), wrapping past midnight when end &lt; start.</summary>
        public static bool IsWithin(int minuteOfDay, int start, int end)
        {
            return start <= end
                ? minuteOfDay >= start && minuteOfDay < end
                : minuteOfDay >= start || minuteOfDay < end;
        }

        public GameTime AddMinutes(long minutes) => new GameTime(TotalMinutes + minutes);

        public bool Equals(GameTime other) => TotalMinutes == other.TotalMinutes;

        public override bool Equals(object obj) => obj is GameTime other && Equals(other);

        public override int GetHashCode() => TotalMinutes.GetHashCode();

        public int CompareTo(GameTime other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public static bool operator ==(GameTime a, GameTime b) => a.Equals(b);

        public static bool operator !=(GameTime a, GameTime b) => !a.Equals(b);

        public string ClockText => $"{Hour:00}:{Minute:00}";

        public override string ToString() => $"{Weekday} Day {Day}, {ClockText}";
    }
}
