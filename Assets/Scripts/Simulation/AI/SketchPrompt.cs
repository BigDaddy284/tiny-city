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
            OllamaProtocol.BuildChatBody(model, PromptText.Persona(r), UserText(sim, r, sketch), Schema(), Temperature, MaxTokens);

        public static string UserText(TownSimulation sim, Resident r, PendingSketch sketch)
        {
            var size = BuildingRules.GridSize;
            var tiles = BuildingRules.PlotSize;
            var sb = new StringBuilder();
            sim.World.TryGetObject(sketch.PlotId, out var plot);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are at {plot?.Name.ToLowerInvariant() ?? "an empty plot"} on the open land, an empty square of ground {tiles} by {tiles} paces, planning something to build on it with blocks.");
            sb.AppendLine($"Your things: {r.Belongings.Describe()}.");
            PromptText.AppendMemories(sb, sim.Memories.Recall(r.Id, sim.Time, new[] { "building", "crafting" }, MemoryCount, sim.Memories.FocusOf(r.Id)));
            sb.Append($"Draw it from above as up to {size} rows of up to {size} letters, the back row first. Each letter is a small square of ground, half a pace each way, and a block letter is a column of blocks there: ")
              .Append(string.Join(", ", BuildingRules.Letters.Select(l => $"{l.Key} = {CraftingRules.Name(l.Value)}")))
              .Append(". Use . for open floor, D for a doorway and B for a bed. A wall one letter thick is fine. ")
              .Append("A person needs a whole pace (2 by 2 letters) to stand on, so a doorway or a bed clears the 2 by 2 square it falls in. Blocks can't be walked through, so leave a doorway, and no open part may be shut in. ")
              .Append($"A building with a bed becomes your home once it is finished, and you sleep there. \"height\" is how many blocks high the walls are, 1 to {BuildingRules.MaxHeight} (4 is about your height; a doorway is cleared up to 4). ")
              .Append($"Each {CraftingRules.Name(MaterialKind.Wood)} or other material you gather makes {BuildingRules.BlocksPerMaterial} blocks, so plan for what you have or can get. ")
              .Append("For example a small wooden home 4 high: \"WWWWWWWWWW\", \"W........W\", \"W.B......W\", \"W........W\", \"W........W\", \"W........W\", \"W........W\", \"WWWWDWWWWW\". Give it a short \"name\", like \"wooden cabin\" or \"stone tower\".");
            return sb.ToString();
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
