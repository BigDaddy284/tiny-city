using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Activities
{
    public sealed class PlanChoice
    {
        public PlanChoice(ActivityDefinition activity, WorldObject target, string reason)
        {
            Activity = activity;
            Target = target;
            Reason = reason;
        }

        /// <summary>A chat: walk over to <paramref name="partner"/> and talk.</summary>
        public PlanChoice(Resident partner, string reason)
            : this(ActivityCatalog.Chat, null, reason)
        {
            TargetResident = partner;
        }

        /// <summary>The person to chat with, for <see cref="ActivityCatalog.Chat"/>; otherwise null.</summary>
        public Resident TargetResident { get; }

        public ActivityDefinition Activity { get; }

        public WorldObject Target { get; }

        public string Reason { get; }
    }

    /// <summary>One thing a resident could do with free time: an activity at a reachable object, or relaxing where they are.</summary>
    public sealed class FreeTimeOption
    {
        public const string RelaxActionId = "relax";
        public const string NoTargetId = "none";

        /// <summary>The target of something done on open ground wherever the resident chooses (Tiny City 2): the AI names the spot.</summary>
        public const string GroundTargetId = "ground";

        public FreeTimeOption(ActivityDefinition activity, WorldObject target, int distance)
        {
            Activity = activity;
            Target = target;
            Distance = distance;
        }

        /// <summary>Going over to chat with someone.</summary>
        public FreeTimeOption(Resident partner, int distance)
            : this(ActivityCatalog.Chat, null, distance)
        {
            Partner = partner;
        }

        /// <summary>Planting or building on open ground at a spot the AI names (Tiny City 2).</summary>
        public static FreeTimeOption OnGround(ActivityDefinition activity) => new FreeTimeOption(activity, null, 0) { IsGround = true };

        /// <summary>True when the target is a spot on open ground that the answer names, not an object.</summary>
        public bool IsGround { get; private set; }

        /// <summary>Null for relaxing.</summary>
        public ActivityDefinition Activity { get; }

        /// <summary>The object to use; null for relaxing and chatting.</summary>
        public WorldObject Target { get; }

        /// <summary>The person to chat with, or null.</summary>
        public Resident Partner { get; }

        /// <summary>Steps to walk there.</summary>
        public int Distance { get; }

        public string ActionId => Activity?.Id ?? RelaxActionId;

        public string TargetId => IsGround ? GroundTargetId : Target?.Id ?? Partner?.Id ?? NoTargetId;
    }

    /// <summary>
    /// What a resident could do right now. It decides nothing: the AI makes every choice, and this lists the
    /// activities the world allows (reachable, with room, allowed by the resident's role and things) for it to pick from.
    /// </summary>
    public sealed class ActivityPlanner
    {
        /// <summary>Each person already using an object makes it count as this many steps further away.</summary>
        public const int CrowdedSteps = 8;

        /// <summary>At most this many objects are offered for each activity, nearest first.</summary>
        private const int MaxTargetsPerActivity = 2;

        private static readonly NeedType[] UrgentNeeds = { NeedType.Thirst, NeedType.Hunger };

        private readonly TownWorld world;
        private readonly Pathfinder pathfinder;
        private readonly IReadOnlyList<Resident> residents;
        private readonly Func<GameTime> clock;

        public ActivityPlanner(TownWorld world, Pathfinder pathfinder, IReadOnlyList<Resident> residents = null, Func<GameTime> clock = null)
        {
            this.world = world;
            this.pathfinder = pathfinder;
            this.residents = residents ?? new Resident[0];
            this.clock = clock ?? (() => new GameTime(0));
        }

        /// <summary>The food supply, crops, stalls and the rules on who may do what.</summary>
        public FoodSupply Food { get; set; }

        /// <summary>
        /// Whether a resident may use an object now: an object is full once as many other residents are using or
        /// heading for it as it has room for. Nothing belongs to anyone.
        /// </summary>
        public bool IsAvailableTo(Resident resident, WorldObject obj) => obj != null && UsersOf(resident, obj) < obj.Capacity;

        /// <summary>How many other residents are using or heading for an object.</summary>
        public int UsersOf(Resident resident, WorldObject obj)
        {
            var users = 0;
            foreach (var other in residents)
            {
                if (other != resident && other.IsAlive && other.TargetObjectId == obj.Id
                    && (other.State == ResidentState.Travelling || other.State == ResidentState.Performing))
                {
                    users++;
                }
            }

            return users;
        }

        /// <summary>
        /// Steps to an object as they are weighed when choosing which to offer: somewhere already in use counts as
        /// further away, so people spread out over the benches instead of all crowding onto the nearest one.
        /// </summary>
        private int WeighedDistance(Resident resident, WorldObject obj)
        {
            var d = pathfinder.Distance(resident.Location, obj.Location);
            return d == Pathfinder.Unreachable ? d : d + (CrowdedSteps * UsersOf(resident, obj));
        }

        /// <summary>The lowest urgent need below <see cref="NeedRules.Critical"/>, or null. Ties go to the earlier need in <see cref="UrgentNeeds"/>.</summary>
        public static NeedType? MostUrgentNeed(Resident resident)
        {
            NeedType? result = null;
            foreach (var need in UrgentNeeds)
            {
                if (resident.Needs[need] < NeedRules.Critical && (result == null || resident.Needs[need] < resident.Needs[result.Value]))
                {
                    result = need;
                }
            }

            return result;
        }

        /// <summary>Whether the resident can use this object for this activity now.</summary>
        public bool CanDo(Resident resident, ActivityDefinition activity, WorldObject obj, GameTime time) =>
            activity.CanUse(obj) && IsAvailableTo(resident, obj) && FoodSupply.Allows(Food, resident, activity, obj, time.Day);

        /// <summary>
        /// Every activity the resident could start right now, whatever their needs, in a fixed order: each at up to
        /// <see cref="MaxTargetsPerActivity"/> reachable objects, plus the ones done on open ground, then relaxing.
        /// AI decisions must pick one of these (chats are added by the simulation).
        /// </summary>
        public IReadOnlyList<FreeTimeOption> FreeTimeOptions(Resident resident)
        {
            var time = clock();
            var options = new List<FreeTimeOption>();
            foreach (var activity in ActivityCatalog.All)
            {
                if (activity == ActivityCatalog.Chat)
                {
                    continue;
                }

                options.AddRange(world.Objects
                    .Where(obj => CanDo(resident, activity, obj, time))
                    .Select(obj => (option: new FreeTimeOption(activity, obj, pathfinder.Distance(resident.Location, obj.Location)), weighed: WeighedDistance(resident, obj)))
                    .Where(o => o.option.Distance != Pathfinder.Unreachable)
                    .OrderBy(o => o.weighed)
                    .ThenBy(o => o.option.Target.Id, StringComparer.Ordinal)
                    .Take(MaxTargetsPerActivity)
                    .Select(o => o.option));
                if (ActivityCatalog.IsDoneOnGround(activity) && world.TryGetMap(resident.Location.MapId, out var here) && here.IsExterior
                    && FoodSupply.Allows(Food, resident, activity, null, time.Day))
                {
                    options.Add(FreeTimeOption.OnGround(activity));
                }
            }

            options.Add(new FreeTimeOption(null, null, 0));
            return options;
        }

        public bool IsComplete(Resident resident, ActivityDefinition activity, GameTime time)
        {
            return resident.MinutesRemaining <= 0
                || (activity.PrimaryNeed != null && resident.Needs[activity.PrimaryNeed.Value] >= Needs.Max - 0.5);
        }
    }
}
