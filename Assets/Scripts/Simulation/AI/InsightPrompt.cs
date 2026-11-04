using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.AI
{
    /// <summary>
    /// Tiny City 2: asks a resident what their recent memories add up to. Memories are numbered 1, 2, 3 in the
    /// prompt (easier for a small model than ids) and each insight cites the numbers it comes from.
    /// </summary>
    public static class InsightPrompt
    {
        public const int MaxTokens = 360;
        public const double Temperature = 0.6;

        public static string BuildBody(string model, Resident r, IReadOnlyList<MemoryRecord> evidence) =>
            OllamaProtocol.BuildChatBody(model, PromptText.Persona(r), UserText(evidence), Schema(), Temperature, MaxTokens);

        public static string UserText(IReadOnlyList<MemoryRecord> evidence)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Here are things that have happened to you lately:");
            for (var i = 0; i < evidence.Count; i++)
            {
                sb.AppendLine($"{i + 1}. Day {evidence[i].Time.Day}: {evidence[i].Summary}");
            }

            sb.Append($"What do they tell you about your life, the people around you or what you should do? Give 1 to {TownSimulation.MaxInsightsPerReflection} insights in \"insights\", ")
              .Append("each with \"insight\" (one sentence in the first person, under 25 words) and \"because\" (the numbers of the memories above it comes from).");
            return sb.ToString();
        }

        public static Dictionary<string, object> Schema()
        {
            var item = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["insight"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["because"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "integer" } },
                },
                ["required"] = new List<object> { "insight", "because" },
            };
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["insights"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = item, ["maxItems"] = TownSimulation.MaxInsightsPerReflection },
                },
                ["required"] = new List<object> { "insights" },
            };
        }

        /// <summary>Reads the insights, turning cited numbers back into memory ids; numbers that weren't listed are dropped.</summary>
        public static bool TryParse(string content, IReadOnlyList<MemoryRecord> evidence, out List<InsightProposal> insights, out string error)
        {
            insights = new List<InsightProposal>();
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            if (!(obj.TryGetValue("insights", out var value) && value is List<object> list))
            {
                error = "the answer had no insights";
                return false;
            }

            foreach (var item in list.OfType<Dictionary<string, object>>())
            {
                var text = AiJson.GetString(item, "insight");
                var ids = new List<string>();
                if (item.TryGetValue("because", out var because) && because is List<object> numbers)
                {
                    foreach (var n in numbers.OfType<double>())
                    {
                        var index = (int)n - 1;
                        if (n == System.Math.Floor(n) && index >= 0 && index < evidence.Count)
                        {
                            ids.Add(evidence[index].Id);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(text) && ids.Count > 0)
                {
                    insights.Add(new InsightProposal(text, ids));
                }
            }

            if (insights.Count == 0)
            {
                error = "no insight had any text and a memory it came from";
                return false;
            }

            return true;
        }
    }
}
