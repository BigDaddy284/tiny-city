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
        public static string Persona(Resident r, TownSimulation sim = null)
        {
            if (sim != null && sim.FoodMustBeMade)
            {
                // Tiny City 2: no city and no jobs, only the open land, what the player told everyone, and the role they chose.
                return $"You are {r.FullName}, aged {r.AgeYears}, one of the people living on the open land of {sim.World.TownName}. " +
                    (sim.Brief.Text.Length == 0 ? string.Empty : $"What everyone here knows: {sim.Brief.Text} ") +
                    (r.Role != null ? $"You have chosen to be the {r.Role}. {sim.Brief.Find(r.Role)?.About} " : sim.Brief.Roles.Count > 0 ? "You haven't taken up any role. " : string.Empty) +
                    (string.IsNullOrWhiteSpace(r.About) ? string.Empty : $"Who you are, in your own words: {r.About} ") +
                    (r.Outlook.Length > 0 ? $"Your personality: {r.Outlook} " : $"Your personality: {r.Personality.Describe().ToLowerInvariant()}. ") +
                    (r.Hobbies.Count > 0 ? $"Things you enjoy: {string.Join(", ", r.Hobbies)}. " : string.Empty) +
                    "Stay in character. Never mention being an AI. Answer with JSON only.";
            }

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

        private static string Work(Resident o) =>
            o.Job != null ? $" ({o.Job.Title.ToLowerInvariant()})" : o.Role != null ? $" (the {o.Role})" : string.Empty;

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
                sb.Append(" Crops: ").Append(string.Join("; ", plots.Take(MaxCropsListed).Select(o =>
                {
                    var crop = sim.Food.CropAt(o.Id);
                    var patch = sim.Food.PatchOf(o.Id);
                    var whose = patch == null ? string.Empty : patch.PlanterId == r.Id ? ", yours" : sim.GetResident(patch.PlanterId) is Resident planter ? $", {planter.FirstName}'s" : string.Empty;
                    var at = $" at ({o.Location.Position.X}, {o.Location.Position.Y})";
                    return $"{o.Name}{at}{whose}: {(crop == null ? "dug, nothing planted yet" : crop.Describe(sim.Time.Day))}";
                }))).Append('.');
            }

            sb.AppendLine();
            sb.AppendLine($"Your things: {r.Belongings.Describe()}. {Economy.CraftingRules.RecipesText()}");
            var mine = sim.Construction.OwnedBy(r.Id).ToList();
            if (mine.Count == 0)
            {
                sb.AppendLine("Building: anyone can choose a spot on open ground and build whatever they like there from blocks of the materials they gather, with beds, stoves, stalls, crafting benches and benches inside or standing alone. Anyone can help build anyone's building with their own materials, and anyone can use what is built.");
            }

            foreach (var project in mine)
            {
                var at = $"at ({project.Corner.Position.X}, {project.Corner.Position.Y})";
                var helpers = project.Builders.Where(id => id != r.Id).Select(sim.GetResident).Where(o => o != null).Select(o => o.FirstName).ToList();
                var helped = helpers.Count == 0 ? string.Empty : $" {string.Join(" and ", helpers)} helped build it.";
                if (project.IsFinished)
                {
                    sb.AppendLine($"Your building: you made a {project.Plan.Name} {at}.{helped}");
                }
                else
                {
                    var needs = string.Join(", ", project.StillNeeded().Select(n => $"{n.count} {Economy.CraftingRules.Name(n.material)}"));
                    sb.AppendLine($"Your building: a {project.Plan.Name} {at}, {project.Placed} of {project.Plan.Cells.Count} blocks laid; it still needs {(needs.Length > 0 ? needs : "no more materials")}.{helped}");
                }
            }

            var others = sim.Construction.Projects.Values.Where(p => p.OwnerId != r.Id && !p.IsFinished).OrderBy(p => p.SiteId, System.StringComparer.Ordinal).Take(MaxOtherBuildingsListed).ToList();
            foreach (var project in others)
            {
                var owner = sim.GetResident(project.OwnerId);
                var needs = string.Join(", ", project.StillNeeded().Select(n => $"{n.count} {Economy.CraftingRules.Name(n.material)}"));
                sb.AppendLine($"{owner?.FullName ?? "Someone"}'s {project.Plan.Name} at ({project.Corner.Position.X}, {project.Corner.Position.Y}): {project.Placed} of {project.Plan.Cells.Count} blocks laid, still needs {needs}; you could help build it.");
            }
        }

        /// <summary>The most unfinished buildings of other people a prompt lists.</summary>
        public const int MaxOtherBuildingsListed = 4;

        /// <summary>The most crops listed in a prompt.</summary>
        public const int MaxCropsListed = 12;

        /// <summary>How many free spots near them a prompt suggests, and how far it looks for them.</summary>
        public const int FreeSpotsListed = 4;

        public const int FreeSpotSearch = 12;

        /// <summary>
        /// Tiny City 2: the land around them in squares, for choosing a spot to plant or build: where they are, where
        /// things are, and some free ground nearby. It only describes; any free spot they can reach will do.
        /// </summary>
        public static void AppendLand(StringBuilder sb, TownSimulation sim, Resident r)
        {
            var map = sim.World.GetMap(r.Location.MapId);
            var here = r.Location.Position;
            sb.AppendLine($"The {map.Name.ToLowerInvariant()} is a grid of squares from (0, 0) to ({map.Width - 1}, {map.Height - 1}); you are at ({here.X}, {here.Y}).");
            var places = new List<string>();
            string Span(IEnumerable<World.GridPos> tiles)
            {
                var list = tiles.ToList();
                int x0 = list.Min(t => t.X), x1 = list.Max(t => t.X), y0 = list.Min(t => t.Y), y1 = list.Max(t => t.Y);
                return x0 == x1 && y0 == y1 ? $"({x0}, {y0})" : $"x {x0}-{x1}, y {y0}-{y1}";
            }

            foreach (var (type, word) in new[] { (World.TileType.Garden, "soil for crops"), (World.TileType.Water, "water") })
            {
                var tiles = new List<World.GridPos>();
                for (var y = 0; y < map.Height; y++)
                {
                    for (var x = 0; x < map.Width; x++)
                    {
                        if (map.GetTile(new World.GridPos(x, y)) == type)
                        {
                            tiles.Add(new World.GridPos(x, y));
                        }
                    }
                }

                if (tiles.Count > 0)
                {
                    places.Add($"{word} at {Span(tiles)}");
                }
            }

            foreach (var group in sim.World.Objects.Where(o => o.Location.MapId == map.Id && o.Type != World.ObjectType.GardenPlot && o.Type != World.ObjectType.BuildPlot && !Building.BuildingRules.IsBuilt(o))
                .GroupBy(o => o.Name).OrderBy(g => g.Min(o => o.Id), System.StringComparer.Ordinal))
            {
                var name = group.Key.ToLowerInvariant();
                places.Add($"{(group.Count() == 1 ? name : name.EndsWith("s") ? name : name.EndsWith("ch") ? name + "es" : name + "s")} at {Span(group.Select(o => o.Location.Position))}");
            }

            foreach (var p in sim.Construction.Projects.Values.Where(p => p.Corner.MapId == map.Id).OrderBy(p => p.SiteId, System.StringComparer.Ordinal))
            {
                var owner = p.OwnerId == r.Id ? "your" : sim.GetResident(p.OwnerId) is Resident o ? $"{o.FirstName}'s" : "a";
                places.Add($"{owner} {p.Plan.Name} at {Span(p.Footprint.Select(t => t.Position))}");
            }

            sb.AppendLine($"Where things are: {string.Join("; ", places)}.");
            var free = sim.FreeGroundNear(r, FreeSpotsListed, FreeSpotSearch).Select(s => $"({s.Position.X}, {s.Position.Y})").ToList();

            if (free.Count > 0)
            {
                sb.AppendLine($"Some free ground near you: {string.Join(", ", free)}; anywhere else that is free will do too.");
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
