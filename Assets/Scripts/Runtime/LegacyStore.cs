using System.IO;
using System.Text;
using TinyCity.Simulation;
using TinyCity.Simulation.History;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Tiny City 2: when everyone in a world has died, its legacy log (<see cref="Legacy.Write"/>) goes in the Legacy
    /// folder, named after the save slot and the day, and as latest.txt, which the next new world reads its lessons from.
    /// </summary>
    public static class LegacyStore
    {
        public const string LatestFile = "latest.txt";

        public static string Folder => Path.Combine(Application.persistentDataPath, "Legacy");

        /// <summary>Writes the log for a world where everyone has died, once; returns its path, or null if it was already written.</summary>
        public static string Write(string slot, TownSimulation sim)
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"{ExportStore.SafeFileName(slot)} - day {sim.Time.Day}.txt");
            if (File.Exists(path))
            {
                return null;
            }

            var text = Legacy.Write(sim);
            File.WriteAllText(path, text, new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(Folder, LatestFile), text, new UTF8Encoding(true));
            return path;
        }

        /// <summary>The lessons the last world that died out left, or none.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> LatestLessons()
        {
            try
            {
                var path = Path.Combine(Folder, LatestFile);
                return Legacy.ReadLessons(File.Exists(path) ? File.ReadAllText(path) : null);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[TinyCity] Couldn't read the last world's legacy: {e.Message}");
                return Legacy.ReadLessons(null);
            }
        }
    }
}
