using System;
using System.Collections.Generic;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Simulation
{
    /// <summary>
    /// Tiny City 2: perceive and react, as the generative agents do. Each minute a resident who is up and about
    /// notices the people near them. Someone they haven't been asked about lately becomes an observation memory and a
    /// "react?" question for the AI: go over and talk, or carry on. This replaces v1's chance meetings and hobby
    /// invitations, which the game decided by rules.
    /// </summary>
    public sealed partial class TownSimulation
    {
        /// <summary>When each (noticer, noticed) pair was last offered a reaction. Saved while it still matters, so a loaded town notices people just as it would have.</summary>
        private readonly Dictionary<string, long> lastNoticed = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>The pairs (noticer + ">" + noticed) still within <see cref="PerceptionRules.ReactCooldownMinutes"/> of being noticed, in id order, for saving.</summary>
        internal IEnumerable<KeyValuePair<string, long>> RecentlyNoticed()
        {
            var list = new List<KeyValuePair<string, long>>();
            foreach (var pair in lastNoticed)
            {
                if (Time.TotalMinutes - pair.Value < PerceptionRules.ReactCooldownMinutes)
                {
                    list.Add(pair);
                }
            }

            list.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
            return list;
        }

        /// <summary>Puts back a saved "noticed" time, if both people are in this town and it isn't in the future.</summary>
        internal bool RestoreNoticed(string pair, long minute)
        {
            var ids = pair?.Split('>');
            if (ids == null || ids.Length != 2 || GetResident(ids[0]) == null || GetResident(ids[1]) == null || minute > Time.TotalMinutes || minute < 0)
            {
                return false;
            }

            lastNoticed[pair] = minute;
            return true;
        }

        private long nextReactionId = 1;

        /// <summary>Someone who can be walked up to and talked to: alive, not already chatting or asleep.</summary>
        public static bool FreeToTalk(Resident r) =>
            r != null && r.IsAlive && r.State != ResidentState.Talking && r.ActivityId != ActivityCatalog.Sleep.Id;

        /// <summary>Up and about and free to notice someone and change what they're doing.</summary>
        public static bool CanReact(Resident r) =>
            FreeToTalk(r) && r.State != ResidentState.Deciding && r.TargetResidentId == null && r.Reaction == null;

        /// <summary>A few words on what someone is doing, as another resident would see it.</summary>
        public string DoingText(Resident r)
        {
            if (r.State == ResidentState.Talking)
            {
                var session = GetConversation(r.ConversationId);
                var other = session == null ? null : GetResident(session.OtherThan(r.Id));
                return other == null ? "chatting" : $"chatting with {other.FirstName}";
            }

            if (r.State == ResidentState.Travelling)
            {
                return "walking by";
            }

            if (r.State == ResidentState.Performing && ActivityCatalog.TryGet(r.ActivityId, out var activity))
            {
                return World.TryGetObject(r.TargetObjectId, out var at)
                    ? $"{activity.Name.ToLowerInvariant()} at the {at.Name.ToLowerInvariant()}"
                    : activity.Name.ToLowerInvariant();
            }

            return "standing about";
        }

        private void Perceive()
        {
            foreach (var r in residents)
            {
                if (r.Reaction != null && !ReactionStillFits(r))
                {
                    r.Reaction = null;
                }

                if (!CanReact(r))
                {
                    continue;
                }

                foreach (var other in residents)
                {
                    if (other == r || !FreeToTalk(other) || !Near(r.Location, other.Location, PerceptionRules.SightDistance)
                        || !SocialRules.CooldownOver(Relationships, r.Id, other.Id, Time))
                    {
                        continue;
                    }

                    var key = r.Id + ">" + other.Id;
                    if (lastNoticed.TryGetValue(key, out var when) && Time.TotalMinutes - when < PerceptionRules.ReactCooldownMinutes)
                    {
                        continue;
                    }

                    lastNoticed[key] = Time.TotalMinutes;
                    var seen = $"{other.FullName}, {DoingText(other)}";
                    var map = World.GetMap(r.Location.MapId);
                    var memory = Memories.Add(r.Id, Time, MemoryType.Event, PerceptionRules.ObservationImportance, $"Saw {seen} in {map.Name}", new[] { other.Id }, new[] { other.Id, "seen", r.Location.MapId });
                    if (memory != null)
                    {
                        Memories.RateImportance(memory.Id, PerceptionRules.ObservationImportance);
                    }

                    r.Reaction = new PendingReaction(nextReactionId++, other.Id, Time.TotalMinutes, seen);
                    break;
                }
            }
        }

        private bool ReactionStillFits(Resident r)
        {
            var other = GetResident(r.Reaction.OtherId);
            return Time.TotalMinutes - r.Reaction.AskedMinute <= PerceptionRules.ReactWaitMinutes
                && FreeToTalk(r) && r.State != ResidentState.Deciding && r.TargetResidentId == null
                && FreeToTalk(other) && Near(r.Location, other.Location, PerceptionRules.SightDistance);
        }

        /// <summary>
        /// The AI's answer to "react?": if they go over, they stop what they're doing and walk over for a chat;
        /// otherwise they carry on. Only the current question for this resident is accepted.
        /// </summary>
        public DecisionOutcome ApplyReaction(string residentId, long reactionId, bool goOver, string reason)
        {
            var r = GetResident(residentId);
            if (r == null)
            {
                return DecisionOutcome.UnknownResident;
            }

            if (!r.IsAlive)
            {
                return DecisionOutcome.NotAlive;
            }

            if (r.Reaction == null || r.Reaction.Id != reactionId || !ReactionStillFits(r))
            {
                return DecisionOutcome.Stale;
            }

            var other = GetResident(r.Reaction.OtherId);
            r.Reaction = null;
            if (!goOver)
            {
                return DecisionOutcome.Applied;
            }

            var why = TextRules.Clean(reason, SocialRules.MaxTopicLength * 2);
            r.ClearPlan();
            r.ResetActivity();
            StartChoice(r, new PlanChoice(other, why.Length > 0 ? why : $"Saw {other.FirstName} nearby"), "(noticed them)");
            r.GoalSource = DecisionSource.Ai;
            return DecisionOutcome.Applied;
        }

        /// <summary>Gives up on a reaction question (the AI couldn't answer); they simply carry on.</summary>
        public bool DropReaction(string residentId, long reactionId)
        {
            var r = GetResident(residentId);
            if (r?.Reaction == null || r.Reaction.Id != reactionId)
            {
                return false;
            }

            r.Reaction = null;
            return true;
        }
    }
}
