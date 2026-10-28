using System;
using System.Collections.Generic;
using System.IO;
using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Keeps the Settings window's choices in <c>player-settings.json</c> in the player's data folder, next to the
    /// saves, so updating the game never resets them. Writes go to a temporary file first.
    /// </summary>
    public static class PlayerSettingsStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "player-settings.json");

        public static PlayerPreferences Load(out IReadOnlyList<string> problems)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    problems = new List<string>();
                    return new PlayerPreferences();
                }

                return PlayerPreferences.FromJson(File.ReadAllText(FilePath), out problems);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                problems = new List<string> { $"Player settings could not be read ({e.Message}); using defaults." };
                return new PlayerPreferences();
            }
        }

        /// <summary>Writes the preferences; returns an error message, or null when saved.</summary>
        public static string Save(PlayerPreferences prefs)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, prefs.ToJson());
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }

                File.Move(temp, FilePath);
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return e.Message;
            }
        }
    }
}
