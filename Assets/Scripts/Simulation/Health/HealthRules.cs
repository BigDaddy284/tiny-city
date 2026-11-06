using System;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.Health
{
    /// <summary>
    /// Every number about health. Neglected needs wear it down and good care slowly restores it; nobody falls ill,
    /// ages or dies in Tiny City 2. The AI never changes health.
    /// </summary>
    public static class HealthRules
    {
        public const double MaxHealth = 100;

        public const double StartingHealth = 90;

        /// <summary>Below this level a need counts as neglected and wears health down.</summary>
        public const double NeglectedNeed = 10;

        public const double NeglectDrainPerHour = 1.5;

        /// <summary>Nobody sleeps, so this is the whole day's recovery: about as much as a night's sleep and a day awake gave before.</summary>
        public const double RecoveryPerHour = 0.8;

        private static readonly NeedType[] VitalNeeds = { NeedType.Hunger, NeedType.Thirst, NeedType.Hygiene };

        /// <summary>
        /// One minute of health: neglected needs wear it down, and otherwise it slowly recovers. Returns true when it has fallen to <paramref name="lowest"/>, where it stops.
        /// </summary>
        public static bool ApplyMinute(Resident r, double lowest)
        {
            var neglected = 0;
            foreach (var need in VitalNeeds)
            {
                if (r.Needs[need] < NeglectedNeed)
                {
                    neglected++;
                }
            }

            var change = neglected == 0
                ? RecoveryPerHour / 60.0
                : -neglected * NeglectDrainPerHour / 60.0;
            r.Health = Math.Max(lowest, Math.Min(MaxHealth, r.Health + change));
            return neglected > 0 && r.Health <= lowest;
        }
    }
}
