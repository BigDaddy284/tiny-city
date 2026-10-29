using System;
using System.Collections.Generic;
using System.IO;
using TinyCity.Simulation.Configuration;
using UnityEngine;

namespace TinyCity.Runtime
{
    public static class SettingsLoader
    {
        public static string SettingsPath =>
            Path.Combine(Application.streamingAssetsPath, "Configuration", "settings.json");

        public static GameSettings Load(out IReadOnlyList<string> problems)
        {
            GameSettings parsed = null;
            var readProblems = new List<string>();

            try
            {
                if (File.Exists(SettingsPath))
                {
                    parsed = JsonUtility.FromJson<GameSettings>(File.ReadAllText(SettingsPath));
                }
                else
                {
                    readProblems.Add($"Settings file not found at {SettingsPath}.");
                }
            }
            catch (Exception e)
            {
                readProblems.Add($"Settings file could not be read: {e.Message}");
            }

            var settings = GameSettings.ValidOrDefault(parsed, out var validationProblems);
            readProblems.AddRange(validationProblems);
            problems = readProblems;

            foreach (var problem in problems)
            {
                Debug.LogWarning($"[TinyCity] {problem}");
            }

            return settings;
        }
    }
}
