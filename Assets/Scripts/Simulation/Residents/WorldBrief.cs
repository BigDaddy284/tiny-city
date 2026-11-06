using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Memory;

namespace TinyCity.Simulation.Residents
{
    /// <summary>A role anyone may take up, as the player lists it: a name and a few words on what it means.</summary>
    public sealed class RoleOption
    {
        public RoleOption(string name, string about, bool canTax = false, IEnumerable<string> skills = null, int max = 0)
        {
            Name = name;
            Max = Math.Max(0, Math.Min(max, WorldBrief.MaxHolders));
            About = about ?? string.Empty;
            CanTax = canTax;
            Skills = (skills ?? Enumerable.Empty<string>())
                .Where(id => id != null && Activities.ActivityCatalog.TryGet(id.Trim(), out _))
                .Select(id => id.Trim()).Distinct().ToList();
        }

        /// <summary>
        /// Tiny City 2 (owner, 5 November): the activity ids only someone in this role may do (<c>can</c> in
        /// residents.json). An activity no role lists is open to everyone, unless it is <see cref="WorldBrief.IsRoleWork"/>.
        /// </summary>
        public IReadOnlyList<string> Skills { get; }

        /// <summary>Whoever takes up this role may ask others in a chat for a share of their goods (the tax man).</summary>
        public bool CanTax { get; }

        public string Name { get; }

        public string About { get; }

        /// <summary>
        /// Owner, 6 November ("everyone wants to be a farmer"): how many living people may hold this role at once
        /// (<c>max</c> in residents.json), or 0 for no limit. A full role can't be taken until a place comes free.
        /// </summary>
        public int Max { get; }
    }

    /// <summary>
    /// Tiny City 2 (owner, 5 November): what the player tells everyone, from <c>residents.json</c>. The brief goes
    /// into every resident's prompts, and the roles are on offer to all: each resident chooses in their morning
    /// plan whether to take one up (<see cref="Resident.Role"/>); changing it later needs a vote at the weekly
    /// meeting. A role's <see cref="RoleOption.Skills"/> are activities only its holders may do. Saved with the
    /// town, so a world keeps the brief it began with.
    /// </summary>
    public sealed class WorldBrief
    {
        public const int MaxTextLength = 1500;
        public const int MaxRoles = 20;
        public const int MaxRoleNameLength = 40;
        public const int MaxRoleAboutLength = 600;

        /// <summary>The largest <see cref="RoleOption.Max"/> a role may have.</summary>
        public const int MaxHolders = 100;

        public WorldBrief(string text, IEnumerable<RoleOption> roles)
        {
            Text = TextRules.Clean(text, MaxTextLength);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Roles = (roles ?? Enumerable.Empty<RoleOption>())
                .Select(r => new RoleOption(TextRules.Clean(r?.Name, MaxRoleNameLength).ToLowerInvariant(), TextRules.Clean(r?.About, MaxRoleAboutLength), r?.CanTax == true, r?.Skills, r?.Max ?? 0))
                .Where(r => r.Name.Length > 0 && seen.Add(r.Name))
                .Take(MaxRoles)
                .ToList();
        }

        public static readonly WorldBrief Empty = new WorldBrief(string.Empty, null);

