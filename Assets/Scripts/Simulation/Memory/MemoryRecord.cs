using System.Collections.Generic;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Memory
{
    public enum MemoryType
    {
        /// <summary>Something that happened, recorded by the game.</summary>
        Event,

        /// <summary>The resident's own end-of-day thought, suggested by the AI and checked by the game.</summary>
        Reflection,

        /// <summary>Background that came with the resident, such as moving into their home.</summary>
        LifeHistory,
    }

    /// <summary>
    /// One thing a resident remembers. Memories are kept for the important things only; the activity log and the
    /// conversation history hold the everyday detail.
    /// </summary>
    public sealed class MemoryRecord
    {
        public const int MinImportance = 1;
        public const int MaxImportance = 10;
        public const int MaxSummaryLength = 240;

        internal MemoryRecord(string id, string residentId, GameTime time, MemoryType type, int importance, string summary, IReadOnlyList<string> participants, IReadOnlyList<string> tags)
        {
            Id = id;
            ResidentId = residentId;
            Time = time;
            Type = type;
            Importance = importance;
            Summary = summary;
            Participants = participants;
            Tags = tags;
        }

        public string Id { get; }

        public string ResidentId { get; }

        public GameTime Time { get; }

        public MemoryType Type { get; }

        /// <summary>From 1 (minor) to 10 (life-changing).</summary>
        public int Importance { get; }

        public string Summary { get; }

        /// <summary>Resident ids involved, including the resident who remembers.</summary>
        public IReadOnlyList<string> Participants { get; }

        /// <summary>Ids of related things (objects, activities, needs, conversations) used to find relevant memories.</summary>
        public IReadOnlyList<string> Tags { get; }
    }
}
