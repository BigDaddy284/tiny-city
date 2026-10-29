using System;
using System.IO;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The game was called The Tiny Town, and Unity keeps each game's data in a folder named after it. On the first
    /// start as Tiny City, saves, exports and settings are copied from the old folder, which is left as it was.
    /// </summary>
    public static class OldDataFolder
    {
        public const string OldName = "The Tiny Town";

        /// <summary>Copies the old folder's files if this game has none yet. Returns a line for the log, or null.</summary>
        public static string CopyIfNeeded()
        {
            try
            {
                var current = Application.persistentDataPath;
                var company = Directory.GetParent(current);
                var root = company?.Parent;
                if (root == null)
                {
                    return null;
                }

                var old = Path.Combine(root.FullName, OldName, OldName);
                if (!Directory.Exists(old) || string.Equals(Path.GetFullPath(old), Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase)
                    || Directory.Exists(Path.Combine(current, "Saves")) || File.Exists(PlayerSettingsStore.FilePath))
                {
                    return null;
                }

                var copied = Copy(old, current);
                return $"Copied {copied} file(s) from {old}, where The Tiny Town kept its saves.";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return $"Couldn't copy saves from The Tiny Town's folder: {e.Message}";
            }
        }

        private static int Copy(string from, string to)
        {
            Directory.CreateDirectory(to);
            var count = 0;
            foreach (var file in Directory.GetFiles(from))
            {
                var target = Path.Combine(to, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Copy(file, target);
                    count++;
                }
            }

            foreach (var folder in Directory.GetDirectories(from))
            {
                count += Copy(folder, Path.Combine(to, Path.GetFileName(folder)));
            }

            return count;
        }
    }
}
