using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Persistence;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// The saved towns: load, copy, rename, export or delete each one, start a new town, or open the saves folder.
    /// The town being played is marked and can't be deleted. Deleting asks for a second click.
    /// </summary>
    internal sealed class SavesWindow
    {
        private const float MaxWidth = 640f;
        private const float ConfirmSeconds = 4f;
        private const string RenameControl = "SlotRename";

        private readonly GameController game;
        private readonly List<(string slot, SaveSummary summary, System.DateTime saved)> slots = new List<(string, SaveSummary, System.DateTime)>();
        private Vector2 scroll;
        private string renaming;
        private string renameText = string.Empty;
        private string confirmDelete;
        private float confirmUntil;
        private string newTownName = string.Empty;

        public SavesWindow(GameController game)
        {
            this.game = game;
        }

        public bool IsOpen { get; private set; }

        /// <summary>Where the window was last drawn, so clicks on it don't reach the camera panels.</summary>
        public Rect Area { get; private set; }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            renaming = null;
            confirmDelete = null;
            if (IsOpen)
            {
                Refresh();
            }
            else
            {
                GUIUtility.keyboardControl = 0;
            }
        }

        /// <summary>Reads every slot's summary again. Done when the window opens and after each change, never every frame.</summary>
        public void Refresh()
        {
            slots.Clear();
            foreach (var slot in SaveStore.ListSlots())
            {
                var summary = SaveStore.TryRead(slot, false, out var json, out _) || SaveStore.TryRead(slot, true, out json, out _)
                    ? SaveSummary.Read(json)
                    : SaveSummary.Read(null);
                slots.Add((slot, summary, SaveStore.LastWritten(slot)));
            }
        }

        public void Draw(Rect cameraArea, GuiStyles styles)
        {
            if (!IsOpen)
            {
                Area = Rect.zero;
                return;
            }

            var width = Mathf.Min(MaxWidth, cameraArea.width - 40f);
            Area = new Rect(cameraArea.x + ((cameraArea.width - width) / 2f), cameraArea.y + 36f, width, cameraArea.height - 72f);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.Window, 0.98f);
            GUI.DrawTexture(Area, Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.AccentA);
            GUI.DrawTexture(new Rect(Area.x, Area.y, Area.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(Area.x + 14f, Area.y + 10f, Area.width - 28f, Area.height - 20f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Saved towns", styles.Title);
            if (GUILayout.Button("Close (S)", GUILayout.Width(80f)))
            {
                game.ToggleSaves();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Playing {game.CurrentTownName}. Autosaves, F5 and closing the game all save it. Loading another town saves this one first.", styles.Small);

            scroll = GUILayout.BeginScrollView(scroll);
            if (slots.Count == 0)
            {
                GUILayout.Label("Nothing has been saved yet. Press F5 to save this town.", styles.Small);
            }

            foreach (var (slot, summary, saved) in slots.ToList())
            {
                DrawSlot(slot, summary, saved, styles);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("New town:", styles.Small, GUILayout.Width(70f));
            newTownName = GUILayout.TextField(newTownName ?? string.Empty, SaveSlotIndex.MaxNameLength);
            if (GUILayout.Button("Start it", GUILayout.Width(70f)))
            {
                game.NewTownSlot(newTownName);
                newTownName = string.Empty;
                GUIUtility.keyboardControl = 0;
                Refresh();
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open saves folder"))
            {
                SaveStore.OpenFolder();
            }

            if (GUILayout.Button("Open exports folder"))
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

        private void DrawSlot(string slot, SaveSummary summary, System.DateTime saved, GuiStyles styles)
        {
            var playing = slot == game.CurrentSlot;
            GUILayout.Space(6f);
            if (renaming == slot)
            {
                GUILayout.BeginHorizontal();
                var e = Event.current;
                if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == RenameControl)
                {
                    FinishRename();
                    e.Use();
                }

                GUI.SetNextControlName(RenameControl);
                renameText = GUILayout.TextField(renameText ?? string.Empty, SaveSlotIndex.MaxNameLength);
                if (GUILayout.Button("OK", GUILayout.Width(40f)))
                {
                    FinishRename();
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(60f)))
                {
                    renaming = null;
                    GUIUtility.keyboardControl = 0;
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(game.SlotNames.NameOf(slot) + (playing ? "  (playing now)" : string.Empty), styles.Text);
            }

            var when = saved == System.DateTime.MinValue ? string.Empty : $" · saved {saved:d MMM yyyy HH:mm}";
            GUILayout.Label(summary.Describe() + when, styles.Small);

            GUILayout.BeginHorizontal();
            GUI.enabled = summary.IsReadable;
            if (GUILayout.Button(playing ? "Reload" : "Load"))
            {
                game.LoadSlot(slot);
                Refresh();
            }

            GUI.enabled = true;
            if (GUILayout.Button("Copy"))
            {
                game.DuplicateSlot(slot);
                Refresh();
            }

            if (GUILayout.Button("Rename"))
            {
                renaming = slot;
                renameText = game.SlotNames.NameOf(slot);
            }

            if (GUILayout.Button("Export"))
            {
                game.ExportSlot(slot);
            }

            GUI.enabled = !playing;
            var confirming = confirmDelete == slot && Time.unscaledTime < confirmUntil;
            if (GUILayout.Button(confirming ? "Click again to delete" : "Delete"))
            {
                if (confirming)
                {
                    game.DeleteSlot(slot);
                    confirmDelete = null;
                    Refresh();
                }
                else
                {
                    confirmDelete = slot;
                    confirmUntil = Time.unscaledTime + ConfirmSeconds;
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void FinishRename()
        {
            if (renaming != null)
            {
                game.RenameSlot(renaming, renameText);
            }

            renaming = null;
            GUIUtility.keyboardControl = 0;
        }
    }
}
