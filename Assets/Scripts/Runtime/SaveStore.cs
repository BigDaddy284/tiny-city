using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyCity.Simulation.Persistence;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Reads and writes save files under the player's persistent data folder. Writes go to a temporary file first
    /// and the previous save is kept as a .bak, so a crash mid-write cannot destroy the only copy.
    /// </summary>
    public static class SaveStore
    {
        /// <summary>
        /// Where towns are kept since the fresh start of 30 October 2026. Saves from before it stay untouched in the old
        /// "Saves" folder beside it and aren't read.
        /// </summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Towns");

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

        /// <summary>The slot names, kept beside the saves.</summary>
        public static string IndexPath => Path.Combine(Folder, "slots.json");

        /// <summary>Every slot with a save or a backup, newest first.</summary>
        public static List<string> ListSlots()
        {
            if (!Directory.Exists(Folder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(Folder)
                .Select(SlotOf)
                .Where(SaveSlotIndex.IsValidSlotId)
                .Distinct()
                .OrderByDescending(LastWritten)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>When the slot was last saved, or <see cref="DateTime.MinValue"/> if it has no file.</summary>
        public static DateTime LastWritten(string slot)
        {
            var path = PathFor(slot);
            return File.Exists(path) ? File.GetLastWriteTime(path) : File.Exists(path + ".bak") ? File.GetLastWriteTime(path + ".bak") : DateTime.MinValue;
        }

        public static SaveSlotIndex ReadIndex()
        {
            try
            {
                return File.Exists(IndexPath) ? SaveSlotIndex.FromJson(File.ReadAllText(IndexPath)) : new SaveSlotIndex();
            }
            catch (IOException)
            {
                return new SaveSlotIndex();
            }
        }

        public static void WriteIndex(SaveSlotIndex index)
        {
            Directory.CreateDirectory(Folder);
            var temp = IndexPath + ".tmp";
            File.WriteAllText(temp, index.ToJson());
            if (File.Exists(IndexPath))
            {
                File.Delete(IndexPath);
            }

            File.Move(temp, IndexPath);
        }

        /// <summary>Copies a slot's save (or its backup, if the save itself is missing) into a new slot.</summary>
        public static void Copy(string from, string to)
        {
            var source = File.Exists(PathFor(from)) ? PathFor(from) : PathFor(from) + ".bak";
            Directory.CreateDirectory(Folder);
            File.Copy(source, PathFor(to), overwrite: false);
        }

        /// <summary>Removes a slot's save, backup and any half-written temporary file.</summary>
        public static void Delete(string slot)
        {
            foreach (var path in new[] { PathFor(slot), PathFor(slot) + ".bak", PathFor(slot) + ".tmp" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>Opens the saves folder in the file browser.</summary>
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file:///" + Folder.Replace('\\', '/'));
        }

        private static string SlotOf(string file)
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase))
            {
                return name.Substring(0, name.Length - ".json.bak".Length);
            }

            return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - ".json".Length) : null;
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
