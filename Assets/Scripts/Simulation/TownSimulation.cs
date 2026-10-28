using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Economy;
using TinyTown.Simulation.Health;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Social;
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
    public sealed partial class TownSimulation
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
        private readonly List<ConversationSession> sessions = new List<ConversationSession>();
        private int decisionWaitMinutes = DefaultDecisionWaitMinutes;
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
            Pathfinder = new Pathfinder(world);
            Economy = new TownEconomy(world);
            Planner = new ActivityPlanner(world, Pathfinder, this.residents, Economy, () => Time);
        }

        public TownWorld World { get; }

        public GameTime Time { get; private set; }

        public IReadOnlyList<Resident> Residents => residents;

        public Pathfinder Pathfinder { get; }

        public ActivityPlanner Planner { get; }

        public MemoryStore Memories { get; } = new MemoryStore();

        public ConversationHistory Conversations { get; } = new ConversationHistory();

        public RelationshipBook Relationships { get; } = new RelationshipBook();

        /// <summary>Money, businesses and the ledger.</summary>
        public TownEconomy Economy { get; }

        /// <summary>Conversations happening right now.</summary>
        public IReadOnlyList<ConversationSession> ActiveConversations => sessions;

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

        public ConversationSession GetConversation(string id) => sessions.FirstOrDefault(c => c.Id == id);

        /// <summary>Adds a resident to a running town, e.g. a newcomer missing from an older save.</summary>
        public void AddResident(Resident resident)
        {
            if (resident == null || GetResident(resident.Id) != null)
            {
                throw new ArgumentException("Residents need a unique id.");
            }

            if (!World.IsWalkable(resident.Location))
            {
                throw new ArgumentException($"Resident '{resident.Id}' is not on a walkable tile.");
            }

            residents.Add(resident);
        }

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
            Economy.RestockOpening(Time);
            if (Time.Weekday == Weekday.Monday && Time.MinuteOfDay == TownEconomy.WeeklyBillsMinute)
            {
                SettleWeek();
            }

            StepLifeEvents();
            StepRomanceDaily();
            StepCommunityEvents();
            foreach (var resident in residents)
            {
                if (resident.IsAlive)
                {
                    Step(resident);
                }
            }

            StepConversations();
            StartChanceMeetings();
            Time = Time.AddMinutes(1);
        }

        private void Step(Resident r)
        {
            ActivityCatalog.TryGet(r.ActivityId, out var activity);
            var working = r.State == ResidentState.Performing && activity?.Kind == ActivityKind.Work;
            if (working)
            {
                r.UnpaidWorkMinutes++;
            }
            else if (r.UnpaidWorkMinutes > 0)
            {
                // The shift has ended, however it ended: pay for the time worked.
                var paid = Economy.PayWages(r, Time);
                if (paid > 0)
                {
                    r.AddLog(Time, $"Was paid {Money.Format(paid)} in wages");
                }
            }

            NeedRules.ApplyMinute(r.Needs, r.Personality, r.State == ResidentState.Performing || r.State == ResidentState.Talking ? activity : null, IllnessCatalog.Get(r.IllnessId));
            StepHealth(r, activity);
            if (!r.IsAlive)
            {
                return;
            }

            StepSharedHobby(r, activity);

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
                    else if (Planner.ShouldInterrupt(r, activity, Time))
                    {
                        r.AddLog(Time, $"Stopped {activity.Verb}: something more important came up");
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
                    if (leaveReason != null)
                    {
                        EndConversation(conversation, r, leaveReason);
                    }

                    break;

                case ResidentState.Travelling when r.TargetResidentId != null:
                    if (Planner.ShouldInterrupt(r, activity, Time))
                    {
                        r.AddLog(Time, "Changed plans on the way to a chat");
                        r.ResetActivity();
                        break;
                    }

                    SeekPartner(r);
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
                    NoteSickDay(r);
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

            var options = FreeTimeOptionsFor(r);
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
            else if (option.Partner != null)
            {
                StartChoice(r, new PlanChoice(option.Partner, why, PlanPriority.Leisure), "(AI)");
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
        public DecisionOutcome ApplyConversationLine(string conversationId, long lineRequestId, string speakerId, string text, string tone, string topic, bool wantsToEnd)
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

            var clean = TextRules.Clean(text, ConversationLine.MaxTextLength);
            if (clean.Length == 0)
            {
                return DecisionOutcome.InvalidText;
            }

            if (!Enum.TryParse(tone ?? string.Empty, true, out ConversationTone parsedTone) || !Enum.IsDefined(typeof(ConversationTone), parsedTone))
            {
                parsedTone = ConversationTone.Neutral;
            }

            AddLine(session, clean, parsedTone, fromAi: true, SocialRules.CleanTopic(topic), wantsToEnd);
            return DecisionOutcome.Applied;
        }

        /// <summary>
        /// Gives up on the AI for this line and uses built-in small talk now. Called by the AI service when an answer
        /// is late in real time or unusable. Returns false if the line was already said or the request is stale.
        /// </summary>
        public bool UseSmallTalk(string conversationId, long lineRequestId)
        {
            var session = GetConversation(conversationId);
            if (session == null || !session.AwaitingLine || session.LineRequestId != lineRequestId || lineRequestId <= 0)
            {
                return false;
            }

            AddSmallTalk(session);
            return true;
        }

        private void StartNextActivity(Resident r)
        {
            var choice = Planner.Choose(r, Time);
            var lonely = r.Needs[NeedType.Social] < NeedRules.ComfortThreshold(NeedType.Social, r.Personality);
            var chatty = r.Personality.Sociable >= SocialRules.ChattyPersonality && choice?.Priority == PlanPriority.Leisure && choice.Activity != ActivityCatalog.AttendEvent;
            if ((choice == null || (choice.IsDiscretionary && choice.Activity.PrimaryNeed == NeedType.Social) || chatty)
                && (lonely || chatty || choice == null))
            {
                var partner = ChatPartners(r).FirstOrDefault();
                if (partner.partner != null)
                {
                    choice = lonely
                        ? new PlanChoice(partner.partner, $"Social is getting low ({r.Needs[NeedType.Social]:0})", PlanPriority.Comfort)
                        : new PlanChoice(partner.partner, $"Free time: fancies a chat with {partner.partner.FirstName}", PlanPriority.Leisure);
                }
            }

            if (AiDecisionsEnabled && (choice == null || choice.IsDiscretionary) && FreeTimeOptionsFor(r).Count >= 2)
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

        internal void StartChoice(Resident r, PlanChoice choice, string sourceNote = null)
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

            if (choice.IsDiscretionary)
            {
                InviteToHobby(r, choice.Activity, choice.Target);
            }
        }

        private static void Relax(Resident r, string goal)
        {
            r.State = ResidentState.Waiting;
            r.MinutesRemaining = RelaxMinutes;
            r.Goal = goal;
        }

        /// <summary>People this resident could walk over to for a chat now: their partner first, then nearest first (ties by id).</summary>
        private IEnumerable<(Resident partner, int distance)> ChatPartners(Resident r)
        {
            var sweetheart = Relationships.PartnerOf(r.Id);
            if (!SocialRules.CanJoinConversation(r, Time) && r.State != ResidentState.Idle && r.State != ResidentState.Deciding)
            {
                return Enumerable.Empty<(Resident, int)>();
            }

            return residents
                .Where(o => o != r && SocialRules.CanBeApproached(o, Time, IsOutdoors(o)) && !IsHeadingIndoors(o) && SocialRules.CooldownOver(Relationships, r.Id, o.Id, Time))
                .Select(o => (partner: o, distance: Pathfinder.Distance(r.Location, o.Location)))
                .Where(p => p.distance != Pathfinder.Unreachable && p.distance <= SocialRules.MaxSeekDistance)
                .OrderBy(p => p.partner.Id == sweetheart ? 0 : 1)
                .ThenBy(p => p.distance)
                .ThenBy(p => p.partner.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Someone walking to a place indoors will be gone before they can be caught, so nobody sets off after them.</summary>
        private bool IsHeadingIndoors(Resident r) =>
            r.State == ResidentState.Travelling && World.TryGetObject(r.TargetObjectId, out var target)
            && World.TryGetMap(target.Location.MapId, out var map) && !map.IsExterior;

        private bool IsOutdoors(Resident r) => World.TryGetMap(r.Location.MapId, out var map) && map.IsExterior;

        private static bool Near(Location a, Location b, int tiles) =>
            a.MapId == b.MapId && Math.Abs(a.Position.X - b.Position.X) + Math.Abs(a.Position.Y - b.Position.Y) <= tiles;

        /// <summary>One step of walking over to someone. Starts the chat on arrival, or gives up if they become unavailable.</summary>
        private void SeekPartner(Resident r)
        {
            var other = GetResident(r.TargetResidentId);
            r.MinutesRemaining--;
            if (other == null || !SocialRules.CanBeApproached(other, Time, IsOutdoors(other)) || r.MinutesRemaining <= 0)
            {
                r.AddLog(Time, other != null && other.State == ResidentState.Talking
                    ? $"Went to find {other.FirstName}, but they'd got talking with someone else"
                    : $"Couldn't catch {other?.FirstName ?? "them"} for a chat");
                r.ResetActivity();
                return;
            }

            if (Near(r.Location, other.Location, 1))
            {
                if (SocialRules.CanJoinConversation(other, Time))
                {
                    StartConversation(r, other);
                }

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

        /// <summary>Residents who pass close by while both are free may stop for a chat, if either could do with company.</summary>
        private void StartChanceMeetings()
        {
            for (var i = 0; i < residents.Count; i++)
            {
                for (var j = i + 1; j < residents.Count; j++)
                {
                    var a = residents[i];
                    var b = residents[j];
                    if (Near(a.Location, b.Location, SocialRules.MeetingDistance)
                        && (a.Needs[NeedType.Social] < SocialRules.MeetingSocialThreshold || b.Needs[NeedType.Social] < SocialRules.MeetingSocialThreshold)
                        && SocialRules.CanJoinConversation(a, Time) && SocialRules.CanJoinConversation(b, Time)
                        && SocialRules.CooldownOver(Relationships, a.Id, b.Id, Time))
                    {
                        var lonelier = a.Needs[NeedType.Social] <= b.Needs[NeedType.Social] ? a : b;
                        StartConversation(lonelier, lonelier == a ? b : a);
                    }
                }
            }
        }

        private void StartConversation(Resident initiator, Resident partner)
        {
            var ab = Relationships.Get(initiator.Id, partner.Id);
            var ba = Relationships.Get(partner.Id, initiator.Id);
            var map = World.GetMap(initiator.Location.MapId);
            var place = World.Objects.FirstOrDefault(o => Near(o.Location, initiator.Location, 1))?.Name ?? (map.IsExterior ? "village green" : map.Name);
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

        /// <summary>Moves each conversation along: asks for the next line, or uses small talk when the AI is off or late.</summary>
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
                    if (!AiDecisionsEnabled || Time.TotalMinutes >= session.LineDeadline)
                    {
                        AddSmallTalk(session);
                    }

                    continue;
                }

                if (Time.TotalMinutes < session.NextLineAt)
                {
                    continue;
                }

                if (AiDecisionsEnabled)
                {
                    session.AwaitingLine = true;
                    session.LineRequestId = nextLineRequestId++;
                    session.LineDeadline = Time.TotalMinutes + SocialRules.LineWaitMinutes;
                }
                else
                {
                    AddSmallTalk(session);
                }
            }
        }

        private void AddSmallTalk(ConversationSession session)
        {
            var speaker = GetResident(session.SpeakerId);
            var listener = GetResident(session.ListenerId);
            var firstMeeting = !Relationships.Get(speaker.Id, listener.Id).HaveMet;
            var number = int.TryParse(session.Id.Substring(session.Id.LastIndexOf('-') + 1), out var n) ? n : 0;
            var (text, tone) = SmallTalk.Line(speaker, listener, firstMeeting, number, session.Lines.Count, session.MaxLines, Time);
            AddLine(session, text, tone, fromAi: false, topic: null, wantsToEnd: false);
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
                NoteExposure(a, b);
                ApplyRomance(session, a, b, ab, ba);
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

        /// <summary>Monday morning: pensions and other income arrive, rent goes out. Falling behind is remembered.</summary>
        private void SettleWeek()
        {
            foreach (var r in residents.Where(r => r.IsAlive))
            {
                Economy.SettleWeek(r, Time);
                if (r.WeeklyRentPence > 0)
                {
                    r.AddLog(Time, $"Paid the weekly rent ({Money.Format(r.WeeklyRentPence)})");
                }

                if (r.MoneyPence < 0)
                {
                    Memories.Add(r.Id, Time, MemoryType.Event, 5, $"I couldn't cover this week's rent; I owe {Money.Format(-r.MoneyPence)}.", tags: new[] { "money", "rent" });
                }
            }
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
            if (activity.ItemId != null || activity.UsesGroceries || activity.NeedsOpenBusiness)
            {
                World.TryGetObject(r.TargetObjectId, out var target);
                var price = Economy.PriceOf(activity, target);
                if (!Economy.Pay(r, activity, target, Time, out var reason))
                {
                    r.AddLog(Time, $"Couldn't {activity.Name.ToLowerInvariant()}: {reason}");
                    r.ResetActivity();
                    return;
                }

                if (price > 0)
                {
                    r.AddLog(Time, $"Paid {Money.Format(price)} ({activity.Name.ToLowerInvariant()})");
                }
            }

            r.State = ResidentState.Performing;
            r.MinutesRemaining = ActivityPlanner.DurationFor(r, activity, Time);
            r.AddLog(Time, $"Started {activity.Verb}");
            if (activity == ActivityCatalog.AttendEvent)
            {
                ArriveAtEvent(r);
            }
        }
    }
}
