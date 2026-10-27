using System;
using System.IO;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Reads and writes save files under the player's persistent data folder. Writes go to a temporary file first
    /// and the previous save is kept as a .bak, so a crash mid-write cannot destroy the only copy.
    /// </summary>
    public static class SaveStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Saves");

        public static string PathFor(string slot) => Path.Combine(Folder, slot + ".json");

        public static bool Exists(string slot) => File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak");

        public static void Write(string slot, string json)
        {
            Directory.CreateDirectory(Folder);
            var path = PathFor(slot);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                File.Replace(temp, path, path + ".bak");
            }
            else
            {
                File.Move(temp, path);
            }
        }

        /// <summary>Reads the save text, or the backup when <paramref name="useBackup"/> is true.</summary>
        public static bool TryRead(string slot, bool useBackup, out string json, out string error)
        {
            var path = PathFor(slot) + (useBackup ? ".bak" : string.Empty);
            json = null;
            error = null;
            try
            {
                if (!File.Exists(path))
                {
                    error = $"No save file at {path}.";
                    return false;
                }

                json = File.ReadAllText(path);
                return true;
            }
            catch (Exception e)
            {
                error = $"Could not read {path}: {e.Message}";
                return false;
            }
        }
    }
}
