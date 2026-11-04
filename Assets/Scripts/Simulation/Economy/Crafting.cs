using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;

namespace TinyCity.Simulation.Economy
{
    /// <summary>Materials gathered in Tiny City 2. Saved by name, so add new kinds at the end.</summary>
    public enum MaterialKind
    {
        Wood,
        Stone,
    }

    /// <summary>Tools crafted in Tiny City 2. Saved by name, so add new kinds at the end.</summary>
    public enum ToolKind
    {
        Axe,
        Pickaxe,
        Hoe,
        FishingRod,
    }

    /// <summary>A resident's materials and tools (Tiny City 2). Tools wear out after <see cref="CraftingRules.ToolUses"/> uses.</summary>
    public sealed class Belongings
    {
        private readonly int[] materials = new int[Enum.GetValues(typeof(MaterialKind)).Length];
        private readonly Dictionary<ToolKind, int> tools = new Dictionary<ToolKind, int>();

        public int Count(MaterialKind kind) => materials[(int)kind];

        public bool IsEmpty => materials.All(m => m == 0) && tools.Count == 0;

        /// <summary>Tools they have, in a fixed order, with the uses each has left.</summary>
        public IEnumerable<(ToolKind tool, int usesLeft)> Tools => tools.OrderBy(t => t.Key).Select(t => (t.Key, t.Value));

        public bool Has(ToolKind tool) => tools.ContainsKey(tool);

        public int UsesLeft(ToolKind tool) => tools.TryGetValue(tool, out var n) ? n : 0;

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

        internal void SetTool(ToolKind tool, int usesLeft)
        {
            if (usesLeft <= 0)
            {
                tools.Remove(tool);
            }
            else
            {
                tools[tool] = Math.Min(usesLeft, CraftingRules.ToolUses);
            }
        }

        /// <summary>Uses a tool once. Returns true if it broke.</summary>
        internal bool Wear(ToolKind tool)
        {
            if (!tools.TryGetValue(tool, out var n))
            {
                return false;
            }

            SetTool(tool, n - 1);
            return n - 1 <= 0;
        }

        public string Describe()
        {
            var parts = new List<string>();
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
            {
                parts.Add($"{Count(kind)} {CraftingRules.Name(kind)}");
            }

            var toolText = tools.Count == 0
                ? "no tools"
                : "tools: " + string.Join(", ", Tools.Select(t => $"{CraftingRules.Name(t.tool)} ({t.usesLeft} use{(t.usesLeft == 1 ? string.Empty : "s")} left)"));
            return string.Join(", ", parts) + "; " + toolText;
        }
    }

    /// <summary>Every number for Tiny City 2's gathering and crafting.</summary>
    public static class CraftingRules
    {
        public const int MaxMaterials = 100;

        /// <summary>How many times a tool can be used before it breaks.</summary>
        public const int ToolUses = 10;

        /// <summary>Wood from one go at the woodland by hand, and with an axe.</summary>
        public const int WoodByHand = 1;

        public const int WoodWithAxe = 4;

        /// <summary>Stone from one go at the rocks; it needs a pickaxe.</summary>
        public const int StoneWithPickaxe = 3;

        /// <summary>Extra vegetables picked with a hoe, and extra fish caught with a rod.</summary>
        public const int HoeExtraPortions = 6;

        public const int RodExtraPortions = 2;

        /// <summary>The wood each tool takes to make.</summary>
        public static int WoodFor(ToolKind tool) => tool == ToolKind.Axe || tool == ToolKind.Pickaxe ? 3 : 2;

        public static string Name(MaterialKind kind) => kind == MaterialKind.Wood ? "wood" : "stone";

        public static string Name(ToolKind tool) => tool switch
        {
            ToolKind.Axe => "axe",
            ToolKind.Pickaxe => "pickaxe",
            ToolKind.Hoe => "hoe",
            _ => "fishing rod",
        };

        /// <summary>"an axe", "a hoe".</summary>
        public static string WithArticle(ToolKind tool) => (tool == ToolKind.Axe ? "an " : "a ") + Name(tool);

        /// <summary>The tool a crafting activity makes, or null.</summary>
        public static ToolKind? ToolMadeBy(ActivityDefinition a) =>
            a == ActivityCatalog.CraftAxe ? ToolKind.Axe
            : a == ActivityCatalog.CraftPickaxe ? ToolKind.Pickaxe
            : a == ActivityCatalog.CraftHoe ? ToolKind.Hoe
            : a == ActivityCatalog.CraftRod ? ToolKind.FishingRod
            : (ToolKind?)null;

        /// <summary>Tiny City 2's gathering and crafting activities, which don't exist in v1.</summary>
        public static bool IsCrafting(ActivityDefinition a) => a == ActivityCatalog.GatherWood || a == ActivityCatalog.GatherStone || ToolMadeBy(a) != null;

        public static bool IsCrafting(string activityId) => activityId != null && ActivityCatalog.TryGet(activityId, out var a) && IsCrafting(a);

        /// <summary>What each tool is for and costs, for prompts.</summary>
        public static string Recipes() =>
            "Gather wood at the woodland (by hand, or much more with an axe) and stone at the rocks (needs a pickaxe). At a workbench, an axe or pickaxe takes 3 wood, a hoe or fishing rod 2. "
            + $"A hoe gives {HoeExtraPortions} more vegetables when picking crops and a rod {RodExtraPortions} more fish. Tools wear out after {ToolUses} uses. Stone is kept for building.";
    }
}
