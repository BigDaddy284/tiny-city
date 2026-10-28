using System.Collections.Generic;
using TinyTown.Simulation.AI;
using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// The Settings window, opened from the button beside the town's name: local AI on or off and which installed
    /// Ollama model to use, volumes, the window and frame rate, and the dark or light theme. Every change
    /// takes effect at once and is kept in <see cref="PlayerSettingsStore"/>.
    /// </summary>
    internal sealed class SettingsWindow
    {
        private const float MaxWidth = 560f;
        private const float SaveDelaySeconds = 1f;
        private static readonly string[] OnOff = { "On", "Off" };
        private static readonly string[] Windows = { "Windowed", "Borderless", "Full screen" };
        private static readonly string[] Themes = { "Dark", "Light" };

        private readonly GameController game;
        private Vector2 scroll;
        private List<Vector2Int> sizes = new List<Vector2Int>();
        private float saveAt = -1f;

        public SettingsWindow(GameController game)
        {
            this.game = game;
        }

        public bool IsOpen { get; private set; }

        /// <summary>Where the window was last drawn, so clicks on it don't reach the camera panels.</summary>
        public Rect Area { get; private set; }

        /// <summary>Why the settings could not be saved last time, or null.</summary>
        public string SaveError { get; private set; }

        private PlayerPreferences Prefs => game.Preferences;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                sizes = ScreenSettings.Sizes();
                game.Ai.RefreshModelList();
            }
            else
            {
                SaveNow();
            }
        }

        /// <summary>Saves a pending change once the player has stopped dragging for a moment.</summary>
        public void Update()
        {
            if (saveAt >= 0f && Time.unscaledTime >= saveAt)
            {
                SaveNow();
            }
        }

        public void SaveNow()
        {
            if (saveAt < 0f)
            {
                return;
            }

            saveAt = -1f;
            SaveError = PlayerSettingsStore.Save(Prefs);
            if (SaveError != null)
            {
                Debug.LogWarning($"[TinyTown] Settings could not be saved: {SaveError}");
            }
        }

        public void Draw(Rect cameraArea, GuiStyles styles)
        {
            if (!IsOpen)
            {
                Area = Rect.zero;
                return;
            }

            var p = UiTheme.Palette;
            var width = Mathf.Min(MaxWidth, cameraArea.width - 40f);
            Area = new Rect(cameraArea.x + ((cameraArea.width - width) / 2f), cameraArea.y + 36f, width, cameraArea.height - 72f);
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
            GUILayout.Label("Settings", styles.Title);
            if (GUILayout.Button("Done", GUILayout.Width(80f)))
            {
                game.ToggleSettings();
            }

            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            DrawAi(styles);
            DrawSound(styles);
            DrawScreen(styles);
            DrawTheme(styles);
            GUILayout.EndScrollView();
            if (SaveError != null)
            {
                GUILayout.Label($"Settings could not be saved: {SaveError}", styles.Warning);
            }

            GUILayout.EndArea();
        }

        private void DrawAi(GuiStyles styles)
        {
            var ai = game.Ai;
            GUILayout.Label("LOCAL AI (OLLAMA)", styles.Section);
            var on = Choice("Use local AI", ai.Enabled ? 0 : 1, OnOff, styles) == 0;
            if (on != ai.Enabled)
            {
                ai.SetEnabled(on);
                Prefs.AiEnabled = on;
                Changed();
            }

            GUILayout.Label(ai.Summary(), styles.Small);
            GUILayout.Label($"Ollama address: {ai.Endpoint} (change it in settings.json)", styles.Small);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Installed models", styles.Text);
            GUILayout.FlexibleSpace();
            GUI.enabled = !ai.ListingModels;
            if (GUILayout.Button(ai.ListingModels ? "Looking…" : "Refresh list", GUILayout.Width(110f)))
            {
                ai.RefreshModelList();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (ai.ModelListError != null)
            {
                GUILayout.Label($"Couldn't ask Ollama for its models: {ai.ModelListError}. Start Ollama, then press Refresh list.", styles.Warning);
            }
            else if (ai.InstalledModels.Count == 0 && !ai.ListingModels)
            {
                GUILayout.Label($"Ollama has no models yet. Install one in a terminal, for example: ollama pull {ai.Model}", styles.Small);
            }

            var listed = false;
            foreach (var model in ai.InstalledModels)
            {
                var chosen = OllamaProtocol.HasModel(new[] { model }, ai.Model);
                listed |= chosen;
                if (GUILayout.Toggle(chosen, model, GUI.skin.button) && !chosen)
                {
                    ai.UseModel(model);
                    Prefs.AiModel = model;
                    Changed();
                }
            }

            if (!listed && ai.InstalledModels.Count > 0)
            {
                GUILayout.Label($"Chosen now: {ai.Model}, which Ollama doesn't have. Pick one above.", styles.Warning);
            }

            if (!ai.Enabled)
            {
                GUILayout.Label("While the AI is off, residents choose with their built-in routine. The town plays the same way, just without AI-written words.", styles.Small);
            }
        }

        private void DrawSound(GuiStyles styles)
        {
            GUILayout.Label("SOUND", styles.Section);
            GUILayout.Label("Music and sounds arrive with the next update. Your levels are kept for it.", styles.Small);
            Prefs.MasterVolume = Volume("Master", Prefs.MasterVolume, styles);
            Prefs.MusicVolume = Volume("Music", Prefs.MusicVolume, styles);
            Prefs.AmbienceVolume = Volume("Ambience", Prefs.AmbienceVolume, styles);
            Prefs.EffectsVolume = Volume("Effects", Prefs.EffectsVolume, styles);
        }

        private void DrawScreen(GuiStyles styles)
        {
            GUILayout.Label("SCREEN", styles.Section);
            var window = (WindowChoice)Choice("Display", (int)Prefs.Window, Windows, styles);
            if (window != Prefs.Window)
            {
                Prefs.Window = window;
                ScreenSettings.ApplyWindow(Prefs);
                Changed();
            }

            if (sizes.Count > 0)
            {
                var current = Prefs.Width > 0 ? new Vector2Int(Prefs.Width, Prefs.Height) : new Vector2Int(Screen.width, Screen.height);
                var index = Mathf.Max(0, sizes.IndexOf(current));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Resolution", styles.Text, GUILayout.Width(120f));
                GUI.enabled = index > 0;
                var step = GUILayout.Button("◀", GUILayout.Width(32f)) ? -1 : 0;
                GUI.enabled = true;
                GUILayout.Label($"{sizes[index].x} × {sizes[index].y}", styles.Text, GUILayout.Width(120f));
                GUI.enabled = index < sizes.Count - 1;
                step = GUILayout.Button("▶", GUILayout.Width(32f)) ? 1 : step;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (step != 0)
                {
                    var size = sizes[Mathf.Clamp(index + step, 0, sizes.Count - 1)];
                    Prefs.Width = size.x;
                    Prefs.Height = size.y;
                    ScreenSettings.ApplyWindow(Prefs);
                    Changed();
                }
            }

            var vsync = Choice("VSync", Prefs.VSync ? 0 : 1, OnOff, styles) == 0;
            if (vsync != Prefs.VSync)
            {
                Prefs.VSync = vsync;
                ScreenSettings.ApplyFrameRate(Prefs);
                Changed();
            }

            var labels = new string[ScreenSettings.FrameLimits.Length];
            var chosen = 1;
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i] = ScreenSettings.FrameLimits[i] == 0 ? "No limit" : ScreenSettings.FrameLimits[i].ToString();
                chosen = ScreenSettings.FrameLimits[i] == Prefs.FrameLimit ? i : chosen;
            }

            GUI.enabled = !Prefs.VSync;
            var limit = ScreenSettings.FrameLimits[Choice("Frame limit", chosen, labels, styles)];
            GUI.enabled = true;
            if (limit != Prefs.FrameLimit)
            {
                Prefs.FrameLimit = limit;
                ScreenSettings.ApplyFrameRate(Prefs);
                Changed();
            }

            if (Prefs.VSync)
            {
                GUILayout.Label("With VSync on, the game draws at your screen's refresh rate.", styles.Small);
            }
        }

        private void DrawTheme(GuiStyles styles)
        {
            GUILayout.Label("THEME", styles.Section);
            var theme = (ThemeChoice)Choice("Theme", (int)Prefs.Theme, Themes, styles);
            if (theme != Prefs.Theme)
            {
                Prefs.Theme = theme;
                UiTheme.Choose(theme);
                Changed();
            }

            GUILayout.Label("The panels and windows use the same colours as Stills & Reels and StackSpace. The town's own art keeps its colours.", styles.Small);
        }

        private static int Choice(string label, int selected, string[] options, GuiStyles styles)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, styles.Text, GUILayout.Width(120f));
            selected = GUILayout.Toolbar(selected, options, GUILayout.Height(26f));
            GUILayout.EndHorizontal();
            return selected;
        }

        private int Volume(string label, int value, GuiStyles styles)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, styles.Text, GUILayout.Width(120f));
            var changed = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, 0f, PlayerPreferences.MaxVolume));
            GUILayout.Label($"{changed}%", styles.Small, GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            if (changed != value)
            {
                Changed();
            }

            return changed;
        }

        private void Changed()
        {
            saveAt = Time.unscaledTime + SaveDelaySeconds;
        }
    }
}
