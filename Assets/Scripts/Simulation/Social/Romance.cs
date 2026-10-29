using System;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Social
{
    public enum RomanceStage
    {
        Dating,

        /// <summary>A committed couple.</summary>
        Committed,
    }

    /// <summary>Two residents who are together. Ids are stored in a fixed order so the same couple always has the same id.</summary>
    public sealed class Partnership
    {
        internal Partnership(string aId, string bId, RomanceStage stage, GameTime since)
        {
            var ordered = string.CompareOrdinal(aId, bId) <= 0;
            FirstId = ordered ? aId : bId;
            SecondId = ordered ? bId : aId;
            Stage = stage;
            Since = since;
            StageSince = since;
        }

        public string FirstId { get; }

        public string SecondId { get; }

        public string Id => $"couple-{FirstId}-{SecondId}";

        public RomanceStage Stage { get; internal set; }

        /// <summary>When they got together.</summary>
        public GameTime Since { get; internal set; }

        /// <summary>When they reached their current stage.</summary>
        public GameTime StageSince { get; internal set; }

        public bool Includes(string residentId) => FirstId == residentId || SecondId == residentId;

        public string OtherThan(string residentId) => FirstId == residentId ? SecondId : SecondId == residentId ? FirstId : null;
    }

    /// <summary>
    /// Romance is optional and never guaranteed: it only grows between compatible adults who are open to it and have
    /// chemistry (decided once per pair by <see cref="Chance"/>), and only the game moves a couple between stages.
    /// </summary>
    public static class RomanceRules
    {
        public const int MaxAgeGap = 12;

        /// <summary>The chance that one resident feels a spark for a given compatible neighbour.</summary>
        public const double ChemistryChance = 0.4;

        /// <summary>Friendship needed before feelings can grow.</summary>
        public const double FriendshipForFeelings = 30;

        public const double RomancePerWarmLine = 2.5;

        public const double RomanceLostPerDisagreement = 6;

        /// <summary>Feelings that show: "has a soft spot for".</summary>
        public const double SoftSpot = 30;

        public const double DatingAt = 50;

        public const double DatingFriendship = 45;

        public const double CommittedAt = 80;

        public const double CommittedFriendship = 65;

        public const int DaysBeforeCommitment = 14;

        /// <summary>A couple splits when either's feelings fall below this, or dislike rises above <see cref="BreakUpDislike"/>.</summary>
        public const double BreakUpRomance = 25;

        public const double BreakUpDislike = 45;

        /// <summary>After breaking up, a pair can't get back together for this long.</summary>
        public const int DaysBeforeReconciling = 7;

        /// <summary>Both adults, alive, open to romance and close enough in age.</summary>
        public static bool Compatible(Resident a, Resident b) =>
            a != null && b != null && a != b && a.IsAlive && b.IsAlive && a.OpenToRomance && b.OpenToRomance
            && !a.IsMinor && !b.IsMinor && !a.IsFamilyOf(b) && Math.Abs(a.AgeYears - b.AgeYears) <= MaxAgeGap;

        /// <summary>Whether <paramref name="from"/> could ever fall for <paramref name="to"/>. Fixed for the pair, and can be one-sided.</summary>
        public static bool HasChemistry(Resident from, Resident to) => Spark(from, to) > 0;

        /// <summary>How strongly <paramref name="from"/> is drawn to <paramref name="to"/>: 0 for no chemistry, otherwise up to 1. Fixed for the pair.</summary>
        public static double Spark(Resident from, Resident to)
        {
            var roll = Chance.Roll(from.Id + ">" + to.Id, 0, 20);
            return roll < ChemistryChance ? 1 - (roll / ChemistryChance) : 0;
        }

        /// <summary>Feelings can grow no further than this: a faint spark never becomes a grand passion.</summary>
        public static double FeelingsCeiling(double spark) => spark <= 0 ? 0 : 55 + (45 * spark);

        /// <summary>The chance each day that a couple has a row: more likely when the spark is weaker.</summary>
        public static double QuarrelChance(double spark) => 0.015 + (0.05 * (1 - spark));

        public const double QuarrelRomanceLoss = 20;

        public const double QuarrelDislike = 6;

        /// <summary>Old grudges soften a little every day.</summary>
        public const double DislikeFadePerDay = 0.2;

        public static bool CanFallFor(Resident from, Resident to, Relationship view) =>
            Compatible(from, to) && HasChemistry(from, to) && view.Friendship >= FriendshipForFeelings && view.Romance < FeelingsCeiling(Spark(from, to));
    }
}
