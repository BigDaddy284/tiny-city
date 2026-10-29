using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>Writes life stories and the town history as text files the player can keep or share.</summary>
    public static class ExportStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Exports");

        /// <summary>Writes <paramref name="text"/> to "&lt;name&gt; - day N.txt" in <see cref="Folder"/> and returns the full path.</summary>
        public static string Write(string name, int day, string text)
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"{SafeFileName(name)} - day {day}.txt");
            File.WriteAllText(path, text, new UTF8Encoding(true));
            return path;
        }

        /// <summary>Copies a save file to "&lt;name&gt; - day N.json" in <see cref="Folder"/> and returns the full path.</summary>
        public static string CopySave(string sourcePath, string name, int day)
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"{SafeFileName(name)} - day {day}.json");
            File.Copy(sourcePath, path, overwrite: true);
            return path;
        }

        /// <summary>Opens the exports folder in the file browser.</summary>
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            Application.OpenURL("file:///" + Folder.Replace('\\', '/'));
        }

        /// <summary>Keeps letters, digits, spaces, dashes and apostrophes, so any name makes a valid Windows file name.</summary>
        public static string SafeFileName(string name)
        {
            var kept = new string((name ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'').ToArray()).Trim();
            return kept.Length == 0 ? "export" : kept.Substring(0, Mathf.Min(kept.Length, 60));
        }
    }
}
