using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;

namespace TinyCity.Simulation.Economy
{
    /// <summary>Materials gathered in Tiny City 2, in three tiers each. Saved by name, so add new kinds at the end.</summary>
    public enum MaterialKind
    {
        Wood,
        Stone,
        Hardwood,
        Iron,
        Heartwood,
        Crystal,
    }

    /// <summary>Tools crafted in Tiny City 2. Saved by name, so add new kinds at the end.</summary>
    public enum ToolKind
    {
        Axe,
        Pickaxe,
        Hoe,
        FishingRod,
    }

    /// <summary>A material that can be gathered: where its tool must be at least <see cref="MinTier"/> (0 = by hand), and how much one go yields.</summary>
    public sealed class Gathering
    {
        public Gathering(MaterialKind material, ToolKind tool, int minTier, int baseYield)
        {
            Material = material;
            Tool = tool;
            MinTier = minTier;
            BaseYield = baseYield;
        }

        public MaterialKind Material { get; }

        public ToolKind Tool { get; }

        public int MinTier { get; }

        /// <summary>What a tool of the lowest allowed tier (or bare hands, for tier 0) yields; each tier above adds one.</summary>
        public int BaseYield { get; }

        /// <summary>What one go yields with a tool of this tier (0 = none), or 0 if the tool isn't good enough.</summary>
        public int YieldWith(int toolTier)
        {
            if (toolTier < MinTier)
            {
                return 0;
            }

            if (MinTier == 0)
            {
                // Picked up by hand: a tool doesn't help.
                return BaseYield;
            }

            return BaseYield + toolTier - Math.Max(MinTier, 1);
        }
    }

    /// <summary>A tool that can be made at a workbench, and the materials it takes.</summary>
    public sealed class Recipe
    {
        public Recipe(ToolKind tool, int tier, params (MaterialKind material, int count)[] cost)
        {
            Tool = tool;
            Tier = tier;
            Cost = cost;
        }

        public ToolKind Tool { get; }

        public int Tier { get; }

        public IReadOnlyList<(MaterialKind material, int count)> Cost { get; }

        public string Describe() => string.Join(" and ", Cost.Select(c => $"{c.count} {CraftingRules.Name(c.material)}"));
    }

    /// <summary>A resident's materials and tools (Tiny City 2). Tools wear out after <see cref="CraftingRules.UsesFor"/> uses.</summary>
    public sealed class Belongings
    {
        private readonly int[] materials = new int[Enum.GetValues(typeof(MaterialKind)).Length];
        private readonly Dictionary<ToolKind, (int tier, int uses)> tools = new Dictionary<ToolKind, (int tier, int uses)>();

        public int Count(MaterialKind kind) => materials[(int)kind];

        public bool IsEmpty => materials.All(m => m == 0) && tools.Count == 0;

        /// <summary>Tools they have, in a fixed order, with each one's tier and the uses it has left.</summary>
        public IEnumerable<(ToolKind tool, int tier, int usesLeft)> Tools => tools.OrderBy(t => t.Key).Select(t => (t.Key, t.Value.tier, t.Value.uses));

        public bool Has(ToolKind tool) => tools.ContainsKey(tool);

        /// <summary>The tier of the tool they hold of this kind, or 0 for none.</summary>
        public int TierOf(ToolKind tool) => tools.TryGetValue(tool, out var t) ? t.tier : 0;

        public int UsesLeft(ToolKind tool) => tools.TryGetValue(tool, out var t) ? t.uses : 0;

        /// <summary>Adds materials up to <see cref="CraftingRules.MaxMaterials"/> of each. Returns how many were kept.</summary>
        public int Add(MaterialKind kind, int count)
        {
            var kept = Math.Max(0, Math.Min(count, CraftingRules.MaxMaterials - materials[(int)kind]));
            materials[(int)kind] += kept;
            return kept;
        }

        /// <summary>Takes this many if they are all there; otherwise takes nothing.</summary>
        public bool Take(MaterialKind kind, int count)
        {
            if (count < 0 || materials[(int)kind] < count)
            {
                return false;
            }

            materials[(int)kind] -= count;
            return true;
        }

        public bool CanPay(Recipe recipe) => recipe.Cost.All(c => Count(c.material) >= c.count);

        /// <summary>Takes a recipe's materials if they are all there; otherwise takes nothing.</summary>
        public bool Pay(Recipe recipe)
        {
            if (!CanPay(recipe))
            {
                return false;
            }

            foreach (var c in recipe.Cost)
            {
                materials[(int)c.material] -= c.count;
            }

            return true;
        }

        internal void SetTool(ToolKind tool, int usesLeft, int tier = 1)
        {
            if (usesLeft <= 0)
            {
                tools.Remove(tool);
            }
            else
            {
                var t = Math.Max(1, Math.Min(tier, CraftingRules.MaxTier));
                tools[tool] = (t, Math.Min(usesLeft, CraftingRules.UsesFor(t)));
            }
        }

