using System;
using System.Collections.Generic;

namespace TinyCity.Simulation.Configuration
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
        public const int DefaultDecisionWaitSeconds = 8;
        public const int MaxDecisionWaitSeconds = 120;
        public const int DefaultParallelRequests = 1;
        public const int MaxParallelRequests = 8;

        /// <summary>False keeps every resident on the built-in routine and never contacts Ollama.</summary>
        public bool enabled = true;
        public string endpoint = DefaultEndpoint;
        public string model = DefaultModel;
        public int timeoutSeconds = DefaultTimeoutSeconds;

        /// <summary>Extra attempts after a malformed answer, timeout or connection error.</summary>
        public int maxRetries = DefaultMaxRetries;

        /// <summary>Game minutes a resident waits for an AI suggestion before using the built-in routine.</summary>
        public int decisionWaitMinutes = DefaultDecisionWaitMinutes;

        /// <summary>
        /// Real seconds a resident waits for an AI suggestion at the least, whatever the game speed: at high speeds
        /// this is more game minutes than <see cref="decisionWaitMinutes"/>, so answers still arrive in time. 0 turns it off.
        /// </summary>
        public int decisionWaitSeconds = DefaultDecisionWaitSeconds;

        /// <summary>How many requests are sent to Ollama at once. More than 1 helps only if Ollama's OLLAMA_NUM_PARALLEL allows it.</summary>
        public int parallelRequests = DefaultParallelRequests;

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

            if (decisionWaitSeconds < 0 || decisionWaitSeconds > MaxDecisionWaitSeconds)
            {
                errors.Add($"Ollama decisionWaitSeconds must be between 0 and {MaxDecisionWaitSeconds}, but was {decisionWaitSeconds}.");
            }

            if (parallelRequests < 1 || parallelRequests > MaxParallelRequests)
            {
                errors.Add($"Ollama parallelRequests must be between 1 and {MaxParallelRequests}, but was {parallelRequests}.");
            }

            return errors;
        }
    }
}
