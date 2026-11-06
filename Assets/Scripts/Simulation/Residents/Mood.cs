using System.Linq;

namespace TinyCity.Simulation.Residents
{
    /// <summary>What a resident's face would say at a glance, shown as a little icon over their head.</summary>
    public enum Mood
    {
        None,
        Happy,
        Hungry,
        Thirsty,
        Grubby,
        Lonely,
        Bored,
    }

    /// <summary>
    /// Reads a mood from the simulation's own state. Nothing is stored or saved: the mood is worked out afresh from
    /// needs, health, activity and company whenever it is asked for, so it can never disagree with the game.
    /// </summary>
    public static class Moods
    {
        /// <summary>A need below this shows as a worry over the resident's head.</summary>
        public const double LowNeed = 20;

        /// <summary>Every need at or above this, in good health, shows as happy.</summary>
        public const double ContentNeed = 70;

        public static Mood Of(TownSimulation simulation, Resident resident)
        {
            if (resident == null || !resident.IsAlive)
            {
                return Mood.None;
            }

            var needs = resident.Needs;
            if (needs[NeedType.Hunger] < LowNeed)
            {
                return Mood.Hungry;
            }

            if (needs[NeedType.Thirst] < LowNeed)
            {
                return Mood.Thirsty;
            }

            if (needs[NeedType.Hygiene] < LowNeed)
            {
                return Mood.Grubby;
            }

            if (needs[NeedType.Social] < LowNeed)
            {
                return Mood.Lonely;
            }

            if (needs[NeedType.Fun] < LowNeed)
            {
                return Mood.Bored;
            }

            for (var i = 0; i < Needs.Count; i++)
            {
                if (needs[(NeedType)i] < ContentNeed)
                {
                    return Mood.None;
                }
            }

            return Mood.Happy;
        }
    }
}