        /// <summary>Uses a tool once. Returns true if it broke.</summary>
        internal bool Wear(ToolKind tool)
        {
            if (!tools.TryGetValue(tool, out var t))
            {
                return false;
            }

            SetTool(tool, t.uses - 1, t.tier);
            return t.uses - 1 <= 0;
        }

        public string Describe()
        {
            var parts = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>().Where(k => Count(k) > 0).Select(k => $"{Count(k)} {CraftingRules.Name(k)}").ToList();
            var stuff = parts.Count == 0 ? "no materials" : string.Join(", ", parts);
            var toolText = tools.Count == 0
                ? "no tools"
                : "tools: " + string.Join(", ", Tools.Select(t => $"{CraftingRules.Name(t.tool, t.tier)} ({t.usesLeft} use{(t.usesLeft == 1 ? string.Empty : "s")} left)"));
            return stuff + "; " + toolText;
        }
    }

    /// <summary>Every number for Tiny City 2's gathering and crafting.</summary>
    public static class CraftingRules
    {
        public const int MaxMaterials = 100;

        /// <summary>Tools come in wooden (1), stone (2) and iron (3).</summary>
        public const int MaxTier = 3;

        /// <summary>Wood or stone from one go of picking it up by hand.</summary>
        public const int ByHand = 1;

        /// <summary>Extra vegetables picked with a hoe, and extra fish caught with a rod.</summary>
        public const int HoeExtraPortions = 6;

        public const int RodExtraPortions = 2;

        /// <summary>How many times a tool of this tier can be used before it breaks.</summary>
        public static int UsesFor(int tier) => 10 * Math.Max(1, Math.Min(tier, MaxTier));

        /// <summary>Each material, the tool and tier it needs, and the yield: higher tiers need better tools and give less.</summary>
        public static readonly IReadOnlyDictionary<ActivityDefinition, Gathering> Gatherings = new Dictionary<ActivityDefinition, Gathering>
        {
            [ActivityCatalog.GatherSticks] = new Gathering(MaterialKind.Wood, ToolKind.Axe, 0, ByHand),
            [ActivityCatalog.GatherLooseStones] = new Gathering(MaterialKind.Stone, ToolKind.Pickaxe, 0, ByHand),
            [ActivityCatalog.GatherWood] = new Gathering(MaterialKind.Wood, ToolKind.Axe, 1, 4),
            [ActivityCatalog.GatherHardwood] = new Gathering(MaterialKind.Hardwood, ToolKind.Axe, 2, 2),
            [ActivityCatalog.GatherHeartwood] = new Gathering(MaterialKind.Heartwood, ToolKind.Axe, 3, 1),
            [ActivityCatalog.GatherStone] = new Gathering(MaterialKind.Stone, ToolKind.Pickaxe, 1, 3),
            [ActivityCatalog.GatherIron] = new Gathering(MaterialKind.Iron, ToolKind.Pickaxe, 2, 2),
            [ActivityCatalog.GatherCrystal] = new Gathering(MaterialKind.Crystal, ToolKind.Pickaxe, 3, 1),
        };

        /// <summary>Each tool that can be made, its tier and what it costs.</summary>
        public static readonly IReadOnlyDictionary<ActivityDefinition, Recipe> Recipes = new Dictionary<ActivityDefinition, Recipe>
        {
            [ActivityCatalog.CraftAxe] = new Recipe(ToolKind.Axe, 1, (MaterialKind.Wood, 3)),
            [ActivityCatalog.CraftPickaxe] = new Recipe(ToolKind.Pickaxe, 1, (MaterialKind.Wood, 3)),
            [ActivityCatalog.CraftStoneAxe] = new Recipe(ToolKind.Axe, 2, (MaterialKind.Wood, 2), (MaterialKind.Stone, 3)),
            [ActivityCatalog.CraftStonePickaxe] = new Recipe(ToolKind.Pickaxe, 2, (MaterialKind.Wood, 2), (MaterialKind.Stone, 3)),
            [ActivityCatalog.CraftIronAxe] = new Recipe(ToolKind.Axe, 3, (MaterialKind.Hardwood, 2), (MaterialKind.Iron, 3)),
            [ActivityCatalog.CraftIronPickaxe] = new Recipe(ToolKind.Pickaxe, 3, (MaterialKind.Hardwood, 2), (MaterialKind.Iron, 3)),
            [ActivityCatalog.CraftHoe] = new Recipe(ToolKind.Hoe, 1, (MaterialKind.Wood, 2)),
            [ActivityCatalog.CraftRod] = new Recipe(ToolKind.FishingRod, 1, (MaterialKind.Wood, 2)),
        };

        public static Gathering GatheringFor(ActivityDefinition a) => a != null && Gatherings.TryGetValue(a, out var g) ? g : null;

        public static Recipe RecipeFor(ActivityDefinition a) => a != null && Recipes.TryGetValue(a, out var r) ? r : null;

        /// <summary>The tier of each material: wood, stone 1; hardwood, iron 2; heartwood, crystal 3.</summary>
        public static int TierOf(MaterialKind kind) => kind switch
        {
            MaterialKind.Wood or MaterialKind.Stone => 1,
            MaterialKind.Hardwood or MaterialKind.Iron => 2,
            _ => 3,
        };

