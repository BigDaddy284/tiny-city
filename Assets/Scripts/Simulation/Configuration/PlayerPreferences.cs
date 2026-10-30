using System;
using System.Collections.Generic;
using TinyCity.Simulation.Persistence;

namespace TinyCity.Simulation.Configuration
{
    /// <summary>How the side panel and windows look. Dark first, as in Stills &amp; Reels, whose default it is.</summary>
    public enum ThemeChoice
    {
        Dark,
        Light,
    }

    public enum WindowChoice
    {
        Windowed,
        Borderless,
        FullScreen,
    }

    /// <summary>
    /// What the player chose in the Settings window, kept in a file of their own next to their saves so a new
    /// version of the game never overwrites it. AI choices made here take the place of settings.json's
    /// <c>enabled</c> and <c>model</c>; until the player changes them, settings.json decides. Every value is
    /// checked when read, so a damaged or hand-edited file still gives usable settings.
    /// </summary>
    public sealed class PlayerPreferences
    {
        public const int MaxVolume = 100;
        public const int MaxFrameLimit = 240;
        public const int MinWidth = 800;
        public const int MinHeight = 600;
        public const int MaxModelNameLength = 200;

        /// <summary>Null until the player turns the AI on or off here.</summary>
        public bool? AiEnabled { get; set; }

        /// <summary>The Ollama model the player picked, or null to use settings.json's.</summary>
        public string AiModel { get; set; }

        public int MasterVolume { get; set; } = 80;

        public int MusicVolume { get; set; } = 60;

        public int AmbienceVolume { get; set; } = 70;

        public int EffectsVolume { get; set; } = 80;

        public WindowChoice Window { get; set; } = WindowChoice.Windowed;

        /// <summary>The window or screen size, or 0 to leave it as it is.</summary>
        public int Width { get; set; }

        public int Height { get; set; }

        public bool VSync { get; set; } = true;

        /// <summary>Frames per second when VSync is off; 0 means no limit.</summary>
        public int FrameLimit { get; set; } = 60;

        public ThemeChoice Theme { get; set; } = ThemeChoice.Dark;

        /// <summary>Whether the camera panels take turns showing residents and buildings by themselves.</summary>
        public bool CycleCameras { get; set; }

        /// <summary>Game minutes between turns, one of <see cref="CameraCycle.Intervals"/>.</summary>
        public int CycleMinutes { get; set; } = CameraCycle.DefaultInterval;

        public CycleMode CycleMode { get; set; } = CycleMode.Everything;

        /// <summary>Whether the cycle skips buildings nobody is in at the moment.</summary>
        public bool CycleBusyOnly { get; set; } = true;

        /// <summary>Puts the player's AI choices into the Ollama settings the game runs with.</summary>
        public void ApplyTo(OllamaSettings ollama)
        {
            if (ollama == null)
            {
                return;
            }

            if (AiEnabled.HasValue)
            {
                ollama.enabled = AiEnabled.Value;
            }

            if (!string.IsNullOrWhiteSpace(AiModel))
            {
                ollama.model = AiModel;
            }
        }

        /// <summary>A volume from 0 to 1, after the master volume: what the sound system will play at.</summary>
        public double Volume(int channel) => Clamp(MasterVolume, 0, MaxVolume) * Clamp(channel, 0, MaxVolume) / (double)(MaxVolume * MaxVolume);

        public string ToJson()
        {
            var root = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["aiEnabled"] = AiEnabled.HasValue ? (object)AiEnabled.Value : null,
                ["aiModel"] = AiModel,
                ["masterVolume"] = MasterVolume,
                ["musicVolume"] = MusicVolume,
                ["ambienceVolume"] = AmbienceVolume,
                ["effectsVolume"] = EffectsVolume,
                ["window"] = Window.ToString(),
                ["width"] = Width,
                ["height"] = Height,
                ["vsync"] = VSync,
                ["frameLimit"] = FrameLimit,
                ["theme"] = Theme.ToString(),
                ["cycleCameras"] = CycleCameras,
                ["cycleMinutes"] = CycleMinutes,
                ["cycleMode"] = CycleMode.ToString(),
                ["cycleBusyOnly"] = CycleBusyOnly,
            };
            return Json.Write(root);
        }

