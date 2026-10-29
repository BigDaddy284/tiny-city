using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.History
{
    /// <summary>Where a timeline entry came from, so the game's records and the AI's words are never confused.</summary>
    public enum TimelineSource
    {
        /// <summary>The village records (<see cref="TownHistory"/>): game fact.</summary>
        Town,

        /// <summary>One of the resident's own memories recorded by the game.</summary>
        Memory,

        /// <summary>An end-of-day reflection the AI wrote for them.</summary>
        AiReflection,
    }

    public sealed class TimelineEntry
    {
        public TimelineEntry(GameTime time, string text, TimelineSource source)
        {
            Time = time;
            Text = text;
            Source = source;
        }

        public GameTime Time { get; }

        public string Text { get; }

        public TimelineSource Source { get; }
    }

    /// <summary>A resident's timeline, their written-up life story, and the town's history as text for exporting.</summary>
    public static class LifeStory
    {
        /// <summary>Memories at least this important join the village records on a resident's timeline.</summary>
        public const int TimelineImportance = 6;

        /// <summary>
        /// The resident's life in order: everything the village records say about them, plus their important memories
        /// that aren't already there (conversations, falling behind on rent, reflections). Oldest first.
        /// </summary>
        public static List<TimelineEntry> Timeline(TownSimulation sim, string residentId)
        {
            var entries = sim.History.About(residentId)
                .Select(e => new TimelineEntry(e.Time, e.Summary, TimelineSource.Town))
                .ToList();

            // Town events already cover birthdays, illness and life stages; memories add the resident's own side.
            foreach (var m in sim.Memories.ForResident(residentId))
            {
                if (m.Type == MemoryType.Reflection)
                {
                    entries.Add(new TimelineEntry(m.Time, m.Summary, TimelineSource.AiReflection));
                }
                else if (m.Importance >= TimelineImportance && !IsCoveredByTown(m))
                {
                    entries.Add(new TimelineEntry(m.Time, m.Summary, TimelineSource.Memory));
                }
            }

            return entries.OrderBy(e => e.Time.TotalMinutes).ThenBy(e => e.Source).ToList();
        }

        /// <summary>A plain-text life story of one resident, for saving to a file.</summary>
        public static string Write(TownSimulation sim, string residentId)
        {
            var r = sim.GetResident(residentId);
            if (r == null)
            {
                return string.Empty;
            }

            var text = new StringBuilder();
            text.AppendLine($"The life of {r.FullName}");
            text.AppendLine(new string('=', r.FullName.Length + 12));
            text.AppendLine($"Written on {Journal.DateText(sim.Time)} in Tiny City.");
            text.AppendLine();
            text.AppendLine(r.IsAlive
                ? $"{r.FullName} is {r.AgeYears}, {Words(r.LifeStage)}, with a birthday on day {HealthRules.BirthdayCycleDay(r)} of each {HealthRules.DaysPerYear}-day year."
                : $"{r.FullName} died on {Journal.DateText(r.DiedAt.Value)}, of {r.CauseOfDeath}, aged {r.AgeYears}.");
            text.AppendLine($"Personality: {r.Personality.Describe()}.");
            text.AppendLine(r.Job != null ? $"Work: {r.Job.Title}, {r.Job.DaysText()}." : $"Work: none{(string.IsNullOrEmpty(r.IncomeLabel) ? string.Empty : $" ({r.IncomeLabel.ToLowerInvariant()})")}.");
            text.AppendLine($"Hobbies: {(r.Hobbies.Count > 0 ? string.Join(", ", r.Hobbies) : "none")}.");
            var partnership = sim.Relationships.PartnershipOf(r.Id);
            var partner = partnership == null ? null : sim.GetResident(partnership.OtherThan(r.Id));
            if (partner != null)
            {
                text.AppendLine($"{(partnership.Stage == RomanceStage.Committed ? "Partner" : "Dating")}: {partner.FullName}, since day {partnership.Since.Day}.");
            }

            var friends = sim.Residents
                .Where(o => o != r && sim.Relationships.TryGet(r.Id, o.Id, out var rel) && rel.HaveMet && rel.Friendship >= SocialRules.GoodFriendsAt)
                .OrderByDescending(o => sim.Relationships.Get(r.Id, o.Id).Friendship)
                .Select(o => o.FirstName)
                .ToList();
            text.AppendLine($"Good friends: {(friends.Count > 0 ? string.Join(", ", friends) : "none yet")}.");
            text.AppendLine();
            text.AppendLine("Timeline");
            text.AppendLine("--------");
            text.AppendLine("Lines marked [memory] are their own memories; [AI] marks a reflection the AI wrote for them. Everything else is from the city records.");
            var timeline = Timeline(sim, residentId);
            if (timeline.Count == 0)
            {
                text.AppendLine("Nothing recorded yet.");
            }

            foreach (var entry in timeline)
            {
                var mark = entry.Source == TimelineSource.AiReflection ? " [AI]" : entry.Source == TimelineSource.Memory ? " [memory]" : string.Empty;
                text.AppendLine($"{Journal.DateText(entry.Time)}{mark}: {entry.Text}");
            }

            return text.ToString();
        }

        /// <summary>The whole town history, oldest first, as plain text.</summary>
        public static string WriteTownHistory(TownSimulation sim)
        {
            var text = new StringBuilder();
            text.AppendLine("A history of Tiny City");
            text.AppendLine("==========================");
            text.AppendLine($"Written on {Journal.DateText(sim.Time)}.");
            text.AppendLine();
            text.AppendLine("Residents");
            text.AppendLine("---------");
            foreach (var r in sim.Residents)
            {
                text.AppendLine(r.IsAlive
                    ? $"{r.FullName}, {r.AgeYears}{(r.Job != null ? ", " + r.Job.Title.ToLowerInvariant() : string.Empty)}"
                    : $"{r.FullName}, died on day {r.DiedAt?.Day} aged {r.AgeYears}");
            }

            text.AppendLine();
            text.AppendLine("Events");
            text.AppendLine("------");
            if (sim.History.Events.Count == 0)
            {
                text.AppendLine("Nothing has happened yet.");
            }

            var day = 0;
            foreach (var e in sim.History.Events)
            {
                if (e.Time.Day != day)
                {
                    day = e.Time.Day;
                    text.AppendLine($"Day {day}, {e.Time.Weekday}");
                }

                text.AppendLine($"  {e.Time.ClockText}  {e.Summary}");
            }

            return text.ToString();
        }

        private static bool IsCoveredByTown(MemoryRecord m) =>
            m.Tags.Any(t => t == "birthday" || t == "health" || t == "life-stage");

        private static string Words(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.YoungAdult:
                    return "a young adult";
                case LifeStage.MiddleAged:
                    return "middle-aged";
                case LifeStage.Elderly:
                    return "elderly";
                default:
                    return "an adult";
            }
        }
    }
}
