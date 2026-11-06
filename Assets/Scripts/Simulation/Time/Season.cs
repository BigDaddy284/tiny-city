
namespace TinyCity.Simulation.Time
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>The four seasons share each <see cref="DaysPerYear"/>-day year equally, spring first.</summary>
    public static class Seasons
    {
        /// <summary>Game days in one year.</summary>
        public const int DaysPerYear = 28;

        public const int DaysPerSeason = DaysPerYear / 4;

        /// <summary>The day of the year, 1 to <see cref="DaysPerYear"/>.</summary>
        public static int CycleDay(int day) => ((day - 1) % DaysPerYear) + 1;

        public static Season Of(int day) => (Season)((CycleDay(day) - 1) / DaysPerSeason);

        public static Season Of(GameTime time) => Of(time.Day);

        /// <summary>1 to <see cref="DaysPerSeason"/>: how far into its season a day is.</summary>
        public static int DayOfSeason(int day) => ((CycleDay(day) - 1) % DaysPerSeason) + 1;
    }
}
