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
            Personality personality,
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
            AgeYears = Math.Max(0, ageYears);
            Personality = personality ?? new Personality(0.5, 0.5, 0.5, 0.5);
            Location = location;
            Needs = needs ?? new Needs();
            Health = TinyCity.Simulation.Health.HealthRules.StartingHealth;
        }

        public string Id { get; }

        public string FirstName { get; }

        public string LastName { get; }

        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>Their age, from the profile. Nobody ages in Tiny City 2.</summary>
        public int AgeYears { get; internal set; }

        public Personality Personality { get; }

        /// <summary>Tiny City 2: the food they have, grown, caught or cooked.</summary>
        public Economy.FoodStock Food { get; } = new Economy.FoodStock();

        /// <summary>Tiny City 2: portions of clean water, made by a water purifier (up to <see cref="Economy.FoodRules.MaxCleanWater"/>).</summary>
        public int CleanWater { get; internal set; }

        /// <summary>Tiny City 2: the materials they have gathered and the tools they have made.</summary>
        public Economy.Belongings Belongings { get; } = new Economy.Belongings();

        /// <summary>A few lines about who they are, from the player's profile (see <see cref="ResidentProfile"/>). Given to the AI as their background.</summary>
        public string About { get; internal set; } = string.Empty;

        /// <summary>
        /// Tiny City 2: the role they have chosen to take up from the world's <see cref="WorldBrief.Roles"/>, or null
        /// for none. Their own choice, made in their morning plan; it changes nothing they can do. Saved.
        /// </summary>
        public string Role { get; internal set; }

        /// <summary>
        /// Tiny City 2: the player's one-sentence personality for them from residents.json ("You live to help
        /// others..."), given to the AI in place of the trait summary when set. Saved.
        /// </summary>
        public string Outlook { get; internal set; } = string.Empty;

        public bool IsAlive { get; internal set; } = true;

        /// <summary>When they died, for the record; null while alive.</summary>
        public GameTime? DiedAt { get; internal set; }

        /// <summary>What they died of, e.g. "old age"; empty while alive.</summary>
        public string CauseOfDeath { get; internal set; } = string.Empty;

        /// <summary>0 (dying) to 100. Worn down by neglect and illness, slowly restored by good care and rest.</summary>
        public double Health { get; internal set; } = 90;

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


        /// <summary>
        /// The rest of the resident's AI plan for free time, in order. It lives only while the game runs: like an
        /// unanswered decision it is not saved, so after loading the resident simply asks again.
        /// </summary>
        public IReadOnlyList<PlanStep> PlannedSteps => plannedSteps;

        /// <summary>The game minute after which the rest of the plan is dropped.</summary>
        public long PlanExpires { get; internal set; }

        /// <summary>How many planned steps the resident has carried out since the game started (for diagnostics, not saved).</summary>
        public int PlanStepsTaken { get; internal set; }

        /// <summary>Tiny City 2: someone they noticed, while the AI is asked whether they go and talk (not saved).</summary>
        public Social.PendingReaction Reaction { get; internal set; }

        /// <summary>Tiny City 2: the building plan they are waiting on the AI for, or null. Not saved.</summary>
        public Building.PendingSketch Sketch { get; internal set; }

        /// <summary>Tiny City 2: today's plan, written by the AI when the resident starts their day (not saved).</summary>
        public DayPlan DayPlan { get; internal set; }

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
        }
    }
}
