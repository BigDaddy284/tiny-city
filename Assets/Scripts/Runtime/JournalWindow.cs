using System.Collections.Generic;
using TinyCity.Simulation;
using TinyCity.Simulation.History;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The town journal: the village records newest first, with a search box, topic filters and an option to show
    /// only the resident chosen in the inspector. Clicking an entry goes to whoever it was about.
    /// </summary>
    internal sealed class JournalWindow
    {
        private const float MaxWidth = 640f;
        private const string SearchControl = "JournalSearch";

        private static readonly string[] TopicNames = { "All", "Life", "Love", "Health", "Friends", "Gatherings" };

        private readonly GameController game;
        private string search = string.Empty;
        private JournalTopic topic;
        private bool onlySelected;
        private Vector2 scroll;
        private List<TownEvent> results = new List<TownEvent>();
        private (long next, string search, JournalTopic topic, string residentId, TownSimulation sim) cachedFor;

        public JournalWindow(GameController game)
        {
            this.game = game;
        }

        public bool IsOpen { get; private set; }

        /// <summary>Where the window was last drawn, so clicks on it don't reach the camera panels.</summary>
        public Rect Area { get; private set; }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            if (!IsOpen && GUI.GetNameOfFocusedControl() == SearchControl)
            {
                GUIUtility.keyboardControl = 0;
            }
        }

        /// <summary>Opens the journal showing only <paramref name="residentId"/>'s entries.</summary>
        public void ShowOnly(string residentId)
        {
            IsOpen = true;
            onlySelected = residentId != null;
        }

        public void Draw(Rect cameraArea, GuiStyles styles)
        {
            if (!IsOpen)
            {
                Area = Rect.zero;
                return;
            }

            var sim = game.Simulation;
            var selected = game.SelectedResident;
            var width = Mathf.Min(MaxWidth, cameraArea.width - 40f);
            Area = new Rect(cameraArea.x + ((cameraArea.width - width) / 2f), cameraArea.y + 36f, width, cameraArea.height - 72f);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.Window, 0.98f);
            GUI.DrawTexture(Area, Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.AccentA);
            GUI.DrawTexture(new Rect(Area.x, Area.y, Area.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(Area.x + 14f, Area.y + 10f, Area.width - 28f, Area.height - 20f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Town Journal", styles.Title);
            if (GUILayout.Button("Close (J)", GUILayout.Width(80f)))
            {
                Toggle();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", styles.Small, GUILayout.Width(50f));
            GUI.SetNextControlName(SearchControl);
            search = GUILayout.TextField(search ?? string.Empty, 60);
            if (GUILayout.Button("Clear", GUILayout.Width(50f)))
            {
                search = string.Empty;
                GUIUtility.keyboardControl = 0;
            }

            GUILayout.EndHorizontal();
            topic = (JournalTopic)GUILayout.Toolbar((int)topic, TopicNames);
            onlySelected = GUILayout.Toggle(onlySelected, $" Only {selected.FirstName}");

            var residentId = onlySelected ? selected.Id : null;
            var key = (sim.History.NextNumber, search, topic, residentId, sim);
            if (!key.Equals(cachedFor))
            {
                results = Journal.Search(sim.History, sim.GetResident, topic, residentId, search);
                cachedFor = key;
            }

            var more = results.Count >= Journal.MaxResults ? $" (the latest {Journal.MaxResults}; search to find older ones)" : string.Empty;
            GUILayout.Label(results.Count == 0 ? "Nothing matches." : $"{results.Count} entries, newest first{more}. Click one to go to who it's about.", styles.Small);

            scroll = GUILayout.BeginScrollView(scroll);
            var day = -1;
            foreach (var entry in results)
            {
                if (entry.Time.Day != day)
                {
                    day = entry.Time.Day;
                    GUILayout.Space(4f);
                    GUILayout.Label($"Day {day}, {entry.Time.Weekday}", styles.Text);
                }

                var mark = Journal.IsNotable(entry.Kind) ? "★ " : string.Empty;
                if (GUILayout.Button($"{entry.Time.ClockText}  {mark}{entry.Summary}", styles.Entry) && entry.ResidentIds.Count > 0)
                {
                    game.GoTo(entry);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Town History"))
            {
                game.ExportTownHistory();
            }

            if (GUILayout.Button($"Export {selected.FirstName}'s Life Story"))
            {
                game.ExportLifeStory(selected);
            }

            if (GUILayout.Button("Open Exports Folder"))
            {
                ExportStore.OpenFolder();
            }

            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(game.Status))
            {
                GUILayout.Label(game.Status, styles.Small);
            }

            GUILayout.EndArea();
        }
    }
}
