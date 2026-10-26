using System;
using System.Collections.Generic;

namespace TinyTown.Simulation.Configuration
{
    /// <summary>
    /// Player-editable settings loaded from StreamingAssets/Configuration/settings.json.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public OllamaSettings ollama = new OllamaSettings();

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (version < 1 || version > CurrentVersion)
            {
                errors.Add($"Settings version {version} is not supported (expected 1 to {CurrentVersion}).");
            }

            if (ollama == null)
            {
                errors.Add("Ollama settings are missing.");
            }
            else
            {
                errors.AddRange(ollama.Validate());
            }

            return errors;
        }

        /// <summary>
        /// Returns these settings if they are valid, otherwise the defaults, plus any problems found.
        /// The game always starts with usable settings even if the file is hand-edited badly.
        /// </summary>
        public static GameSettings ValidOrDefault(GameSettings candidate, out IReadOnlyList<string> problems)
        {
            if (candidate == null)
            {
                problems = new List<string> { "Settings could not be read; using defaults." };
                return new GameSettings();
            }

            var errors = candidate.Validate();
            if (errors.Count == 0)
            {
                problems = errors;
                return candidate;
            }

            var withNote = new List<string>(errors) { "Using default settings instead." };
            problems = withNote;
            return new GameSettings();
        }
    }
}
