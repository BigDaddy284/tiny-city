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
        public const int DefaultMaxRetries = 1;
        public const int MaxMaxRetries = 3;
        public const int DefaultDecisionWaitMinutes = 20;
        public const int MaxDecisionWaitMinutes = 240;

        /// <summary>False keeps every resident on the built-in routine and never contacts Ollama.</summary>
        public bool enabled = true;
        public string endpoint = DefaultEndpoint;
        public string model = DefaultModel;
        public int timeoutSeconds = DefaultTimeoutSeconds;

        /// <summary>Extra attempts after a malformed answer, timeout or connection error.</summary>
        public int maxRetries = DefaultMaxRetries;

        /// <summary>Game minutes a resident waits for an AI suggestion before using the built-in routine.</summary>
        public int decisionWaitMinutes = DefaultDecisionWaitMinutes;

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

            if (maxRetries < 0 || maxRetries > MaxMaxRetries)
            {
                errors.Add($"Ollama maxRetries must be between 0 and {MaxMaxRetries}, but was {maxRetries}.");
            }

            if (decisionWaitMinutes < 1 || decisionWaitMinutes > MaxDecisionWaitMinutes)
            {
                errors.Add($"Ollama decisionWaitMinutes must be between 1 and {MaxDecisionWaitMinutes}, but was {decisionWaitMinutes}.");
            }

            return errors;
        }
    }
}
