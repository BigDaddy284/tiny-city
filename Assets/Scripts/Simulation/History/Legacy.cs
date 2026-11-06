using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.History
{
    /// <summary>
    /// Tiny City 2 (owner, 5 November): when everyone in a world has died, a log of what happened is written for the
    /// next world to read and learn from. <see cref="Write"/> makes the whole log as plain text; its
    /// <see cref="LessonsHeading"/> section holds the short lessons that <see cref="ReadLessons"/> reads back, which
    /// the next world's residents start with as memories. Engine-free: the game decides where the file goes.
    /// </summary>
    public static class Legacy
    {
        public const string LessonsHeading = "For the next world";

        /// <summary>The most lessons a log hands on, and the most insights among them.</summary>
        public const int MaxLessons = 10;

        public const int MaxInsights = 5;

        public static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

        public static bool EveryoneHasDied(TownSimulation sim) => sim.Residents.Count > 0 && sim.LivingCount == 0;

        /// <summary>The short lessons for the next world: how long they lasted, how they died, what they made, and what they came to believe.</summary>
        public static IReadOnlyList<string> Lessons(TownSimulation sim)
        {
            var lessons = new List<string>();
            var dead = sim.Residents.Where(r => !r.IsAlive && r.DiedAt != null).OrderBy(r => r.DiedAt.Value.TotalMinutes).ToList();
            var lastDay = dead.Count > 0 ? dead.Last().DiedAt.Value.Day : sim.Time.Day;
            lessons.Add($"Before us, {sim.Residents.Count} people lived on this land for {Days(lastDay - Content.FlatLand.StartTime.Day + 1)}, and in the end all of them died.");
            var causes = dead.GroupBy(r => r.CauseOfDeath).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key} ({g.Count()})");
            if (dead.Count > 0)
            {
                lessons.Add($"What they died of: {string.Join(", ", causes)}. The first to die was {dead[0].FullName}, of {dead[0].CauseOfDeath} on day {dead[0].DiedAt.Value.Day - Content.FlatLand.StartTime.Day + 1}.");
            }

            var projects = sim.Construction.Projects.Values.ToList();
            var finished = projects.Where(p => p.IsFinished).Select(p => p.Plan.Name).ToList();
            lessons.Add(projects.Count == 0
                ? "They never built anything."
                : $"They finished {finished.Count} building{(finished.Count == 1 ? string.Empty : "s")}{(finished.Count > 0 ? " (" + string.Join(", ", finished.Take(6)) + ")" : string.Empty)} and left {projects.Count - finished.Count} unfinished.");
            var crops = sim.Food.Patches.Count();
            lessons.Add($"When the last of them died, {crops} crop patch{(crops == 1 ? " was" : "es were")} still in the ground.");

            var insights = sim.Memories.All.Where(m => m.Type == MemoryType.Insight)
                .OrderByDescending(m => m.Time.TotalMinutes).ThenBy(m => m.Id, StringComparer.Ordinal).Take(MaxInsights);
            foreach (var m in insights)
            {
                var who = sim.GetResident(m.ResidentId);
                lessons.Add($"{who?.FullName ?? "One of them"} came to believe: {m.Summary}");
            }

            return lessons.Take(MaxLessons).ToList();
        }

        /// <summary>The whole log: the lessons first, then each resident's life and the world's history.</summary>
        public static string Write(TownSimulation sim)
        {
            var text = new StringBuilder();
            text.AppendLine($"The end of {sim.World.TownName}");
            text.AppendLine("==========================");
            text.AppendLine($"Written on {Journal.DateText(sim.Time)}, when the last resident died.");
            text.AppendLine();
            text.AppendLine(LessonsHeading);
            text.AppendLine(new string('-', LessonsHeading.Length));
            foreach (var lesson in Lessons(sim))
            {
                text.AppendLine($"- {lesson}");
            }

            text.AppendLine();
            text.AppendLine("How each of them died");
            text.AppendLine("---------------------");
            foreach (var r in sim.Residents.Where(r => !r.IsAlive && r.DiedAt != null).OrderBy(r => r.DiedAt.Value.TotalMinutes))
            {
                text.AppendLine($"- {r.FullName} died of {r.CauseOfDeath} on {Journal.DateText(r.DiedAt.Value)}, aged {r.AgeYears}.");
            }

            text.AppendLine();
            text.AppendLine(LifeStory.WriteTownHistory(sim));
            foreach (var r in sim.Residents)
            {
                text.AppendLine();
                text.AppendLine(LifeStory.Write(sim, r.Id));
            }

            return text.ToString();
        }

        /// <summary>The lessons in a log <see cref="Write"/> made, or none from anything else.</summary>
        public static IReadOnlyList<string> ReadLessons(string log)
        {
            var lessons = new List<string>();
            if (string.IsNullOrEmpty(log))
            {
                return lessons;
            }

            var lines = log.Replace("\r\n", "\n").Split('\n');
            var start = Array.FindIndex(lines, l => l.Trim() == LessonsHeading);
            if (start < 0)
            {
                return lessons;
            }

            for (var i = start + 2; i < lines.Length && lessons.Count < MaxLessons; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("- ", StringComparison.Ordinal))
                {
                    break;
                }

                var lesson = TextRules.Clean(line.Substring(2), MemoryRecord.MaxSummaryLength);
                if (lesson.Length > 0)
                {
                    lessons.Add(lesson);
                }
            }

            return lessons;
        }
    }
}
