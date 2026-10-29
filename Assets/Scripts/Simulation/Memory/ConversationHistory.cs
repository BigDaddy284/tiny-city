using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Memory
{
    /// <summary>The town's searchable history of things residents said, oldest first.</summary>
    public sealed class ConversationHistory
    {
        /// <summary>
        /// Upper bound on stored records so a very long game cannot grow without limit. One resident makes a few
        /// remarks and chats a day (a busy town has well over a hundred chats a day), so this keeps the last week or so;
        /// important moments live on as memories. The oldest record is dropped first.
        /// </summary>
        public const int MaxRecords = 1500;

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

        /// <summary>Takes the next conversation id now, for a conversation that is recorded when it ends.</summary>
        public string ReserveId()
        {
            var id = $"conv-{NextNumber:000000}";
            NextNumber++;
            return id;
        }

        /// <summary>Records a finished conversation under an id from <see cref="ReserveId"/>.</summary>
        internal ConversationRecord AddChat(string id, GameTime started, IReadOnlyList<string> participants, string context, IReadOnlyList<ConversationLine> lines)
        {
            var record = new ConversationRecord(id, ConversationKind.Chat, started, participants.ToList(), TextRules.Clean(context, 60), lines.ToList());
            Insert(record);
            return record;
        }

        /// <summary>Restores a saved record exactly. Used only by the save loader.</summary>
        internal void Restore(ConversationRecord record, long nextNumber)
        {
            Insert(record);
            NextNumber = Math.Max(NextNumber, nextNumber);
        }

        /// <summary>Makes sure ids from a loaded save, including conversations still going on, are never handed out again.</summary>
        internal void RestoreNextNumber(long nextNumber)
        {
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