        public static string Name(MaterialKind kind) => kind switch
        {
            MaterialKind.Wood => "wood",
            MaterialKind.Stone => "stone",
            MaterialKind.Hardwood => "hardwood",
            MaterialKind.Iron => "iron ore",
            MaterialKind.Heartwood => "heartwood",
            _ => "crystal",
        };

        /// <summary>Everything that can be handed over in a chat (Tiny City 2's barter): every material, then every tool.</summary>
        public static IReadOnlyList<string> ItemNames =>
            Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>().Select(Name)
                .Concat(Enum.GetValues(typeof(ToolKind)).Cast<ToolKind>().Select(t => Name(t))).ToList();

        public static bool TryMaterial(string name, out MaterialKind kind)
        {
            var n = (name ?? string.Empty).Trim();
            foreach (MaterialKind k in Enum.GetValues(typeof(MaterialKind)))
            {
                if (string.Equals(Name(k), n, StringComparison.OrdinalIgnoreCase) || string.Equals(k.ToString(), n, StringComparison.OrdinalIgnoreCase))
                {
                    kind = k;
                    return true;
                }
            }

            kind = default;
            return false;
        }

        /// <summary>A tool by name; "wooden axe" or "iron pickaxe" find the axe or pickaxe whatever its tier.</summary>
        public static bool TryTool(string name, out ToolKind tool)
        {
            var n = (name ?? string.Empty).Trim().ToLowerInvariant();
            foreach (var t in new[] { ToolKind.Pickaxe, ToolKind.Axe, ToolKind.FishingRod, ToolKind.Hoe })
            {
                if (n.EndsWith(Name(t), StringComparison.Ordinal) || n == t.ToString().ToLowerInvariant() || (t == ToolKind.FishingRod && n.EndsWith("rod", StringComparison.Ordinal)))
                {
                    tool = t;
                    return true;
                }
            }

            tool = default;
            return false;
        }

        public static string Name(ToolKind tool) => tool switch
        {
            ToolKind.Axe => "axe",
            ToolKind.Pickaxe => "pickaxe",
            ToolKind.Hoe => "hoe",
            _ => "fishing rod",
        };

        /// <summary>"wooden axe", "iron pickaxe"; hoes and rods have one tier and no material in their name.</summary>
        public static string Name(ToolKind tool, int tier) =>
            tool == ToolKind.Axe || tool == ToolKind.Pickaxe ? $"{TierWord(tier)} {Name(tool)}" : Name(tool);

        public static string TierWord(int tier) => tier >= 3 ? "iron" : tier == 2 ? "stone" : "wooden";

        /// <summary>"an iron axe", "a hoe".</summary>
        public static string WithArticle(ToolKind tool, int tier = 1)
        {
            var name = Name(tool, tier);
            return ("aeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name;
        }

        public static bool IsGathering(ActivityDefinition a) => GatheringFor(a) != null;

        /// <summary>Tiny City 2's gathering and crafting activities, which don't exist in v1.</summary>
        public static bool IsCrafting(ActivityDefinition a) => IsGathering(a) || RecipeFor(a) != null;

        public static bool IsCrafting(string activityId) => activityId != null && ActivityCatalog.TryGet(activityId, out var a) && IsCrafting(a);

        /// <summary>Whether this resident's belongings let them do a gathering or crafting activity now.</summary>
        public static bool Allows(Belongings b, ActivityDefinition a)
        {
            var g = GatheringFor(a);
            if (g != null)
            {
                return g.YieldWith(b.TierOf(g.Tool)) > 0;
            }

            var recipe = RecipeFor(a);
            return recipe == null || (b.TierOf(recipe.Tool) < recipe.Tier && b.CanPay(recipe));
        }

        /// <summary>What each material and tool is for and costs, for prompts.</summary>
        public static string RecipesText()
        {
            var gather = string.Join("; ", Gatherings.Values.Select(g =>
                $"{Name(g.Material)} ({(g.MinTier == 0 ? $"{g.BaseYield} a go picked up by hand" : $"needs a {TierWord(g.MinTier)} {Name(g.Tool)} or better, {g.BaseYield} a go")})"));
            var make = string.Join("; ", Recipes.Values.Select(r => $"{Name(r.Tool, r.Tier)}: {r.Describe()}"));
            return $"Higher-tier materials need better tools and come in smaller amounts, and each better tool gathers one more of the lower tiers. Fallen branches and loose stones are picked up by hand; trees are chopped and rocks broken with tools: {gather}. "
                + $"At a workbench: {make}; a better tool replaces a worse one. Wooden tools last {UsesFor(1)} uses, stone {UsesFor(2)}, iron {UsesFor(3)}. "
                + $"A hoe gives {HoeExtraPortions} more vegetables when picking crops and a rod {RodExtraPortions} more fish. Heartwood, crystal and stone are kept for building.";
        }
    }
}
