using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.History
{
    public enum TownEventKind
    {
        Death,
        Couple,
        Committed,
        BreakUp,
        Reconciled,
        Gathering,

        // Added in Phase 5 for the journal. Saved by name, so the order above must not change.
        Birthday,
        LifeStage,
        Illness,
        SeriousIllness,
        Recovered,
        Met,
        Friends,

        // Added with newcomers (save version 7).
        MovedIn,
    }

    /// <summary>Something that happened in the village and belongs to its history, separate from any one resident's memories.</summary>
    public sealed class TownEvent
    {
        internal TownEvent(string id, GameTime time, TownEventKind kind, string summary, IEnumerable<string> residentIds)
        {
            Id = id;
            Time = time;
            Kind = kind;
            Summary = summary;
            ResidentIds = (residentIds ?? Enumerable.Empty<string>()).Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();
        }

        /// <summary>Stable id such as "evt-000001", never reused.</summary>
        public string Id { get; }

        public GameTime Time { get; }

        public TownEventKind Kind { get; }

        public string Summary { get; }

        public IReadOnlyList<string> ResidentIds { get; }
    }

    /// <summary>
    /// The village's history, which the journal shows: births and deaths, birthdays, illness, friendships, couples and
    /// gatherings. Capped, oldest dropped first.
    /// </summary>
    public sealed class TownHistory
    {
        public const int MaxEvents = 3000;

        public const int MaxSummaryLength = 200;

        private readonly List<TownEvent> events = new List<TownEvent>();

        public IReadOnlyList<TownEvent> Events => events;

        /// <summary>The number the next event id will use.</summary>
        public long NextNumber { get; private set; } = 1;

        public TownEvent Add(GameTime time, TownEventKind kind, string summary, IEnumerable<string> residentIds)
        {
            var entry = new TownEvent($"evt-{NextNumber:000000}", time, kind, Memory.TextRules.Clean(summary, MaxSummaryLength), residentIds);
            NextNumber++;
            Append(entry);
            return entry;
        }

        public IEnumerable<TownEvent> About(string residentId) => events.Where(e => e.ResidentIds.Contains(residentId));

        /// <summary>Events whose id number is at least <paramref name="number"/>, oldest first: what is new since then.</summary>
        public IEnumerable<TownEvent> Since(long number) => events.Where(e => NumberOf(e.Id) >= number);

        internal static long NumberOf(string id) => long.TryParse(id.Substring(id.IndexOf('-') + 1), out var n) ? n : 0;

        /// <summary>Puts back a saved event. Ids must be new and the counter is kept ahead of them.</summary>
        internal void Restore(TownEvent entry)
        {
            if (events.Any(e => e.Id == entry.Id))
            {
                throw new ArgumentException($"Duplicate town event id {entry.Id}.");
            }

            Append(entry);
            var n = NumberOf(entry.Id);
            if (n >= NextNumber)
            {
                NextNumber = n + 1;
            }
        }

        internal void RestoreNextNumber(long next) => NextNumber = Math.Max(NextNumber, next);

        private void Append(TownEvent entry)
        {
            events.Add(entry);
            if (events.Count > MaxEvents)
            {
                events.RemoveAt(0);
            }
        }
    }
}
