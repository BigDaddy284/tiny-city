using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Residents;

namespace TinyTown.Simulation.Health
{
    /// <summary>One kind of illness and what it does. Ids are stable because saves store them.</summary>
    public sealed class Illness
    {
        public Illness(string id, string name, bool serious, bool contagious, bool canWork, bool keepsToThemselves, int recoveryMinutes, double healthDrainPerHour, IReadOnlyDictionary<NeedType, double> decayMultiplier)
        {
            Id = id;
            Name = name;
            Serious = serious;
            Contagious = contagious;
            CanWork = canWork;
            KeepsToThemselves = keepsToThemselves;
            RecoveryMinutes = recoveryMinutes;
            HealthDrainPerHour = healthDrainPerHour;
            this.decayMultiplier = decayMultiplier ?? new Dictionary<NeedType, double>();
        }

        private readonly IReadOnlyDictionary<NeedType, double> decayMultiplier;

        public string Id { get; }

        /// <summary>Lower-case name for sentences, e.g. "a cold".</summary>
        public string Name { get; }

        /// <summary>Needs the doctor: recovery doesn't start until the clinic has treated it.</summary>
        public bool Serious { get; }

        /// <summary>Chatting with someone who has it can pass it on.</summary>
        public bool Contagious { get; }

        public bool CanWork { get; }

        /// <summary>Too unwell to chat or be approached.</summary>
        public bool KeepsToThemselves { get; }

        /// <summary>Minutes of ordinary time to get better; resting counts double.</summary>
        public int RecoveryMinutes { get; }

        /// <summary>Health lost per hour while it runs its course (or, if serious, until treated).</summary>
        public double HealthDrainPerHour { get; }

        /// <summary>How much faster a need runs down while ill, e.g. 1.5 for energy with flu.</summary>
        public double DecayMultiplier(NeedType need) => decayMultiplier.TryGetValue(need, out var m) ? m : 1.0;
    }

    public static class IllnessCatalog
    {
        public static readonly Illness Cold = new Illness(
            "cold", "a cold", serious: false, contagious: true, canWork: true, keepsToThemselves: false,
            recoveryMinutes: 36 * 60, healthDrainPerHour: 0,
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.2, [NeedType.Fun] = 1.2 });

        public static readonly Illness Flu = new Illness(
            "flu", "flu", serious: false, contagious: true, canWork: false, keepsToThemselves: true,
            recoveryMinutes: 72 * 60, healthDrainPerHour: 0.3,
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.5, [NeedType.Fun] = 1.3, [NeedType.Hunger] = 0.8, [NeedType.Thirst] = 1.2 });

        /// <summary>What flu can turn into when someone is already in poor health. Only the clinic can start the recovery.</summary>
        public static readonly Illness Pneumonia = new Illness(
            "pneumonia", "pneumonia", serious: true, contagious: false, canWork: false, keepsToThemselves: true,
            recoveryMinutes: 96 * 60, healthDrainPerHour: 0.25,
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.8, [NeedType.Fun] = 1.4, [NeedType.Hunger] = 0.7, [NeedType.Thirst] = 1.3 });

        public static readonly IReadOnlyList<Illness> All = new[] { Cold, Flu, Pneumonia };

        public static Illness Get(string id) => id == null ? null : All.FirstOrDefault(i => i.Id == id);
    }
}
