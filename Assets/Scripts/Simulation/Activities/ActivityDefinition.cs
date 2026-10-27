using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Activities
{
    public enum ActivityKind
    {
        /// <summary>Restores a need; chosen when that need gets low.</summary>
        Care,
        Sleep,
        Work,
        Leisure,
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
            IReadOnlyDictionary<NeedType, double> decayMultiplier = null)
        {
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

        public double GainPerMinute(NeedType need) => gainPerMinute[(int)need];

        public double DecayMultiplier(NeedType need) => decayMultiplier[(int)need];

        public bool CanUse(WorldObject obj) => obj != null && UsableObjects.Contains(obj.Type);
    }
}
