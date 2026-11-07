using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.AI
{
    /// <summary>What the AI answered to "what will you build here?", before the game checks it.</summary>
    public sealed class SketchReply
    {
        public SketchReply(string name, int height, IReadOnlyList<string> rows)
        {
            Name = name;
            Height = height;
            Rows = rows;
        }

        public string Name { get; }

        public int Height { get; }

        public IReadOnlyList<string> Rows { get; }
    }

    /// <summary>Tiny City 2: a resident at an empty plot sketches a small building in block letters, which <see cref="BuildPlan.TryParse"/> then checks.</summary>
    public static class SketchPrompt
    {
        public const int MemoryCount = 3;
        public const int MaxTokens = 700;
        public const double Temperature = 0.8;

        public static string BuildBody(string model, TownSimulation sim, Resident r, PendingSketch sketch) =>
            OllamaProtocol.BuildChatBody(model, PromptText.Persona(r, sim), UserText(sim, r, sketch), Schema(), Temperature, MaxTokens);

        public static string UserText(TownSimulation sim, Resident r, PendingSketch sketch)
        {
            var size = BuildingRules.GridSize;
            var sb = new StringBuilder();
            var site = sim.Construction.SiteOf(sketch.SiteId);
            var corner = site?.Corner ?? r.Location;
            var room = Room(sim, corner, sketch.SiteId);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are at ({corner.Position.X}, {corner.Position.Y}) on the {sim.World.GetMap(corner.MapId).Name.ToLowerInvariant()}, the spot you chose to build on, planning what to build there with blocks. "
                + $"Your plan's back-left corner goes on this spot and it reaches towards bigger x and y; the clear ground here is about {room} by {room} paces (at most {BuildingRules.MaxSize} by {BuildingRules.MaxSize}). Make it whatever size and shape you like within that.");
            sb.AppendLine($"Your things: {r.Belongings.Describe()}.");
            PromptText.AppendMemories(sb, sim.Memories.Recall(r.Id, sim.Time, new[] { "building", "crafting" }, MemoryCount, sim.Memories.FocusOf(r.Id)));
            sb.Append($"Draw it from above as up to {size} rows of up to {size} letters, the back row first. Each letter is a small square of ground, half a pace each way, and a block letter is a column of blocks there: ")
              .Append(string.Join(", ", BuildingRules.Letters.Select(l => $"{l.Key} = {CraftingRules.Name(l.Value)}")))
              .Append(". Use . for open floor and D for a doorway. Furniture: ")
              .Append(string.Join(", ", BuildingRules.Furniture.Select(f => $"{f.Key} = {f.Value.name}")))
              .Append(". A wall one letter thick is fine, and furniture can stand inside or on its own with no walls at all (for example just \"M\" for a market stall). ")
              .Append("A person needs a whole pace (2 by 2 letters) to stand on, so a doorway or a piece of furniture clears the 2 by 2 square it falls in. Blocks can't be walked through, so leave a doorway, and no open part may be shut in. ")
              .Append($"Whatever you build, anyone can use, and anyone can help you build it. \"height\" is how many blocks high the walls are, 1 to {BuildingRules.MaxHeight} (4 is about your height; a doorway is cleared up to 4). ")
              .Append($"Each {CraftingRules.Name(MaterialKind.Wood)} or other material you gather makes {BuildingRules.BlocksPerMaterial} blocks, so plan for what you have or can get. ")
              .Append("For example a small wooden shelter 4 high, 3 by 3 paces: \"WWWWWW\", \"W....W\", \"W....W\", \"W....W\", \"W....W\", \"WWDDWW\". Give it a short \"name\", like \"wooden cabin\" or \"stone tower\".");
            return sb.ToString();
        }

        /// <summary>How many paces each way the open ground runs from the corner (a square), up to <see cref="BuildingRules.MaxSize"/>.</summary>
        public static int Room(TownSimulation sim, World.Location corner, string siteId)
        {
            if (!sim.World.TryGetMap(corner.MapId, out var map))
            {
                return 0;
            }

            bool Free(int x, int y)
            {
                var l = new World.Location(corner.MapId, corner.Position.X + x, corner.Position.Y + y);
                return map.IsWalkable(l.Position) && !sim.Construction.IsClaimed(l) && !sim.World.Objects.Any(o => o.Location == l && o.Id != siteId);
            }

            var k = 0;
            while (k < BuildingRules.MaxSize && Enumerable.Range(0, k + 1).All(i => Free(k, i) && Free(i, k)))
            {
                k++;
            }

            return k;
        }

        public static Dictionary<string, object> Schema() => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                ["height"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = BuildingRules.MaxHeight },
                ["rows"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["maxItems"] = BuildingRules.GridSize,
                },
            },
            ["required"] = new List<object> { "name", "height", "rows" },
        };

        public static bool TryParse(string content, out SketchReply reply, out string error)
        {
            reply = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            if (!AiJson.TryGetInt(obj, "height", out var height))
            {
                error = "the answer had no whole-number \"height\"";
                return false;
            }

            if (!(obj.TryGetValue("rows", out var value) && value is List<object> list) || list.Any(v => !(v is string)))
            {
                error = "the answer had no \"rows\" of letters";
                return false;
            }

            reply = new SketchReply(AiJson.GetString(obj, "name"), height, list.Cast<string>().ToList());
            return true;
        }
    }
}
