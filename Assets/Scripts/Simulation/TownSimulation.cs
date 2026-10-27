using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation
{
    /// <summary>What happened to an AI suggestion handed to the simulation.</summary>
    public enum DecisionOutcome
    {
        Applied,

        /// <summary>The resident is no longer waiting for this answer; it is ignored, not retried.</summary>
        Stale,
        UnknownResident,
        NotAlive,

        /// <summary>The action is not one of the resident's current free-time options.</summary>
        InvalidAction,

        /// <summary>The action is allowed but not at that target.</summary>
        InvalidTarget,

        /// <summary>The text was empty or unusable.</summary>
        InvalidText,
    }

    /// <summary>
    /// Owns the town's live state and advances it one in-game minute per <see cref="Tick"/>.
    /// Given the same starting state it always produces the same result.
    /// </summary>
    public sealed class TownSimulation
    {
        /// <summary>How long a resident waits before re-planning after finding a destination unreachable.</summary>
        public const int UnreachableWaitMinutes = 15;

        /// <summary>How long a resident with nothing to do relaxes before checking again.</summary>
        public const int RelaxMinutes = 15;

        public const int DefaultDecisionWaitMinutes = 20;

        /// <summary>AI-suggested reflections can matter, but only game events may become the most important memories.</summary>
        public const int MaxReflectionImportance = 6;

        public const int MaxReasonLength = 100;

        /// <summary>
        /// A resident's day for reflection purposes runs from noon to noon, so going to bed after midnight, or still
        /// sleeping early the next morning, counts as the same day.
        /// </summary>
        private const int ReflectionDayStartMinute = 12 * 60;

        private readonly List<Resident> residents;
        private int decisionWaitMinutes = DefaultDecisionWaitMinutes;
        private long nextDecisionId = 1;

        public TownSimulation(TownWorld world, IEnumerable<Resident> residents, GameTime time)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            this.residents = residents.ToList();
            if (this.residents.Select(r => r.Id).Distinct().Count() != this.residents.Count)
            {
                throw new ArgumentException("Resident ids must be unique.");
            }

            Time = time;
            Pathfinder = new Pathfinder(world);
            Planner = new ActivityPlanner(world, Pathfinder);
        }

        public TownWorld World { get; }

        public GameTime Time { get; private set; }

        public IReadOnlyList<Resident> Residents => residents;

        public Pathfinder Pathfinder { get; }

        public ActivityPlanner Planner { get; }

        public MemoryStore Memories { get; } = new MemoryStore();

        public ConversationHistory Conversations { get; } = new ConversationHistory();

        /// <summary>
        /// Whether residents pause briefly for an AI suggestion before free-time activities. Set by the AI service
        /// while Ollama is reachable; not saved. When false the town behaves exactly as the built-in routine.
        /// </summary>
        public bool AiDecisionsEnabled { get; set; }

        /// <summary>Game minutes a resident waits for an AI suggestion before the built-in routine decides.</summary>
        public int DecisionWaitMinutes
        {
            get => decisionWaitMinutes;
            set => decisionWaitMinutes = Math.Max(1, Math.Min(240, value));
        }

        public Resident GetResident(string id) => residents.FirstOrDefault(r => r.Id == id);

        public void Advance(int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                Tick();
            }
        }

        public void Tick()
        {
            foreach (var resident in residents)
            {
                if (resident.IsAlive)
                {
                    Step(resident);
                }
            }

            Time = Time.AddMinutes(1);
        }

        private void Step(Resident r)
        {
            ActivityCatalog.TryGet(r.ActivityId, out var activity);
            NeedRules.ApplyMinute(r.Needs, r.Personality, r.State == ResidentState.Performing ? activity : null);

            switch (r.State)
            {
                case ResidentState.Waiting:
                    r.MinutesRemaining--;
                    if (r.MinutesRemaining <= 0)
                    {
                        r.ResetActivity();
                    }

                    break;

                case ResidentState.Performing:
                    if (r.MinutesRemaining > 0)
                    {
                        r.MinutesRemaining--;
                    }

                    if (Planner.IsComplete(r, activity, Time))
                    {
                        r.ResetActivity();
                    }
                    else if (Planner.ShouldInterrupt(r, activity, Time))
                    {
                        r.AddLog(Time, $"Stopped {activity.Verb}: something more important came up");
                        r.ResetActivity();
                    }

                    break;

                case ResidentState.Travelling:
                    if (Planner.ShouldInterrupt(r, activity, Time))
                    {
                        r.AddLog(Time, $"Changed plans on the way to {activity.Name.ToLowerInvariant()}");
                        r.ResetActivity();
                        break;
                    }

                    Move(r, activity);
                    break;

                case ResidentState.Deciding:
                    var routine = Planner.Choose(r, Time);
                    if (!AiDecisionsEnabled || (routine != null && !routine.IsDiscretionary))
                    {
                        r.ResetActivity();
                        StartChoice(r, routine);
                    }
                    else if (Time.TotalMinutes >= r.DecisionDeadline)
                    {
                        r.AddLog(Time, "Went with the usual routine (no AI answer in time)");
                        r.ResetActivity();
                        StartChoice(r, routine);
                    }

                    break;

                default:
                    StartNextActivity(r);
                    break;
            }
        }

        /// <summary>
        /// Applies an AI suggestion for a resident who is deciding. Everything is checked against the current state:
        /// the answer must be for the decision the resident is waiting on, and the action and target must be one of
        /// <see cref="ActivityPlanner.FreeTimeOptions"/> right now. Reason and speech are cleaned and shortened.
        /// </summary>
        public DecisionOutcome ApplyDecision(string residentId, long decisionId, string actionId, string targetId, string reason, string say)
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

            if (r.State != ResidentState.Deciding || r.PendingDecisionId != decisionId || decisionId <= 0)
            {
                return DecisionOutcome.Stale;
            }

            var options = Planner.FreeTimeOptions(r);
            var sameAction = options.Where(o => o.ActionId == actionId).ToList();
            if (sameAction.Count == 0)
            {
                return DecisionOutcome.InvalidAction;
            }

            var option = sameAction.FirstOrDefault(o => o.TargetId == targetId);
            if (option == null)
            {
                return DecisionOutcome.InvalidTarget;
            }

            var why = TextRules.Clean(reason, MaxReasonLength);
            if (why.Length == 0)
            {
                why = option.Activity == null ? "Felt like taking it easy" : $"Fancied {option.Activity.Verb}";
            }

            r.ResetActivity();
            if (option.Activity == null)
            {
                Relax(r, why);
                r.AddLog(Time, $"Decided to take it easy: {why} (AI)");
            }
            else
            {
                StartChoice(r, new PlanChoice(option.Activity, option.Target, why), "(AI)");
            }

            r.GoalSource = DecisionSource.Ai;
            Conversations.AddRemark(r.Id, Time, say, option.ActionId);
            return DecisionOutcome.Applied;
        }

        /// <summary>Gives up on an AI suggestion, for example after failed retries, and lets the routine decide now.</summary>
        public bool AbandonDecision(string residentId, long decisionId, string why)
        {
            var r = GetResident(residentId);
            if (r == null || !r.IsAlive || r.State != ResidentState.Deciding || r.PendingDecisionId != decisionId)
            {
                return false;
            }

            r.AddLog(Time, $"Went with the usual routine ({TextRules.Clean(why, 60)})");
            r.ResetActivity();
            StartChoice(r, Planner.Choose(r, Time));
            return true;
        }

        /// <summary>The reflection day a moment belongs to; see <see cref="ReflectionDayStartMinute"/>.</summary>
        public static int ReflectionDayFor(GameTime time) =>
            (int)((time.TotalMinutes - ReflectionDayStartMinute) / GameTime.MinutesPerDay) + 1;

        /// <summary>A resident is ready to reflect once they have gone to bed for the night and haven't reflected on today yet.</summary>
        public bool IsReflectionDue(Resident r) =>
            r != null && r.IsAlive && r.State == ResidentState.Performing && r.ActivityId == ActivityCatalog.Sleep.Id
            && ActivityPlanner.IsSleepTime(Time) && ReflectionDayFor(Time) > r.LastReflectionDay;

        /// <summary>
        /// Stores an AI-suggested reflection as a memory, after checking it is for a day not yet reflected on.
        /// Importance is clamped to 1..<see cref="MaxReflectionImportance"/>.
        /// </summary>
        public DecisionOutcome ApplyReflection(string residentId, int day, string summary, int importance)
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

            if (day <= r.LastReflectionDay || day > ReflectionDayFor(Time))
            {
                return DecisionOutcome.Stale;
            }

            var memory = Memories.Add(r.Id, Time, MemoryType.Reflection, Math.Min(importance, MaxReflectionImportance), summary, tags: new[] { $"day-{day}" });
            if (memory == null)
            {
                return DecisionOutcome.InvalidText;
            }

            r.LastReflectionDay = day;
            return DecisionOutcome.Applied;
        }

        private void StartNextActivity(Resident r)
        {
            var choice = Planner.Choose(r, Time);
            if (AiDecisionsEnabled && (choice == null || choice.IsDiscretionary) && Planner.FreeTimeOptions(r).Count >= 2)
            {
                r.ResetActivity();
                r.State = ResidentState.Deciding;
                r.PendingDecisionId = nextDecisionId++;
                r.DecisionDeadline = Time.TotalMinutes + DecisionWaitMinutes;
                r.Goal = "Thinking about what to do";
                return;
            }

            StartChoice(r, choice);
        }

        private void StartChoice(Resident r, PlanChoice choice, string sourceNote = null)
        {
            if (choice == null)
            {
                r.ResetActivity();
                Relax(r, "Taking it easy");
                return;
            }

            if (choice.Priority == PlanPriority.UrgentNeed && choice.Activity.PrimaryNeed != null)
            {
                RememberUrgentNeed(r, choice.Activity.PrimaryNeed.Value);
            }

            r.ActivityId = choice.Activity.Id;
            r.TargetObjectId = choice.Target.Id;
            r.Goal = choice.Reason;
            r.State = ResidentState.Travelling;
            var note = sourceNote == null ? string.Empty : " " + sourceNote;
            r.AddLog(Time, $"Decided to {choice.Activity.Name.ToLowerInvariant()} at {choice.Target.Name}: {choice.Reason}{note}");
            if (r.Location == choice.Target.Location)
            {
                BeginPerforming(r, choice.Activity);
            }
        }

        private static void Relax(Resident r, string goal)
        {
            r.State = ResidentState.Waiting;
            r.MinutesRemaining = RelaxMinutes;
            r.Goal = goal;
        }

        /// <summary>Running desperately low on something is worth remembering, once per need per day.</summary>
        private void RememberUrgentNeed(Resident r, NeedType need)
        {
            var tag = $"urgent-{need.ToString().ToLowerInvariant()}";
            var dayStart = GameTime.FromDayAndTime(Time.Day, 0, 0);
            if (Memories.HasMemoryWithTag(r.Id, tag, dayStart))
            {
                return;
            }

            Memories.Add(r.Id, Time, MemoryType.Event, 3, UrgentNeedSummary(need), tags: new[] { tag, need.ToString().ToLowerInvariant() });
        }

        private static string UrgentNeedSummary(NeedType need)
        {
            switch (need)
            {
                case NeedType.Hunger:
                    return "I got so hungry I had to drop everything and eat.";
                case NeedType.Thirst:
                    return "I was parched and had to stop for a drink.";
                case NeedType.Energy:
                    return "I was so exhausted I could barely keep my eyes open.";
                case NeedType.Toilet:
                    return "I had to rush off to the toilet.";
                default:
                    return $"My {need.ToString().ToLowerInvariant()} got desperately low.";
            }
        }

        private void Move(Resident r, ActivityDefinition activity)
        {
            if (activity == null || !World.TryGetObject(r.TargetObjectId, out var target))
            {
                r.ResetActivity();
                return;
            }

            if (r.Location == target.Location)
            {
                BeginPerforming(r, activity);
                return;
            }

            if (Pathfinder.TryGetNextStep(r.Location, target.Location, out var next))
            {
                if (next.MapId != r.Location.MapId)
                {
                    r.AddLog(Time, $"Went {(World.GetMap(next.MapId).IsExterior ? "outside" : "inside")} to {World.GetMap(next.MapId).Name}");
                }

                r.Location = next;
                return;
            }

            r.AddLog(Time, $"Couldn't find a way to {target.Name}; waiting before trying again");
            var tag = $"unreachable-{target.Id}";
            if (!Memories.HasMemoryWithTag(r.Id, tag, GameTime.FromDayAndTime(Time.Day, 0, 0)))
            {
                Memories.Add(r.Id, Time, MemoryType.Event, 4, $"I couldn't find any way to get to the {target.Name.ToLowerInvariant()}.", tags: new[] { tag, target.Id });
            }

            r.ResetActivity();
            r.State = ResidentState.Waiting;
            r.MinutesRemaining = UnreachableWaitMinutes;
            r.Goal = $"Waiting: no route to {target.Name}";
        }

        private void BeginPerforming(Resident r, ActivityDefinition activity)
        {
            r.State = ResidentState.Performing;
            r.MinutesRemaining = ActivityPlanner.DurationFor(r, activity, Time);
            r.AddLog(Time, $"Started {activity.Verb}");
        }
    }
}
