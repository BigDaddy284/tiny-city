using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Activities
{
    /// <summary>Why the planner picked something, from most to least pressing.</summary>
    public enum PlanPriority
    {
        UrgentNeed,
        Bedtime,
        Work,
        Nap,
        Comfort,
        Leisure,
    }

    public sealed class PlanChoice
    {
        public PlanChoice(ActivityDefinition activity, WorldObject target, string reason, PlanPriority priority = PlanPriority.Leisure)
        {
            Activity = activity;
            Target = target;
            Reason = reason;
            Priority = priority;
        }

        /// <summary>A chat: walk over to <paramref name="partner"/> and talk.</summary>
        public PlanChoice(Resident partner, string reason, PlanPriority priority)
            : this(ActivityCatalog.Chat, null, reason, priority)
        {
            TargetResident = partner;
        }

        /// <summary>The person to chat with, for <see cref="ActivityCatalog.Chat"/>; otherwise null.</summary>
        public Resident TargetResident { get; }

        public PlanPriority Priority { get; }

        /// <summary>Free-time choices are the ones the AI may make instead; needs, sleep and work are not up for debate.</summary>
        public bool IsDiscretionary => Priority == PlanPriority.Comfort || Priority == PlanPriority.Leisure;

        public ActivityDefinition Activity { get; }

        public WorldObject Target { get; }

        public string Reason { get; }
    }

    /// <summary>One thing a resident could do with free time: an activity at a reachable object, or relaxing where they are.</summary>
    public sealed class FreeTimeOption
    {
        public const string RelaxActionId = "relax";
        public const string NoTargetId = "none";

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

        /// <summary>Null for relaxing.</summary>
        public ActivityDefinition Activity { get; }

        /// <summary>The object to use; null for relaxing and chatting.</summary>
        public WorldObject Target { get; }

        /// <summary>The person to chat with, or null.</summary>
        public Resident Partner { get; }

        /// <summary>Steps to walk there.</summary>
        public int Distance { get; }

        public string ActionId => Activity?.Id ?? RelaxActionId;

        public string TargetId => Target?.Id ?? Partner?.Id ?? NoTargetId;
    }

    /// <summary>
    /// The built-in, rule-based decision maker. It always produces a valid choice without any AI, so it is also
    /// the fallback when Ollama is unavailable. Priority order: urgent needs, bedtime, work, comfort, leisure.
    /// </summary>
    public sealed class ActivityPlanner
    {
        public static readonly int BedtimeMinute = GameTime.MinuteOfDayFor(22, 30);
        public static readonly int WakeMinute = GameTime.MinuteOfDayFor(6, 30);

        /// <summary>Residents leave for work this many minutes before their shift starts.</summary>
        public const int CommuteLeadMinutes = 30;

        /// <summary>Energy a resident must regain before getting up outside sleeping hours.</summary>
        public const double RestedEnergy = 70;

        /// <summary>Below this energy a resident goes for a nap when nothing more important is due.</summary>
        public const double TiredEnergy = 35;

        private const double TravelPenaltyPerStep = 0.15;

        /// <summary>Free-time activities are only worth starting if their main need has at least this much room to improve.</summary>
        private const double MinimumLeisureDeficit = 10;

        /// <summary>At most this many objects are offered for each free-time activity, nearest first.</summary>
        private const int MaxTargetsPerActivity = 2;

        private static readonly NeedType[] UrgentNeeds = { NeedType.Toilet, NeedType.Thirst, NeedType.Hunger, NeedType.Energy };

        private readonly TownWorld world;
        private readonly Pathfinder pathfinder;
        private readonly IReadOnlyList<Resident> residents;

        public ActivityPlanner(TownWorld world, Pathfinder pathfinder, IReadOnlyList<Resident> residents = null)
        {
            this.world = world;
            this.pathfinder = pathfinder;
            this.residents = residents ?? new Resident[0];
        }

        /// <summary>
        /// Whether a resident may use an object now: things inside someone else's home are private, and an object is
        /// full once as many other residents are using or heading for it as it has room for.
        /// </summary>
        public bool IsAvailableTo(Resident resident, WorldObject obj)
        {
            if (obj == null || (!world.GetMap(obj.Location.MapId).IsExterior && obj.Location.MapId != resident.HomeMapId))
            {
                return false;
            }

            var users = 0;
            foreach (var other in residents)
            {
                if (other != resident && other.IsAlive && other.TargetObjectId == obj.Id
                    && (other.State == ResidentState.Travelling || other.State == ResidentState.Performing))
                {
                    users++;
                }
            }

            return users < obj.Capacity;
        }

        public static bool IsSleepTime(GameTime time) => GameTime.IsWithin(time.MinuteOfDay, BedtimeMinute, WakeMinute);

        public static bool IsWorkTime(Resident resident, GameTime time)
        {
            var job = resident.Job;
            if (job == null || (time.IsWeekend && !job.WorksWeekends))
            {
                return false;
            }

            return GameTime.IsWithin(time.MinuteOfDay, job.StartMinuteOfDay - CommuteLeadMinutes, job.EndMinuteOfDay);
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

        public PlanChoice Choose(Resident resident, GameTime time)
        {
            var urgent = MostUrgentNeed(resident);
            if (urgent != null)
            {
                var choice = Nearest(resident, ActivityCatalog.ForNeed(urgent.Value), $"{urgent.Value} is urgent ({resident.Needs[urgent.Value]:0})", PlanPriority.UrgentNeed);
                if (choice != null)
                {
                    return choice;
                }
            }

            if (IsSleepTime(time))
            {
                var bed = BedChoice(resident, "It's bedtime", PlanPriority.Bedtime);
                if (bed != null)
                {
                    return bed;
                }
            }

            if (IsWorkTime(resident, time) && world.TryGetObject(resident.Job.WorkObjectId, out var workplace)
                && pathfinder.Distance(resident.Location, workplace.Location) != Pathfinder.Unreachable)
            {
                return new PlanChoice(ActivityCatalog.Work, workplace, $"Shift as {resident.Job.Title} until {FormatMinute(resident.Job.EndMinuteOfDay)}", PlanPriority.Work);
            }

            if (resident.Needs[NeedType.Energy] < TiredEnergy)
            {
                var nap = BedChoice(resident, "Feeling tired", PlanPriority.Nap);
                if (nap != null)
                {
                    return nap;
                }
            }

            // Null means every need is comfortable and nothing is due; the simulation lets the resident take it easy.
            return BestScored(resident, comfortOnly: true) ?? BestScored(resident, comfortOnly: false);
        }

        /// <summary>
        /// What the resident could sensibly do with free time right now, in a fixed order: activities whose main need
        /// has room to improve, at up to <see cref="MaxTargetsPerActivity"/> reachable objects each, then relaxing.
        /// AI decisions must pick one of these.
        /// </summary>
        public IReadOnlyList<FreeTimeOption> FreeTimeOptions(Resident resident)
        {
            var options = new List<FreeTimeOption>();
            foreach (var activity in ActivityCatalog.All)
            {
                if (activity.PrimaryNeed == null || (activity.Kind != ActivityKind.Care && activity.Kind != ActivityKind.Leisure)
                    || resident.Needs[activity.PrimaryNeed.Value] > Needs.Max - MinimumLeisureDeficit)
                {
                    continue;
                }

                options.AddRange(world.Objects
                    .Where(obj => activity.CanUse(obj) && IsAvailableTo(resident, obj))
                    .Select(obj => new FreeTimeOption(activity, obj, pathfinder.Distance(resident.Location, obj.Location)))
                    .Where(o => o.Distance != Pathfinder.Unreachable)
                    .OrderBy(o => o.Distance)
                    .ThenBy(o => o.Target.Id, StringComparer.Ordinal)
                    .Take(MaxTargetsPerActivity));
            }

            options.Add(new FreeTimeOption(null, null, 0));
            return options;
        }

        /// <summary>Whether something more important should stop the resident's current activity.</summary>
        public bool ShouldInterrupt(Resident resident, ActivityDefinition current, GameTime time)
        {
            if (current == null)
            {
                return false;
            }

            var urgent = MostUrgentNeed(resident);
            if (urgent != null && current.PrimaryNeed != urgent && Nearest(resident, ActivityCatalog.ForNeed(urgent.Value), string.Empty) != null)
            {
                return true;
            }

            if (current.Kind == ActivityKind.Leisure || current.Kind == ActivityKind.Care || current.Kind == ActivityKind.Social)
            {
                // Finish looking after an urgent need first; otherwise work and bedtime come before leisure.
                var forUrgent = current.PrimaryNeed != null && current.PrimaryNeed == urgent;
                if (!forUrgent && current.Kind != ActivityKind.Care && (IsWorkTime(resident, time) || IsSleepTime(time)) && WorkOrBedIsPossible(resident, time))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the planner would send the resident to work or bed right now. False when, say, the workplace can't
        /// be reached, so leisure isn't interrupted for something that can't happen.
        /// </summary>
        private bool WorkOrBedIsPossible(Resident resident, GameTime time)
        {
            var next = Choose(resident, time);
            return next != null && (next.Priority == PlanPriority.Work || next.Priority == PlanPriority.Bedtime);
        }

        public bool IsComplete(Resident resident, ActivityDefinition activity, GameTime time)
        {
            switch (activity.Kind)
            {
                case ActivityKind.Sleep:
                    return !IsSleepTime(time) && resident.Needs[NeedType.Energy] >= RestedEnergy;
                case ActivityKind.Work:
                    return resident.MinutesRemaining <= 0 || !IsWorkTime(resident, time);
                default:
                    return resident.MinutesRemaining <= 0
                        || (activity.PrimaryNeed != null && resident.Needs[activity.PrimaryNeed.Value] >= Needs.Max - 0.5);
            }
        }

        /// <summary>How long an activity runs once the resident arrives.</summary>
        public static int DurationFor(Resident resident, ActivityDefinition activity, GameTime time)
        {
            if (activity.Kind == ActivityKind.Work && resident.Job != null)
            {
                return Math.Max(1, resident.Job.EndMinuteOfDay - time.MinuteOfDay);
            }

            return activity.DurationMinutes;
        }

        private PlanChoice BedChoice(Resident resident, string reason, PlanPriority priority)
        {
            if (world.TryGetObject(resident.BedObjectId, out var bed)
                && pathfinder.Distance(resident.Location, bed.Location) != Pathfinder.Unreachable)
            {
                return new PlanChoice(ActivityCatalog.Sleep, bed, reason, priority);
            }

            return Nearest(resident, ActivityCatalog.Sleep, reason, priority);
        }

        private PlanChoice Nearest(Resident resident, ActivityDefinition activity, string reason, PlanPriority priority = PlanPriority.UrgentNeed)
        {
            WorldObject best = null;
            var bestDistance = int.MaxValue;
            foreach (var obj in world.Objects.Where(o => activity.CanUse(o) && IsAvailableTo(resident, o)))
            {
                var d = pathfinder.Distance(resident.Location, obj.Location);
                if (d != Pathfinder.Unreachable && d < bestDistance)
                {
                    best = obj;
                    bestDistance = d;
                }
            }

            return best == null ? null : new PlanChoice(activity, best, reason, priority);
        }

        /// <summary>
        /// Scores care and leisure activities by how much they help the resident's low needs, their personality and
        /// the walk to get there. Comfort mode only considers activities whose main need is below its comfort level.
        /// </summary>
        private PlanChoice BestScored(Resident resident, bool comfortOnly)
        {
            PlanChoice best = null;
            var bestScore = double.NegativeInfinity;
            foreach (var activity in ActivityCatalog.All)
            {
                if (activity.PrimaryNeed == null || activity.Kind == ActivityKind.Work || activity.Kind == ActivityKind.Sleep)
                {
                    continue;
                }

                if (!comfortOnly && activity.Kind != ActivityKind.Leisure)
                {
                    continue;
                }

                var primary = activity.PrimaryNeed.Value;
                var threshold = comfortOnly ? NeedRules.ComfortThreshold(primary, resident.Personality) : Needs.Max;
                var primaryDeficit = threshold - resident.Needs[primary];
                if (primaryDeficit <= (comfortOnly ? 0 : MinimumLeisureDeficit))
                {
                    continue;
                }

                var secondary = 0.0;
                for (var i = 0; i < Needs.Count; i++)
                {
                    var need = (NeedType)i;
                    if (need != primary && activity.GainPerMinute(need) > 0)
                    {
                        secondary += Math.Max(0, NeedRules.ComfortThreshold(need, resident.Personality) - resident.Needs[need]);
                    }
                }

                var baseScore = (primaryDeficit * Preference(resident.Personality, activity)) + (0.5 * secondary);
                foreach (var obj in world.Objects.Where(o => activity.CanUse(o) && IsAvailableTo(resident, o)))
                {
                    var d = pathfinder.Distance(resident.Location, obj.Location);
                    if (d == Pathfinder.Unreachable)
                    {
                        continue;
                    }

                    var score = baseScore - (TravelPenaltyPerStep * d);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        var reason = comfortOnly
                            ? $"{primary} is getting low ({resident.Needs[primary]:0})"
                            : $"Free time: fancies {activity.Verb}";
                        best = new PlanChoice(activity, obj, reason, comfortOnly ? PlanPriority.Comfort : PlanPriority.Leisure);
                    }
                }
            }

            return best;
        }

        private static double Preference(Personality p, ActivityDefinition activity)
        {
            if (activity == ActivityCatalog.Exercise)
            {
                return 0.5 + p.Active;
            }

            if (activity == ActivityCatalog.WatchTown)
            {
                return 0.5 + p.Sociable;
            }

            if (activity == ActivityCatalog.Read)
            {
                return 0.5 + p.Bookish;
            }

            return 1.0;
        }

        private static string FormatMinute(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
    }
}
