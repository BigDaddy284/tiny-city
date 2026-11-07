using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation
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

        /// <summary>
        /// A day plan from someone without a role named a role that is full (or none), so it is asked again with the
        /// roles that are free now.
        /// </summary>
        RoleTaken,
    }

    /// <summary>
    /// Owns the town's live state and advances it one in-game minute per <see cref="Tick"/>.
    /// Given the same starting state it always produces the same result.
    /// </summary>
    public sealed partial class TownSimulation
    {
        /// <summary>How long a resident waits before re-planning after finding a destination unreachable.</summary>
        public const int UnreachableWaitMinutes = 15;

        /// <summary>How long a resident with nothing to do relaxes before checking again.</summary>
        public const int RelaxMinutes = 15;

        /// <summary>AI-suggested reflections can matter, but only game events may become the most important memories.</summary>
        public const int MaxReflectionImportance = 6;

        public const int MaxReasonLength = 100;

        /// <summary>
        /// One AI answer plans up to this many activities: the one to do now and the rest afterwards, so each resident
        /// asks the model less often (owner, 5 November: ask less, never pause). Each later step is checked again when
        /// it is due (see <see cref="PlanStep"/>).
        /// </summary>
        public const int MaxPlanSteps = 6;

        /// <summary>A plan's later steps are dropped this many game minutes after it was made.</summary>
        public const int PlanMinutes = 360;

        /// <summary>
        /// A resident's day for reflection purposes runs from noon to noon, so a reflection late at night or in the
        /// small hours counts for the same day.
        /// </summary>
        private const int ReflectionDayStartMinute = 12 * 60;

        /// <summary>Nobody sleeps, so each resident looks back on their day from this time of the evening (21:00).</summary>
        public const int ReflectionFromMinute = 21 * 60;

        private readonly List<Resident> residents;
        private readonly List<ConversationSession> sessions = new List<ConversationSession>();
        private long nextDecisionId = 1;
        private long nextLineRequestId = 1;

        public TownSimulation(TownWorld world, IEnumerable<Resident> residents, GameTime time)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            this.residents = residents.ToList();
            if (this.residents.Select(r => r.Id).Distinct().Count() != this.residents.Count)
            {
                throw new ArgumentException("Resident ids must be unique.");
            }

            Time = time;
            Construction = new Building.Construction(world);
            Food.Attach(world);
            Pathfinder = new Pathfinder(world, Construction.IsBlocked);
            Planner = new ActivityPlanner(world, Pathfinder, this.residents, () => Time) { Food = Food };
            Food.SellerAt = SellerAt;
            Food.BuildingAllows = BuildingAllows;
            Food.RoleAllows = (r, a) => Brief.MayDo(r.Role, a.Id);
            // A world with no roles limits nobody, so everyone works as well as a role holder would.
            Food.Skilled = (r, a) => !Brief.IsSkill(a.Id) || Brief.IsSkilledAt(r.Role, a.Id);
            Food.MeetingAllows = MeetingAllows;
            Food.StoreOpen = () => StoreIsOpen;
            Food.KeepsStore = r => Brief.IsSkilledAt(r.Role, ActivityCatalog.KeepStore.Id);
        }

        public TownWorld World { get; }

        public GameTime Time { get; private set; }

        public IReadOnlyList<Resident> Residents => residents;

        public Pathfinder Pathfinder { get; }

        /// <summary>Tiny City 2's buildings on their plots.</summary>
        public Building.Construction Construction { get; }

        public ActivityPlanner Planner { get; }

        public MemoryStore Memories { get; } = new MemoryStore();

        public ConversationHistory Conversations { get; } = new ConversationHistory();

        public RelationshipBook Relationships { get; } = new RelationshipBook();

        /// <summary>Conversations happening right now.</summary>
        public IReadOnlyList<ConversationSession> ActiveConversations => sessions;

        /// <summary>
        /// Whether the AI is connected. Set by the AI service while Ollama is reachable; not saved. The AI makes every
        /// choice: each time a resident is free they wait for it (there is no routine to fall back on), and
        /// conversations wait for its lines. While it is off, nobody follows the rest of an AI plan.
        /// </summary>
        public bool AiDecisionsEnabled { get; set; }

        /// <summary>
        /// Owner, 5 November: hunger and thirst fall at this share of the usual rate, so food and drink matter as
        /// things to trade without everyone starving within days.
        /// </summary>
        public const double HungerThirstRate = 0.5;

        /// <summary>Owner, 5 November: nobody dies; neglect wears health down to this and no further.</summary>
        public const double LowestHealth = 1;

        /// <summary>True while a living resident is waiting for an AI choice or a conversation for an AI line.</summary>
        public bool WaitingForAi =>
            Residents.Any(r => r.IsAlive && r.State == ResidentState.Deciding) || ActiveConversations.Any(c => c.AwaitingLine);

        /// <summary>What has happened in the town, for the journal and life stories.</summary>
        public TownHistory History { get; } = new TownHistory();

        public int LivingCount => residents.Count(r => r.IsAlive);

        public Resident GetResident(string id) => residents.FirstOrDefault(r => r.Id == id);

        public ConversationSession GetConversation(string id) => sessions.FirstOrDefault(c => c.Id == id);

        /// <summary>Puts back a conversation from a save. Used only by the save loader.</summary>
        internal void RestoreConversation(ConversationSession session, long nextConversationNumber)
        {
            sessions.Add(session);
            Conversations.RestoreNextNumber(nextConversationNumber);
        }

        /// <summary>Removes a conversation that couldn't be restored, without recording it. Used only by the save loader.</summary>
        internal void DropConversation(ConversationSession session)
        {
            sessions.Remove(session);
            foreach (var id in session.ParticipantIds)
            {
                var r = GetResident(id);
                if (r != null && r.ConversationId == session.Id)
                {
                    r.ResetActivity();
                }
            }
        }

        /// <summary>The most recent line a resident said, in a conversation still going on or in the history.</summary>
        public ConversationLine LastLineBy(string residentId)
        {
            ConversationLine latest = null;
            foreach (var session in sessions)
            {
                var line = session.Lines.LastOrDefault(l => l.SpeakerId == residentId);
                if (line != null && (latest == null || line.Time.TotalMinutes >= latest.Time.TotalMinutes))
                {
                    latest = line;
                }
            }

            var recorded = Conversations.LastLineBy(residentId);
            return latest == null || (recorded != null && recorded.Time.TotalMinutes > latest.Time.TotalMinutes) ? recorded ?? latest : latest;
        }

        public void Advance(int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                Tick();
            }
        }

        public void Tick()
        {
            StepFood();
            StepSketches();
            StepMeeting();
            foreach (var resident in residents)
            {
                if (!resident.IsAlive)
                {
                    continue;
                }

                Step(resident);
            }

            StepConversations();
            Perceive();
            Time = Time.AddMinutes(1);
        }

        private void Step(Resident r)
        {
            ActivityCatalog.TryGet(r.ActivityId, out var activity);
            NeedRules.ApplyMinute(r.Needs, r.Personality, r.State == ResidentState.Performing || r.State == ResidentState.Talking ? activity : null, HungerThirstRate);
            StepHealth(r, activity);

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
                        FinishActivity(r, activity);
                        r.ResetActivity();
                    }

                    break;

                case ResidentState.Talking:
                    var conversation = GetConversation(r.ConversationId);
                    if (conversation == null)
                    {
                        r.ResetActivity();
                        break;
                    }

                    var leaveReason = SocialRules.ReasonToLeave(r, Time);
                    if (leaveReason != null && CanSeeToUrgentNeed(r))
                    {
                        EndConversation(conversation, r, leaveReason);
                    }

                    break;

                case ResidentState.Travelling when r.TargetResidentId != null:
                    SeekPartner(r);
                    break;

                case ResidentState.Travelling:
                    Move(r, activity);
                    break;

                case ResidentState.Deciding:
                    break;

                default:
                    StartNextActivity(r);
                    break;
            }
        }

        /// <summary>
        /// Applies an AI suggestion for a resident who is deciding. Everything is checked against the current state:
        /// the answer must be for the decision the resident is waiting on, and the action and target must be one of
        /// <see cref="ActivityPlanner.FreeTimeOptions"/> right now. The reason is cleaned and shortened and goes in their log; nothing is said out loud (owner, 6 November: bubbles are only for chats).
        /// </summary>
        public DecisionOutcome ApplyDecision(string residentId, long decisionId, string actionId, string targetId, string reason, IReadOnlyList<PlanStep> then = null,
            int? x = null, int? y = null, string what = null)
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

            var options = FreeTimeOptionsFor(r);
            var sameAction = options.Where(o => o.ActionId == actionId).ToList();
            if (sameAction.Count == 0)
            {
                // Small models often get the action's name wrong but the target right. If only one option uses that
                // target, that is what they meant; otherwise asking again would only hold the clock.
                var byTarget = options.Where(o => o.TargetId == targetId).ToList();
                if (byTarget.Count != 1)
                {
                    return DecisionOutcome.InvalidAction;
                }

                sameAction = byTarget;
                actionId = byTarget[0].ActionId;
            }

            var option = sameAction.FirstOrDefault(o => o.TargetId == targetId);
            if (option == null)
            {
                // A target that doesn't go with the action means they want to do it but named the wrong
                // place, so they go to the nearest place it can be done.
                option = sameAction.OrderBy(o => o.Distance).First();
            }

            if (option.IsGround)
            {
                // Tiny City 2: planting or building wherever they choose. The game only checks the spot is free ground they can reach.
                var spot = x != null && y != null ? new Location(r.Location.MapId, x.Value, y.Value) : r.Location;
                var problem = x == null || y == null ? "no spot was given" : GroundProblem(r, spot, option.Activity);
                if (problem != null)
                {
                    var near = option.Activity == ActivityCatalog.PlantCrops ? FreeSoil(r, 1) : FreeGroundNear(r, 1, AI.PromptText.FreeSpotSearch);
                    if (near.Count == 0)
                    {
                        r.AddLog(Time, $"Couldn't go {option.Activity.Verb} at ({x}, {y}): {problem}");
                        return DecisionOutcome.InvalidTarget;
                    }

                    spot = near[0];
                    r.AddLog(Time, $"Couldn't go {option.Activity.Verb} at ({x}, {y}) ({problem}), so chose ({spot.Position.X}, {spot.Position.Y}) nearby");
                }

                if (option.Activity == ActivityCatalog.PlanBuilding)
                {
                    DropUnplannedSite(r);
                }

                var placedId = option.Activity == ActivityCatalog.PlantCrops ? Food.AddPatch(spot, what, r.Id).Id
                    : Construction.AddSite(r.Id, spot).Id;
                World.TryGetObject(placedId, out var placed);
                option = new FreeTimeOption(option.Activity, placed, Pathfinder.Distance(r.Location, spot));
            }

            var why = TextRules.Clean(reason, MaxReasonLength);
            if (why.Length == 0)
            {
                why = option.Activity == null ? "Felt like taking it easy" : $"Fancied {option.Activity.Verb}";
            }

            r.ResetActivity();
            StartOption(r, option, why, "(AI)");
            var actions = new HashSet<string>(options.Select(o => o.ActionId));
            r.SetPlan((then ?? Array.Empty<PlanStep>()).Where(step => step != null && actions.Contains(step.ActionId)).Take(MaxPlanSteps - 1), Time.TotalMinutes + PlanMinutes);
            return DecisionOutcome.Applied;
        }

        private void StartOption(Resident r, FreeTimeOption option, string why, string sourceNote)
        {
            if (option.Activity == null)
            {
                Relax(r, why);
                r.AddLog(Time, $"Decided to take it easy: {why} {sourceNote}");
            }
            else if (option.Partner != null)
            {
                StartChoice(r, new PlanChoice(option.Partner, why), sourceNote);
            }
            else
            {
                StartChoice(r, new PlanChoice(option.Activity, option.Target, why), sourceNote);
            }

            r.GoalSource = DecisionSource.Ai;
        }

        /// <summary>
        /// Starts the next step of the resident's AI plan if it is still possible: it must be one of their free-time
        /// options right now, at the planned target or, if that has gone (full, shut, too far), the nearest one for
        /// the same activity. Steps that no longer fit are skipped, and an old plan is dropped.
        /// </summary>
        private bool TryFollowPlan(Resident r)
        {
            if (r.PlannedSteps.Count > 0 && Time.TotalMinutes > r.PlanExpires)
            {
                r.ClearPlan();
            }

            while (r.PlannedSteps.Count > 0)
            {
                var step = r.TakePlannedStep();
                var options = FreeTimeOptionsFor(r);
                // A spot on open ground isn't part of a planned step, so those aren't followed.
                var option = options.FirstOrDefault(o => o.ActionId == step.ActionId && o.TargetId == step.TargetId && !o.IsGround)
                    ?? (step.ActionId == ActivityCatalog.Chat.Id ? null : options.FirstOrDefault(o => o.ActionId == step.ActionId && !o.IsGround));
                if (option == null)
                {
                    continue;
                }

                var why = option.Activity == null ? "Planned to take it easy for a bit" : $"Planned on {option.Activity.Verb} next";
                StartOption(r, option, why, "(AI plan)");
                r.PlanStepsTaken++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether something they could do right now would see to their most urgent need. Owner's AI log of 7 November:
        /// starving people with nothing to eat walked out of every chat ("hunger couldn't wait") only to choose another
        /// chat, so a need with nothing to meet it no longer ends a chat.
        /// </summary>
        private bool CanSeeToUrgentNeed(Resident r)
        {
            switch (ActivityPlanner.MostUrgentNeed(r))
            {
                case null:
                    return false;
                case NeedType.Hunger:
                    return r.Food.Count(FoodKind.Meal) > 0 || (Food.MayTakeFromStore(r) && Food.Store.Food.Count(FoodKind.Meal) > 0);
                case NeedType.Thirst:
                case NeedType.Hygiene:
                    return r.CleanWater > 0 || (Food.MayTakeFromStore(r) && Food.Store.Water > 0);
                default:
                    return true;
            }
        }

        /// <summary>
        /// Tiny City 2: why a resident can't plant or start building at this spot, or null if they can: it must be
        /// open ground on an outdoor map that they can reach, with nothing on it and no building planned over it.
        /// Owner, 6 November: crops grow only in the farming patch (soil), and nobody builds on it.
        /// </summary>
        public string GroundProblem(Resident r, Location spot, ActivityDefinition activity = null)
        {
            if (activity != null && World.TryGetMap(spot.MapId, out var soilMap) && soilMap.InBounds(spot.Position))
            {
                var soil = soilMap.GetTile(spot.Position) == TileType.Garden;
                if (activity == ActivityCatalog.PlantCrops && !soil)
                {
                    return "crops only grow in the farming patch";
                }

                if (activity == ActivityCatalog.PlanBuilding && soil)
                {
                    return "the farming patch is kept for crops";
                }
            }

            if (!World.TryGetMap(spot.MapId, out var map) || !map.IsExterior)
            {
                return "that's not outdoors";
            }

            if (!map.IsWalkable(spot.Position))
            {
                return map.InBounds(spot.Position) ? "that isn't open ground" : "that's off the land";
            }

            if (World.Objects.Any(o => o.Location == spot) || Construction.IsClaimed(spot) || Construction.IsBlocked(spot))
            {
                return "something is already there";
            }

            if (Pathfinder.Distance(r.Location, spot) == Pathfinder.Unreachable)
            {
                return "they can't get there";
            }

            if (activity == ActivityCatalog.PlanBuilding && AI.SketchPrompt.Room(this, spot, null) is var room && room < Building.BuildingRules.MinBuildRoom)
            {
                return $"only {room} by {room} paces are clear there, and a building needs at least {Building.BuildingRules.MinBuildRoom} by {Building.BuildingRules.MinBuildRoom}";
            }

            return null;
        }

        /// <summary>
        /// Tiny City 2: free open ground near a resident (not on a path), nearest first, where they could plant,
        /// build or lie down. Used for the prompt's suggestions and when an answer gives no usable spot.
        /// </summary>
        public List<Location> FreeGroundNear(Resident r, int count, int search)
        {
            var free = new List<Location>();
            if (!World.TryGetMap(r.Location.MapId, out var map) || !map.IsExterior)
            {
                return free;
            }

            var here = r.Location.Position;
            for (var d = 1; d <= search && free.Count < count; d++)
            {
                for (var dy = -d; dy <= d && free.Count < count; dy++)
                {
                    foreach (var dx in new[] { -(d - Math.Abs(dy)), d - Math.Abs(dy) }.Distinct())
                    {
                        var spot = new Location(map.Id, here.X + dx, here.Y + dy);
                        if (free.Count < count && !(map.InBounds(spot.Position) && (map.GetTile(spot.Position) == TileType.Path || map.GetTile(spot.Position) == TileType.Garden)) && GroundProblem(r, spot, ActivityCatalog.PlanBuilding) == null)
                        {
                            free.Add(spot);
                        }
                    }
                }
            }

            return free;
        }

        /// <summary>Free soil in the farming patch where a resident could plant, nearest to them first.</summary>
        public List<Location> FreeSoil(Resident r, int count)
        {
            var free = new List<Location>();
            if (!World.TryGetMap(r.Location.MapId, out var map) || !map.IsExterior)
            {
                return free;
            }

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var spot = new Location(map.Id, x, y);
                    if (map.GetTile(spot.Position) == TileType.Garden && GroundProblem(r, spot, ActivityCatalog.PlantCrops) == null)
                    {
                        free.Add(spot);
                    }
                }
            }

            return free.OrderBy(s => Pathfinder.Distance(r.Location, s)).ThenBy(s => s.Position.Y).ThenBy(s => s.Position.X).Take(count).ToList();
        }

        /// <summary>
        /// Gives up on an AI answer, for example after failed retries. There is no routine to fall back on, so the
        /// resident asks again with a fresh decision id.
        /// </summary>
        public bool AbandonDecision(string residentId, long decisionId, string why)
        {
            var r = GetResident(residentId);
            if (r == null || !r.IsAlive || r.State != ResidentState.Deciding || r.PendingDecisionId != decisionId)
            {
                return false;
            }

            r.PendingDecisionId = nextDecisionId++;
            r.AddLog(Time, $"Thought again ({TextRules.Clean(why, 60)})");
            return true;
        }

        /// <summary>The reflection day a moment belongs to; see <see cref="ReflectionDayStartMinute"/>.</summary>
        public static int ReflectionDayFor(GameTime time) =>
            (int)((time.TotalMinutes - ReflectionDayStartMinute) / GameTime.MinutesPerDay) + 1;

        /// <summary>
        /// Tiny City 2: a living resident who is choosing what to do and has no plan for today yet (after waking,
        /// at the start of a town, or after loading one) should plan their day before choosing.
        /// </summary>
        public bool NeedsDayPlan(Resident r) =>
            r != null && r.IsAlive && r.State == ResidentState.Deciding && (r.DayPlan == null || r.DayPlan.Day != DayPlan.DayFor(Time));

        /// <summary>
        /// Stores an AI-written plan for today, after checking it is for today and still wanted. The plan only
        /// guides later choices, so it changes nothing else.
        /// </summary>
        public DecisionOutcome ApplyDayPlan(string residentId, int day, IEnumerable<DayPlanEntry> entries, string role = null)
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

            if (day != DayPlan.DayFor(Time) || r.DayPlan?.Day == day)
            {
                return DecisionOutcome.Stale;
            }

            var plan = DayPlan.Make(day, Time, entries);
            if (plan == null)
            {
                return DecisionOutcome.InvalidText;
            }

            // From the owner's log of 6 November: all twenty planned at once and most picked the same full role, so
            // fifteen had none all day and could only chat. Someone without a role must name one with a place left.
            if (r.Role == null && Brief.Roles.Count > 0 && Brief.Roles.Any(o => !RoleIsFull(o, r)))
            {
                var wanted = string.IsNullOrWhiteSpace(role) ? null : Brief.Find(role);
                if (wanted == null || RoleIsFull(wanted, r))
                {
                    r.AddLog(Time, wanted == null ? "Planned the day without choosing a role" : $"Wanted to be the {wanted.Name}, but all {wanted.Max} places are taken");
                    return DecisionOutcome.RoleTaken;
                }
            }

            r.DayPlan = plan;
            r.AddLog(Time, "Planned the day");
            ChooseRole(r, role);
            return DecisionOutcome.Applied;
        }

        /// <summary>
        /// The world's brief and the roles on offer to everyone (Tiny City 2), from residents.json when the world was
        /// made. Saved.
        /// </summary>
        public WorldBrief Brief { get; set; } = WorldBrief.Empty;

        /// <summary>
        /// A resident's own choice of role in their morning plan: one from <see cref="Brief"/>, "none" to give it up,
        /// or nothing to keep what they have. Unknown names are ignored.
        /// </summary>
        private void ChooseRole(Resident r, string choice)
        {
            if (string.IsNullOrWhiteSpace(choice) || Brief.Roles.Count == 0)
            {
                return;
            }

            var none = string.Equals(choice.Trim(), RoleNone, StringComparison.OrdinalIgnoreCase);
            var role = none ? null : Brief.Find(choice);
            if ((!none && role == null) || string.Equals(role?.Name, r.Role, StringComparison.Ordinal))
            {
                return;
            }

            if (role != null && RoleIsFull(role, r))
            {
                r.AddLog(Time, $"Wanted to be the {role.Name}, but all {role.Max} places are taken");
                Memories.Add(r.Id, Time, MemoryType.Event, 4,
                    $"I wanted to be the {role.Name}, but there can only be {role.Max} and they are all taken, so I need to do something else for now.",
                    tags: new[] { "role" });
                return;
            }

            if (RequestRoleChange(r, role, none))
            {
                return;
            }

            var old = r.Role;
            r.Role = role?.Name;
            r.AddLog(Time, role == null ? $"Gave up being the {old}" : old == null ? $"Took up being the {role.Name}" : $"Stopped being the {old} and took up being the {role.Name}");
            Memories.Add(r.Id, Time, MemoryType.Event, 5,
                role == null ? $"I decided to stop being the {old}." : old == null ? $"I decided to take up being the {role.Name}." : $"I decided to stop being the {old} and be the {role.Name} instead.",
                tags: new[] { "role" });
        }

        /// <summary>How many living people hold this role.</summary>
        public int HoldersOf(string role) =>
            role == null ? 0 : residents.Count(o => o.IsAlive && string.Equals(o.Role, role, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Owner, 6 November: whether every place in a limited role (<see cref="RoleOption.Max"/>) is taken by
        /// someone other than <paramref name="except"/>, so nobody else may take it up.
        /// </summary>
        public bool RoleIsFull(RoleOption role, Resident except = null) =>
            role != null && role.Max > 0
            && residents.Count(o => o.IsAlive && o != except && string.Equals(o.Role, role.Name, StringComparison.OrdinalIgnoreCase)) >= role.Max;

        /// <summary>What a day plan answers to have no role.</summary>
        public const string RoleNone = "none";

        /// <summary>A resident is ready to reflect from <see cref="ReflectionFromMinute"/> until a new day begins (<see cref="DayPlan.DayStartHour"/>), when not in a chat, if they haven't reflected on today yet.</summary>
        public bool IsReflectionDue(Resident r) =>
            r != null && r.IsAlive && r.State != ResidentState.Talking
            && (Time.MinuteOfDay >= ReflectionFromMinute || Time.Hour < DayPlan.DayStartHour)
            && ReflectionDayFor(Time) > r.LastReflectionDay;

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

            // The AI rated this one as it wrote it.
            memory.ImportanceRated = true;

            r.LastReflectionDay = day;
            return DecisionOutcome.Applied;
        }

        /// <summary>
        /// Everything a resident could do with free time right now: <see cref="ActivityPlanner.FreeTimeOptions"/> plus
        /// going to chat with anyone who can be approached, with relaxing last. AI decisions must pick one of these.
        /// </summary>
        public IReadOnlyList<FreeTimeOption> FreeTimeOptionsFor(Resident r)
        {
            var options = Planner.FreeTimeOptions(r).ToList();
            var relax = options[options.Count - 1];
            options.RemoveAt(options.Count - 1);
            options.AddRange(ChatPartners(r).Take(SocialRules.MaxChatOptions).Select(p => new FreeTimeOption(p.partner, p.distance)));
            options.Add(relax);
            return options;
        }

        /// <summary>Applies an AI-written conversation line, after checking it is for the turn the conversation is waiting on.</summary>
        public DecisionOutcome ApplyConversationLine(string conversationId, long lineRequestId, string speakerId, string text, string tone, string topic, bool wantsToEnd,
            int giveFood = 0, bool acceptOffer = false, int taxPercent = 0, bool payTax = false,
            string giveItem = null, int giveAmount = 0)
        {
            var session = GetConversation(conversationId);
            if (session == null || !session.AwaitingLine || session.LineRequestId != lineRequestId || lineRequestId <= 0 || session.SpeakerId != speakerId)
            {
                return DecisionOutcome.Stale;
            }

            var speaker = GetResident(speakerId);
            if (speaker == null || !speaker.IsAlive)
            {
                return DecisionOutcome.NotAlive;
            }

            var clean = Social.PrivateTraits.WithoutTraitTalk(TextRules.Clean(text, ConversationLine.MaxTextLength), residents);
            if (clean.Length == 0)
            {
                return DecisionOutcome.InvalidText;
            }

            if (!Enum.TryParse(tone ?? string.Empty, true, out ConversationTone parsedTone) || !Enum.IsDefined(typeof(ConversationTone), parsedTone))
            {
                parsedTone = ConversationTone.Neutral;
            }

            ApplyFoodInChat(session, speaker, giveFood, acceptOffer);
            ApplyTaxInChat(session, speaker, taxPercent, payTax);
            ApplyGoodsInChat(session, speaker, giveItem, giveAmount);
            AddLine(session, clean, parsedTone, fromAi: true, SocialRules.CleanTopic(topic), wantsToEnd);
            return DecisionOutcome.Applied;
        }

        /// <summary>
        /// Gives up on an AI line that was late in real time or unusable, and asks for it again with a fresh request
        /// id (there is no built-in small talk). Returns false if the line was already said or the request is stale.
        /// </summary>
        public bool AskForLineAgain(string conversationId, long lineRequestId)
        {
            var session = GetConversation(conversationId);
            if (session == null || !session.AwaitingLine || session.LineRequestId != lineRequestId || lineRequestId <= 0)
            {
                return false;
            }

            session.LineRequestId = nextLineRequestId++;
            return true;
        }

        private void StartNextActivity(Resident r)
        {
            if (AiDecisionsEnabled && TryFollowPlan(r))
            {
                return;
            }

            // Wait for the AI, however long it takes.
            r.ResetActivity();
            r.State = ResidentState.Deciding;
            r.PendingDecisionId = nextDecisionId++;
            r.Goal = "Thinking about what to do";
        }

        internal void StartChoice(Resident r, PlanChoice choice, string sourceNote = null)
        {
            if (choice == null)
            {
                r.ResetActivity();
                Relax(r, "Taking it easy");
                return;
            }

            if (choice.TargetResident != null)
            {
                r.ActivityId = ActivityCatalog.Chat.Id;
                r.TargetResidentId = choice.TargetResident.Id;
                r.Goal = choice.Reason;
                r.State = ResidentState.Travelling;
                r.MinutesRemaining = SocialRules.SeekMinutes;
                r.AddLog(Time, $"Went to find {choice.TargetResident.FirstName} for a chat: {choice.Reason}{(sourceNote == null ? string.Empty : " " + sourceNote)}");
                return;
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

        /// <summary>People this resident could walk over to for a chat now: nearest first (ties by id).</summary>
        private IEnumerable<(Resident partner, int distance)> ChatPartners(Resident r)
        {
            return residents
                .Where(o => o != r && FreeToTalk(o) && SocialRules.CooldownOver(Relationships, r.Id, o.Id, Time))
                .Select(o => (partner: o, distance: Pathfinder.Distance(r.Location, o.Location)))
                .Where(p => p.distance != Pathfinder.Unreachable && p.distance <= SocialRules.MaxSeekDistance)
                .OrderBy(p => p.distance)
                .ThenBy(p => p.partner.Id, StringComparer.Ordinal)
                .ToList();
        }

        private static bool Near(Location a, Location b, int tiles) =>
            a.MapId == b.MapId && Math.Abs(a.Position.X - b.Position.X) + Math.Abs(a.Position.Y - b.Position.Y) <= tiles;

        /// <summary>One step of walking over to someone. Starts the chat on arrival, or gives up if they become unavailable.</summary>
        private void SeekPartner(Resident r)
        {
            var other = GetResident(r.TargetResidentId);
            r.MinutesRemaining--;
            if (other == null || !FreeToTalk(other) || r.MinutesRemaining <= 0)
            {
                r.AddLog(Time, other != null && other.State == ResidentState.Talking
                    ? $"Went to find {other.FirstName}, but they'd got talking with someone else"
                    : $"Couldn't catch {other?.FirstName ?? "them"} for a chat");
                r.ResetActivity();
                return;
            }

            if (Near(r.Location, other.Location, 1))
            {
                StartConversation(r, other);

                return;
            }

            if (Pathfinder.TryGetNextStep(r.Location, other.Location, out var next))
            {
                if (next.MapId != r.Location.MapId)
                {
                    r.AddLog(Time, $"Went {(World.GetMap(next.MapId).IsExterior ? "outside" : "inside")} to {World.GetMap(next.MapId).Name}");
                }

                r.Location = next;
                return;
            }

            r.AddLog(Time, $"Couldn't find a way to {other.FirstName}");
            r.ResetActivity();
        }

        internal void StartConversation(Resident initiator, Resident partner)
        {
            var ab = Relationships.Get(initiator.Id, partner.Id);
            var ba = Relationships.Get(partner.Id, initiator.Id);
            var map = World.GetMap(initiator.Location.MapId);
            var place = World.Objects.FirstOrDefault(o => Near(o.Location, initiator.Location, 1))?.Name ?? "open land";
            var session = new ConversationSession(Conversations.ReserveId(), initiator.Id, partner.Id, Time, SocialRules.LinesFor(initiator, partner, ab, ba), place);
            sessions.Add(session);
            foreach (var (me, other) in new[] { (initiator, partner), (partner, initiator) })
            {
                me.ResetActivity();
                me.State = ResidentState.Talking;
                me.ActivityId = ActivityCatalog.Chat.Id;
                me.ConversationId = session.Id;
                me.Goal = $"Chatting with {other.FirstName}";
                me.AddLog(Time, $"Started chatting with {other.FullName} at the {place.ToLowerInvariant()}");
            }
        }

        /// <summary>Moves each conversation along: asks the AI for the next line when it is due.</summary>
        private void StepConversations()
        {
            foreach (var session in sessions.ToList())
            {
                if (Time.TotalMinutes - session.Started.TotalMinutes - session.WaitedMinutes >= SocialRules.MaxConversationMinutes)
                {
                    EndConversation(session, null, "they'd been talking for ages");
                    continue;
                }

                if (session.AwaitingLine)
                {
                    session.WaitedMinutes++;
                    continue;
                }

                if (Time.TotalMinutes < session.NextLineAt)
                {
                    continue;
                }

                session.AwaitingLine = true;
                session.LineRequestId = nextLineRequestId++;
            }
        }

        private void AddLine(ConversationSession session, string text, ConversationTone tone, bool fromAi, string topic, bool wantsToEnd)
        {
            session.Lines.Add(new ConversationLine(session.SpeakerId, Time, text, tone, fromAi));
            session.AwaitingLine = false;
            session.LineRequestId = 0;
            session.NextLineAt = Time.TotalMinutes + SocialRules.MinutesPerLine;
            if (!string.IsNullOrEmpty(topic))
            {
                session.Topic = topic;
            }

            if (session.Lines.Count >= session.MaxLines || (wantsToEnd && session.Lines.Count >= SocialRules.MinLines))
            {
                EndConversation(session, null, null);
            }
        }

        /// <summary>
        /// Ends a conversation: records it in the history, updates both relationships by the game's rules, makes
        /// shared memories of anything significant, and frees both residents.
        /// </summary>
        private void EndConversation(ConversationSession session, Resident leaver, string reason)
        {
            sessions.Remove(session);
            var a = GetResident(session.ParticipantIds[0]);
            var b = GetResident(session.ParticipantIds[1]);
            var ab = Relationships.Get(a.Id, b.Id);
            var ba = Relationships.Get(b.Id, a.Id);
            var firstMeeting = !ab.HaveMet;
            var friendshipBefore = new[] { ab.Friendship, ba.Friendship };

            if (session.Lines.Count > 0)
            {
                Conversations.AddChat(session.Id, session.Started, session.ParticipantIds, string.IsNullOrEmpty(session.Topic) ? session.Place : session.Topic, session.Lines);
                SocialRules.ApplyConversation(ab, ba, session.Lines, Time);
                RecordSocialHistory(session, a, b, firstMeeting, friendshipBefore);
            }

            var about = string.IsNullOrEmpty(session.Topic) ? string.Empty : $" about {session.Topic.ToLowerInvariant()}";
            var views = new[] { (me: a, other: b, view: ab, before: friendshipBefore[0]), (me: b, other: a, view: ba, before: friendshipBefore[1]) };
            foreach (var (me, other, view, before) in views)
            {
                string summary = null;
                var importance = 0;
                if (firstMeeting && session.Lines.Count > 0)
                {
                    summary = $"I met {other.FullName} for the first time at the {session.Place.ToLowerInvariant()}{(about.Length > 0 ? " and we talked" + about : string.Empty)}.";
                    importance = 5;
                }
                else if (SocialRules.HadDisagreement(session.Lines))
                {
                    summary = $"{other.FirstName} and I didn't see eye to eye{about}.";
                    importance = 4;
                }
                else if (before < SocialRules.GoodFriendsAt && view.Friendship >= SocialRules.GoodFriendsAt)
                {
                    summary = $"{other.FirstName} and I are becoming good friends.";
                    importance = 6;
                }

                if (summary != null)
                {
                    Memories.Add(me.Id, Time, MemoryType.Event, importance, summary, new[] { other.Id }, new[] { other.Id, session.Id, "chat", "social" });
                }

                me.ResetActivity();
                me.AddLog(Time, leaver == null
                    ? $"Finished chatting with {other.FirstName}"
                    : leaver == me ? $"Left the chat with {other.FirstName}: {reason}" : $"{other.FirstName} had to go: {reason}");
            }
        }

        /// <summary>First meetings and new friendships go in the town history, once each way the pair crosses the line.</summary>
        private void RecordSocialHistory(ConversationSession session, Resident a, Resident b, bool firstMeeting, double[] friendshipBefore)
        {
            var pair = new[] { a.Id, b.Id };
            if (firstMeeting)
            {
                History.Add(Time, TownEventKind.Met, $"{a.FullName} and {b.FullName} met for the first time at the {session.Place.ToLowerInvariant()}.", pair);
            }

            var wereFriends = friendshipBefore[0] >= SocialRules.GoodFriendsAt && friendshipBefore[1] >= SocialRules.GoodFriendsAt;
            var nowFriends = Relationships.Get(a.Id, b.Id).Friendship >= SocialRules.GoodFriendsAt && Relationships.Get(b.Id, a.Id).Friendship >= SocialRules.GoodFriendsAt;
            if (nowFriends && !wereFriends && !History.Events.Any(e => e.Kind == TownEventKind.Friends && e.ResidentIds.Contains(a.Id) && e.ResidentIds.Contains(b.Id)))
            {
                History.Add(Time, TownEventKind.Friends, $"{a.FullName} and {b.FullName} became good friends.", pair);
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
            if (FoodRules.IsMadeFood(activity) && !StartFood(r, activity, out var noFood))
            {
                r.AddLog(Time, $"Couldn't {activity.Name.ToLowerInvariant()}: {noFood}");
                r.ResetActivity();
                return;
            }

            if (activity == ActivityCatalog.DrinkCleanWater || activity == ActivityCatalog.WashWithCleanWater)
            {
                if (r.CleanWater > 0)
                {
                    r.CleanWater--;
                }
                else if (Food.MayTakeFromStore(r) && Food.Store.Water > 0)
                {
                    Food.Store.Water--;
                }
                else
                {
                    r.AddLog(Time, $"Couldn't {activity.Name.ToLowerInvariant()}: there was no clean water left");
                    r.ResetActivity();
                    return;
                }
            }

            if (activity == ActivityCatalog.PlanBuilding)
            {
                StartSketch(r);
            }

            r.State = ResidentState.Performing;
            r.MinutesRemaining = activity.DurationMinutes;
            r.AddLog(Time, $"Started {activity.Verb}");
        }
    }
}
