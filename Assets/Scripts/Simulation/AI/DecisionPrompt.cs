using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.AI
{
    /// <summary>What the AI answered for a free-time decision, before the game validates it.</summary>
    public sealed class DecisionReply
    {
        public DecisionReply(string action, string targetId, string reason, string say)
        {
            Action = action;
            TargetId = targetId;
            Reason = reason;
            Say = say;
        }

        public string Action { get; }

        public string TargetId { get; }

        public string Reason { get; }

        public string Say { get; }
    }

    /// <summary>Builds the free-time decision prompt and reads the answer. Only offered options can be chosen.</summary>
    public static class DecisionPrompt
    {
        public const int MemoryCount = 4;
        public const int RecentLogCount = 4;
        public const int MaxTokens = 160;
        public const double Temperature = 0.8;

        public static string BuildBody(string model, TownSimulation sim, Resident r, IReadOnlyList<FreeTimeOption> options)
        {
            var memories = sim.Memories.Recall(r.Id, sim.Time, Cues(sim, r, options), MemoryCount);
            return OllamaProtocol.BuildChatBody(model, PromptText.Persona(r), UserText(sim, r, options, memories), Schema(options), Temperature, MaxTokens);
        }

        public static string UserText(TownSimulation sim, Resident r, IReadOnlyList<FreeTimeOption> options, IReadOnlyList<MemoryRecord> memories)
        {
            var sb = new StringBuilder();
            var map = sim.World.GetMap(r.Location.MapId);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are in {map.Name}.");
            sb.AppendLine($"How you feel: {PromptText.Feelings(r)}.");
            sb.AppendLine($"Coming up: {PromptText.Commitments(r, sim.Time)}.");
            sb.AppendLine($"Money: {PromptText.Purse(r)}.");
            PromptText.AppendMemories(sb, memories);
            PromptText.AppendPeople(sb, sim, r);

            var recent = PromptText.RecentLog(r, sim.Time.AddMinutes(-6 * 60), RecentLogCount).ToList();
            if (recent.Count > 0)
            {
                sb.AppendLine("Earlier today:");
                foreach (var entry in recent)
                {
                    sb.AppendLine($"- {entry.Time.ClockText} {entry.Text}");
                }
            }

            sb.AppendLine("You have some free time. Choose exactly one option:");
            foreach (var o in options)
            {
                sb.AppendLine($"- action {PromptText.Quote(o.ActionId)}, target_id {PromptText.Quote(o.TargetId)}: {Describe(o)}");
            }

            sb.Append("Reply as JSON with \"action\" and \"target_id\" copied from one option, \"reason\" (why, in your own words, under 15 words) ")
              .Append("and \"say\" (something you say or think out loud, under 15 words).");
            return sb.ToString();
        }

        /// <summary>Constrains the model to the offered ids; the game still checks the pair is a real option.</summary>
        public static Dictionary<string, object> Schema(IReadOnlyList<FreeTimeOption> options)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["action"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = options.Select(o => (object)o.ActionId).Distinct().ToList() },
                    ["target_id"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = options.Select(o => (object)o.TargetId).Distinct().ToList() },
                    ["reason"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["say"] = new Dictionary<string, object> { ["type"] = "string" },
                },
                ["required"] = new List<object> { "action", "target_id", "reason", "say" },
            };
        }

        /// <summary>
        /// Reads the answer. A missing target is filled in only when the action has exactly one possible target;
        /// everything else is left for <see cref="TownSimulation.ApplyDecision"/> to accept or reject.
        /// </summary>
        public static bool TryParse(string content, IReadOnlyList<FreeTimeOption> options, out DecisionReply reply, out string error)
        {
            reply = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            var action = AiJson.GetString(obj, "action")?.Trim();
            if (string.IsNullOrEmpty(action))
            {
                error = "the answer had no action";
                return false;
            }

            var target = (AiJson.GetString(obj, "target_id") ?? AiJson.GetString(obj, "target"))?.Trim();
            if (string.IsNullOrEmpty(target))
            {
                var candidates = options.Where(o => o.ActionId == action).ToList();
                if (candidates.Count != 1)
                {
                    error = $"the answer had no target_id for '{action}'";
                    return false;
                }

                target = candidates[0].TargetId;
            }

            reply = new DecisionReply(action, target, AiJson.GetString(obj, "reason"), AiJson.GetString(obj, "say"));
            return true;
        }

        /// <summary>Words that help recall memories relevant to these options.</summary>
        public static IEnumerable<string> Cues(TownSimulation sim, Resident r, IReadOnlyList<FreeTimeOption> options)
        {
            var cues = new List<string> { r.Location.MapId };
            foreach (var o in options)
            {
                cues.Add(o.ActionId);
                if (o.Target != null)
                {
                    cues.Add(o.Target.Id);
                }

                if (o.Partner != null)
                {
                    cues.Add(o.Partner.Id);
                }
            }

            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                if (r.Needs[need] < 65)
                {
                    cues.Add(need.ToString().ToLowerInvariant());
                }
            }

            return cues;
        }

        internal static string Describe(FreeTimeOption o)
        {
            if (o.Activity == null)
            {
                return $"take it easy where you are for {TownSimulation.RelaxMinutes} minutes";
            }

            var walk = o.Distance == 0 ? "right here" : $"{o.Distance} steps away";
            if (o.Partner != null)
            {
                return $"go and chat with {o.Partner.FullName} ({walk})";
            }

            var cost = o.PricePence > 0 ? $", costs {Economy.Money.Format(o.PricePence)}" : string.Empty;
            return $"{o.Activity.Name.ToLowerInvariant()} at the {o.Target.Name.ToLowerInvariant()} ({walk}, about {o.Activity.DurationMinutes} minutes{cost})";
        }
    }
}
