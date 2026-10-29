using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Activities
{
    public enum ActivityKind
    {
        /// <summary>Restores a need; chosen when that need gets low.</summary>
        Care,
        Sleep,
        Work,
        Leisure,

        /// <summary>Talking with another resident. Uses no object; see <see cref="ActivityCatalog.Chat"/>.</summary>
        Social,

        /// <summary>A job to get done, such as buying groceries. Chosen when needed, not to restore a need.</summary>
        Errand,
    }

    public sealed class ActivityDefinition
    {
        private readonly double[] gainPerMinute;
        private readonly double[] decayMultiplier;

        public ActivityDefinition(
            string id,
            string name,
            string verb,
            ActivityKind kind,
            NeedType? primaryNeed,
            IEnumerable<ObjectType> usableObjects,
            int durationMinutes,
            IReadOnlyDictionary<NeedType, double> gainPerMinute,
            IReadOnlyDictionary<NeedType, double> decayMultiplier = null,
            string itemId = null,
            bool usesGroceries = false,
            string hobby = null,
            bool shared = false,
            bool needsOpenBusiness = false)
        {
            Hobby = hobby;
            Shared = shared;
            NeedsOpenBusiness = needsOpenBusiness || itemId != null;
            ItemId = itemId;
            UsesGroceries = usesGroceries;
            Id = id;
            Name = name;
            Verb = verb;
            Kind = kind;
            PrimaryNeed = primaryNeed;
            UsableObjects = usableObjects.ToArray();
            DurationMinutes = Math.Max(1, durationMinutes);
            this.gainPerMinute = new double[Needs.Count];
            this.decayMultiplier = Enumerable.Repeat(1.0, Needs.Count).ToArray();
            foreach (var pair in gainPerMinute)
            {
                this.gainPerMinute[(int)pair.Key] = pair.Value;
            }

            if (decayMultiplier != null)
            {
                foreach (var pair in decayMultiplier)
                {
                    this.decayMultiplier[(int)pair.Key] = pair.Value;
                }
            }
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>Present-participle phrase for the inspector, e.g. "eating".</summary>
        public string Verb { get; }

        public ActivityKind Kind { get; }

        /// <summary>The need this activity exists to restore, if any. The activity ends early once it is full.</summary>
        public NeedType? PrimaryNeed { get; }

        public IReadOnlyList<ObjectType> UsableObjects { get; }

        public int DurationMinutes { get; }

        /// <summary>What must be bought from the business where it happens, e.g. "coffee"; null if it's free.</summary>
        public string ItemId { get; }

        /// <summary>Uses up one meal's worth of the resident's groceries.</summary>
        public bool UsesGroceries { get; }

        /// <summary>The hobby this activity belongs to, e.g. "chess"; only residents with that hobby do it. Null for anyone.</summary>
        public string Hobby { get; }

        /// <summary>More fun with company: residents doing it together at the same object enjoy each other's company and remember it.</summary>
        public bool Shared { get; }

        /// <summary>Only possible while the business it happens in is open, e.g. seeing the doctor.</summary>
        public bool NeedsOpenBusiness { get; }

        public double GainPerMinute(NeedType need) => gainPerMinute[(int)need];

        public double DecayMultiplier(NeedType need) => decayMultiplier[(int)need];

        public bool CanUse(WorldObject obj) => obj != null && UsableObjects.Contains(obj.Type);
    }
}
