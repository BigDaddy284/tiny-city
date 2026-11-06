using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.AI
{
    /// <summary>
    /// Asks a resident how much each of a few new memories matters to them, from 1 (everyday) to 10 (life-changing),
    /// as the generative agents memory stream does. Several memories go in one request to save AI calls.
    /// </summary>
    public static class ImportancePrompt
    {
        /// <summary>The most memories rated in one request.</summary>
        public const int MaxPerRequest = 8;

        public const int MaxTokens = 200;
        public const double Temperature = 0.2;

        public static string BuildBody(string model, TownSimulation sim, Resident r, IReadOnlyList<MemoryRecord> memories)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Here are some things you remember. Rate how much each one matters to you, from 1 (everyday, like brushing your teeth) to 10 (life-changing, like a death or falling in love).");
            foreach (var m in memories)
            {
                sb.Append("- ").Append(m.Id).Append(": ").AppendLine(m.Summary);
            }

            sb.Append("Answer with \"ratings\": one object per memory with its \"id\" and \"importance\".");

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["ratings"] = new Dictionary<string, object>
                    {
                        // Owner's log, 6 November: a small model counted "1", "2", "3"… until it ran out of words, so
                        // the ids are only those asked about and the list is no longer than the memories.
                        ["type"] = "array",
                        ["maxItems"] = memories.Count,
                        ["items"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["id"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = memories.Select(m => (object)m.Id).ToList() },
                                ["importance"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 10 },
                            },
                            ["required"] = new List<object> { "id", "importance" },
                        },
                    },
                },
                ["required"] = new List<object> { "ratings" },
            };

            return OllamaProtocol.BuildChatBody(model, PromptText.Persona(r, sim), sb.ToString(), schema, Temperature, MaxTokens);
        }

        /// <summary>
        /// Reads the ratings, keeping only ids that were asked about (each once) with a whole number from 1 to 10.
        /// Fails if nothing usable came back.
        /// </summary>
        public static bool TryParse(string content, IEnumerable<string> askedIds, out Dictionary<string, int> ratings, out string error)
        {
            ratings = new Dictionary<string, int>();
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            var asked = new HashSet<string>(askedIds ?? Enumerable.Empty<string>());
            if (!(obj.TryGetValue("ratings", out var value) && value is List<object> list))
            {
                error = "the answer had no ratings";
                return false;
            }

            foreach (var item in list.OfType<Dictionary<string, object>>())
            {
                var id = AiJson.GetString(item, "id");
                if (id != null && asked.Contains(id) && !ratings.ContainsKey(id)
                    && AiJson.TryGetInt(item, "importance", out var importance)
                    && importance >= MemoryRecord.MinImportance && importance <= MemoryRecord.MaxImportance)
                {
                    ratings[id] = importance;
                }
            }

            if (ratings.Count == 0)
            {
                error = "none of the ratings were for the memories asked about";
                return false;
            }

            return true;
        }
    }
}
