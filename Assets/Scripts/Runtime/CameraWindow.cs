using TinyCity.Simulation.Configuration;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The Cameras window, opened from the button beside Settings: lets the camera panels take turns by themselves
    /// every so many game hours, showing residents, buildings, half of each or everything (<see cref="CameraCycle"/>).
    /// Choices are kept with the other player settings.
    /// </summary>
    internal sealed class CameraWindow
    {
        private const float MaxWidth = 560f;
        private static readonly string[] OnOff = { "On", "Off" };
        private static readonly string[] Modes = { "Residents Only", "Buildings Only", "Half and Half", "Cycle All" };

        private readonly GameController game;

        public CameraWindow(GameController game)
        {
            this.game = game;
        }

        public bool IsOpen { get; private set; }

        /// <summary>Where the window was last drawn, so clicks on it don't reach the camera panels.</summary>
        public Rect Area { get; private set; }

        public string SaveError { get; private set; }

        private PlayerPreferences Prefs => game.Preferences;

        public void Toggle() => IsOpen = !IsOpen;

        public void Draw(Rect cameraArea, GuiStyles styles)
        {
            if (!IsOpen)
            {
                Area = Rect.zero;
                return;
            }

            var p = UiTheme.Palette;
            var width = Mathf.Min(MaxWidth, cameraArea.width - 40f);
            Area = new Rect(cameraArea.x + ((cameraArea.width - width) / 2f), cameraArea.y + 36f, width, Mathf.Min(380f, cameraArea.height - 72f));
            GUI.color = UiTheme.ToColor(p.Window, 0.98f);
            GUI.DrawTexture(Area, Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(p.BarBorder);
            GUI.DrawTexture(new Rect(Area.x, Area.y, Area.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Area.x, Area.yMax - 1f, Area.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Area.x, Area.y, 1f, Area.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Area.xMax - 1f, Area.y, 1f, Area.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(Area.x + 16f, Area.y + 12f, Area.width - 32f, Area.height - 24f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cameras", styles.Title);
            if (GUILayout.Button("Done", GUILayout.Width(80f)))
            {
                game.ToggleCameras();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("AUTO CYCLE", styles.Section);
            var changed = false;
            var on = Choice("Cycle cameras", Prefs.CycleCameras ? 0 : 1, OnOff, styles) == 0;
            changed |= on != Prefs.CycleCameras;
            Prefs.CycleCameras = on;

            var intervals = CameraCycle.Intervals;
            var labels = new string[intervals.Length];
            var chosen = 0;
            for (var i = 0; i < intervals.Length; i++)
            {
                labels[i] = intervals[i] < 60 ? $"{intervals[i]} min" : $"{intervals[i] / 60} h";
                chosen = intervals[i] == Prefs.CycleMinutes ? i : chosen;
            }

            var minutes = intervals[Choice("Every (game time)", chosen, labels, styles)];
            changed |= minutes != Prefs.CycleMinutes;
            Prefs.CycleMinutes = minutes;

            var mode = (CycleMode)Choice("Show", (int)Prefs.CycleMode, Modes, styles);
            changed |= mode != Prefs.CycleMode;
            Prefs.CycleMode = mode;

            var busy = Choice("Skip empty buildings", Prefs.CycleBusyOnly ? 0 : 1, OnOff, styles) == 0;
            changed |= busy != Prefs.CycleBusyOnly;
            Prefs.CycleBusyOnly = busy;

            GUILayout.Space(8f);
            GUILayout.Label(game.CameraCycleStatus, styles.Text);
            GUI.enabled = Prefs.CycleCameras;
            if (GUILayout.Button("Next Now", GUILayout.Width(110f), GUILayout.Height(26f)))
            {
                game.NextCameraTurn();
            }

            GUI.enabled = true;
            GUILayout.Space(8f);
            GUILayout.Label(
                "With one panel open it shows each in turn. With more, they share the list: four panels show four at a time, then the next four. " +
                "Half and Half puts the first half of the panels on residents and the rest on buildings. Buildings are the businesses and the homes someone lives in. " +
                "Town View pauses the cycle.",
                styles.Small);
            if (SaveError != null)
            {
                GUILayout.Label($"Settings could not be saved: {SaveError}", styles.Warning);
            }

            GUILayout.EndArea();
            if (changed)
            {
                game.RestartCameraCycle();
                SaveError = PlayerSettingsStore.Save(Prefs);
            }
        }

        private static int Choice(string label, int selected, string[] options, GuiStyles styles)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, styles.Text, GUILayout.Width(150f));
            selected = GUILayout.Toolbar(selected, options, GUILayout.Height(26f));
            GUILayout.EndHorizontal();
            return selected;
        }
    }
}
