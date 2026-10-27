using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.AI
{
    /// <summary>Shared pieces of prompt text describing a resident and their situation.</summary>
    internal static class PromptText
    {
        public static string Persona(Resident r)
        {
            var job = r.Job == null ? "a resident" : $"the {r.Job.Title.ToLowerInvariant()}";
            return $"You are {r.FullName}, aged {r.AgeYears}, {job} in The Tiny Town, a small, cosy village. " +
                $"Your personality: {r.Personality.Describe().ToLowerInvariant()}. " +
                "Stay in character: warm, down to earth and a little playful. Never mention being an AI. Answer with JSON only.";
        }

        public static string Clock(GameTime time) => $"{time.Weekday} {time.ClockText} (day {time.Day})";

        public static string Feelings(Resident r)
        {
            var parts = new List<string>();
            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                var value = r.Needs[need];
                parts.Add($"{need} {value:0}/100{Word(value)}");
            }

            return string.Join(", ", parts);
        }

        /// <summary>Work only gets a mention when the resident leaves within this many minutes, so evenings don't sound like work is next.</summary>
        public const int WorkSoonMinutes = 3 * 60;

        public static string Commitments(Resident r, GameTime now)
        {
            var notes = new List<string>();
            var minutesToWork = MinutesUntilLeavingForWork(r, now);
            if (minutesToWork >= 0 && minutesToWork <= WorkSoonMinutes)
            {
                notes.Add($"you leave for work as {r.Job.Title.ToLowerInvariant()} in {Duration(minutesToWork)}");
            }
            else if (r.Job != null && ActivityPlanner.IsWorkTime(r, now))
            {
                notes.Add("you are on shift now");
            }
            else
            {
                notes.Add("no work until " + (minutesToWork < 0 ? "further notice" : NextWorkText(now, minutesToWork)));
            }

            var minutesToBed = ((ActivityPlanner.BedtimeMinute - now.MinuteOfDay) + GameTime.MinutesPerDay) % GameTime.MinutesPerDay;
            notes.Add($"bedtime is in {Duration(minutesToBed)}");
            return string.Join("; ", notes);
        }

        /// <summary>Minutes until the resident next leaves for a shift (working days only), or -1 if they have no job.</summary>
        public static int MinutesUntilLeavingForWork(Resident r, GameTime now)
        {
            if (r.Job == null)
            {
                return -1;
            }

            var leaveMinute = r.Job.StartMinuteOfDay - ActivityPlanner.CommuteLeadMinutes;
            for (var day = now.Day; day <= now.Day + GameTime.DaysPerWeek; day++)
            {
                var leave = GameTime.FromDayAndTime(day, 0, 0).AddMinutes(leaveMinute);
                if (leave.TotalMinutes > now.TotalMinutes && (r.Job.WorksWeekends || !leave.IsWeekend))
                {
                    return (int)(leave.TotalMinutes - now.TotalMinutes);
                }
            }

            return -1;
        }

        private static string NextWorkText(GameTime now, int minutesToWork)
        {
            var leave = now.AddMinutes(minutesToWork);
            var when = leave.Day == now.Day ? "later today" : leave.Day == now.Day + 1 ? "tomorrow" : leave.Weekday.ToString();
            return $"{when} at {leave.ClockText}";
        }

        public static void AppendMemories(StringBuilder sb, IReadOnlyList<MemoryRecord> memories)
        {
            if (memories.Count == 0)
            {
                return;
            }

            sb.AppendLine("Things you remember:");
            foreach (var m in memories)
            {
                sb.Append("- ").Append(m.Summary).Append(" (day ").Append(m.Time.Day).AppendLine(")");
            }
        }

        /// <summary>Lists the other living residents and how this resident feels about each, by the game's numbers.</summary>
        public static void AppendPeople(StringBuilder sb, TownSimulation sim, Resident r)
        {
            var others = sim.Residents.Where(o => o.Id != r.Id && o.IsAlive).ToList();
            if (others.Count == 0)
            {
                return;
            }

            sb.AppendLine("People in the village:");
            foreach (var o in others)
            {
                sb.Append("- ").Append(o.FullName).Append(", ").Append(o.AgeYears).Append(": ").AppendLine(Relationship(sim, r, o));
            }
        }

        public static string Relationship(TownSimulation sim, Resident r, Resident other)
        {
            if (!sim.Relationships.TryGet(r.Id, other.Id, out var rel) || !rel.HaveMet)
            {
                return "you have never really met";
            }

            var text = $"{rel.Describe()} (friendship {rel.Friendship:0}/100, trust {rel.Trust:0}/100";
            if (rel.Dislike >= 10)
            {
                text += $", dislike {rel.Dislike:0}/100";
            }

            return text + $"), talked {rel.Interactions} time{(rel.Interactions == 1 ? string.Empty : "s")}";
        }

        public static IEnumerable<ActivityLogEntry> RecentLog(Resident r, GameTime since, int max) =>
            r.Log.Where(e => e.Time.TotalMinutes >= since.TotalMinutes).Reverse().Take(max).Reverse();

        public static string Quote(string text) => "\"" + (text ?? string.Empty).Replace("\"", "'") + "\"";

        private static string Word(double value) => value < 20 ? " (desperate)" : value < 40 ? " (low)" : value < 65 ? " (could be better)" : string.Empty;

        private static string Duration(int minutes)
        {
            if (minutes < 60)
            {
                return $"{minutes} minutes";
            }

            var hours = minutes / 60;
            var rest = minutes % 60;
            return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
        }
    }
}
