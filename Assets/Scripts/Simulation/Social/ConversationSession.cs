using System.Collections.Generic;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Social
{
    /// <summary>
    /// A conversation in progress. The game sets its length, whose turn it is and when the next line is due; the AI
    /// (or built-in small talk) only supplies each line. Saved with the town so a loaded game carries on mid-chat.
    /// </summary>
    /// <summary>Tiny City 2: food one speaker has offered the other in a chat, waiting for their answer. Not saved.</summary>
    public sealed class FoodOffer
    {
        public FoodOffer(string fromId, string toId, int portions, long pricePence)
        {
            FromId = fromId;
            ToId = toId;
            Portions = portions;
            PricePence = pricePence;
        }

        public string FromId { get; }

        public string ToId { get; }

        public int Portions { get; }

        /// <summary>0 for a gift.</summary>
        public long PricePence { get; }
    }

    /// <summary>
    /// Tiny City 2: a tax man has asked the other person in a chat to hand over a share of their food and
    /// materials; the other's next line pays it or not. Not saved.
    /// </summary>
    public sealed class TaxDemand
    {
        public TaxDemand(string fromId, string toId, int percent)
        {
            FromId = fromId;
            ToId = toId;
            Percent = percent;
        }

        public string FromId { get; }

        public string ToId { get; }

        public int Percent { get; }
    }

    public sealed class ConversationSession
    {
        internal ConversationSession(string id, string initiatorId, string partnerId, GameTime started, int maxLines, string place)
        {
            Id = id;
            ParticipantIds = new[] { initiatorId, partnerId };
            Started = started;
            MaxLines = maxLines;
            Place = place ?? string.Empty;
            NextLineAt = started.TotalMinutes;
        }

        public string Id { get; }

        /// <summary>Two residents; the first spoke first.</summary>
        public IReadOnlyList<string> ParticipantIds { get; }

        public GameTime Started { get; }

        /// <summary>The game's limit on lines. Ending earlier is allowed once <see cref="SocialRules.MinLines"/> are said.</summary>
        public int MaxLines { get; }

        public string Place { get; }

        public List<ConversationLine> Lines { get; } = new List<ConversationLine>();

        /// <summary>What the last AI line said they were talking about, if anything.</summary>
        public string Topic { get; internal set; } = string.Empty;

        /// <summary>Tiny City 2: food offered and not yet answered (not saved, so a loaded chat has none).</summary>
        public FoodOffer FoodOffer { get; internal set; }

        /// <summary>A tax man's demand waiting for the other person's answer, or null (Tiny City 2, not saved).</summary>
        public TaxDemand TaxDemand { get; internal set; }

        /// <summary>Game minute when the next line should be said.</summary>
        public long NextLineAt { get; internal set; }

        /// <summary>Waiting for the AI to write the next line. Not saved: a loaded game asks again.</summary>
        public bool AwaitingLine { get; internal set; }

        /// <summary>The id an AI line must quote; changes for every line.</summary>
        public long LineRequestId { get; internal set; }

        /// <summary>Game minutes spent waiting for AI lines; they don't count towards <see cref="SocialRules.MaxConversationMinutes"/>.</summary>
        public long WaitedMinutes { get; internal set; }

        /// <summary>Game minute at which built-in small talk is used instead of the AI.</summary>
        public long LineDeadline { get; internal set; }

        public string SpeakerId => ParticipantIds[Lines.Count % 2];

        public string ListenerId => ParticipantIds[(Lines.Count + 1) % 2];

        public string OtherThan(string residentId) => ParticipantIds[0] == residentId ? ParticipantIds[1] : ParticipantIds[0];
    }
}
