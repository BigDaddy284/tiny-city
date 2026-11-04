using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Simulation.AI
{
    /// <summary>What the AI wrote for one line of a conversation, before the game checks it.</summary>
    public sealed class DialogueReply
    {
        public DialogueReply(string say, string tone, string topic, bool end, int giveFood = 0, long pricePence = 0, bool acceptOffer = false)
        {
            Say = say;
            Tone = tone;
            Topic = topic;
            End = end;
            GiveFood = giveFood;
            PricePence = pricePence;
            AcceptOffer = acceptOffer;
        }

        /// <summary>Tiny City 2: portions of food offered to the other person (0 for none).</summary>
        public int GiveFood { get; }

        /// <summary>What the offer costs them, in pence; 0 is a gift.</summary>
        public long PricePence { get; }

        /// <summary>Tiny City 2: whether they take the food the other person offered.</summary>
        public bool AcceptOffer { get; }

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
            var memories = sim.Memories.Recall(speaker.Id, sim.Time, new[] { session.ListenerId, "chat", "social" }, MemoryCount, sim.Memories.FocusOf(speaker.Id));
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

            var food = sim.FoodMustBeMade;
            if (food)
            {
                sb.AppendLine($"Your food: {speaker.Food.Describe(sim.Time.Day)}. Your money: {Economy.Money.Format(speaker.MoneyPence)}.");
                var offer = session.FoodOffer;
                if (offer != null && offer.ToId == speaker.Id)
                {
                    sb.AppendLine($"{listener.FirstName} has just offered you {offer.Portions} portion{(offer.Portions == 1 ? string.Empty : "s")} of food " +
                        (offer.PricePence > 0 ? $"for {Economy.Money.Format(offer.PricePence)}." : "as a gift."));
                }
            }

            sb.Append("Reply as JSON: \"say\" is your next line (one or two sentences, under 25 words, no narration), ")
              .Append("\"tone\" is one of warm, playful, neutral or disagree, ")
              .Append("\"topic\" is what you are talking about in a few words, ")
              .Append("and \"end\" is true if you want to wrap up the chat.");
            if (food)
            {
                sb.Append($" If you want to hand over some of your food, \"give_food\" is how many portions (0 to {FoodRules.MaxOfferPortions}) and \"price\" what you ask for them in pounds (0 for a gift); otherwise give_food is 0.")
                  .Append(" \"accept_food\" is true if you take food you have just been offered.");
            }

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
                    ["give_food"] = new Dictionary<string, object> { ["type"] = "integer" },
                    ["price"] = new Dictionary<string, object> { ["type"] = "number" },
                    ["accept_food"] = new Dictionary<string, object> { ["type"] = "boolean" },
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

            var give = AiJson.TryGetNumber(obj, "give_food", out var g) && g >= 1 && g <= FoodRules.MaxOfferPortions ? (int)g : 0;
            var price = AiJson.TryGetNumber(obj, "price", out var p) && p > 0 && p * 100 <= FoodRules.MaxOfferPence ? (long)Math.Round(p * 100) : 0;
            reply = new DialogueReply(say, AiJson.GetString(obj, "tone"), AiJson.GetString(obj, "topic"), AiJson.TryGetBool(obj, "end", out var end) && end,
                give, price, AiJson.TryGetBool(obj, "accept_food", out var accept) && accept);
            return true;
        }

        private static string Name(TownSimulation sim, string residentId) => sim.GetResident(residentId)?.FirstName ?? "Someone";
    }
}
