using System.Linq;

namespace TinyTown.Simulation.Residents
{
    /// <summary>What a resident's face would say at a glance, shown as a little icon over their head.</summary>
    public enum Mood
    {
        None,
        Happy,
        Asleep,
        Ill,
        NeedsToilet,
        Hungry,
        Thirsty,
        Tired,
        Grubby,
        Lonely,
        Bored,
        InLove,
    }

    /// <summary>
    /// Reads a mood from the simulation's own state. Nothing is stored or saved: the mood is worked out afresh from
    /// needs, health, activity and company whenever it is asked for, so it can never disagree with the game.
    /// </summary>
    public static class Moods
    {
        /// <summary>A need below this shows as a worry over the resident's head.</summary>
        public const double LowNeed = 20;

        /// <summary>The toilet becomes the only thing on their mind a little earlier.</summary>
        public const double UrgentToilet = 15;

        /// <summary>Every need at or above this, in good health, shows as happy.</summary>
        public const double ContentNeed = 70;

        public static Mood Of(TownSimulation simulation, Resident resident)
        {
            if (resident == null || !resident.IsAlive)
            {
                return Mood.None;
            }

            if (resident.State == ResidentState.Performing && resident.ActivityId == Activities.ActivityCatalog.Sleep.Id)
            {
                return Mood.Asleep;
            }

            if (!string.IsNullOrEmpty(resident.IllnessId))
            {
                return Mood.Ill;
            }

            var needs = resident.Needs;
            if (needs[NeedType.Toilet] < UrgentToilet)
            {
                return Mood.NeedsToilet;
            }

            if (needs[NeedType.Hunger] < LowNeed)
            {
                return Mood.Hungry;
            }

            if (needs[NeedType.Thirst] < LowNeed)
            {
                return Mood.Thirsty;
            }

            if (needs[NeedType.Energy] < LowNeed)
            {
                return Mood.Tired;
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

            if (resident.State == ResidentState.Talking && simulation != null && TalkingToPartner(simulation, resident))
            {
                return Mood.InLove;
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

        private static bool TalkingToPartner(TownSimulation simulation, Resident resident)
        {
            var partnerId = simulation.Relationships.PartnerOf(resident.Id);
            if (partnerId == null || resident.ConversationId == null)
            {
                return false;
            }

            var partner = simulation.Residents.FirstOrDefault(r => r.Id == partnerId);
            return partner != null && partner.IsAlive && partner.State == ResidentState.Talking && partner.ConversationId == resident.ConversationId;
        }
    }
}
