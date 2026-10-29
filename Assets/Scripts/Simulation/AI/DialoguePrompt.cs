using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Simulation.AI
{
    /// <summary>What the AI wrote for one line of a conversation, before the game checks it.</summary>
    public sealed class DialogueReply
    {
        public DialogueReply(string say, string tone, string topic, bool end)
        {
            Say = say;
            Tone = tone;
            Topic = topic;
            End = end;
        }

        public string Say { get; }

        public string Tone { get; }

        public string Topic { get; }

        public bool End { get; }
    }

    /// <summary>
    /// Asks the AI for the next line of a conversation. The game picks the speaker, the turn and the length; the
    /// AI writes the words and suggests a tone, which the game's social rules turn into relationship changes.
    /// </summary>
    public static class DialoguePrompt
    {
        public const int MemoryCount = 3;
        public const int MaxTokens = 120;
        public const double Temperature = 0.9;

        public static readonly string[] Tones = { "warm", "playful", "neutral", "disagree" };

        public static string BuildBody(string model, TownSimulation sim, ConversationSession session)
        {
            var speaker = sim.GetResident(session.SpeakerId);
            var memories = sim.Memories.Recall(speaker.Id, sim.Time, new[] { session.ListenerId, "chat", "social" }, MemoryCount);
            return OllamaProtocol.BuildChatBody(model, PromptText.Persona(speaker), UserText(sim, session, memories), Schema(), Temperature, MaxTokens);
        }

        public static string UserText(TownSimulation sim, ConversationSession session, IReadOnlyList<MemoryRecord> memories)
        {
            var speaker = sim.GetResident(session.SpeakerId);
            var listener = sim.GetResident(session.ListenerId);
            var sb = new StringBuilder();
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)}. You are chatting with {listener.FullName} (aged {listener.AgeYears}) at the {session.Place.ToLowerInvariant()}.");
            sb.AppendLine($"{listener.FirstName} to you: {PromptText.Relationship(sim, speaker, listener)}.");
            sb.AppendLine($"How you feel: {PromptText.Feelings(speaker)}.");
            PromptText.AppendMemories(sb, memories);

            var last = sim.Conversations.Involving(speaker.Id)
                .LastOrDefault(c => c.Kind == ConversationKind.Chat && c.Participants.Contains(listener.Id));
            if (last != null && last.Lines.Count > 0)
            {
                sb.AppendLine($"Last time you talked (day {last.Started.Day}): " +
                    string.Join(" / ", last.Lines.Skip(System.Math.Max(0, last.Lines.Count - 4)).Select(l => $"{Name(sim, l.SpeakerId)}: {PromptText.Quote(l.Text)}")));
            }

            if (session.Lines.Count == 0)
            {
                sb.AppendLine("You start the conversation.");
            }
            else
            {
                sb.AppendLine("The conversation so far:");
                foreach (var line in session.Lines)
                {
                    sb.AppendLine($"{Name(sim, line.SpeakerId)}: {PromptText.Quote(line.Text)}");
                }
            }

            var remaining = session.MaxLines - session.Lines.Count;
            if (remaining <= 1)
            {
                sb.AppendLine("This is your last line, so say goodbye naturally.");
            }

            sb.Append("Reply as JSON: \"say\" is your next line (one or two sentences, under 25 words, no narration), ")
              .Append("\"tone\" is one of warm, playful, neutral or disagree, ")
              .Append("\"topic\" is what you are talking about in a few words, ")
              .Append("and \"end\" is true if you want to wrap up the chat.");
            return sb.ToString();
        }

        public static Dictionary<string, object> Schema()
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["say"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["tone"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = Tones.Cast<object>().ToList() },
                    ["topic"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["end"] = new Dictionary<string, object> { ["type"] = "boolean" },
                },
                ["required"] = new List<object> { "say", "tone", "topic", "end" },
            };
        }

        /// <summary>Reads the answer. An unknown tone becomes neutral in the simulation; an empty line is rejected.</summary>
        public static bool TryParse(string content, out DialogueReply reply, out string error)
        {
            reply = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            var say = AiJson.GetString(obj, "say");
            if (string.IsNullOrWhiteSpace(say))
            {
                error = "the answer had nothing to say";
                return false;
            }

            reply = new DialogueReply(say, AiJson.GetString(obj, "tone"), AiJson.GetString(obj, "topic"), AiJson.TryGetBool(obj, "end", out var end) && end);
            return true;
        }

        private static string Name(TownSimulation sim, string residentId) => sim.GetResident(residentId)?.FirstName ?? "Someone";
    }
}
