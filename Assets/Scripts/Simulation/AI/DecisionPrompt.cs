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
        public DecisionReply(string action, string targetId, string reason, string say, IReadOnlyList<PlanStep> then = null, int? x = null, int? y = null, string what = null)
        {
            Action = action;
            TargetId = targetId;
            Reason = reason;
            Say = say;
            Then = then ?? new List<PlanStep>();
            X = x;
            Y = y;
            What = what;
        }

        /// <summary>The spot on open ground, for something done wherever they choose (Tiny City 2), or null.</summary>
        public int? X { get; }

        public int? Y { get; }

        /// <summary>What they plant, when planting on open ground, or null.</summary>
        public string What { get; }

        public string Action { get; }

        public string TargetId { get; }

        public string Reason { get; }

        public string Say { get; }

        /// <summary>What the resident means to do afterwards, in order; checked again by the game when each is due.</summary>
        public IReadOnlyList<PlanStep> Then { get; }
    }

    /// <summary>Builds the free-time decision prompt and reads the answer. Only offered options can be chosen.</summary>
    public static class DecisionPrompt
    {
        public const int MemoryCount = 4;
        public const int RecentLogCount = 4;
        public const int MaxTokens = 220;
        public const double Temperature = 0.8;

        public static string BuildBody(string model, TownSimulation sim, Resident r, IReadOnlyList<FreeTimeOption> options)
        {
            var memories = sim.Memories.Recall(r.Id, sim.Time, Cues(sim, r, options), MemoryCount, sim.Memories.FocusOf(r.Id));
            return OllamaProtocol.BuildChatBody(model, PromptText.Persona(r, sim), UserText(sim, r, options, memories), Schema(options, sim.PlanStepLimit), Temperature, MaxTokens);
        }

        public static string UserText(TownSimulation sim, Resident r, IReadOnlyList<FreeTimeOption> options, IReadOnlyList<MemoryRecord> memories)
        {
            var sb = new StringBuilder();
            var map = sim.World.GetMap(r.Location.MapId);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are in {map.Name}.");
            sb.AppendLine($"How you feel: {PromptText.Feelings(r, sim)}.");
            sb.AppendLine($"Coming up: {PromptText.Commitments(r, sim.Time)}.");
            if (!sim.FoodMustBeMade)
            {
                sb.AppendLine($"Money: {PromptText.Purse(r, sim)}.");
            }
            else
            {
                sb.AppendLine(PromptText.NoMoney);
            }

            PromptText.AppendFood(sb, sim, r);
            if (sim.FoodMustBeMade)
            {
                sb.AppendLine("Nobody has a bed of their own: you sleep wherever you choose, in any bed nobody is using, on a bench, or on the bare ground.");
            }
            else if (r.IsHomeless)
            {
                sb.AppendLine("You have nowhere to live yet, so you sleep on a park bench.");
            }

            PromptText.AppendDayPlan(sb, r, sim.Time);
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

            sb.AppendLine(sim.AiRunsEverything
                ? (sim.FoodMustBeMade
                    ? "Nobody decides for you. If you don't eat, drink, sleep, wash or use the toilet, nobody will do it for you, and your health suffers. Choose one option to do now:"
                    : "Nobody decides for you. If you don't eat, drink, sleep, wash or go to work, nobody will do it for you, and hunger, tiredness and lost wages hurt. Choose one option to do now:")
                : "You have some free time. Choose one option to do now:");
            foreach (var o in options)
            {
                sb.AppendLine($"- action {PromptText.Quote(o.ActionId)}, target_id {PromptText.Quote(o.TargetId)}: {Describe(o)}");
            }

            var ground = options.Any(o => o.IsGround);
            if (ground)
            {
                PromptText.AppendLand(sb, sim, r);
            }

            sb.Append("Reply as JSON with \"action\" and \"target_id\" copied from one option, \"reason\" (why, in your own words, under 15 words), ")
              .Append("\"say\" (something you say or think out loud, under 15 words) ")
              .Append($"and \"then\": up to {sim.PlanStepLimit - 1} more options from the list to do afterwards, in order, each with \"action\" and \"target_id\".");
            if (ground)
            {
                sb.Append($" For target_id {PromptText.Quote(FreeTimeOption.GroundTargetId)}, also give \"x\" and \"y\": the spot you choose, and when planting, \"what\" you plant.");
            }


            return sb.ToString();
        }

        /// <summary>Constrains the model to the offered ids; the game still checks the pair is a real option.</summary>
        public static Dictionary<string, object> Schema(IReadOnlyList<FreeTimeOption> options, int maxSteps = TownSimulation.MaxPlanSteps)
        {
            var actions = options.Select(o => (object)o.ActionId).Distinct().ToList();
            var targets = options.Select(o => (object)o.TargetId).Distinct().ToList();
            var step = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["action"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = actions },
                    ["target_id"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = targets },
                },
                ["required"] = new List<object> { "action", "target_id" },
            };
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["action"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = actions },
                    ["target_id"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = targets },
                    ["reason"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["say"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["then"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = step, ["maxItems"] = maxSteps - 1 },
                    ["x"] = new Dictionary<string, object> { ["type"] = "integer" },
                    ["y"] = new Dictionary<string, object> { ["type"] = "integer" },
                    ["what"] = new Dictionary<string, object> { ["type"] = "string" },
                },
                ["required"] = new List<object> { "action", "target_id", "reason", "say", "then" },
            };
        }

        /// <summary>
        /// Reads the answer. A missing target is filled in only when the action has exactly one possible target;
        /// everything else is left for <see cref="TownSimulation.ApplyDecision"/> to accept or reject.
        /// </summary>
        public static bool TryParse(string content, IReadOnlyList<FreeTimeOption> options, out DecisionReply reply, out string error, int maxSteps = TownSimulation.MaxPlanSteps)
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
                // With several possible targets the game picks the nearest (Tiny City 2) or rejects it (Tiny City).
                target = candidates.Count == 1 ? candidates[0].TargetId : string.Empty;
            }

            int? x = AiJson.TryGetInt(obj, "x", out var gx) ? gx : (int?)null;
            int? y = AiJson.TryGetInt(obj, "y", out var gy) ? gy : (int?)null;
            reply = new DecisionReply(action, target, AiJson.GetString(obj, "reason"), AiJson.GetString(obj, "say"), ReadSteps(obj, maxSteps), x, y, AiJson.GetString(obj, "what"));
            return true;
        }

        /// <summary>
        /// Reads the optional "then" list. Steps without an action, or past <see cref="TownSimulation.MaxPlanSteps"/>,
        /// are dropped rather than failing the whole answer; the game checks the rest when each step is due.
        /// </summary>
        private static List<PlanStep> ReadSteps(Dictionary<string, object> obj, int maxSteps)
        {
            var steps = new List<PlanStep>();
            if (!obj.TryGetValue("then", out var value) || !(value is IList<object> list))
            {
                return steps;
            }

            foreach (var item in list)
            {
                var action = AiJson.GetString(item as Dictionary<string, object>, "action")?.Trim();
                if (string.IsNullOrEmpty(action))
                {
                    continue;
                }

                var target = AiJson.GetString((Dictionary<string, object>)item, "target_id")?.Trim();
                steps.Add(new PlanStep(action, string.IsNullOrEmpty(target) ? null : target));
                if (steps.Count >= maxSteps - 1)
                {
                    break;
                }
            }

            return steps;
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
                if (sim.HasNeed(need) && r.Needs[need] < 65)
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

            if (o.IsGround)
            {
                return o.Activity == ActivityCatalog.PlantCrops
                    ? $"plant whatever you like, anywhere you like on open ground (about {o.Activity.DurationMinutes} minutes there)"
                    : o.Activity == ActivityCatalog.Sleep
                    ? $"lie down and sleep on the bare ground, anywhere you like (up to {o.Activity.DurationMinutes / 60} hours)"
                    : $"choose a spot anywhere on open ground and plan a building there; it is its back corner, and it reaches towards bigger x and y (about {o.Activity.DurationMinutes} minutes)";
            }

            var cost = o.PricePence > 0 ? $", costs {Economy.Money.Format(o.PricePence)}" : o.Activity == ActivityCatalog.BuyFood ? ", handed out free" : string.Empty;
            var warning = Economy.FoodRules.HealthWarning(o.Activity);
            cost += warning == null ? string.Empty : "; " + warning;
            return $"{o.Activity.Name.ToLowerInvariant()} at the {o.Target.Name.ToLowerInvariant()} ({walk}, about {o.Activity.DurationMinutes} minutes{cost})";
        }
    }
}
