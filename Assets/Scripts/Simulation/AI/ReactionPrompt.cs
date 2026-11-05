using System.Collections.Generic;
using System.Text;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Simulation.AI
{
    /// <summary>What the AI answered to "react?", before the game checks it.</summary>
    public sealed class ReactionReply
    {
        public ReactionReply(bool goOver, string reason)
        {
            GoOver = goOver;
            Reason = reason;
        }

        public bool GoOver { get; }

        public string Reason { get; }
    }

    /// <summary>Tiny City 2: a resident notices someone; do they go over and talk, or carry on with what they're doing?</summary>
    public static class ReactionPrompt
    {
        public const int MemoryCount = 3;
        public const int MaxTokens = 120;
        public const double Temperature = 0.7;

        public static string BuildBody(string model, TownSimulation sim, Resident r, PendingReaction reaction) =>
            OllamaProtocol.BuildChatBody(model, PromptText.Persona(r, sim), UserText(sim, r, reaction), Schema(), Temperature, MaxTokens);

        public static string UserText(TownSimulation sim, Resident r, PendingReaction reaction)
        {
            var sb = new StringBuilder();
            var other = sim.GetResident(reaction.OtherId);
            var map = sim.World.GetMap(r.Location.MapId);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are {sim.DoingText(r)} in {map.Name}.");
            sb.AppendLine($"How you feel: {PromptText.Feelings(r)}.");
            sb.AppendLine($"Coming up: {PromptText.Commitments(r, sim.Time)}.");
            PromptText.AppendDayPlan(sb, r, sim.Time);
            sb.AppendLine($"You notice {reaction.Seen}, nearby.");
            if (other != null)
            {
                sb.AppendLine($"{other.FirstName} and you: {PromptText.Relationship(sim, r, other)}.");
                PromptText.AppendMemories(sb, sim.Memories.Recall(r.Id, sim.Time, new[] { other.Id }, MemoryCount, sim.Memories.FocusOf(r.Id)));
            }

            sb.Append("Do you stop what you're doing and go over to talk to them? Answer \"react\": true to go over or false to carry on, ")
              .Append("and \"reason\": why, in your own words, under 15 words.");
            return sb.ToString();
        }

        public static Dictionary<string, object> Schema() => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["react"] = new Dictionary<string, object> { ["type"] = "boolean" },
                ["reason"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            ["required"] = new List<object> { "react", "reason" },
        };

        public static bool TryParse(string content, out ReactionReply reply, out string error)
        {
            reply = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            if (!AiJson.TryGetBool(obj, "react", out var goOver))
            {
                error = "the answer had no true or false \"react\"";
                return false;
            }

            reply = new ReactionReply(goOver, AiJson.GetString(obj, "reason"));
            return true;
        }
    }
}
