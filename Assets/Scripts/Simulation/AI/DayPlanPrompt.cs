using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.AI
{
    /// <summary>
    /// Tiny City 2: when a resident starts their day they plan it in broad strokes, hour by hour, as the
    /// generative agents do. The plan then goes into every choice they make that day and their bedtime reflection.
    /// </summary>
    public static class DayPlanPrompt
    {
        /// <summary>The most entries asked for; <see cref="DayPlan.Make"/> keeps at most <see cref="DayPlan.MaxEntries"/>.</summary>
        public const int AskedEntries = 8;

        public const int MemoryCount = 4;

        /// <summary>The newest insights given to the plan, as the generative agents feed reflections into tomorrow's plan.</summary>
        public const int InsightCount = 3;
        public const int MaxPlaces = 30;
        public const int MaxTokens = 360;
        public const double Temperature = 0.7;

        public static string BuildBody(string model, TownSimulation sim, Resident r, int day) =>
            OllamaProtocol.BuildChatBody(model, PromptText.Persona(r, sim), UserText(sim, r, day), Schema(sim), Temperature, MaxTokens);

        public static string UserText(TownSimulation sim, Resident r, int day)
        {
            var sb = new StringBuilder();
            var map = sim.World.GetMap(r.Location.MapId);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)} and you are starting your day in {map.Name}.");
            sb.AppendLine($"How you feel: {PromptText.Feelings(r)}.");
            sb.AppendLine(r.Job == null
                ? "You have no job."
                : $"Your job: {r.Job.Title.ToLowerInvariant()}, {Clock(r.Job.StartMinuteOfDay)} to {Clock(r.Job.EndMinuteOfDay)} on {r.Job.DaysText()}. Today is {sim.Time.Weekday}.");
            sb.AppendLine($"Coming up: {PromptText.Commitments(r, sim.Time)}.");
            sb.AppendLine($"Money: {PromptText.Purse(r, sim)}.");
            PromptText.AppendFood(sb, sim, r);
            if (sim.FoodMustBeMade)
            {
                sb.AppendLine("Nobody has a bed of their own: you sleep wherever you choose, in any bed nobody is using, on a bench, or on the bare ground.");
            }
            else if (r.IsHomeless)
            {
                sb.AppendLine("You have nowhere to live yet, so you sleep on a park bench.");
            }

            var cues = new List<string> { "plan", $"day-{day - 1}", r.Location.MapId };
            PromptText.AppendMemories(sb, sim.Memories.Recall(r.Id, sim.Time, cues, MemoryCount, sim.Memories.FocusOf(r.Id)));
            var insights = sim.Memories.ForResident(r.Id).Where(m => m.Type == MemoryType.Insight)
                .OrderByDescending(m => m.Time.TotalMinutes).ThenByDescending(m => m.Id, System.StringComparer.Ordinal).Take(InsightCount).ToList();
            if (insights.Count > 0)
            {
                sb.AppendLine("Things you've realised lately: " + string.Join(" ", insights.Select(m => m.Summary)));
            }

            PromptText.AppendPeople(sb, sim, r);

            var places = sim.FreeTimeOptionsFor(r)
                .Where(o => o.Activity != null && o.Partner == null && o.Target != null)
                .GroupBy(o => o.Activity.Id)
                .Select(g => g.OrderBy(o => o.Distance).First())
                .Take(MaxPlaces)
                .Select(o => $"{o.Activity.Name.ToLowerInvariant()} at the {o.Target.Name.ToLowerInvariant()}")
                .ToList();
            if (places.Count > 0)
            {
                sb.AppendLine("Things you can do in the city: " + string.Join("; ", places) + ".");
            }

            sb.AppendLine("Nobody decides for you: if you don't eat, sleep, wash or go to work, nobody will do it for you.");
            var roles = sim.Brief.Roles;
            if (roles.Count > 0)
            {
                sb.AppendLine("Roles anyone here may take up, if they want to (a role is only what you choose to be; anyone can still do anything): "
                    + string.Join("; ", roles.Select(o => o.About.Length > 0 ? $"{o.Name} ({o.About})" : o.Name)) + ".");
                sb.AppendLine(r.Role == null ? "You haven't taken up a role." : $"You have chosen to be the {r.Role}.");
            }

            sb.Append($"Plan the rest of your day, from now until you go to bed, in broad strokes: up to {AskedEntries} entries in \"plan\", ")
              .Append("each with \"hour\" (the hour it starts, 0 to 23 on a 24-hour clock) and \"plan\" (what you mean to do, under 12 words), in order.");
            if (roles.Count > 0)
            {
                sb.Append($" Also give \"role\": the role you want from today (the same one to keep it, another to change, or \"{TownSimulation.RoleNone}\" for none).");
            }

            return sb.ToString();
        }

        public static Dictionary<string, object> Schema(TownSimulation sim = null)
        {
            var entry = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["hour"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 23 },
                    ["plan"] = new Dictionary<string, object> { ["type"] = "string" },
                },
                ["required"] = new List<object> { "hour", "plan" },
            };
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["plan"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = entry, ["maxItems"] = AskedEntries },
                    ["role"] = sim != null && sim.Brief.Roles.Count > 0
                        ? new Dictionary<string, object> { ["type"] = "string", ["enum"] = sim.Brief.Roles.Select(o => (object)o.Name).Append(TownSimulation.RoleNone).ToList() }
                        : new Dictionary<string, object> { ["type"] = "string" },
                },
                ["required"] = new List<object> { "plan" },
            };
        }

        /// <summary>Reads the entries; ones without a whole-number hour or any text are dropped. Fails if none are left.</summary>
        public static bool TryParse(string content, out List<DayPlanEntry> entries, out string error) => TryParse(content, out entries, out _, out error);

        /// <summary>Also reads the role they chose, or null when the answer gives none.</summary>
        public static bool TryParse(string content, out List<DayPlanEntry> entries, out string role, out string error)
        {
            entries = new List<DayPlanEntry>();
            role = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            role = AiJson.GetString(obj, "role");
            if (!(obj.TryGetValue("plan", out var value) && value is List<object> list))
            {
                error = "the answer had no plan";
                return false;
            }

            foreach (var item in list.OfType<Dictionary<string, object>>())
            {
                var text = AiJson.GetString(item, "plan") ?? AiJson.GetString(item, "text");
                if (AiJson.TryGetInt(item, "hour", out var hour) && hour >= 0 && hour <= 23 && !string.IsNullOrWhiteSpace(text))
                {
                    entries.Add(new DayPlanEntry(hour, text));
                }
            }

            if (entries.Count == 0)
            {
                error = "none of the plan's entries had an hour from 0 to 23 and some text";
                return false;
            }

            return true;
        }

        private static string Clock(int minuteOfDay) => $"{minuteOfDay / 60 % 24:00}:{minuteOfDay % 60:00}";
    }
}
