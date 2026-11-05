using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.AI
{
    /// <summary>Shared pieces of prompt text describing a resident and their situation.</summary>
    internal static class PromptText
    {
        public static string Persona(Resident r)
        {
            var job = r.Job == null ? "a resident" : $"the {r.Job.Title.ToLowerInvariant()}";
            return $"You are {r.FullName}, aged {r.AgeYears}, {job} in Tiny City, a small, neon-lit city. " +
                (string.IsNullOrWhiteSpace(r.About) ? string.Empty : $"Your background, in your own words: {r.About} ") +
                $"Your personality: {r.Personality.Describe().ToLowerInvariant()}. " +
                (r.Hobbies.Count > 0 ? $"Your hobbies: {string.Join(", ", r.Hobbies)}. " : string.Empty) +
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

            return string.Join(", ", parts) + "; " + HealthText(r);
        }

        /// <summary>How the resident's health feels from the inside, for prompts.</summary>
        public static string HealthText(Resident r)
        {
            var illness = Health.IllnessCatalog.Get(r.IllnessId);
            if (illness != null)
            {
                return illness.Serious
                    ? $"you are seriously ill with {illness.Name}{(r.IllnessTreated ? " but the doctor has treated you" : " and need the doctor")}"
                    : $"you are poorly with {illness.Name}";
            }

            return r.Health >= 80 ? "you are in good health" : r.Health >= 50 ? "your health is fair" : "your health is poor";
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

            var minutesToBed = SleepSchedule.MinutesToBed(r, now);
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
                if (leave.TotalMinutes > now.TotalMinutes && r.Job.WorksOn(leave.Weekday))
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

        /// <summary>Tiny City 2: today's plan, and which part of it is for now, if the resident has one.</summary>
        public static void AppendDayPlan(StringBuilder sb, Resident r, GameTime now)
        {
            var plan = r.DayPlan;
            if (plan == null || plan.Day != DayPlan.DayFor(now))
            {
                return;
            }

            sb.AppendLine($"Your plan for today: {plan.Describe()}.");
            var current = plan.Now(now);
            if (current != null)
            {
                sb.AppendLine($"Right now your plan says: {current.Text}. You can change your mind if something else matters more.");
            }
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

            // Everyone they know gets a line, closest friends first; strangers share one line so prompts stay short.
            var known = others
                .Where(o => sim.Relationships.TryGet(r.Id, o.Id, out var rel) && rel.HaveMet)
                .OrderByDescending(o => sim.Relationships.Get(r.Id, o.Id).Friendship)
                .ThenBy(o => o.Id, System.StringComparer.Ordinal)
                .ToList();
            var strangers = others.Where(o => !known.Contains(o)).ToList();
            sb.AppendLine("People in the city:");
            foreach (var o in known.Take(MaxPeopleListed))
            {
                sb.Append("- ").Append(o.FullName).Append(", ").Append(o.AgeYears).Append(Work(o)).Append(": ").AppendLine(Relationship(sim, r, o));
            }

            if (strangers.Count > 0)
            {
                sb.Append("- You have never really met: ").AppendLine(string.Join(", ", strangers.Select(o => o.FullName + Work(o))));
            }
        }

        /// <summary>At most this many people the resident knows are described in a prompt.</summary>
        public const int MaxPeopleListed = 6;

        private static string Work(Resident o) => o.Job == null ? string.Empty : $" ({o.Job.Title.ToLowerInvariant()})";

        /// <summary>Money and food at home, so the AI can weigh paid treats against what's in the cupboard.</summary>
        public static string Purse(Resident r, TownSimulation sim = null) =>
            $"you have {Economy.Money.Format(r.MoneyPence)}"
            + (sim?.FoodMustBeMade == true ? string.Empty : $", and food at home for {r.Groceries} meal{(r.Groceries == 1 ? string.Empty : "s")}")
            + (r.MoneyPence < r.WeeklyRentPence ? $"; money is tight, rent of {Economy.Money.Format(r.WeeklyRentPence)} is due on Monday" : string.Empty);

        /// <summary>
        /// Tiny City 2: what food the resident has and how the town's garden plots are doing, because nothing is
        /// free and someone has to choose to grow, catch and cook it.
        /// </summary>
        public static void AppendFood(StringBuilder sb, TownSimulation sim, Resident r)
        {
            if (!sim.FoodMustBeMade)
            {
                return;
            }

            sb.AppendLine($"Your food: {r.Food.Describe(sim.Time.Day)}.");
            sb.Append("Nobody hands out food in this city: every meal is grown or caught by someone, and raw food can be cooked into meals that fill you up more. People can sell food at a market stall or give or sell it to each other in a chat, if they choose to. Food goes off after a few days.");
            var plots = sim.World.Objects.Where(o => o.Type == World.ObjectType.GardenPlot).ToList();
            if (plots.Count > 0)
            {
                sb.Append(" Garden: ").Append(string.Join("; ", plots.Select(o =>
                {
                    var crop = sim.Food.CropAt(o.Id);
                    return $"{o.Name} is {(crop == null ? "empty, ready for planting" : crop.Describe(sim.Time.Day))}";
                }))).Append('.');
            }

            sb.AppendLine();
            sb.AppendLine($"Your things: {r.Belongings.Describe()}. {Economy.CraftingRules.RecipesText()}");
            var project = sim.Construction.Of(r.Id);
            if (project == null)
            {
                var free = sim.World.Objects.Count(o => o.Type == World.ObjectType.BuildPlot && sim.Construction.At(o.Id) == null);
                sb.AppendLine($"Building: the open land, through the gate at the bottom of the city, has {free} empty plot{(free == 1 ? string.Empty : "s")} where anyone can plan a building of their own and build it from blocks of the materials they gather; one with a bed becomes their home once it is finished.");
            }
            else if (project.IsFinished)
            {
                sb.AppendLine($"Your building: you built a {project.Plan.Name} on the open land{(project.Plan.IsHome ? (r.BedObjectId == Building.Construction.BedIdOf(project.PlotId) ? ", and you live in it" : string.Empty) : string.Empty)}.");
            }
            else
            {
                var needs = string.Join(", ", project.StillNeeded().Select(n => $"{n.count} {Economy.CraftingRules.Name(n.material)}"));
                var home = project.Plan.IsHome ? " It has a bed, so it will be your home once finished." : string.Empty;
                sb.AppendLine($"Your building: a {project.Plan.Name} on the open land, {project.Placed} of {project.Plan.Cells.Count} blocks laid; it still needs {(needs.Length > 0 ? needs : "no more materials")}.{home}");
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

            text += $"), talked {rel.Interactions} time{(rel.Interactions == 1 ? string.Empty : "s")}";
            var couple = sim.Relationships.PartnershipOf(r.Id);
            if (couple != null && couple.Includes(other.Id))
            {
                text += couple.Stage == Social.RomanceStage.Committed ? "; your partner, in a committed relationship" : "; you are dating";
            }
            else if (rel.Romance >= Social.RomanceRules.SoftSpot)
            {
                text += "; you have a soft spot for them";
            }

            return text;
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
