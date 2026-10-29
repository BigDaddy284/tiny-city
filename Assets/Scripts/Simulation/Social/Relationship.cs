using System;

namespace TinyCity.Simulation.Social
{
    /// <summary>
    /// How one resident feels about another. Directional: Mabel's view of Arthur is separate from Arthur's view of
    /// Mabel. Values run from 0 to 100 and only change through <see cref="SocialRules"/>.
    /// </summary>
    public sealed class Relationship
    {
        public const double Min = 0;
        public const double Max = 100;
        public const long Never = -1;

        internal Relationship(string ownerId, string otherId)
        {
            OwnerId = ownerId;
            OtherId = otherId;
        }

        public string OwnerId { get; }

        public string OtherId { get; }

        /// <summary>Stable id for saves and memories, e.g. "rel-res-0001-res-0002".</summary>
        public string Id => $"rel-{OwnerId}-{OtherId}";

        public double Friendship { get; private set; } = 10;

        public double Trust { get; private set; } = 20;

        public double Respect { get; private set; } = 30;

        public double Dislike { get; private set; }

        /// <summary>Romantic feelings, 0–100. Only grows between compatible adults with chemistry; see <see cref="RomanceRules"/>.</summary>
        public double Romance { get; private set; }

        /// <summary>When these two last broke up, or <see cref="Never"/>.</summary>
        public long BrokeUpMinute { get; internal set; } = Never;

        /// <summary>Finished conversations together.</summary>
        public int Interactions { get; internal set; }

        public long LastInteractionMinute { get; internal set; } = Never;

        public bool HaveMet => Interactions > 0;

        /// <summary>A plain description for the inspector and prompts.</summary>
        public string Describe()
        {
            if (!HaveMet)
            {
                return "has never met";
            }

            if (Dislike >= 40 && Dislike > Friendship)
            {
                return "doesn't get on with";
            }

            return Friendship >= 60 ? "close friend"
                : Friendship >= 35 ? "friend"
                : Friendship >= 15 ? "acquaintance"
                : "barely knows";
        }

        internal void Change(double friendship = 0, double trust = 0, double respect = 0, double dislike = 0, double romance = 0)
        {
            Romance = Clamp(Romance + romance);
            Friendship = Clamp(Friendship + friendship);
            Trust = Clamp(Trust + trust);
            Respect = Clamp(Respect + respect);
            Dislike = Clamp(Dislike + dislike);
        }

        internal void Set(double friendship, double trust, double respect, double dislike, double romance = 0)
        {
            Romance = Clamp(romance);
            Friendship = Clamp(friendship);
            Trust = Clamp(trust);
            Respect = Clamp(respect);
            Dislike = Clamp(dislike);
        }

        private static double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Max(Min, Math.Min(Max, v));
    }
}
