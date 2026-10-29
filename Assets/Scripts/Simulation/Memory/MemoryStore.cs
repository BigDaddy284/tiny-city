using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Memory
{
    /// <summary>
    /// Every resident's memories. Additions are validated and cleaned, ids are stable and never reused, and each
    /// resident's memories are capped so a long game cannot grow without bound.
    /// </summary>
    public sealed class MemoryStore
    {
        /// <summary>When a resident has more than this many memories, their least important oldest one is forgotten.</summary>
        public const int MaxPerResident = 300;

        private const int MaxTags = 12;

        private readonly List<MemoryRecord> records = new List<MemoryRecord>();

        public IReadOnlyList<MemoryRecord> All => records;

        /// <summary>The number the next memory id will use. Saved so ids stay unique after loading.</summary>
        public long NextNumber { get; private set; } = 1;

        public IEnumerable<MemoryRecord> ForResident(string residentId) => records.Where(m => m.ResidentId == residentId);

        /// <summary>Adds a memory and returns it, or returns null if the summary is empty after cleaning.</summary>
        public MemoryRecord Add(string residentId, GameTime time, MemoryType type, int importance, string summary, IEnumerable<string> participants = null, IEnumerable<string> tags = null)
        {
            if (string.IsNullOrWhiteSpace(residentId))
            {
                throw new ArgumentException("A memory needs a resident id.", nameof(residentId));
            }

            var clean = TextRules.Clean(summary, MemoryRecord.MaxSummaryLength);
            if (clean.Length == 0)
            {
                return null;
            }

            var people = new List<string> { residentId };
            people.AddRange((participants ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p) && !people.Contains(p)));
            var tagList = (tags ?? Enumerable.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(MaxTags).ToList();
            var record = new MemoryRecord($"mem-{NextNumber:000000}", residentId, time, type, ClampImportance(importance), clean, people, tagList);
            NextNumber++;
            Insert(record);
            return record;
        }

        /// <summary>
        /// The memories most worth mentioning right now: important, recent, and sharing tags with the current
        /// situation. Ties go to the newer memory so the result is deterministic.
        /// </summary>
        public IReadOnlyList<MemoryRecord> Recall(string residentId, GameTime now, IEnumerable<string> cues, int count)
        {
            var cueSet = new HashSet<string>(cues ?? Enumerable.Empty<string>());
            return ForResident(residentId)
                .Select(m => (memory: m, score: Score(m, now, cueSet)))
                .OrderByDescending(x => x.score)
                .ThenByDescending(x => x.memory.Time.TotalMinutes)
                .ThenByDescending(x => x.memory.Id, StringComparer.Ordinal)
                .Take(Math.Max(0, count))
                .Select(x => x.memory)
                .ToList();
        }

        public bool HasMemoryWithTag(string residentId, string tag, GameTime since) =>
            ForResident(residentId).Any(m => m.Time.TotalMinutes >= since.TotalMinutes && m.Tags.Contains(tag));

        /// <summary>Restores a saved memory exactly. Used only by the save loader.</summary>
        internal void Restore(MemoryRecord record, long nextNumber)
        {
            Insert(record);
            NextNumber = Math.Max(NextNumber, nextNumber);
        }

        internal static int ClampImportance(int importance) =>
            Math.Max(MemoryRecord.MinImportance, Math.Min(MemoryRecord.MaxImportance, importance));

        private static double Score(MemoryRecord m, GameTime now, HashSet<string> cues)
        {
            var ageDays = Math.Max(0, now.TotalMinutes - m.Time.TotalMinutes) / (double)GameTime.MinutesPerDay;
            var recency = 4.0 * Math.Exp(-ageDays / 3.0);
            var matches = m.Tags.Count(cues.Contains) + m.Participants.Skip(1).Count(cues.Contains);
            return m.Importance + recency + (2.0 * matches);
        }

        private void Insert(MemoryRecord record)
        {
            records.Add(record);
            var mine = ForResident(record.ResidentId).ToList();
            if (mine.Count > MaxPerResident)
            {
                var forget = mine.OrderBy(m => m.Importance).ThenBy(m => m.Time.TotalMinutes).ThenBy(m => m.Id, StringComparer.Ordinal).First();
                records.Remove(forget);
            }
        }
    }
}
