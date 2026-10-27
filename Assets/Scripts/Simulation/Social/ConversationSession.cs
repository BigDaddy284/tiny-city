using System.Collections.Generic;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Social
{
    /// <summary>
    /// A conversation in progress. The game sets its length, whose turn it is and when the next line is due; the AI
    /// (or built-in small talk) only supplies each line. Saved with the town so a loaded game carries on mid-chat.
    /// </summary>
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