        /// <summary>Reads preferences written by <see cref="ToJson"/>. Anything missing or wrong keeps its default and is reported.</summary>
        public static PlayerPreferences FromJson(string json, out IReadOnlyList<string> problems)
        {
            var found = new List<string>();
            problems = found;
            var prefs = new PlayerPreferences();
            if (string.IsNullOrWhiteSpace(json))
            {
                return prefs;
            }

            Dictionary<string, object> root;
            try
            {
                root = Json.Parse(json) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                found.Add($"Player settings could not be read ({e.Message}); using defaults.");
                return prefs;
            }

            if (root == null)
            {
                found.Add("Player settings are not a JSON object; using defaults.");
                return prefs;
            }

            if (root.TryGetValue("aiEnabled", out var ai) && ai is bool on)
            {
                prefs.AiEnabled = on;
            }

            if (root.TryGetValue("aiModel", out var model) && model is string name)
            {
                name = name.Trim();
                if (name.Length > 0 && name.Length <= MaxModelNameLength)
                {
                    prefs.AiModel = name;
                }
                else
                {
                    found.Add("The chosen AI model name is not valid; using settings.json's.");
                }
            }

            prefs.MasterVolume = Number(root, "masterVolume", prefs.MasterVolume, 0, MaxVolume, found);
            prefs.MusicVolume = Number(root, "musicVolume", prefs.MusicVolume, 0, MaxVolume, found);
            prefs.AmbienceVolume = Number(root, "ambienceVolume", prefs.AmbienceVolume, 0, MaxVolume, found);
            prefs.EffectsVolume = Number(root, "effectsVolume", prefs.EffectsVolume, 0, MaxVolume, found);
            prefs.Window = Choice(root, "window", prefs.Window, found);
            prefs.Width = Number(root, "width", 0, 0, 16384, found);
            prefs.Height = Number(root, "height", 0, 0, 16384, found);
            if (prefs.Width < MinWidth || prefs.Height < MinHeight)
            {
                prefs.Width = 0;
                prefs.Height = 0;
            }

            if (root.TryGetValue("vsync", out var vsync) && vsync is bool v)
            {
                prefs.VSync = v;
            }

            prefs.FrameLimit = Number(root, "frameLimit", prefs.FrameLimit, 0, MaxFrameLimit, found);
            prefs.Theme = Choice(root, "theme", prefs.Theme, found);
            if (root.TryGetValue("cycleCameras", out var cycle) && cycle is bool cycling)
            {
                prefs.CycleCameras = cycling;
            }

            var minutes = Number(root, "cycleMinutes", prefs.CycleMinutes, CameraCycle.Intervals[0], CameraCycle.Intervals[CameraCycle.Intervals.Length - 1], found);
            if (Array.IndexOf(CameraCycle.Intervals, minutes) < 0)
            {
                found.Add($"Player setting cycleMinutes {minutes} is not one of the choices; using {CameraCycle.DefaultInterval}.");
                minutes = CameraCycle.DefaultInterval;
            }

            prefs.CycleMinutes = minutes;
            prefs.CycleMode = Choice(root, "cycleMode", prefs.CycleMode, found);
            if (root.TryGetValue("cycleBusyOnly", out var busy) && busy is bool busyOnly)
            {
                prefs.CycleBusyOnly = busyOnly;
            }

            return prefs;
        }

        private static int Number(Dictionary<string, object> root, string key, int fallback, int min, int max, List<string> problems)
        {
            if (!root.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            if (value is double d && !double.IsNaN(d))
            {
                var rounded = (int)Math.Round(Math.Max(int.MinValue, Math.Min(int.MaxValue, d)));
                if (rounded < min || rounded > max)
                {
                    problems.Add($"Player setting {key} was {rounded}; kept between {min} and {max}.");
                }

                return Clamp(rounded, min, max);
            }

            problems.Add($"Player setting {key} is not a number; using {fallback}.");
            return fallback;
        }

        private static T Choice<T>(Dictionary<string, object> root, string key, T fallback, List<string> problems)
            where T : struct
        {
            if (!root.TryGetValue(key, out var value) || value == null)
            {
                return fallback;
            }

            if (value is string text && Enum.TryParse<T>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                return parsed;
            }

            problems.Add($"Player setting {key} '{value}' is not known; using {fallback}.");
            return fallback;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
