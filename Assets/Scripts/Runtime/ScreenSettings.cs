using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>Puts the player's window, size, VSync and frame-limit choices into effect.</summary>
    public static class ScreenSettings
    {
        /// <summary>Frame limits offered when VSync is off; 0 means no limit.</summary>
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, 0 };

        public static FullScreenMode ModeOf(WindowChoice choice)
        {
            switch (choice)
            {
                case WindowChoice.Borderless:
                    return FullScreenMode.FullScreenWindow;
                case WindowChoice.FullScreen:
                    return FullScreenMode.ExclusiveFullScreen;
                default:
                    return FullScreenMode.Windowed;
            }
        }

        public static void Apply(PlayerPreferences prefs)
        {
            ApplyWindow(prefs);
            ApplyFrameRate(prefs);
        }

        public static void ApplyWindow(PlayerPreferences prefs)
        {
            var mode = ModeOf(prefs.Window);
            if (prefs.Width > 0 && prefs.Height > 0)
            {
                Screen.SetResolution(prefs.Width, prefs.Height, mode);
            }
            else if (Screen.fullScreenMode != mode)
            {
                Screen.fullScreenMode = mode;
            }
        }

        public static void ApplyFrameRate(PlayerPreferences prefs)
        {
            QualitySettings.vSyncCount = prefs.VSync ? 1 : 0;
            Application.targetFrameRate = prefs.VSync || prefs.FrameLimit <= 0 ? -1 : prefs.FrameLimit;
        }

        /// <summary>The screen sizes the display offers, smallest first, without repeats for different refresh rates.</summary>
        public static List<Vector2Int> Sizes()
        {
            var sizes = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Where(s => s.x >= PlayerPreferences.MinWidth && s.y >= PlayerPreferences.MinHeight)
                .Distinct()
                .OrderBy(s => s.x * s.y)
                .ThenBy(s => s.x)
                .ToList();
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!sizes.Contains(current))
            {
                sizes.Add(current);
                sizes = sizes.OrderBy(s => s.x * s.y).ThenBy(s => s.x).ToList();
            }

            return sizes;
        }
    }
}
