using System;
using System.Collections.Generic;

namespace TinyTown.Simulation.Configuration
{
    /// <summary>
    /// Connection settings for the player's locally installed Ollama service.
    /// Public fields so Unity's JsonUtility can read them from settings.json.
    /// </summary>
    [Serializable]
    public sealed class OllamaSettings
    {
        public const string DefaultEndpoint = "http://localhost:11434";
        public const string DefaultModel = "qwen2.5:7b";
        public const int DefaultTimeoutSeconds = 60;
        public const int MaxTimeoutSeconds = 600;

        public string endpoint = DefaultEndpoint;
        public string model = DefaultModel;
        public int timeoutSeconds = DefaultTimeoutSeconds;

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"Ollama endpoint '{endpoint}' is not an http or https URL.");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                errors.Add("Ollama model name is empty.");
            }

            if (timeoutSeconds <= 0 || timeoutSeconds > MaxTimeoutSeconds)
            {
                errors.Add($"Ollama timeout must be between 1 and {MaxTimeoutSeconds} seconds, but was {timeoutSeconds}.");
            }

            return errors;
        }
    }
}
