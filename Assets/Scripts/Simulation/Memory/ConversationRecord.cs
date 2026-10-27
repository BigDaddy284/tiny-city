using System.Collections.Generic;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Memory
{
    public enum ConversationKind
    {
        /// <summary>Something one resident says aloud or to themselves.</summary>
        Remark,

        /// <summary>A back-and-forth conversation between residents.</summary>
        Chat,
    }

    /// <summary>The feel of a line. The AI picks it from this list; the game decides what it does to relationships.</summary>
    public enum ConversationTone
    {
        Neutral,
        Warm,
        Playful,
        Disagree,
    }

    public sealed class ConversationLine
    {
        public const int MaxTextLength = 160;

        internal ConversationLine(string speakerId, GameTime time, string text, ConversationTone tone = ConversationTone.Neutral, bool fromAi = false)
        {
            SpeakerId = speakerId;
            Time = time;
            Text = text;
            Tone = tone;
            FromAi = fromAi;
        }

        public ConversationTone Tone { get; }

        /// <summary>True when the AI wrote the line; false for the built-in small talk.</summary>
        public bool FromAi { get; }

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
