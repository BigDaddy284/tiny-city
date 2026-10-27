using System.Collections.Generic;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Memory
{
    public enum ConversationKind
    {
        /// <summary>Something one resident says aloud or to themselves. Phase 3 adds conversations between residents.</summary>
        Remark,
    }

    public sealed class ConversationLine
    {
        public const int MaxTextLength = 160;

        internal ConversationLine(string speakerId, GameTime time, string text)
        {
            SpeakerId = speakerId;
            Time = time;
            Text = text;
        }

        public string SpeakerId { get; }

        public GameTime Time { get; }

        public string Text { get; }
    }

    /// <summary>A chronological record of something said, kept separate from memories and current state.</summary>
    public sealed class ConversationRecord
    {
        internal ConversationRecord(string id, ConversationKind kind, GameTime started, IReadOnlyList<string> participants, string context, IReadOnlyList<ConversationLine> lines)
        {
            Id = id;
            Kind = kind;
            Started = started;
            Participants = participants;
            Context = context;
            Lines = lines;
        }

        public string Id { get; }

        public ConversationKind Kind { get; }

        public GameTime Started { get; }

        public IReadOnlyList<string> Participants { get; }

        /// <summary>What was happening, e.g. the activity id, so history can be searched later.</summary>
        public string Context { get; }

        public IReadOnlyList<ConversationLine> Lines { get; }
    }
}
