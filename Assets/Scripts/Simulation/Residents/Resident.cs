using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Residents
{
    public enum ResidentState
    {
        Idle,
        Travelling,
        Performing,
        Waiting,

        /// <summary>Waiting a short while for an AI suggestion about free time. Never saved as such: see the save loader.</summary>
        Deciding,

        /// <summary>In a conversation; see <see cref="Resident.ConversationId"/>.</summary>
        Talking,
    }

    /// <summary>Who chose the current activity, so the inspector can keep game rules and AI explanations apart.</summary>
    public enum DecisionSource
    {
        Routine,
        Ai,
    }

    public enum LifeStage
    {
        YoungAdult,
        Adult,
        MiddleAged,
        Elderly,
    }

    public sealed class ActivityLogEntry
    {
        public ActivityLogEntry(GameTime time, string text)
        {
            Time = time;
            Text = text ?? string.Empty;
        }

        public GameTime Time { get; }

        public string Text { get; }
    }

    public sealed class Resident
    {
        public const int MaxLogEntries = 40;

        private readonly List<ActivityLogEntry> log = new List<ActivityLogEntry>();
        private readonly List<PlanStep> plannedSteps = new List<PlanStep>();

        public Resident(
            string id,
            string firstName,
            string lastName,
            int ageYears,
            int birthdayDayOfYear,
            string colourHex,
            Personality personality,
            string homeMapId,
            string bedObjectId,
            Job job,
            Location location,
            Needs needs = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Resident id is required.", nameof(id));
            }

            Id = id;
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            AgeYears = Math.Max(18, ageYears);
            BirthdayDayOfYear = Math.Max(1, Math.Min(birthdayDayOfYear, 365));
            ColourHex = colourHex ?? "#C8643C";
            Personality = personality ?? new Personality(0.5, 0.5, 0.5, 0.5);
            HomeMapId = homeMapId;
            BedObjectId = bedObjectId;
            Job = job;
            Location = location;
            Needs = needs ?? new Needs();
            Health = TinyCity.Simulation.Health.HealthRules.StartingHealthFor(AgeYears);
        }

        public string Id { get; }

        public string FirstName { get; }

        public string LastName { get; }

        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>Goes up on each birthday; see <see cref="Health.HealthRules.DaysPerYear"/>.</summary>
        public int AgeYears { get; internal set; }

        public int BirthdayDayOfYear { get; }

        public LifeStage LifeStage => AgeYears >= 65 ? LifeStage.Elderly
            : AgeYears >= 45 ? LifeStage.MiddleAged
            : AgeYears >= 30 ? LifeStage.Adult
            : LifeStage.YoungAdult;

        public string ColourHex { get; }

        public Personality Personality { get; }

        /// <summary>The home they live in, or null for someone with nowhere to live; see <see cref="IsHomeless"/>.</summary>
        public string HomeMapId { get; internal set; }

        /// <summary>Their bed at home, or null when homeless.</summary>
        public string BedObjectId { get; internal set; }

        /// <summary>Nowhere to live: they sleep on a park bench and get by on what's free.</summary>
        public bool IsHomeless => string.IsNullOrEmpty(HomeMapId);

        /// <summary>Null for the retired and the unemployed. Replaced only by game logic, e.g. upgrading an old save.</summary>
        public Job Job { get; internal set; }

        /// <summary>Money in pence. Can go below zero when rent is due and there isn't enough; purchases can't.</summary>
        public long MoneyPence { get; internal set; }

        /// <summary>Meals' worth of food at home. Cooking uses one; without any, a resident can only snack.</summary>
        public int Groceries { get; internal set; }

        /// <summary>Paid every Monday morning, in pence: rent for their home.</summary>
        public long WeeklyRentPence { get; internal set; }

        /// <summary>Income every Monday morning that isn't wages, e.g. a pension, in pence.</summary>
        public long WeeklyIncomePence { get; internal set; }

        /// <summary>What <see cref="WeeklyIncomePence"/> is called, e.g. "Pension".</summary>
        public string IncomeLabel { get; internal set; } = string.Empty;

        /// <summary>Minutes worked since wages were last paid; paid when the shift ends.</summary>
        public int UnpaidWorkMinutes { get; internal set; }

        public bool IsAlive { get; internal set; } = true;

        /// <summary>When they died, for the record; null while alive.</summary>
        public GameTime? DiedAt { get; internal set; }

        /// <summary>What they died of, e.g. "old age"; empty while alive.</summary>
        public string CauseOfDeath { get; internal set; } = string.Empty;

        /// <summary>0 (dying) to 100. Worn down by neglect and illness, slowly restored by good care and rest.</summary>
        public double Health { get; internal set; } = 90;

        /// <summary>The current illness's id from <see cref="Health.IllnessCatalog"/>, or null when well.</summary>
        public string IllnessId { get; internal set; }

        /// <summary>Minutes of recovery left; resting counts double.</summary>
        public int IllnessMinutesLeft { get; internal set; }

        /// <summary>Whether the clinic has treated a serious illness, which lets recovery begin.</summary>
        public bool IllnessTreated { get; internal set; }

        /// <summary>The last day they chatted with someone contagious, or 0.</summary>
        public int ExposedDay { get; internal set; }

        /// <summary>The last day they called in sick, so it's only said once a day.</summary>
        public int SickDay { get; internal set; }

        private List<string> hobbies = new List<string>();

        /// <summary>Hobby ids such as "chess", from <see cref="Activities.ActivityDefinition.Hobby"/>.</summary>
        public IReadOnlyList<string> Hobbies => hobbies;

        internal void SetHobbies(IEnumerable<string> ids) => hobbies = (ids ?? new string[0]).Where(h => !string.IsNullOrWhiteSpace(h)).Distinct().Take(MaxHobbies).ToList();

        public const int MaxHobbies = 4;

        /// <summary>The day they moved to the city from out of town, or 0 for someone who has always lived here.</summary>
        public int MovedInDay { get; internal set; }

        public bool IsNewcomer => MovedInDay > 0;

        /// <summary>Whether they're open to romance at all. Some residents simply aren't looking.</summary>
        public bool OpenToRomance { get; internal set; } = true;

        public Needs Needs { get; }

        public Location Location { get; internal set; }

        public ResidentState State { get; internal set; } = ResidentState.Idle;

        /// <summary>The activity being travelled to or performed, or null when idle.</summary>
        public string ActivityId { get; internal set; }

        public string TargetObjectId { get; internal set; }

        /// <summary>The resident being walked to for a chat, or null.</summary>
        public string TargetResidentId { get; internal set; }

        /// <summary>The live conversation while <see cref="ResidentState.Talking"/>, or null.</summary>
        public string ConversationId { get; internal set; }

        /// <summary>Minutes left of the current activity, or of waiting.</summary>
        public int MinutesRemaining { get; internal set; }

        /// <summary>Why the resident chose their current activity, for the inspector.</summary>
        public string Goal { get; internal set; } = string.Empty;

        /// <summary>Whether <see cref="Goal"/> is the built-in routine's reason or the AI's explanation.</summary>
        public DecisionSource GoalSource { get; internal set; } = DecisionSource.Routine;

        /// <summary>While <see cref="ResidentState.Deciding"/>, the id an AI answer must quote to be accepted.</summary>
        public long PendingDecisionId { get; internal set; }

        /// <summary>While deciding, the game minute at which the built-in routine takes over.</summary>
        public long DecisionDeadline { get; internal set; }

        /// <summary>
        /// The rest of the resident's AI plan for free time, in order. It lives only while the game runs: like an
        /// unanswered decision it is not saved, so after loading the resident simply asks again.
        /// </summary>
        public IReadOnlyList<PlanStep> PlannedSteps => plannedSteps;

        /// <summary>The game minute after which the rest of the plan is dropped.</summary>
        public long PlanExpires { get; internal set; }

        /// <summary>How many planned steps the resident has carried out since the game started (for diagnostics, not saved).</summary>
        public int PlanStepsTaken { get; internal set; }

        /// <summary>The last day the resident reflected on, so each day gets at most one reflection.</summary>
        public int LastReflectionDay { get; internal set; }

        public IReadOnlyList<ActivityLogEntry> Log => log;

        internal void AddLog(GameTime time, string text)
        {
            log.Add(new ActivityLogEntry(time, text));
            if (log.Count > MaxLogEntries)
            {
                log.RemoveRange(0, log.Count - MaxLogEntries);
            }
        }

        internal void SetPlan(IEnumerable<PlanStep> steps, long expires)
        {
            plannedSteps.Clear();
            plannedSteps.AddRange(steps);
            PlanExpires = expires;
        }

        internal PlanStep TakePlannedStep()
        {
            var step = plannedSteps[0];
            plannedSteps.RemoveAt(0);
            return step;
        }

        internal void ClearPlan() => plannedSteps.Clear();

        internal void ResetActivity()
        {
            State = ResidentState.Idle;
            ActivityId = null;
            TargetObjectId = null;
            TargetResidentId = null;
            ConversationId = null;
            MinutesRemaining = 0;
            Goal = string.Empty;
            GoalSource = DecisionSource.Routine;
            PendingDecisionId = 0;
            DecisionDeadline = 0;
        }
    }
}
