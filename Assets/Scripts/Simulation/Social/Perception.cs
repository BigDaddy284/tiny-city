namespace TinyCity.Simulation.Social
{
    /// <summary>Tiny City 2: every number for what residents notice and when they are asked whether to react.</summary>
    public static class PerceptionRules
    {
        /// <summary>How far, in tiles on the same map, a resident notices someone.</summary>
        public const int SightDistance = 6;

        /// <summary>The cheap filter: the same two people are only offered a reaction this often.</summary>
        public const int ReactCooldownMinutes = 180;

        /// <summary>The AI budget: each resident is offered at most one reaction this often, whoever they see.</summary>
        public const int ReactEveryMinutes = 120;

        /// <summary>A reaction not answered within this many game minutes is dropped (the moment has passed).</summary>
        public const int ReactWaitMinutes = 15;

        /// <summary>What noticing someone is worth as a memory. The game's figure stands (the AI isn't asked to rate
        /// these), because observations are the most common memory and rating each would double the AI's work.</summary>
        public const int ObservationImportance = 2;
    }

    /// <summary>
    /// Tiny City 2: a resident noticed someone and the AI is being asked whether they go over and talk. Lives only
    /// while the game runs (not saved), like an unanswered decision.
    /// </summary>
    public sealed class PendingReaction
    {
        public PendingReaction(long id, string otherId, long askedMinute, string seen)
        {
            Id = id;
            OtherId = otherId;
            AskedMinute = askedMinute;
            Seen = seen;
        }

        /// <summary>The id an answer must quote to be accepted.</summary>
        public long Id { get; }

        public string OtherId { get; }

        public long AskedMinute { get; }

        /// <summary>What they saw, in words, for the prompt.</summary>
        public string Seen { get; }
    }
}
