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

        /// <summary>From this age residents are adults: they leave home, and can work, drink and fall in love.</summary>
        public const int AdultAge = 18;

        /// <summary>Babies are carried about by a parent until this age, then live their own day.</summary>
        public const int WalkingAge = 3;

        /// <summary>
        /// Children grow a year older every this many days until <see cref="AdultAge"/> (adults age once every
        /// 28-day year), so a newborn is grown up in 54 days.
        /// </summary>
        public const int ChildDaysPerYear = 3;

        /// <summary>The most people who can ever move in from out of town, counting any who have since died.</summary>
        public const int MaxNewcomers = 10;

        /// <summary>Nobody arrives in the first days of a new city.</summary>
        public const int FirstArrivalDay = 3;

        /// <summary>When the day's chance of someone arriving is rolled: mid-morning, so they arrive in daylight.</summary>
        public static readonly int ArrivalMinute = GameTime.MinuteOfDayFor(10, 0);

        /// <summary>When anyone sleeping rough is offered a home that has come free.</summary>
        public static readonly int HousingMinute = GameTime.MinuteOfDayFor(9, 0);

        /// <summary>The chance each day that someone moves in, while there's room: about two a week.</summary>
        public const double ArrivalChance = 0.25;

        /// <summary>When the day's chance of a couple having a baby is rolled.</summary>
        public static readonly int BirthMinute = GameTime.MinuteOfDayFor(6, 30);

        /// <summary>The chance each day that a committed couple who can have a baby has one: about one every three weeks.</summary>
        public const double BirthChance = 0.05;

        /// <summary>Neither parent may be older than this.</summary>
        public const int MaxParentAge = 50;

        public const int MaxChildrenPerCouple = 3;

        /// <summary>Weekly pocket money for children from this age.</summary>
        public const int PocketMoneyAge = 8;

        public static bool BabyArrives(string coupleId, int day) => Chance.Roll("birth:" + coupleId, day, 0) < BirthChance;

        public static bool SomeoneArrives(string townId, int day) => day >= FirstArrivalDay && Chance.Roll("arrival:" + townId, day, 0) < ArrivalChance;
    }
}