        /// <summary>What the shipped residents.json says, and what a file without a brief gets.</summary>
        public static WorldBrief Default => new WorldBrief(
            "You and the others have come to an empty stretch of open land to start a life together from nothing. There are no shops, no houses and no one in charge, and no money: nobody has any, so to get what you need from others you trade, swapping food, materials, tools and help. Each of you can only do the work of the role you take up; without one you can only drink and wash in the dirty pond and catch the odd fish by hand to eat raw, so you need each other. A role can only be changed by a majority vote at the weekly meeting. Look after yourself first: hunger, thirst, tiredness and dirt wear your health down and leave you weak and miserable, so see to them before anything else. Nothing lasts: food goes off within days, tools wear out and break, and crops only grow if someone plants and waters them, so take what you need, keep some back and replace what you use up. Things change: seasons turn, supplies run low and others make their own choices, so what worked yesterday may not work today. Other people matter: working with them, trading with them or competing with them all cost something and can gain something, and how you treat each other decides whether you all last. When something isn't working, don't keep repeating it: try another way until you find one that does.",
            new[]
            {
                new RoleOption("farmer", "You grow the food everyone depends on, and only a farmer can. You plant squares of the farming patch, the only soil where crops grow, water them on different days until they are ready and pick them, and you plan ahead so there is always something growing. A hoe makes each harvest bigger.",
                    skills: new[] { "plant-crops", "water-crops", "harvest" }, max: 3),
                new RoleOption("fisher", "You bring in fish from the pond. Anyone can catch the odd fish by hand, but you catch far more, and a rod means more still. Fish keep only a couple of days, so you catch what is needed and share or trade the rest.",
                    skills: new[] { "catch-fish" }, max: 3),
                new RoleOption("lumberjack", "Only you can gather wood: fallen branches by hand, and with an axe, wood from the trees. Better axes let you cut hardwood and then heartwood, and everyone building or making tools needs what you bring in.",
                    skills: new[] { "gather-sticks", "gather-wood", "gather-hardwood", "gather-heartwood" }, max: 2),
                new RoleOption("miner", "Only you can gather stone: loose stones by hand, and with a pickaxe, stone from the rocks. Better pickaxes let you dig iron ore and then crystal, the strongest materials there are.",
                    skills: new[] { "gather-loose-stones", "gather-stone", "gather-iron", "gather-crystal" }, max: 2),
                new RoleOption("toolmaker", "Only you can make tools: the axes, pickaxes, hoes and rods at the crafting bench that let everyone else work, and you replace them as they wear out and break. You may make them for others or trade them.",
                    skills: new[] { "craft-axe", "craft-pickaxe", "craft-stone-axe", "craft-stone-pickaxe", "craft-iron-axe", "craft-iron-pickaxe", "craft-hoe", "craft-rod" }, max: 1),
                new RoleOption("cook", "Only you can cook. You turn raw vegetables and fish into proper meals at a stove, which fill people up far better than raw food and don't make them ill. You keep an eye on what is running low and make sure nobody goes hungry.",
                    skills: new[] { "cook" }, max: 2),
                new RoleOption("builder", "Only you know masonry and building. You plan and build what the group needs: shelters, stoves, stalls, benches and crafting benches. You can ask others to bring materials, and you think about where things should go.",
                    skills: new[] { "plan-building", "build" }, max: 3),
                new RoleOption("water purifier", "Only you can clean water. Pond water is dirty and makes people unwell; you turn it into clean water at the pond, which quenches thirst properly, and you can share it or trade it.",
                    skills: new[] { "purify-water" }, max: 2),
                new RoleOption("merchant", "Only you can run a stall. You trade food and goods with the others, deciding what you want in return. You can build up plenty for yourself or help everyone get what they need; how fair you are is up to you.",
                    skills: new[] { "run-stall" }, max: 1),
                new RoleOption("tax man", "You collect a share of what people hold for the good of everyone. In a chat you can ask someone to hand over a percentage of their food and materials; they decide whether to pay, so they have to accept your authority. You may pay a security enforcer from what you collect.", canTax: true, max: 1),
                new RoleOption("security enforcer", "You keep the peace. You stand up for anyone being treated unfairly and back up whoever pays you, such as the tax man, if you agree to work for them. Your only tools are your words and your presence.", max: 1),
                new RoleOption("mayor", "You lead the group and run the weekly meeting, helping everyone decide what to do: who grows, who builds, what is shared. You have no power except what people give you by listening, so you have to earn their trust.", max: 1),
            });

        /// <summary>The brief's text, or empty.</summary>
        public string Text { get; }

        public IReadOnlyList<RoleOption> Roles { get; }

        /// <summary>Whether someone with this role may ask others for tax.</summary>
        public bool CanTax(string role) => Find(role)?.CanTax == true;

        /// <summary>Activities anyone may have a go at even though a role is better at them: anyone can catch a fish by hand.</summary>
        public static readonly IReadOnlyList<string> EveryoneMayTry = new[] { "catch-fish" };

        /// <summary>
        /// Work that always belongs to a role (owner, 6 November: without a role you can only drink and wash in the
        /// pond and eat raw fish): every gathering, including fallen branches and loose stones, every tool, cooking,
        /// growing crops, purifying water and building. In a world with roles nobody without the right one may do
        /// these, even if no role lists them.
        /// </summary>
        public static bool IsRoleWork(string activityId) =>
            activityId != null && (activityId.StartsWith("gather-", StringComparison.Ordinal) || activityId.StartsWith("craft-", StringComparison.Ordinal) || RoleWork.Contains(activityId));

        private static readonly string[] RoleWork = { "cook", "plant-crops", "water-crops", "harvest", "purify-water", "plan-building", "build" };

        /// <summary>Whether some role lists this activity as its own, or it is <see cref="IsRoleWork"/> in a world with roles.</summary>
        public bool IsSkill(string activityId) => Roles.Any(r => r.Skills.Contains(activityId)) || (Roles.Count > 0 && IsRoleWork(activityId));

        /// <summary>The words towns started before 6 November were told, which said anyone could pick up branches and stones.</summary>
        private const string OldHandGatheringWords = "eat raw fish and pick up fallen branches and loose stones";

        // Owner, 6 November: crops grow only in the farming patch, so an older farmer no longer chooses where to dig.
        private const string OldFarmerWords = "You choose where to dig a patch, plant it, water it on different days until it is ready and pick it,";
        private const string NewFarmerWords = "You plant squares of the farming patch, the only soil where crops grow, water them on different days until they are ready and pick them,";

