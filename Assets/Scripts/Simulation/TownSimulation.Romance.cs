using System;
using System.Linq;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation
{
    /// <summary>
    /// Romance between compatible adults: feelings grow from warm conversations, and the game alone decides when a
    /// pair starts dating, commits, breaks up or gets back together. It can be switched off in settings.
    /// </summary>
    public sealed partial class TownSimulation
    {
        /// <summary>Whether new romances can start and feelings change. Existing couples are kept either way.</summary>
        public bool RomanceEnabled { get; set; } = true;

        /// <summary>The village's history of couples, deaths and gatherings.</summary>
        public TownHistory History { get; } = new TownHistory();

        /// <summary>Feelings fade a little each day two people with feelings don't spend time together.</summary>
        public const double RomanceFadePerDay = 1.5;

        public const int DaysBeforeFeelingsFade = 3;

        /// <summary>Romance gained for simply having a pleasant chat, on top of warm lines.</summary>
        public const double RomancePerChat = 1;

        /// <summary>Romance gained once a day when a couple share a hobby.</summary>
        public const double RomancePerSharedHobby = 2;

        private void ApplyRomance(ConversationSession session, Resident a, Resident b, Relationship ab, Relationship ba)
        {
            if (!RomanceEnabled || session.Lines.Count == 0)
            {
                return;
            }

            var disagreements = session.Lines.Count(l => l.Tone == ConversationTone.Disagree);
            foreach (var (me, other, view) in new[] { (a, b, ab), (b, a, ba) })
            {
                var change = -disagreements * RomanceRules.RomanceLostPerDisagreement;
                var partner = Relationships.PartnerOf(me.Id);
                if (RomanceRules.CanFallFor(me, other, view) && (partner == null || partner == other.Id))
                {
                    // Someone in a couple has eyes only for their partner.
                    var warmth = session.Lines.Count(l => l.SpeakerId == other.Id && (l.Tone == ConversationTone.Warm || l.Tone == ConversationTone.Playful));
                    var gain = (warmth * RomanceRules.RomancePerWarmLine) + (disagreements == 0 ? RomancePerChat : 0);
                    change += Math.Min(gain, RomanceRules.FeelingsCeiling(RomanceRules.Spark(me, other)) - view.Romance);
                }

                var before = view.Romance;
                view.Change(romance: change);
                if (before < RomanceRules.SoftSpot && view.Romance >= RomanceRules.SoftSpot && Relationships.PartnerOf(me.Id) == null)
                {
                    Memories.Add(me.Id, Time, MemoryType.Event, 6, $"I think I've got a soft spot for {other.FirstName}.", new[] { other.Id }, new[] { other.Id, "romance", "feelings" });
                }
            }

            UpdateCouple(a, b);
        }

        /// <summary>Moves a pair between single, dating and committed according to how both of them feel.</summary>
        internal void UpdateCouple(Resident a, Resident b)
        {
            var ab = Relationships.Get(a.Id, b.Id);
            var ba = Relationships.Get(b.Id, a.Id);
            var couple = Relationships.PartnershipOf(a.Id);
            if (couple != null && couple.Includes(b.Id))
            {
                if (ShouldBreakUp(ab) || ShouldBreakUp(ba))
                {
                    BreakUp(couple, a, b, ab.Romance <= ba.Romance ? a : b);
                }
                else if (couple.Stage == RomanceStage.Dating
                    && Time.TotalMinutes - couple.StageSince.TotalMinutes >= RomanceRules.DaysBeforeCommitment * GameTime.MinutesPerDay
                    && Feels(ab, RomanceRules.CommittedAt, RomanceRules.CommittedFriendship) && Feels(ba, RomanceRules.CommittedAt, RomanceRules.CommittedFriendship))
                {
                    couple.Stage = RomanceStage.Committed;
                    couple.StageSince = Time;
                    CoupleMoment(a, b, TownEventKind.Committed, 9, "{0} and I have decided we're in this for good.", $"{a.FullName} and {b.FullName} are now a committed couple.", "commitment");
                }

                return;
            }

            if (!RomanceEnabled || couple != null || Relationships.PartnershipOf(b.Id) != null || !RomanceRules.Compatible(a, b)
                || ab.Dislike >= RomanceRules.BreakUpDislike || ba.Dislike >= RomanceRules.BreakUpDislike
                || !Feels(ab, RomanceRules.DatingAt, RomanceRules.DatingFriendship) || !Feels(ba, RomanceRules.DatingAt, RomanceRules.DatingFriendship))
            {
                return;
            }

            var brokeUp = Math.Max(ab.BrokeUpMinute, ba.BrokeUpMinute);
            if (brokeUp != Relationship.Never && Time.TotalMinutes - brokeUp < RomanceRules.DaysBeforeReconciling * GameTime.MinutesPerDay)
            {
                return;
            }

            Relationships.StartPartnership(a.Id, b.Id, Time);
            if (brokeUp != Relationship.Never)
            {
                CoupleMoment(a, b, TownEventKind.Reconciled, 8, "{0} and I patched things up and we're back together.", $"{a.FullName} and {b.FullName} got back together.", "reconciled");
            }
            else
            {
                CoupleMoment(a, b, TownEventKind.Couple, 8, "{0} and I have started seeing each other.", $"{a.FullName} and {b.FullName} started dating.", "dating");
            }
        }

        /// <summary>A row between partners: feelings take a knock and a little resentment lingers.</summary>
        private void Quarrel(Resident a, Resident b)
        {
            foreach (var (me, other) in new[] { (a, b), (b, a) })
            {
                Relationships.Get(me.Id, other.Id).Change(friendship: -3, dislike: RomanceRules.QuarrelDislike, romance: -RomanceRules.QuarrelRomanceLoss);
                Memories.Add(me.Id, Time, MemoryType.Event, 6, $"{other.FirstName} and I had a row. I hope we can sort it out.", new[] { other.Id }, new[] { other.Id, "romance", "quarrel" });
                me.AddLog(Time, $"Had a row with {other.FirstName}");
            }
        }

        private static bool Feels(Relationship view, double romance, double friendship) => view.Romance >= romance && view.Friendship >= friendship;

        private static bool ShouldBreakUp(Relationship view) => view.Romance < RomanceRules.BreakUpRomance || view.Dislike >= RomanceRules.BreakUpDislike;

        private void BreakUp(Partnership couple, Resident a, Resident b, Resident endedBy)
        {
            Relationships.EndPartnership(couple);
            Relationships.Get(a.Id, b.Id).BrokeUpMinute = Time.TotalMinutes;
            Relationships.Get(b.Id, a.Id).BrokeUpMinute = Time.TotalMinutes;
            var other = endedBy == a ? b : a;
            Memories.Add(endedBy.Id, Time, MemoryType.Event, 8, $"I ended things with {other.FirstName}. It wasn't working any more.", new[] { other.Id }, new[] { other.Id, "romance", "breakup" });
            Memories.Add(other.Id, Time, MemoryType.Event, 9, $"{endedBy.FirstName} broke up with me.", new[] { endedBy.Id }, new[] { endedBy.Id, "romance", "breakup" });
            endedBy.AddLog(Time, $"Broke up with {other.FirstName}");
            other.AddLog(Time, $"{endedBy.FirstName} broke up with them");
            History.Add(Time, TownEventKind.BreakUp, $"{a.FullName} and {b.FullName} broke up.", new[] { a.Id, b.Id });
        }

        private void CoupleMoment(Resident a, Resident b, TownEventKind kind, int importance, string memory, string history, string tag)
        {
            foreach (var (me, other) in new[] { (a, b), (b, a) })
            {
                Memories.Add(me.Id, Time, MemoryType.Event, importance, string.Format(memory, other.FirstName), new[] { other.Id }, new[] { other.Id, "romance", tag });
                me.AddLog(Time, history);
            }

            History.Add(Time, kind, history, new[] { a.Id, b.Id });
        }

        /// <summary>
        /// At midnight: grudges soften, feelings fade between people who haven't seen each other lately, couples
        /// sometimes have a row, and every couple is re-checked.
        /// </summary>
        private void StepRomanceDaily()
        {
            if (Time.MinuteOfDay != 0)
            {
                return;
            }

            foreach (var view in Relationships.All.Where(v => v.Dislike > 0))
            {
                view.Change(dislike: -RomanceRules.DislikeFadePerDay);
            }

            if (!RomanceEnabled)
            {
                return;
            }

            var fadeBefore = Time.TotalMinutes - DaysBeforeFeelingsFade * GameTime.MinutesPerDay;
            foreach (var view in Relationships.All.Where(v => v.Romance > 0 && v.LastInteractionMinute < fadeBefore))
            {
                view.Change(romance: -RomanceFadePerDay);
            }

            foreach (var couple in Relationships.Partnerships.ToList())
            {
                var a = GetResident(couple.FirstId);
                var b = GetResident(couple.SecondId);
                var spark = (RomanceRules.Spark(a, b) + RomanceRules.Spark(b, a)) / 2;
                if (Chance.Roll(couple.Id, Time.Day, 21) < RomanceRules.QuarrelChance(spark))
                {
                    Quarrel(a, b);
                }

                UpdateCouple(a, b);
            }
        }

        /// <summary>A partner's death ends the partnership; the survivor remembers them above anything else.</summary>
        private void Widow(Resident deceased)
        {
            var couple = Relationships.PartnershipOf(deceased.Id);
            if (couple == null)
            {
                return;
            }

            Relationships.EndPartnership(couple);
            var survivor = GetResident(couple.OtherThan(deceased.Id));
            if (survivor != null)
            {
                Memories.Add(survivor.Id, Time, MemoryType.Event, 10, $"I lost {deceased.FirstName}, my partner. The house feels so empty.", new[] { deceased.Id }, new[] { deceased.Id, "romance", "loss", "death" });
            }
        }
    }
}
