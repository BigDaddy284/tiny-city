using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Memory
{
    /// <summary>The town's searchable history of things residents said, oldest first.</summary>
    public sealed class ConversationHistory
    {
        /// <summary>
        /// Upper bound on stored records so a very long game cannot grow without limit. One resident makes a few
        /// remarks a day, so this keeps years of history; the oldest record is dropped first.
        /// </summary>
        public const int MaxRecords = 5000;

        private readonly List<ConversationRecord> records = new List<ConversationRecord>();

        public IReadOnlyList<ConversationRecord> All => records;

        public long NextNumber { get; private set; } = 1;

        public IEnumerable<ConversationRecord> Involving(string residentId) => records.Where(c => c.Participants.Contains(residentId));

        /// <summary>The most recent line a resident said, or null.</summary>
        public ConversationLine LastLineBy(string residentId)
        {
            for (var i = records.Count - 1; i >= 0; i--)
            {
                var line = records[i].Lines.LastOrDefault(l => l.SpeakerId == residentId);
                if (line != null)
                {
                    return line;
                }
            }

            return null;
        }

        /// <summary>Records a remark, or returns null if the text is empty after cleaning.</summary>
        public ConversationRecord AddRemark(string residentId, GameTime time, string text, string context)
        {
            if (string.IsNullOrWhiteSpace(residentId))
            {
                throw new ArgumentException("A remark needs a speaker.", nameof(residentId));
            }

            var clean = TextRules.Clean(text, ConversationLine.MaxTextLength);
            if (clean.Length == 0)
            {
                return null;
            }

            var record = new ConversationRecord(
                $"conv-{NextNumber:000000}",
                ConversationKind.Remark,
                time,
                new[] { residentId },
                TextRules.Clean(context, 60),
                new[] { new ConversationLine(residentId, time, clean) });
            NextNumber++;
            Insert(record);
            return record;
        }

        /// <summary>Restores a saved record exactly. Used only by the save loader.</summary>
        internal void Restore(ConversationRecord record, long nextNumber)
        {
            Insert(record);
            NextNumber = Math.Max(NextNumber, nextNumber);
        }

        private void Insert(ConversationRecord record)
        {
            records.Add(record);
            if (records.Count > MaxRecords)
            {
                records.RemoveAt(0);
            }
        }
    }
}
