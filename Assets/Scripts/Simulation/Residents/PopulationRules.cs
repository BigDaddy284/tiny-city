using TinyCity.Simulation.Health;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Residents
{
    /// <summary>
    /// How many people the city holds and how newcomers arrive. Every number is here, and luck comes only from
    /// <see cref="Chance"/>, so a town grows the same way every time it is replayed.
    /// </summary>
    public static class PopulationRules
    {
        /// <summary>The most living residents a city can have, whoever they are.</summary>
        public const int MaxLiving = 50;

        /// <summary>The most people who can ever move in from out of town, counting any who have since died.</summary>
        public const int MaxNewcomers = 10;

        /// <summary>Nobody arrives in the first days of a new city.</summary>
        public const int FirstArrivalDay = 3;

        /// <summary>When the day's chance of someone arriving is rolled: mid-morning, so they arrive in daylight.</summary>
        public static readonly int ArrivalMinute = GameTime.MinuteOfDayFor(10, 0);

        /// <summary>The chance each day that someone moves in, while there's room: about two a week.</summary>
        public const double ArrivalChance = 0.25;

        public static bool SomeoneArrives(string townId, int day) => day >= FirstArrivalDay && Chance.Roll("arrival:" + townId, day, 0) < ArrivalChance;
    }
}
