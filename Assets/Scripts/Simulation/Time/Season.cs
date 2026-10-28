using TinyTown.Simulation.Health;

namespace TinyTown.Simulation.Time
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>The four seasons share each <see cref="HealthRules.DaysPerYear"/>-day year equally, spring first.</summary>
    public static class Seasons
    {
        public const int DaysPerSeason = HealthRules.DaysPerYear / 4;

        public static Season Of(int day) => (Season)((HealthRules.CycleDay(day) - 1) / DaysPerSeason);

        public static Season Of(GameTime time) => Of(time.Day);

        /// <summary>1 to <see cref="DaysPerSeason"/>: how far into its season a day is.</summary>
        public static int DayOfSeason(int day) => ((HealthRules.CycleDay(day) - 1) % DaysPerSeason) + 1;
    }
}
