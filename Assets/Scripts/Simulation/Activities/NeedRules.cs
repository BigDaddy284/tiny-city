using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.Activities
{
    /// <summary>Deterministic per-minute need changes. Nothing else changes need values.</summary>
    public static class NeedRules
    {
        /// <summary>Below this a need is urgent and interrupts most activities.</summary>
        public const double Critical = 20;

        /// <summary>Below this a resident starts looking after the need when nothing more important is due.</summary>
        public const double Comfortable = 65;

        private static readonly double[] BaseDecayPerHour =
        {
            6.0, // Hunger
            8.0, // Thirst
            3.5, // Energy
            3.0, // Hygiene
            7.0, // Toilet
            2.0, // Social
            2.0, // Exercise
            4.0, // Fun
        };

        public static double DecayPerHour(NeedType need, Personality personality)
        {
            var rate = BaseDecayPerHour[(int)need];
            switch (need)
            {
                case NeedType.Exercise:
                    return rate * (0.5 + personality.Active);
                case NeedType.Social:
                    return rate * (0.5 + personality.Sociable);
                default:
                    return rate;
            }
        }

        /// <summary>The level below which a resident wants to look after a need. Tidy residents shower sooner.</summary>
        public static double ComfortThreshold(NeedType need, Personality personality)
        {
            return need == NeedType.Hygiene ? Comfortable + (20 * (personality.Tidy - 0.5)) : Comfortable;
        }

        /// <summary>Applies one minute of decay (faster for some needs while ill) plus the gains of <paramref name="activity"/> if one is being performed.</summary>
        public static void ApplyMinute(Needs needs, Personality personality, ActivityDefinition activity, Health.Illness illness = null)
        {
            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                var decay = DecayPerHour(need, personality) / 60.0 * (illness?.DecayMultiplier(need) ?? 1.0);
                var gain = 0.0;
                if (activity != null)
                {
                    decay *= activity.DecayMultiplier(need);
                    gain = activity.GainPerMinute(need);
                }

                needs[need] = needs[need] - decay + gain;
            }
        }
    }
}
