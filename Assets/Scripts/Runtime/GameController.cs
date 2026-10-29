using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Runs the town: advances the simulation with the clock, saves and loads, and hands drawing to the
    /// <see cref="TownView"/> (the maps and residents), the <see cref="CameraPanels"/> (what each camera watches), the
    /// <see cref="Inspector"/> (the panel on the right), the <see cref="JournalWindow"/> and the <see cref="Notifications"/>.
    /// All rules live in <see cref="TownSimulation"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>Remembers which town was being played, so the game reopens it next time.</summary>
        private const string SlotPrefsKey = "TinyCity.Slot";
        private const string SidebarPrefsKey = "TinyCity.SidebarHidden";

        /// <summary>Game minutes per real second. At 1x one in-game day lasts 24 real minutes.</summary>
        public static readonly int[] Speeds = { 0, 1, 2, 4, 10 };

        private const int AutosaveEveryGameMinutes = 6 * 60;
        private const int MaxTicksPerFrame = 600;

        private int lastSpeedIndex = 1;
        private float accumulator;
        private long lastAutosaveMinute;
        private string selectedId;

        private TownView view;
        private Inspector inspector;
        private JournalWindow journal;
        private SavesWindow saves;
        private SettingsWindow settingsWindow;
        private int placeMenuPanel = -1;
        private Rect placeMenuButton;
        private Rect placeMenuArea;
        private Vector2 placeMenuScroll;
        private GuiStyles styles;

        public TownSimulation Simulation { get; private set; }

        public GameSettings Settings { get; set; } = new GameSettings();

        /// <summary>Talks to Ollama; residents fall back to their built-in routine whenever it can't help.</summary>
        public AiService Ai { get; private set; }

        /// <summary>The camera panels, from one to ten. Panel 0 uses the main camera.</summary>
        public CameraPanels Panels { get; private set; }

        /// <summary>Pop-ups for the town's big moments.</summary>
        public Notifications Notifications { get; private set; }

        public bool JournalOpen => journal != null && journal.IsOpen;

        public bool SavesOpen => saves != null && saves.IsOpen;

        public bool SettingsOpen => settingsWindow != null && settingsWindow.IsOpen;

        /// <summary>Whether the side panel is put away so the city fills the window (H, or the button in its top row).</summary>
        public bool SidebarHidden { get; private set; }

        /// <summary>The player's choices from the Settings window: AI, volumes, screen and theme.</summary>
        public PlayerPreferences Preferences { get; private set; } = new PlayerPreferences();

        /// <summary>The save slot this town is saved to: autosaves, F5 and saving on exit all go here.</summary>
        public string CurrentSlot { get; private set; } = SaveSlotIndex.DefaultSlot;

        /// <summary>The names the player gave their towns.</summary>
        public SaveSlotIndex SlotNames { get; private set; } = new SaveSlotIndex();

        public string CurrentTownName => SlotNames.NameOf(CurrentSlot);

        /// <summary>Where the maps and residents are drawn in the world.</summary>
        public TownView View => view;

        public SoundPlayer Sound { get; private set; }

        public int SpeedIndex { get; private set; } = 1;

        public int Speed => Speeds[SpeedIndex];

        public string Status { get; private set; } = string.Empty;

        /// <summary>The resident shown in the inspector.</summary>
        public Resident SelectedResident => Simulation.GetResident(selectedId) ?? Simulation.Residents[0];

        /// <summary>Advances the town immediately, as if that many minutes had passed. Used by tests.</summary>
        public void AdvanceMinutes(int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                TickOnce();
            }

            UpdateView(1f);
        }

        private void Start()
        {
            view = new GameObject("Town").AddComponent<TownView>();
            Sound = gameObject.AddComponent<SoundPlayer>();
            Panels = new CameraPanels(Camera.main) { ResidentClicked = id => Select(Simulation.GetResident(id), followInActivePanel: false) };
            inspector = new Inspector(this);
            journal = new JournalWindow(this);
            saves = new SavesWindow(this);
            settingsWindow = new SettingsWindow(this);
            Notifications = new Notifications();
            Preferences = PlayerSettingsStore.Load(out var preferenceProblems);
            foreach (var problem in preferenceProblems)
            {
                Debug.LogWarning($"[TinyCity] {problem}");
            }

            Preferences.ApplyTo(Settings.ollama);
            UiTheme.Choose(Preferences.Theme);
            UiTheme.Changed += ApplyThemeToCameras;
            ScreenSettings.Apply(Preferences);
            Ai = new AiService(Settings.ollama, new UnityAiTransport()) { Log = message => Debug.Log($"[TinyCity] AI: {message}") };
            Ai.SetGameSpeed(Speed);

            SidebarHidden = PlayerPrefs.GetInt(SidebarPrefsKey, 0) == 1;
            SlotNames = SaveStore.ReadIndex();
            var remembered = PlayerPrefs.GetString(SlotPrefsKey, SaveSlotIndex.DefaultSlot);
            CurrentSlot = SaveSlotIndex.IsValidSlotId(remembered) ? remembered : SaveSlotIndex.DefaultSlot;
            if (!SaveStore.Exists(CurrentSlot) || !TryLoad(CurrentSlot))
            {
                // Nothing to load (or it couldn't be loaded): a new town, in a slot of its own if this one holds a damaged save.
                if (SaveStore.Exists(CurrentSlot))
                {
                    UseSlot(SaveSlotIndex.NewSlotId(SaveStore.ListSlots()));
                }

                StartNewTown(Status.Length == 0 ? "Started a new town." : Status + " Started a new town instead.");
            }

            Panels.LoadLayout(view, Simulation);
            ApplyThemeToCameras();
            UpdateView(1f);
        }

        private void OnDestroy()
        {
            UiTheme.Changed -= ApplyThemeToCameras;
        }

        /// <summary>Behind the maps, every panel shows the theme's canvas colour, and the title bar takes the theme too.</summary>
        private void ApplyThemeToCameras()
        {
            var colour = UiTheme.ToColor(UiTheme.Palette.Canvas);
            foreach (var panel in Panels.All)
            {
                panel.Camera.backgroundColor = colour;
            }

            WindowFrame.Apply(UiTheme.Palette);
        }

        /// <summary>The title bar can lose its colours when the window is recreated (for example by a screen mode change).</summary>
        private void OnApplicationFocus(bool focused)
        {
            if (focused)
            {
                WindowFrame.Apply(UiTheme.Palette);
            }
        }

        private void Update()
        {
            Ai?.Update(UnityEngine.Time.realtimeSinceStartupAsDouble);
            var ticks = 0;
            if (Speed > 0)
            {
                accumulator += UnityEngine.Time.unscaledDeltaTime * Speed;
                while (accumulator >= 1f && ticks < MaxTicksPerFrame)
                {
                    accumulator -= 1f;
                    TickOnce();
                    ticks++;
                }

                if (ticks == MaxTicksPerFrame)
                {
                    accumulator = 0f;
                }
            }

            UpdateView(Speed > 0 && ticks <= 1 ? accumulator : 1f);
            if (Notifications != null && Notifications.Update(Simulation, UnityEngine.Time.unscaledTime) > 0)
            {
                Sound?.PlayChime();
            }

            Sound?.Steer(Simulation, Panels.Active, view, Preferences);
            settingsWindow?.Update();
        }

        private void OnApplicationQuit()
        {
            if (Simulation != null)
            {
                Save("on exit");
                Panels?.SaveLayout();
            }

            settingsWindow?.SaveNow();
        }

        private void TickOnce()
        {
            view.RememberPositions(Simulation);
            Simulation.Tick();
            Sound?.Hear(Simulation);
            if (Simulation.Time.TotalMinutes - lastAutosaveMinute >= AutosaveEveryGameMinutes)
            {
                Save("autosave");
            }
        }

        public void StartNewTown(string message)
        {
            SetSimulation(StarterTown.CreateNewGame());
            Status = message;
        }

        private void SetSimulation(TownSimulation simulation)
        {
            Simulation = simulation;
            simulation.RomanceEnabled = Settings.Simulation.romance;
            simulation.CommunityEventsEnabled = Settings.Simulation.communityEvents;
            Ai?.Attach(simulation);
            if (view.World == null)
            {
                view.Build(simulation.World, TinyCity.Simulation.Time.Seasons.Of(simulation.Time));
            }

            view.ForgetResidents();
            Sound?.Forget();
            Panels.ClearSpeech();
            Notifications.Reset(simulation);
            if (simulation.GetResident(selectedId) == null)
            {
                selectedId = simulation.Residents[0].Id;
            }

            // A new panel, or one following someone who isn't in this town, follows the selected resident.
            for (var i = 0; i < Panels.Count; i++)
            {
                var panel = Panels.All[i];
                if (panel.IsFree ? panel.MapId == null : simulation.GetResident(panel.FollowId) == null)
                {
                    Panels.Follow(i, selectedId, simulation);
                }
                else if (panel.IsFree && !simulation.World.TryGetMap(panel.MapId, out _))
                {
                    panel.LookAtMap(simulation.Residents[0].Location.MapId, view);
                }
            }

            lastAutosaveMinute = simulation.Time.TotalMinutes;
            accumulator = 0f;
        }

        public void Save(string reason)
        {
            try
            {
                SaveStore.Write(CurrentSlot, SaveSerializer.Save(Simulation));
                lastAutosaveMinute = Simulation.Time.TotalMinutes;
                Status = $"Saved ({reason}) at {Simulation.Time}.";
            }
            catch (Exception e)
            {
                Status = $"Save failed: {e.Message}";
                Debug.LogError($"[TinyCity] {Status}");
            }
        }

        /// <summary>Loads the current slot again, dropping anything since the last save.</summary>
        public bool TryLoad() => TryLoad(CurrentSlot);

        /// <summary>Loads <paramref name="slot"/> (its backup if the save is damaged) and makes it the current slot. On failure the town being played carries on.</summary>
        public bool TryLoad(string slot)
        {
            var world = StarterTown.CreateWorld();
            var problems = new List<string>();
            foreach (var useBackup in new[] { false, true })
            {
                if (!SaveStore.TryRead(slot, useBackup, out var json, out var readError))
                {
                    problems.Add(readError);
                    continue;
                }

                var result = SaveSerializer.Load(json, world);
                if (!result.Success)
                {
                    problems.AddRange(result.Errors);
                    continue;
                }

                if (result.UpgradedFromVersion == 1)
                {
                    // Version 1 saves predate memories; give the starter residents their background.
                    StarterTown.AddLifeHistory(result.Simulation);
                }

                if (result.UpgradedFromVersion > 0 && result.UpgradedFromVersion < 4)
                {
                    // Saves from before the economy: wages for existing jobs, and starting money, food and rent.
                    StarterTown.AddFinancesToOlderSave(result.Simulation);
                }

                if (result.UpgradedFromVersion > 0 && result.UpgradedFromVersion < 5)
                {
                    // Saves from before hobbies: the starter residents get theirs.
                    StarterTown.AddHobbiesToOlderSave(result.Simulation);
                }

                if (result.UpgradedFromVersion > 0 && result.UpgradedFromVersion < 6)
                {
                    // Saves from before romance: starter residents take their usual openness to it.
                    StarterTown.AddRomanceToOlderSave(result.Simulation);
                }

                // Older saves have fewer residents; the newcomers move in at home.
                var newcomers = StarterTown.AddMissingResidents(result.Simulation);

                // The neon bar opened after older saves were made; Felix takes the job if he is still looking for work.
                StarterTown.AddBarJobToOlderSave(result.Simulation);

                UseSlot(slot);
                SetSimulation(result.Simulation);
                foreach (var warning in result.Warnings)
                {
                    Debug.LogWarning($"[TinyCity] Save repaired: {warning}");
                }

                Status = (useBackup ? $"The save of {CurrentTownName} was damaged, so its backup was loaded. " : $"Loaded {CurrentTownName}. ")
                    + (result.Warnings.Count > 0 ? $"{result.Warnings.Count} problem(s) were repaired. " : string.Empty)
                    + (newcomers > 0 ? $"{newcomers} new neighbour(s) moved in." : string.Empty);
                return true;
            }

            foreach (var problem in problems)
            {
                Debug.LogWarning($"[TinyCity] Load failed: {problem}");
            }

            Status = $"Couldn't load {SlotNames.NameOf(slot)}: {problems.LastOrDefault()}";
            return false;
        }

        private void UseSlot(string slot)
        {
            CurrentSlot = slot;
            PlayerPrefs.SetString(SlotPrefsKey, slot);
            PlayerPrefs.Save();
        }

        // ---- Save slots ----

        /// <summary>Saves the town being played, then switches to <paramref name="slot"/>.</summary>
        public bool LoadSlot(string slot)
        {
            if (slot != CurrentSlot)
            {
                Save("before switching towns");
            }

            return TryLoad(slot);
        }

        /// <summary>Saves the town being played and starts a brand-new one in a new slot.</summary>
        public string NewTownSlot(string name)
        {
            Save("before starting a new town");
            var slot = SaveSlotIndex.NewSlotId(SaveStore.ListSlots().Append(CurrentSlot));
            SetSlotName(slot, string.IsNullOrWhiteSpace(name) ? SlotNames.NameOf(slot) : name);
            UseSlot(slot);
            StartNewTown(string.Empty);
            Save("new town");
            Status = $"Started a new town, {CurrentTownName}.";
            return slot;
        }

        /// <summary>Copies a slot into a new one called "Copy of …". The town being played is saved first, so its copy is up to date.</summary>
        public string DuplicateSlot(string slot)
        {
            try
            {
                if (slot == CurrentSlot)
                {
                    Save("before copying");
                }

                var slots = SaveStore.ListSlots();
                var copy = SaveSlotIndex.NewSlotId(slots.Append(CurrentSlot));
                SaveStore.Copy(slot, copy);
                SetSlotName(copy, SlotNames.CopyName(slot, slots));
                Status = $"Copied {SlotNames.NameOf(slot)} to {SlotNames.NameOf(copy)}.";
                return copy;
            }
            catch (Exception e)
            {
                Status = $"Couldn't copy {SlotNames.NameOf(slot)}: {e.Message}";
                Debug.LogError($"[TinyCity] {Status}");
                return null;
            }
        }

        public void RenameSlot(string slot, string name)
        {
            SetSlotName(slot, name);
            Status = $"Renamed to {SlotNames.NameOf(slot)}.";
        }

        /// <summary>Deletes a saved town. The one being played can't be deleted.</summary>
        public bool DeleteSlot(string slot)
        {
            if (slot == CurrentSlot)
            {
                Status = "You can't delete the town you're playing. Load another one first.";
                return false;
            }

            try
            {
                var name = SlotNames.NameOf(slot);
                SaveStore.Delete(slot);
                SlotNames.Forget(slot);
                SaveStore.WriteIndex(SlotNames);
                Status = $"Deleted {name}.";
                return true;
            }
            catch (Exception e)
            {
                Status = $"Couldn't delete {SlotNames.NameOf(slot)}: {e.Message}";
                Debug.LogError($"[TinyCity] {Status}");
                return false;
            }
        }

        /// <summary>Copies a slot's save file to the Exports folder, to back it up or share it.</summary>
        public void ExportSlot(string slot)
        {
            try
            {
                if (slot == CurrentSlot)
                {
                    Save("before exporting");
                }

                SaveStore.TryRead(slot, false, out var json, out _);
                var day = json == null ? 0 : SaveSummary.Read(json).Time.Day;
                var source = System.IO.File.Exists(SaveStore.PathFor(slot)) ? SaveStore.PathFor(slot) : SaveStore.PathFor(slot) + ".bak";
                var path = ExportStore.CopySave(source, $"{SlotNames.NameOf(slot)} save", day);
                Status = $"{SlotNames.NameOf(slot)} was exported to {path}";
            }
            catch (Exception e)
            {
                Status = $"Couldn't export {SlotNames.NameOf(slot)}: {e.Message}";
                Debug.LogError($"[TinyCity] {Status}");
            }
        }

        private void SetSlotName(string slot, string name)
        {
            SlotNames.SetName(slot, name);
            try
            {
                SaveStore.WriteIndex(SlotNames);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TinyCity] Couldn't save the town names: {e.Message}");
            }
        }

        private void UpdateView(float tickProgress)
        {
            if (Simulation == null || view == null)
            {
                return;
            }

            view.ApplyTime(Simulation.Time);
            view.UpdateResidents(Simulation, Mathf.Clamp01(tickProgress));
            Panels.Update(CameraArea(), view, Simulation);
        }

        /// <summary>The part of the window left of the inspector, in GUI coordinates (origin top left).</summary>
        private Rect CameraArea() => new Rect(0f, 0f, Mathf.Max(1f, Screen.width - (SidebarHidden ? 0f : Inspector.Width)), Screen.height);

        public void ToggleSidebar()
        {
            SidebarHidden = !SidebarHidden;
            PlayerPrefs.SetInt(SidebarPrefsKey, SidebarHidden ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// While the side panel is hidden, the top-right camera panel's header starts with a button that brings it back,
        /// so it sits in line with that panel's own buttons instead of over them.
        /// </summary>
        private bool ShowsSidebarButton(Rect panel)
        {
            var area = CameraArea();
            return SidebarHidden && panel.yMin <= area.yMin + 1f && panel.xMax >= area.xMax - 1f;
        }

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (SpeedIndex > 0)
            {
                lastSpeedIndex = SpeedIndex;
            }

            // At 1x the clock runs one game minute per real second, so the speed is game minutes per second.
            Ai?.SetGameSpeed(Speed);
        }

        /// <summary>Shows <paramref name="r"/> in the inspector, and optionally points the active panel at them.</summary>
        public void Select(Resident r, bool followInActivePanel)
        {
            if (r == null)
            {
                return;
            }

            selectedId = r.Id;
            if (followInActivePanel && r.IsAlive)
            {
                Panels.Follow(Panels.ActiveIndex, r.Id, Simulation);
            }
        }

        public void SetPanelCount(int count) => Panels.SetCount(count, view, Simulation);

        public void ToggleJournal()
        {
            if (!journal.IsOpen)
            {
                CloseWindows();
            }

            journal.Toggle();
        }

        public void ToggleSaves()
        {
            if (!saves.IsOpen)
            {
                CloseWindows();
            }

            saves.Toggle();
        }

        public void ToggleSettings()
        {
            if (!settingsWindow.IsOpen)
            {
                CloseWindows();
            }

            settingsWindow.Toggle();
        }

        /// <summary>Only one of the journal, saves and settings windows is open at a time.</summary>
        private void CloseWindows()
        {
            if (journal.IsOpen)
            {
                journal.Toggle();
            }

            if (saves.IsOpen)
            {
                saves.Toggle();
            }

            if (settingsWindow.IsOpen)
            {
                settingsWindow.Toggle();
            }
        }

        private bool OverWindow(Vector2 point) => journal.Area.Contains(point) || saves.Area.Contains(point) || settingsWindow.Area.Contains(point);

        /// <summary>Opens the journal with only the inspector's resident's entries.</summary>
        public void ShowJournalFor(Resident r)
        {
            Select(r, followInActivePanel: false);
            journal.ShowOnly(r.Id);
        }

        /// <summary>Shows whoever a journal entry or notification is about, and points the active panel at them if they're alive.</summary>
        public void GoTo(TownEvent entry)
        {
            var r = entry.ResidentIds.Select(id => Simulation.GetResident(id)).FirstOrDefault(x => x != null);
            Select(r, followInActivePanel: true);
        }

        public void ExportLifeStory(Resident r) => Export($"{r.FullName} life story", LifeStory.Write(Simulation, r.Id), $"{r.FirstName}'s life story");

        public void ExportTownHistory() => Export("Town history", LifeStory.WriteTownHistory(Simulation), "The town history");

        private void Export(string name, string text, string what)
        {
            try
            {
                var path = ExportStore.Write(name, Simulation.Time.Day, text);
                Status = $"{what} was saved to {path}";
            }
            catch (Exception e)
            {
                Status = $"Export failed: {e.Message}";
                Debug.LogError($"[TinyCity] {Status}");
            }
        }

        public void WatchEveryone() => Panels.WatchEveryone(view, Simulation);

        private void OnGUI()
        {
            if (Simulation == null)
            {
                return;
            }

            GUI.skin = UiTheme.Skin;
            styles = UiTheme.Styles;
            var e = Event.current;
            if (e.type == EventType.MouseDown && !OverWindow(e.mousePosition))
            {
                // Clicking outside the journal and saves windows leaves their text boxes, so the game's keys work again.
                GUIUtility.keyboardControl = 0;
            }

            if (e.type == EventType.MouseDown && placeMenuPanel >= 0 && !placeMenuArea.Contains(e.mousePosition) && !placeMenuButton.Contains(e.mousePosition))
            {
                placeMenuPanel = -1;
            }

            HandleKeys(e);
            Panels.DrawOverlays(view, Simulation, styles, DrawPanelHeader);
            DrawPlaceMenu();
            journal.Draw(CameraArea(), styles);
            saves.Draw(CameraArea(), styles);
            settingsWindow.Draw(CameraArea(), styles);
            Notifications.Draw(CameraArea(), styles, GoTo);
            if (!SidebarHidden)
            {
                inspector.Draw(styles);
            }

            var overMenu = placeMenuPanel >= 0 && placeMenuArea.Contains(e.mousePosition);
            if ((e.isMouse || e.type == EventType.ScrollWheel) && !OverWindow(e.mousePosition) && !overMenu)
            {
                // After the buttons have had their chance, so header buttons and the inspector win over the panels.
                Panels.HandleMouse(e, view);
            }
        }

        private void DrawPanelHeader(int index)
        {
            var panel = Panels.All[index];
            var rect = panel.ScreenRect;
            var y = rect.y + 2f;
            var x = rect.xMax - 4f;

            // An open place list may hang over this header; its buttons mustn't take the list's clicks.
            GUI.enabled = !(placeMenuPanel >= 0 && placeMenuArea.Contains(Event.current.mousePosition));
            if (ShowsSidebarButton(rect))
            {
                x -= 62f;
                if (GUI.Button(new Rect(x, y, 60f, 18f), "◀ Panel", styles.HeaderButton))
                {
                    ToggleSidebar();
                    GUI.enabled = true;
                    return;
                }
            }

            if (Panels.Count > 1)
            {
                x -= 22f;
                if (GUI.Button(new Rect(x, y, 20f, 18f), "×", styles.HeaderButton))
                {
                    Panels.Remove(index);
                    placeMenuPanel = -1;
                    GUI.enabled = true;
                    return;
                }
            }

            x -= 52f;
            if (GUI.Button(new Rect(x, y, 50f, 18f), panel.IsFree ? "Follow" : "Free", styles.HeaderButton))
            {
                Panels.Activate(index);
                if (panel.IsFree)
                {
                    Panels.Follow(index, selectedId, Simulation);
                }
                else
                {
                    Panels.CycleMap(index, view, Simulation);
                }
            }

            x -= 62f;
            var placeButton = new Rect(x, y, 60f, 18f);
            if (GUI.Button(placeButton, "Place ▾", styles.HeaderButton))
            {
                Panels.Activate(index);
                placeMenuPanel = placeMenuPanel == index ? -1 : index;
                placeMenuScroll = Vector2.zero;
            }

            if (placeMenuPanel == index)
            {
                placeMenuButton = placeButton;
            }

            x -= 22f;
            if (GUI.Button(new Rect(x, y, 20f, 18f), "▶", styles.HeaderButton))
            {
                Panels.Activate(index);
                Step(index, 1);
            }

            x -= 22f;
            if (GUI.Button(new Rect(x, y, 20f, 18f), "◀", styles.HeaderButton))
            {
                Panels.Activate(index);
                Step(index, -1);
            }

            GUI.enabled = true;
            GUI.Label(new Rect(rect.x + 6f, y, Mathf.Max(0f, x - rect.x - 10f), 18f), Panels.Describe(index, Simulation), styles.Header);
        }

        /// <summary>
        /// The list under a panel's "Place" button: the village and every building. Picking one makes that panel a
        /// free camera there. Drawn after every header so it sits on top of the panels below.
        /// </summary>
        private void DrawPlaceMenu()
        {
            if (placeMenuPanel < 0 || placeMenuPanel >= Panels.Count)
            {
                placeMenuPanel = -1;
                return;
            }

            const float RowHeight = 22f;
            const float MenuWidth = 220f;
            var places = CameraPanels.Places(Simulation.World);
            var area = CameraArea();
            var x = Mathf.Clamp(placeMenuButton.xMax - MenuWidth, area.x + 4f, area.xMax - MenuWidth - 4f);
            var y = placeMenuButton.yMax + 2f;
            var height = Mathf.Min((places.Count * RowHeight) + 8f, area.yMax - y - 6f);
            placeMenuArea = new Rect(x, y, MenuWidth, height);
            GUI.Box(placeMenuArea, GUIContent.none, styles.Notice);

            var current = Panels.All[placeMenuPanel];
            var inner = new Rect(0f, 0f, MenuWidth - 20f, places.Count * RowHeight);
            placeMenuScroll = GUI.BeginScrollView(new Rect(x + 4f, y + 4f, MenuWidth - 8f, height - 8f), placeMenuScroll, inner);
            for (var i = 0; i < places.Count; i++)
            {
                var map = places[i];
                var label = (map.IsExterior ? $"{map.Name} (outside)" : map.Name) + (current.IsFree && current.MapId == map.Id ? "  ✓" : string.Empty);
                if (GUI.Button(new Rect(0f, i * RowHeight, inner.width, RowHeight - 2f), label, styles.Entry))
                {
                    Panels.ShowPlace(placeMenuPanel, map.Id, view);
                    placeMenuPanel = -1;
                }
            }

            GUI.EndScrollView();
        }

        /// <summary>A following panel moves to the next or previous resident; a free one to the next or previous place.</summary>
        private void Step(int index, int step)
        {
            if (Panels.All[index].IsFree)
            {
                Panels.CycleMap(index, view, Simulation, step);
            }
            else
            {
                Panels.CycleResident(index, step, Simulation);
                selectedId = Panels.All[index].FollowId ?? selectedId;
            }
        }

        private void HandleKeys(Event e)
        {
            if (e.type != EventType.KeyDown)
            {
                return;
            }

            if (GUIUtility.keyboardControl != 0)
            {
                // Typing in the journal's search box: keys are text, and Escape leaves the box.
                if (e.keyCode == KeyCode.Escape)
                {
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                }

                return;
            }

            var active = Panels.ActiveIndex;
            switch (e.keyCode)
            {
                case KeyCode.Space:
                    SetSpeed(SpeedIndex == 0 ? lastSpeedIndex : 0);
                    break;
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                    SetSpeed(e.keyCode - KeyCode.Alpha0);
                    break;
                case KeyCode.F5:
                    Save("manual");
                    break;
                case KeyCode.F9:
                    TryLoad();
                    break;
                case KeyCode.Tab:
                    Panels.CycleMap(active, view, Simulation);
                    break;
                case KeyCode.F:
                    Panels.Follow(active, selectedId, Simulation);
                    break;
                case KeyCode.N:
                    Step(active, 1);
                    break;
                case KeyCode.B:
                    Step(active, -1);
                    break;
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                    SetPanelCount(Panels.Count + 1);
                    break;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    SetPanelCount(Panels.Count - 1);
                    break;
                case KeyCode.H:
                    ToggleSidebar();
                    break;
                case KeyCode.J:
                    ToggleJournal();
                    break;
                case KeyCode.Escape when placeMenuPanel >= 0:
                    placeMenuPanel = -1;
                    break;
                case KeyCode.Escape when JournalOpen:
                    ToggleJournal();
                    break;
                case KeyCode.S:
                    ToggleSaves();
                    break;
                case KeyCode.Escape when SavesOpen:
                    ToggleSaves();
                    break;
                case KeyCode.Escape when SettingsOpen:
                    ToggleSettings();
                    break;
                default:
                    return;
            }

            e.Use();
        }
    }
}
