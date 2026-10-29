using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.AI
{
    public sealed class ReflectionReply
    {
        public ReflectionReply(string summary, int importance)
        {
            Summary = summary;
            Importance = importance;
        }

        public string Summary { get; }

        public int Importance { get; }
    }

    /// <summary>At bedtime a resident looks back on the day; the AI suggests one memory and how much it mattered.</summary>
    public static class ReflectionPrompt
    {
        public const int MaxLogLines = 16;
        public const int MaxSaidLines = 6;
        public const int MaxTokens = 160;
        public const double Temperature = 0.7;

        public static string BuildBody(string model, TownSimulation sim, Resident r, int day)
        {
            var sb = new StringBuilder();
            var dayStart = GameTime.FromDayAndTime(day, 6, 0);
            sb.AppendLine($"It is {PromptText.Clock(sim.Time)} and you are going to bed. What you did today:");
            foreach (var entry in PromptText.RecentLog(r, dayStart, MaxLogLines))
            {
                sb.AppendLine($"- {entry.Time.ClockText} {entry.Text}");
            }

            var said = sim.Conversations.Involving(r.Id)
                .Where(c => c.Started.TotalMinutes >= dayStart.TotalMinutes)
                .SelectMany(c => c.Lines)
                .Where(l => l.SpeakerId == r.Id)
                .Select(l => l.Text)
                .ToList();
            said = said.Skip(Math.Max(0, said.Count - MaxSaidLines)).ToList();
            if (said.Count > 0)
            {
                sb.AppendLine("Things you said: " + string.Join(" / ", said.Select(PromptText.Quote)));
            }

            PromptText.AppendMemories(sb, sim.Memories.Recall(r.Id, sim.Time, r.IsHomeless ? new[] { "home" } : new[] { r.HomeMapId }, 3));
            sb.Append("Write one short diary-style memory of today in the first person, under 30 words, as \"summary\". ")
              .Append("Rate how much today mattered to you as \"importance\" from 1 (an ordinary day) to 10 (life-changing); most days are 1 to 3.");

            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["summary"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["importance"] = new Dictionary<string, object> { ["type"] = "integer" },
                },
                ["required"] = new List<object> { "summary", "importance" },
            };

            return OllamaProtocol.BuildChatBody(model, PromptText.Persona(r), sb.ToString(), schema, Temperature, MaxTokens);
        }

        public static bool TryParse(string content, out ReflectionReply reply, out string error)
        {
            reply = null;
            if (!AiJson.TryParseObject(content, out var obj, out error))
            {
                return false;
            }

            var summary = AiJson.GetString(obj, "summary");
            if (string.IsNullOrWhiteSpace(summary))
            {
                error = "the answer had no summary";
                return false;
            }

            if (!AiJson.TryGetInt(obj, "importance", out var importance))
            {
                importance = 2;
            }

            reply = new ReflectionReply(summary, importance);
            return true;
        }
    }
}
