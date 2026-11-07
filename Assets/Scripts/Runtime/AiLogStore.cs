using System;
using System.IO;
using System.Text;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Writes every AI question and answer to <c>persistentDataPath/AiLog/ai-log.txt</c> (what was asked, what came
    /// back, and whether the game used it), so the owner can send it in to look into how residents behave. When the
    /// file passes <see cref="MaxBytes"/> it becomes <c>ai-log-previous.txt</c> and a new one starts.
    /// </summary>
    internal static class AiLogStore
    {
        public const long MaxBytes = 20L * 1024 * 1024;

        public static string Folder => Path.Combine(Application.persistentDataPath, "AiLog");

        public static string FilePath => Path.Combine(Folder, "ai-log.txt");

        private static bool failed;

        /// <summary>
        /// " | model | 2.4s" from Ollama's answer (its "model" and "total_duration"), so a log covering several models
        /// shows which answered what and how fast; empty when the answer has neither.
        /// </summary>
        private static string ModelAndTime(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return string.Empty;
            }

            var model = System.Text.RegularExpressions.Regex.Match(response, "\"model\"\\s*:\\s*\"([^\"]+)\"");
            var nanos = System.Text.RegularExpressions.Regex.Match(response, "\"total_duration\"\\s*:\\s*(\\d+)");
            var text = model.Success ? " | " + model.Groups[1].Value : string.Empty;
            if (nanos.Success && long.TryParse(nanos.Groups[1].Value, out var ns))
            {
                text += $" | {ns / 1e9:0.0}s";
            }

            return text;
        }

        public static void Write(TownSimulation simulation, AiRequest request, string response, AiReplyCheck check)
        {
            if (failed)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Folder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    var previous = Path.Combine(Folder, "ai-log-previous.txt");
                    File.Delete(previous);
                    File.Move(FilePath, previous);
                }

                var answer = OllamaProtocol.TryReadChatContent(response, out var content, out _) ? content : response;
                var when = simulation == null ? string.Empty : $" | day {simulation.Time.Day} {simulation.Time.ClockText}";
                var sb = new StringBuilder();
                sb.Append("=== ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(when)
                  .Append(" | ").Append(request.Key).Append(ModelAndTime(response)).Append(" | ").Append(check.Status)
                  .Append(string.IsNullOrEmpty(check.Detail) ? string.Empty : ": " + check.Detail).AppendLine();
                sb.AppendLine("--- asked:");
                sb.AppendLine(OllamaProtocol.PromptText(request.Body));
                sb.AppendLine("--- answered:");
                sb.AppendLine(answer);
                sb.AppendLine();
                File.AppendAllText(FilePath, sb.ToString());
            }
            catch (Exception e)
            {
                failed = true;
                Debug.LogWarning($"[TinyCity] The AI log could not be written, so it is off for this run: {e.Message}");
            }
        }
    }
}
