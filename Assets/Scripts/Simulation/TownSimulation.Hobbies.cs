using System;
using System.Linq;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Social;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation
{
    /// <summary>Hobbies shared with others: invitations, enjoying each other's company, and remembering it.</summary>
    public sealed partial class TownSimulation
    {
        /// <summary>Extra social need restored per minute while sharing a hobby with someone.</summary>
        public const double SharedHobbySocialPerMinute = 1.0;

        /// <summary>Friendship gained the first time each day two residents share a hobby.</summary>
        public const double SharedHobbyFriendship = 3;

        /// <summary>While doing a shared hobby, company at the same spot is enjoyed and, once a day per pair, remembered.</summary>
        private void StepSharedHobby(Resident r, ActivityDefinition activity)
        {
            if (activity?.Shared != true || r.State != ResidentState.Performing)
            {
                return;
            }

            foreach (var other in residents)
            {
                if (other == r || !other.IsAlive || other.State != ResidentState.Performing || other.ActivityId != activity.Id || other.TargetObjectId != r.TargetObjectId)
                {
                    continue;
                }

                r.Needs[NeedType.Social] += SharedHobbySocialPerMinute;
                var tag = $"together-{activity.Id}-{other.Id}";
                if (Memories.HasMemoryWithTag(r.Id, tag, GameTime.FromDayAndTime(Time.Day, 0, 0)))
                {
                    continue;
                }

                World.TryGetObject(r.TargetObjectId, out var place);
                Memories.Add(r.Id, Time, MemoryType.Event, 4, $"I spent time {activity.Verb} with {other.FirstName} at the {place?.Name.ToLowerInvariant() ?? "village"}.", new[] { other.Id }, new[] { tag, other.Id, activity.Hobby, "hobby", "social" });
                var view = Relationships.Get(r.Id, other.Id);
                view.Change(friendship: SharedHobbyFriendship, trust: 1);
                view.Interactions++;
                view.LastInteractionMinute = Time.TotalMinutes;
            }
        }

        /// <summary>
        /// Someone setting off for a shared hobby invites one free neighbour with the same hobby, the nearest, to join
        /// them there. The game decides who is free; nobody is pulled away from work, sleep, illness or a chat.
        /// </summary>
        private void InviteToHobby(Resident host, ActivityDefinition activity, WorldObject place)
        {
            if (activity?.Shared != true || place == null)
            {
                return;
            }

            var guest = residents
                .Where(o => o != host && o.IsAlive && o.Hobbies.Contains(activity.Hobby) && SocialRules.CanJoinConversation(o, Time)
                    && o.ActivityId != activity.Id && Planner.CanDo(o, activity, place, Time))
                .Select(o => (resident: o, distance: Pathfinder.Distance(o.Location, place.Location)))
                .Where(p => p.distance != Pathfinder.Unreachable && p.distance <= SocialRules.MaxSeekDistance)
                .OrderBy(p => p.distance)
                .ThenBy(p => p.resident.Id, StringComparer.Ordinal)
                .Select(p => p.resident)
                .FirstOrDefault();
            if (guest == null)
            {
                return;
            }

            guest.ResetActivity();
            guest.ActivityId = activity.Id;
            guest.TargetObjectId = place.Id;
            guest.State = ResidentState.Travelling;
            guest.Goal = $"{host.FirstName} asked them to join in {activity.Verb}";
            guest.AddLog(Time, $"Accepted {host.FirstName}'s invitation to {activity.Name.ToLowerInvariant()} at {place.Name}");
            host.AddLog(Time, $"Invited {guest.FirstName} to join them");
            if (guest.Location == place.Location)
            {
                BeginPerforming(guest, activity);
            }
        }
    }
}