        /// <summary>
        /// A brief saved by an older town brought up to date: a role with a shipped role's name gets that role's skills
        /// it is missing (fallen branches went to the lumberjack and loose stones to the miner on 6 November), and the
        /// old words saying anyone could pick them up, and the farmer's old words about digging anywhere, are replaced.
        /// </summary>
        public WorldBrief UpgradedFromOlderSave()
        {
            var shipped = Default;
            var roles = Roles.Select(r =>
            {
                var same = shipped.Find(r.Name);
                var missing = same == null ? new List<string>() : same.Skills.Where(id => !r.Skills.Contains(id)).ToList();
                var about = r.About.Replace(OldFarmerWords, NewFarmerWords);
                var max = r.Max == 0 && same != null ? same.Max : r.Max;
                return missing.Count == 0 && about == r.About && max == r.Max ? r : new RoleOption(r.Name, about, r.CanTax, r.Skills.Concat(missing), max);
            });
            return new WorldBrief(Text.Replace(OldHandGatheringWords, "catch the odd fish by hand to eat raw"), roles);
        }

        /// <summary>Whether someone with this role (or none) may do this activity.</summary>
        public bool MayDo(string role, string activityId) =>
            !IsSkill(activityId) || EveryoneMayTry.Contains(activityId) || IsSkilledAt(role, activityId);

        /// <summary>Whether this role lists the activity as its own.</summary>
        public bool IsSkilledAt(string role, string activityId) => Find(role)?.Skills.Contains(activityId) == true;

        /// <summary>The role with this name (any case), or null.</summary>
        public RoleOption Find(string name) =>
            string.IsNullOrWhiteSpace(name) ? null : Roles.FirstOrDefault(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Reads the brief from residents.json's <c>brief</c> and <c>roles</c>; <see cref="Default"/> when it has neither.</summary>
        public static WorldBrief Parse(Dictionary<string, object> root, List<string> problems)
        {
            if (root == null || (!root.ContainsKey("brief") && !root.ContainsKey("roles")))
            {
                return Default;
            }

            var text = root.TryGetValue("brief", out var b) && b is string s ? s : string.Empty;
            if (text.Length > MaxTextLength)
            {
                problems?.Add($"The brief is longer than {MaxTextLength} letters, so its end was cut.");
            }

            var roles = new List<RoleOption>();
            if (root.TryGetValue("roles", out var list) && list is List<object> items)
            {
                foreach (var item in items)
                {
                    if (item is string name)
                    {
                        roles.Add(new RoleOption(name, string.Empty));
                    }
                    else if (item is Dictionary<string, object> d && d.TryGetValue("name", out var n) && n is string roleName)
                    {
                        var skills = d.TryGetValue("can", out var c) && c is List<object> can ? can.OfType<string>().ToList() : new List<string>();
                        var unknown = skills.Where(id => !Activities.ActivityCatalog.TryGet(id.Trim(), out _)).ToList();
                        if (unknown.Count > 0)
                        {
                            problems?.Add($"The {roleName} role's \"can\" has activities the game doesn't know, which were left out: {string.Join(", ", unknown)}.");
                        }

                        roles.Add(new RoleOption(roleName, d.TryGetValue("about", out var a) && a is string about ? about : string.Empty, d.TryGetValue("canTax", out var t) && t is bool tax && tax, skills, d.TryGetValue("max", out var m) && m is double most && most >= 1 && most <= MaxHolders ? (int)most : 0));
                        if (d.TryGetValue("max", out var given) && !(given is double g && g >= 1 && g <= MaxHolders && g == Math.Floor(g)))
                        {
                            problems?.Add($"The {roleName} role's \"max\" must be a whole number from 1 to {MaxHolders}, so it has no limit.");
                        }
                    }
                    else
                    {
                        problems?.Add("A role with no name was left out.");
                    }
                }

                if (roles.Count > MaxRoles)
                {
                    problems?.Add($"Only the first {MaxRoles} roles are used.");
                }
            }

            return new WorldBrief(text, roles);
        }

        public Dictionary<string, object> ToJson() => new Dictionary<string, object>
        {
            ["brief"] = Text,
            ["roles"] = Roles.Select(r =>
            {
                var role = new Dictionary<string, object> { ["name"] = r.Name, ["about"] = r.About };
                if (r.CanTax)
                {
                    role["canTax"] = true;
                }

                if (r.Skills.Count > 0)
                {
                    role["can"] = r.Skills.Cast<object>().ToList();
                }

                if (r.Max > 0)
                {
                    role["max"] = (double)r.Max;
                }

                return (object)role;
            }).ToList(),
        };
    }
}
