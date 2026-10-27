using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Persistence;

namespace TinyTown.Simulation.AI
{
    /// <summary>
    /// Builds and reads Ollama HTTP API messages (POST /api/chat, GET /api/tags). Kept free of networking so it
    /// can be tested without a running service.
    /// </summary>
    public static class OllamaProtocol
    {
        /// <summary>How long Ollama keeps the model in memory after a request, so the next one starts quickly.</summary>
        public const string KeepAlive = "30m";

        public static string ChatUrl(string endpoint) => Trim(endpoint) + "/api/chat";

        public static string TagsUrl(string endpoint) => Trim(endpoint) + "/api/tags";

        /// <summary>
        /// A non-streaming chat request. <paramref name="schema"/> is a JSON schema Ollama uses to constrain the answer
        /// (structured outputs); the game still validates whatever comes back.
        /// </summary>
        public static string BuildChatBody(string model, string system, string user, Dictionary<string, object> schema, double temperature, int maxTokens)
        {
            var body = new Dictionary<string, object>
            {
                ["model"] = model,
                ["stream"] = false,
                ["keep_alive"] = KeepAlive,
                ["options"] = new Dictionary<string, object>
                {
                    ["temperature"] = temperature,
                    ["num_predict"] = maxTokens,
                },
                ["messages"] = new List<object>
                {
                    new Dictionary<string, object> { ["role"] = "system", ["content"] = system ?? string.Empty },
                    new Dictionary<string, object> { ["role"] = "user", ["content"] = user ?? string.Empty },
                },
            };

            if (schema != null)
            {
                body["format"] = schema;
            }

            return Json.Write(body);
        }

        /// <summary>A request with no messages, which makes Ollama load the model into memory and reply at once.</summary>
        public static string BuildLoadModelBody(string model) => Json.Write(new Dictionary<string, object>
        {
            ["model"] = model,
            ["stream"] = false,
            ["keep_alive"] = KeepAlive,
            ["messages"] = new List<object>(),
        });

        /// <summary>Reads the assistant's text from a chat response, or Ollama's own error message.</summary>
        public static bool TryReadChatContent(string responseJson, out string content, out string error)
        {
            content = null;
            if (!TryParse(responseJson, out var root, out error))
            {
                return false;
            }

            var message = root.TryGetValue("message", out var m) ? m as Dictionary<string, object> : null;
            content = AiJson.GetString(message, "content");
            if (content == null)
            {
                error = "Ollama's reply had no message content";
                return false;
            }

            return true;
        }

        /// <summary>Reads the installed model names from a /api/tags response.</summary>
        public static bool TryReadModelNames(string responseJson, out List<string> names, out string error)
        {
            names = new List<string>();
            if (!TryParse(responseJson, out var root, out error))
            {
                return false;
            }

            if (!(root.TryGetValue("models", out var value) && value is List<object> models))
            {
                error = "Ollama's model list was missing";
                return false;
            }

            foreach (var entry in models.OfType<Dictionary<string, object>>())
            {
                var name = AiJson.GetString(entry, "name") ?? AiJson.GetString(entry, "model");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return true;
        }

        /// <summary>Ollama names untagged models "name:latest", so "llama3" and "llama3:latest" are the same model.</summary>
        public static bool HasModel(IEnumerable<string> installed, string configured)
        {
            var wanted = Normalise(configured);
            return installed.Any(n => Normalise(n) == wanted);
        }

        /// <summary>Ollama reports problems as {"error": "..."}; returns that text if present.</summary>
        public static string ReadError(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return null;
            }

            try
            {
                return AiJson.GetString(Json.Parse(responseJson) as Dictionary<string, object>, "error");
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static bool TryParse(string json, out Dictionary<string, object> root, out string error)
        {
            root = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Ollama sent an empty reply";
                return false;
            }

            try
            {
                root = Json.Parse(json) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                error = $"Ollama's reply was not valid JSON ({e.Message})";
                return false;
            }

            if (root == null)
            {
                error = "Ollama's reply was not a JSON object";
                return false;
            }

            var ollamaError = AiJson.GetString(root, "error");
            if (ollamaError != null)
            {
                error = $"Ollama: {ollamaError}";
                return false;
            }

            return true;
        }

        private static string Normalise(string name)
        {
            var n = (name ?? string.Empty).Trim().ToLowerInvariant();
            return n.Contains(':') ? n : n + ":latest";
        }

        private static string Trim(string endpoint) => (endpoint ?? string.Empty).Trim().TrimEnd('/');
    }
}
