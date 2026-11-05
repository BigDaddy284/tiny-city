using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.AI
{
    /// <summary>What the AI answered for one resident's turn at the town meeting, before the game checks it.</summary>
    public sealed class MeetingReply
    {
        public MeetingReply(string say, IReadOnlyDictionary<string, bool> votes)
        {
            Say = say;
            Votes = votes;
        }

        public string Say { get; }

        /// <summary>Yes or no by the requester's resident id; people the AI didn't vote on are left out.</summary>
        public IReadOnlyDictionary<string, bool> Votes { get; }
    }

    /// <summary>
    /// Tiny City 2: a resident's turn at the weekly town meeting: what they raise about the land and its problems,
    /// and their vote on each role change asked for by someone who came.
    /// </summary>
    public static class MeetingPrompt
    {
        public const int MemoryCount = 4;
        public const int MaxTokens = 220;
        public const double Temperature = 0.7;

        public static string BuildBody(string model, TownSimulation sim, Resident r) =>
            OllamaProtocol.BuildChatBody(model, PromptText.Persona(r, sim), UserText(sim, r), Schema(), Temperature, MaxTokens);

        /// <summary>The role changes asked for by people at the meeting, oldest id first.</summary>
        public static List<(Resident who, string wanted)> Requests(TownSimulation sim) =>
            sim.RoleRequests.Where(p => sim.Meeting?.Attendees.Contains(p.Key) == true)
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => (sim.GetResident(p.Key), p.Value)).Where(p => p.Item1 != null).ToList();

        public static string UserText(TownSimulation sim, Resident r)
        {
            var sb = new StringBuilder();
            var meeting = sim.Meeting;
            var others = meeting.Attendees.Where(id => id != r.Id).Select(sim.GetResident).Where(o => o != null).Select(o => o.FirstName).ToList();
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are at the weekly town meeting at the meeting circle, with {(others.Count == 0 ? "nobody else" : string.Join(", ", others))}.");
            sb.AppendLine($"How you feel: {PromptText.Feelings(r)}.");
            PromptText.AppendFood(sb, sim, r);
            if (meeting.Said.Count > 0)
            {
                sb.AppendLine("Said so far:");
                foreach (var (id, said) in meeting.Said.Skip(Math.Max(0, meeting.Said.Count - 6)))
                {
                    sb.AppendLine($"- {sim.GetResident(id)?.FirstName}: \"{said}\"");
                }
            }

            PromptText.AppendMemories(sb, sim.Memories.Recall(r.Id, sim.Time, null, MemoryCount, sim.Memories.FocusOf(r.Id)));
            var requests = Requests(sim);
            if (requests.Count == 0)
            {
                sb.AppendLine("Nobody has asked to change role this week, so there is nothing to vote on.");
            }
            else
            {
                sb.AppendLine("Role changes to vote on (each passes only if more than half of those here vote yes):");
                foreach (var (who, wanted) in requests)
                {
                    var from = who.Role == null ? string.Empty : $"stop being the {who.Role} and ";
                    var change = wanted == TownSimulation.RoleNone ? $"give up being the {who.Role}" : $"{from}become the {wanted}";
                    sb.AppendLine($"- {(who.Id == r.Id ? "You" : who.FirstName)} asked to {change}.");
                }
            }

            sb.Append("Take your turn: \"say\" is what you raise with everyone, in your own voice, under 40 words: what the land needs, what's going wrong, who should do what. ")
              .Append("\"votes\" lists each person whose change you vote on, by first name, with \"yes\": true for or false against. Think about whether the town needs that role.");
            return sb.ToString();
        }

        public static Dictionary<string, object> Schema() => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["say"] = new Dictionary<string, object> { ["type"] = "string" },
                ["votes"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["person"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["yes"] = new Dictionary<string, object> { ["type"] = "boolean" },
                        },
                        ["required"] = new List<object> { "person", "yes" },
                    },
                },
            },
            ["required"] = new List<object> { "say", "votes" },
        };

        /// <summary>Reads the answer; votes are matched to the requesters at the meeting by first or full name.</summary>
        public static bool TryParse(string content, TownSimulation sim, out MeetingReply reply, out string error)
        {
            reply = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            var say = AiJson.GetString(obj, "say");
            if (say == null)
            {
                error = "the answer had no \"say\"";
                return false;
            }

            var votes = new Dictionary<string, bool>(StringComparer.Ordinal);
            var requests = Requests(sim);
            if (obj.TryGetValue("votes", out var value) && value is IList<object> list)
            {
                foreach (var item in list.OfType<Dictionary<string, object>>())
                {
                    var person = AiJson.GetString(item, "person")?.Trim();
                    if (string.IsNullOrEmpty(person) || !AiJson.TryGetBool(item, "yes", out var yes))
                    {
                        continue;
                    }

                    var match = requests.FirstOrDefault(q => string.Equals(q.who.FirstName, person, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(q.who.FullName, person, StringComparison.OrdinalIgnoreCase) || q.who.Id == person);
                    if (match.who != null)
                    {
                        votes[match.who.Id] = yes;
                    }
                }
            }

            reply = new MeetingReply(say, votes);
            return true;
        }
    }
}
