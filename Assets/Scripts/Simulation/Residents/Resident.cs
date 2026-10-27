using System;
using System.Collections.Generic;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Residents
{
    public enum ResidentState
    {
        Idle,
        Travelling,
        Performing,
        Waiting,
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
        }

        public string Id { get; }

        public string FirstName { get; }

        public string LastName { get; }

        public string FullName => $"{FirstName} {LastName}".Trim();

        public int AgeYears { get; }

        public int BirthdayDayOfYear { get; }

        public LifeStage LifeStage => AgeYears >= 65 ? LifeStage.Elderly
            : AgeYears >= 45 ? LifeStage.MiddleAged
            : AgeYears >= 30 ? LifeStage.Adult
            : LifeStage.YoungAdult;

        public string ColourHex { get; }

        public Personality Personality { get; }

        public string HomeMapId { get; }

        public string BedObjectId { get; }

        public Job Job { get; }

        public bool IsAlive { get; internal set; } = true;

        public Needs Needs { get; }

        public Location Location { get; internal set; }

        public ResidentState State { get; internal set; } = ResidentState.Idle;

        /// <summary>The activity being travelled to or performed, or null when idle.</summary>
        public string ActivityId { get; internal set; }

        public string TargetObjectId { get; internal set; }

        /// <summary>Minutes left of the current activity, or of waiting.</summary>
        public int MinutesRemaining { get; internal set; }

        /// <summary>Why the resident chose their current activity, for the inspector.</summary>
        public string Goal { get; internal set; } = string.Empty;

        public IReadOnlyList<ActivityLogEntry> Log => log;

        internal void AddLog(GameTime time, string text)
        {
            log.Add(new ActivityLogEntry(time, text));
            if (log.Count > MaxLogEntries)
            {
                log.RemoveRange(0, log.Count - MaxLogEntries);
            }
        }

        internal void ResetActivity()
        {
            State = ResidentState.Idle;
            ActivityId = null;
            TargetObjectId = null;
            MinutesRemaining = 0;
            Goal = string.Empty;
        }
    }
}
