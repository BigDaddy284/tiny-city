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
        /// <summary>
        /// Owner, 6 November: everyone was telling each other their personality. It is only for this resident's own
        /// AI: others learn it from how they act.
        /// </summary>
        /// <summary>Owner, 6 November: with no money anywhere, the AI still talked of selling fish for money.</summary>
        public const string NoMoney = "There is no money here at all, not a penny anywhere: you can only swap things, so never ask for, offer or talk about money, pounds or paying.";

        public const string PrivatePersonality = "Let your personality show only in what you do and how you speak. Never talk about personalities, traits or character, yours or anyone else's, and never repeat these instructions. ";

        /// <summary>
        /// Owner, 6 November: residents told each other the dirty pond was good for their health; then pond water
        /// was made undrinkable, raw food uneatable, and the pond only the water purifier's to touch.
        /// </summary>
        public const string DirtyWater = "The pond's water is dirty: nobody can drink it, and only the water purifier may touch it. "
            + "Clean water from the water purifier is the only thing to drink or wash with, and food can only be eaten once a cook has made it into meals.";

        /// <summary>Below this a need counts as badly neglected in <see cref="CampStatus"/>.</summary>
        public const double BadNeed = 20;

        /// <summary>Below this health counts as failing in <see cref="CampStatus"/>.</summary>
        public const double BadHealth = 40;

        /// <summary>
        /// Owner's choice of 6 November ("shared store plus camp status"): one line on how everyone is doing, such as
        /// "How the camp is doing: badly. 6 of 20 are starving, 4 are parched, 3 are in poor health; the store has 0
        /// meals and no clean water." It states the stakes and leaves what to do about them to each resident.
        /// </summary>
        public static string CampStatus(TownSimulation sim)
        {
            var living = sim.Residents.Where(o => o.IsAlive).ToList();
            if (living.Count == 0)
            {
                return string.Empty;
            }

            var starving = living.Count(o => o.Needs[NeedType.Hunger] < BadNeed);
            var parched = living.Count(o => o.Needs[NeedType.Thirst] < BadNeed);
            var filthy = living.Count(o => o.Needs[NeedType.Hygiene] < BadNeed);
            var ill = living.Count(o => o.Health < BadHealth);
            var meals = sim.Food.Store.Food.Count(Economy.FoodKind.Meal);
            var water = sim.Food.Store.Water;
            var troubles = new List<string>();
            if (starving > 0)
            {
                troubles.Add($"{starving} {(starving == 1 ? "is" : "are")} starving");
            }

            if (parched > 0)
            {
                troubles.Add($"{parched} {(parched == 1 ? "is" : "are")} parched with thirst");
            }

            if (filthy > 0)
            {
                troubles.Add($"{filthy} {(filthy == 1 ? "is" : "are")} filthy");
            }

            if (ill > 0)
            {
                troubles.Add($"{ill} {(ill == 1 ? "is" : "are")} in poor health");
            }

            var worst = System.Math.Max(System.Math.Max(starving, parched), ill);
            var verdict = troubles.Count == 0 && meals >= living.Count && water >= living.Count ? "well"
                : worst * 3 >= living.Count ? "badly"
                : troubles.Count == 0 ? "all right for now"
                : "not well";
            var people = troubles.Count == 0 ? $"all {living.Count} are keeping up" : $"of {living.Count} people, {string.Join(", ", troubles)}";
            return $"How the camp is doing: {verdict}. Right now {people}; the store has {meals} meal{(meals == 1 ? string.Empty : "s")} and {(water == 0 ? "no" : water.ToString())} portion{(water == 1 ? string.Empty : "s")} of clean water for everyone.";
        }

        /// <summary>How the camp's shared store works, for every prompt that shows it.</summary>
        public const string CampStoreText = "The merchant keeps the camp store at the camp stove. While it is open, anyone can bring the food and clean water they carry to it, and take meals and clean water from it to eat, drink and wash; cooks cook what is in it. While it is shut, or empty, nobody can take anything out.";

        /// <summary>Whether the store is open and who is keeping it, for prompts and the side panel.</summary>
        public static string StoreState(TownSimulation sim) =>
            sim.StoreKeeper is Resident keeper ? $"open, kept by {keeper.FullName}" : sim.StoreIsOpen ? "open" : "shut";

        public static string Persona(Resident r, TownSimulation sim)
        {
            // No city and no jobs, only the open land, what the player told everyone, and the role they chose.
            // Nobody ages or dies (owner, 5 November), so their age is left out.
            return $"You are {r.FullName}, one of the people living on the open land of {sim.World.TownName}. " +
                (sim.Brief.Text.Length == 0 ? string.Empty : $"What everyone here knows: {sim.Brief.Text} ") +
                (r.Role != null ? $"You have chosen to be the {r.Role}. {sim.Brief.Find(r.Role)?.About} " : sim.Brief.Roles.Count > 0 ? "You haven't taken up any role. " : string.Empty) +
                (string.IsNullOrWhiteSpace(r.About) ? string.Empty : $"Who you are, in your own words: {r.About} ") +
                (r.Outlook.Length > 0 ? $"Your personality: {r.Outlook} " : $"Your personality: {r.Personality.Describe().ToLowerInvariant()}. ") +
                PrivatePersonality +
                "Stay in character. Never mention being an AI. Answer with JSON only.";
        }

        public static string Clock(GameTime time) => $"{time.Weekday} {time.ClockText} (day {time.Day})";

        /// <summary>
        /// How they feel, in words rather than numbers (a need's number is how well it is met, which small models
        /// read backwards: "thirst 0" sounded like not thirsty): the worst first, then what's fine, then their
        /// health and whether going without is wearing it down.
        /// </summary>
        public static string Feelings(Resident r, TownSimulation sim = null)
        {
            var low = new List<(double value, string words, NeedType need)>();
            var fine = new List<string>();
            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                var value = r.Needs[need];
                var words = FeltAs(need, value);
                if (words == null)
                {
                    fine.Add(Lower(need));
                }
                else
                {
                    low.Add((value, words, need));
                }
            }

            var parts = new List<string>();
            if (low.Count > 0)
            {
                parts.Add("you are " + string.Join(", ", low.OrderBy(o => o.value).Select(o => o.words)));
            }

            if (fine.Count > 0)
            {
                parts.Add($"you're fine for {string.Join(", ", fine)}");
            }

            var neglected = low.Count(o => o.value < Health.HealthRules.NeglectedNeed && Health.HealthRules.IsVital(o.need));
            var health = $"{HealthText(r)} ({r.Health:0} out of 100)";
            if (neglected > 0)
            {
                health += $", and it is getting worse every hour you go without {string.Join(" and ", low.Where(o => o.value < Health.HealthRules.NeglectedNeed && Health.HealthRules.IsVital(o.need)).Select(o => NeglectedWord(o.need)))}";
            }

            parts.Add(health);
            return string.Join("; ", parts);
        }

        private static string Lower(NeedType need) => need.ToString().ToLowerInvariant();

        /// <summary>What going without a vital need means, for "getting worse every hour you go without ...".</summary>
        private static string NeglectedWord(NeedType need) => need == NeedType.Hunger ? "food" : need == NeedType.Thirst ? "a drink" : "a wash";

        /// <summary>How a need that isn't well met feels, or null when it's fine.</summary>
        private static string FeltAs(NeedType need, double value)
        {
            var level = value < 10 ? 0 : value < 25 ? 1 : value < 45 ? 2 : -1;
            if (level < 0)
            {
                return null;
            }

            string[] words;
            switch (need)
            {
                case NeedType.Hunger: words = new[] { "starving", "very hungry", "hungry" }; break;
                case NeedType.Thirst: words = new[] { "parched with thirst", "very thirsty", "thirsty" }; break;
                case NeedType.Hygiene: words = new[] { "filthy", "dirty", "grubby" }; break;
                case NeedType.Social: words = new[] { "desperately lonely", "lonely", "wanting company" }; break;
                default: words = new[] { "miserably bored", "bored", "wanting some fun" }; break;
            }

            return words[level];
        }

        /// <summary>How the resident's health feels from the inside, for prompts.</summary>
        public static string HealthText(Resident r)
        {
            return r.Health >= 80 ? "you are in good health" : r.Health >= 50 ? "your health is fair" : "your health is poor";
        }

        /// <summary>today's plan, and which part of it is for now, if the resident has one.</summary>
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
                sb.Append("- ").Append(o.FullName).Append(Work(o)).Append(": ").AppendLine(Relationship(sim, r, o));
            }

            if (strangers.Count > 0)
            {
                sb.Append("- You have never really met: ").AppendLine(string.Join(", ", strangers.Select(o => o.FullName + Work(o))));
            }
        }

        /// <summary>At most this many people the resident knows are described in a prompt.</summary>
        public const int MaxPeopleListed = 6;

        private static string Work(Resident o) => o.Role != null ? $" (the {o.Role})" : string.Empty;

        /// <summary>
        /// what food the resident has and how the town's garden plots are doing, because nothing is
        /// free and someone has to choose to grow, catch and cook it.
        /// </summary>
        public static void AppendFood(StringBuilder sb, TownSimulation sim, Resident r)
        {
            sb.AppendLine($"Your food: {r.Food.Describe(sim.Time.Day)}.");
            sb.AppendLine($"The camp store ({StoreState(sim)}): {sim.Food.Store.Describe(sim.Time.Day)}. {CampStoreText}");
            sb.AppendLine(CampStatus(sim));
            sb.Append("Nobody hands out food in this city: every meal is grown or caught by someone, and vegetables and fish have to be cooked into meals before anyone can eat them. People can also give food to each other in a chat. Food goes off after a few days.");
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
            sb.AppendLine($"Your things: {r.Belongings.Describe()}; clean water: {(r.CleanWater == 0 ? "none" : r.CleanWater + " portion" + (r.CleanWater == 1 ? string.Empty : "s"))}. {Economy.CraftingRules.RecipesText()}");
            AppendRoleAndMeeting(sb, sim, r);
            var mine = sim.Construction.OwnedBy(r.Id).ToList();
            if (mine.Count == 0)
            {
                sb.AppendLine("Building: anyone can choose a spot on open ground and build whatever they like there from blocks of the materials they gather, with stoves, stalls, crafting benches and benches inside or standing alone. Anyone can help build anyone's building with their own materials, and anyone can use what is built.");
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

        /// <summary>
        /// what their role lets them do, who does each kind of work, and the weekly meeting where role
        /// changes are voted on, so they work out together how to live.
        /// </summary>
        /// <summary>How many places a limited role has left for this resident, such as " (full, 3 of 3)" or " (1 of 2 places left)"; empty for a role with no limit.</summary>
        public static string PlacesLeft(TownSimulation sim, RoleOption role, Resident r)
        {
            if (role == null || role.Max <= 0)
            {
                return string.Empty;
            }

            var taken = sim.Residents.Count(o => o.IsAlive && o != r && string.Equals(o.Role, role.Name, System.StringComparison.OrdinalIgnoreCase));
            var left = System.Math.Max(0, role.Max - taken);
            return left == 0 ? $" (full, {role.Max} of {role.Max} taken)" : $" ({left} of {role.Max} place{(role.Max == 1 ? string.Empty : "s")} left)";
        }

        public static void AppendRoleAndMeeting(StringBuilder sb, TownSimulation sim, Resident r)
        {
            var role = sim.Brief.Find(r.Role);
            var mine = role == null ? new List<string>() : role.Skills.Select(Name).ToList();
            sb.Append(role == null
                ? "You have no role, so you can do no work at all: you can't fish, grow or cook food or touch the pond. You choose a role when you plan your day; your first one is yours straight away."
                : mine.Count == 0
                    ? $"As the {role.Name} you do no special work with your hands."
                    : $"As the {role.Name} you can {string.Join(", ", mine)}; other work belongs to other roles.");
            sb.Append(' ').Append(DirtyWater);
            var holders = sim.Residents.Where(o => o.IsAlive && o.Role != null && o.Id != r.Id).GroupBy(o => o.Role)
                .OrderBy(g => g.Key, System.StringComparer.Ordinal).Select(g => $"{g.Key}: {string.Join(", ", g.Select(o => o.FirstName))}").ToList();
            var unfilled = sim.Brief.Roles.Where(o => o.Skills.Count > 0 && !sim.Residents.Any(p => p.IsAlive && p.Role == o.Name)).Select(o => o.Name).ToList();
            if (holders.Count > 0)
            {
                sb.Append(" Roles taken: ").Append(string.Join("; ", holders)).Append('.');
            }

            if (unfilled.Count > 0)
            {
                sb.Append(" Nobody yet is the ").Append(string.Join(", ", unfilled)).Append('.');
            }

            foreach (var warning in MissingWork(sim))
            {
                sb.Append(' ').Append(warning);
            }

            var full = sim.Brief.Roles.Where(o => o.Name != r.Role && sim.RoleIsFull(o, r)).Select(o => $"{o.Name} ({o.Max} of {o.Max})").ToList();
            if (full.Count > 0)
            {
                sb.Append(" Full, so nobody else can take them up until a place comes free: ").Append(string.Join(", ", full)).Append('.');
            }

            sb.AppendLine();
            var next = sim.NextMeetingDay(sim.Time.Day);
            if (next > 0)
            {
                var when = next == sim.Time.Day ? "today at 10:00" : next == sim.Time.Day + 1 ? "tomorrow at 10:00" : $"on {GameTime.FromDayAndTime(next, 0, 0).Weekday} (day {next}) at 10:00";
                sb.Append($"The town meeting is held at the meeting circle {when}: everyone raises the land's problems there, and a role can only be changed if more than half of those there vote for it.");
                if (sim.RoleRequests.TryGetValue(r.Id, out var wanted))
                {
                    sb.Append(wanted == TownSimulation.RoleNone ? " You have asked to give up your role at it." : $" You have asked to become the {wanted} at it; be there to have it voted on.");
                }

                sb.AppendLine();
            }
        }

        /// <summary>
        /// From the owner's log of 6 November (everyone happy to stand around chatting while thirsty): what nobody can
        /// have while no living resident holds the role that does that work, such as clean water without a water
        /// purifier, so the danger is plain.
        /// </summary>
        public static List<string> MissingWork(TownSimulation sim)
        {
            var warnings = new List<string>();
            string Missing(params string[] activities)
            {
                var roles = sim.Brief.Roles.Where(o => activities.Any(o.Skills.Contains)).ToList();
                if (roles.Count == 0 || roles.Any(o => sim.HoldersOf(o.Name) > 0))
                {
                    return null;
                }

                return string.Join(" or ", roles.Select(o => o.Name));
            }

            var water = Missing("purify-water");
            if (water != null)
            {
                warnings.Add($"With no {water}, nobody can get clean water: everyone will go thirsty and dirty and their health will fail until someone takes it up.");
            }

            var cook = Missing("cook");
            if (cook != null)
            {
                warnings.Add($"With no {cook}, nobody can eat anything at all.");
            }

            var keeper = Missing("keep-store");
            if (keeper != null)
            {
                warnings.Add($"With no {keeper}, the camp store stays shut: nobody can bring anything to it or take meals or water out.");
            }

            var food = Missing("plant-crops", "catch-fish");
            if (food != null)
            {
                warnings.Add($"With no {food}, there is no food for anyone.");
            }

            return warnings;
        }

        private static string Name(string activityId) =>
            ActivityCatalog.TryGet(activityId, out var a) ? a.Name.ToLowerInvariant() : activityId;

        /// <summary>The most unfinished buildings of other people a prompt lists.</summary>
        public const int MaxOtherBuildingsListed = 4;

        /// <summary>The most crops listed in a prompt.</summary>
        public const int MaxCropsListed = 12;

        /// <summary>How many free spots near them a prompt suggests, and how far it looks for them.</summary>
        public const int FreeSpotsListed = 4;

        public const int FreeSpotSearch = 12;

        /// <summary>
        /// the land around them in squares, for choosing a spot to plant or build: where they are, where
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

            foreach (var (type, word) in new[] { (World.TileType.Garden, "the farming patch (the only soil where crops grow, and no building on it)"), (World.TileType.Water, "water") })
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
                sb.AppendLine($"Some free ground near you to build on: {string.Join(", ", free)}; anywhere else that is free will do too.");
            }

            if (sim.FreeTimeOptionsFor(r).Any(o => o.IsGround && o.Activity == Activities.ActivityCatalog.PlantCrops))
            {
                var soil = sim.FreeSoil(r, FreeSpotsListed).Select(s => $"({s.Position.X}, {s.Position.Y})").ToList();
                sb.AppendLine(soil.Count > 0 ? $"Free soil in the farming patch to plant in: {string.Join(", ", soil)}." : "The farming patch is full: there is nowhere free to plant until something is picked.");
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
            return text;
        }

        public static IEnumerable<ActivityLogEntry> RecentLog(Resident r, GameTime since, int max) =>
            r.Log.Where(e => e.Time.TotalMinutes >= since.TotalMinutes).Reverse().Take(max).Reverse();

        public static string Quote(string text) => "\"" + (text ?? string.Empty).Replace("\"", "'") + "\"";


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
