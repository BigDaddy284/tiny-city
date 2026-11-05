using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Memory;

namespace TinyCity.Simulation.Residents
{
    /// <summary>A role anyone may take up, as the player lists it: a name and a few words on what it means.</summary>
    public sealed class RoleOption
    {
        public RoleOption(string name, string about, bool canTax = false)
        {
            Name = name;
            About = about ?? string.Empty;
            CanTax = canTax;
        }

        /// <summary>Whoever takes up this role may ask others in a chat for a share of their goods (the tax man).</summary>
        public bool CanTax { get; }

        public string Name { get; }

        public string About { get; }
    }

    /// <summary>
    /// Tiny City 2 (owner, 5 November): what the player tells everyone, from <c>residents.json</c>. The brief goes
    /// into every resident's prompts, and the roles are on offer to all: each resident chooses in their morning
    /// plan whether to take one up, change it or have none (<see cref="Resident.Role"/>). A role unlocks and blocks
    /// nothing; anyone can still do anything. Saved with the town, so a world keeps the brief it began with.
    /// </summary>
    public sealed class WorldBrief
    {
        public const int MaxTextLength = 1500;
        public const int MaxRoles = 20;
        public const int MaxRoleNameLength = 40;
        public const int MaxRoleAboutLength = 600;

        public WorldBrief(string text, IEnumerable<RoleOption> roles)
        {
            Text = TextRules.Clean(text, MaxTextLength);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Roles = (roles ?? Enumerable.Empty<RoleOption>())
                .Select(r => new RoleOption(TextRules.Clean(r?.Name, MaxRoleNameLength).ToLowerInvariant(), TextRules.Clean(r?.About, MaxRoleAboutLength), r?.CanTax == true))
                .Where(r => r.Name.Length > 0 && seen.Add(r.Name))
                .Take(MaxRoles)
                .ToList();
        }

        public static readonly WorldBrief Empty = new WorldBrief(string.Empty, null);

        /// <summary>What the shipped residents.json says, and what a file without a brief gets.</summary>
        public static WorldBrief Default => new WorldBrief(
            "You and the others have come to an empty stretch of open land to start a life together from nothing. There are no shops, no houses and no one in charge. Stay alive first: hunger, thirst, tiredness, dirt and illness wear your health down, and if it runs out you die for good, so see to them before anything else. Nothing lasts: food goes off within days, tools wear out and break, and crops only grow if someone plants and waters them, so take what you need, keep some back and replace what you use up. Things change: seasons turn, people fall ill and others make their own choices, so what worked yesterday may not work today. Other people matter: working with them, trading with them or competing with them all cost something and can gain something, and how you treat each other decides whether you all last. When something isn't working, don't keep repeating it: try another way until you find one that does.",
            new[]
            {
                new RoleOption("farmer", "You grow the food everyone depends on. You choose where to dig a patch, plant it, water it on different days until it is ready and pick it, and you plan ahead so there is always something growing. A hoe makes each harvest bigger."),
                new RoleOption("fisher", "You bring in fish from the pond. Fish keep only a couple of days, so you catch what is needed, share or trade the rest, and a rod means a bigger catch."),
                new RoleOption("lumberjack", "You cut wood from the trees and pick up fallen branches. Better axes let you cut hardwood and then heartwood, and everyone building or making tools needs what you bring in."),
                new RoleOption("miner", "You break stone from the rocks and gather loose stones. Better pickaxes let you dig iron ore and then crystal, the strongest materials there are."),
                new RoleOption("toolmaker", "You make the axes, pickaxes, hoes and rods at the crafting bench that let everyone else work faster, and you replace them as they wear out and break. You may make them for others or trade them."),
                new RoleOption("cook", "You turn raw vegetables and fish into proper meals at a stove, which fill people up far better than raw food. You keep an eye on what is running low and make sure nobody goes hungry."),
                new RoleOption("infrastructure specialist", "You plan and build what the group needs: shelters with beds, stoves, stalls, benches and crafting benches. You can ask others to help build and bring materials, and you think about where things should go."),
                new RoleOption("merchant", "You run a stall and trade food and goods with the others, setting your own prices. You can grow rich or help everyone get what they need; how fair you are is up to you."),
                new RoleOption("tax man", "You collect a share of what people hold for the good of everyone. In a chat you can ask someone to hand over a percentage of their food and materials; they decide whether to pay, so they have to accept your authority. You may pay a security enforcer from what you collect.", canTax: true),
                new RoleOption("security enforcer", "You keep the peace. You stand up for anyone being treated unfairly and back up whoever pays you, such as the tax man, if you agree to work for them. Your only tools are your words and your presence."),
                new RoleOption("mayor", "You lead the group and help everyone decide what to do: who grows, who builds, what is shared. You have no power except what people give you by listening, so you have to earn their trust."),
            });

        /// <summary>The brief's text, or empty.</summary>
        public string Text { get; }

        public IReadOnlyList<RoleOption> Roles { get; }

        /// <summary>Whether someone with this role may ask others for tax.</summary>
        public bool CanTax(string role) => Find(role)?.CanTax == true;

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
                        roles.Add(new RoleOption(roleName, d.TryGetValue("about", out var a) && a is string about ? about : string.Empty, d.TryGetValue("canTax", out var t) && t is bool tax && tax));
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

                return (object)role;
            }).ToList(),
        };
    }
}
