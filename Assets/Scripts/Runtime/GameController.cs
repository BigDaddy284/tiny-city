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
    /// <see cref="TownView"/> (the maps and residents), the <see cref="TownCamera"/> (what the camera watches), the
    /// <see cref="Inspector"/> (the panel on the right), the <see cref="JournalWindow"/> and the <see cref="Notifications"/>.
    /// All rules live in <see cref="TownSimulation"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>Remembers which town was being played, so the game reopens it next time.</summary>
        private const string SlotPrefsKey = "TinyCity.Slot";
        private const string SidebarPrefsKey = "TinyCity.SidebarHidden";

        /// <summary>Game minutes per real second. At 1x one in-game day lasts 24 real minutes.</summary>
        /// <summary>Tiny City 2 runs at 1x, 2x and 4x at most: the AI makes every choice, and the clock waits for it.</summary>
        public static readonly int[] Speeds = { 0, 1, 2, 4 };

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
        private GuiStyles styles;

        public TownSimulation Simulation { get; private set; }

        public GameSettings Settings { get; set; } = new GameSettings();

        /// <summary>Talks to Ollama; residents fall back to their built-in routine whenever it can't help.</summary>
        public AiService Ai { get; private set; }

        /// <summary>The one camera, which is the main camera.</summary>
        public TownCamera TownCamera { get; private set; }

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

        /// <summary>
        /// True while the clock is held because Ollama isn't connected: Tiny City 2 has no routine to fall back on.
        /// While it is connected the clock keeps running (owner, 5 November): someone waiting for a choice or a line
        /// just stands there thinking.
        /// </summary>
        public bool HoldingForAi => Simulation != null
            && (Ai == null || Ai.Status != TinyCity.Simulation.AI.AiConnectionStatus.Connected);

        /// <summary>Game minutes actually passing per real second, averaged over the last few seconds.</summary>
        public float ActualSpeed { get; private set; }

        /// <summary>The resident shown in the inspector.</summary>
        public Resident SelectedResident => Simulation.GetResident(selectedId) ?? Simulation.Residents[0];

        /// <summary>
        /// Plays <paramref name="simulation"/> instead of the current town, unsaved. Used by Play Mode tests that need
        /// the city's indoor maps and lit windows, which Tiny City 2's flat map doesn't have.
        /// </summary>
        public void ShowTown(TownSimulation simulation) => SetSimulation(simulation);

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
            // Tiny City 2 (owner, 5 November): one camera only. It starts over the whole land, and clicking someone follows them.
            TownCamera = new TownCamera(Camera.main) { ResidentClicked = id => Select(Simulation.GetResident(id), follow: true) };
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
            Ai.Queue.Answered = (request, response, check) => AiLogStore.Write(Simulation, request, response, check);

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

            TownCamera.ShowWholeLand(view, Simulation);
            ApplyThemeToCameras();
            UpdateView(1f);
        }

        private void OnDestroy()
        {
            UiTheme.Changed -= ApplyThemeToCameras;
        }

        /// <summary>Behind the maps, the camera shows the theme's canvas colour, and the title bar takes the theme too.</summary>
        private void ApplyThemeToCameras()
        {
            TownCamera.Camera.backgroundColor = UiTheme.ToColor(UiTheme.Palette.Canvas);

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
                    if (HoldingForAi)
                    {
                        // Don't bank the waiting time, or the city would rush to catch up once the AI answers.
                        accumulator = Mathf.Min(accumulator, 1f);
                        break;
                    }

                    accumulator -= 1f;
                    TickOnce();
                    ticks++;
                }

                if (ticks == MaxTicksPerFrame)
                {
                    accumulator = 0f;
                }
            }

            var dt = Mathf.Max(UnityEngine.Time.unscaledDeltaTime, 0.0001f);
            ActualSpeed = Mathf.Lerp(ActualSpeed, ticks / dt, Mathf.Clamp01(dt / 3f));
            UpdateView(Speed > 0 && ticks <= 1 ? accumulator : 1f);
            if (Notifications != null && Notifications.Update(Simulation, UnityEngine.Time.unscaledTime) > 0)
            {
                Sound?.PlayChime();
            }

            Sound?.Steer(Simulation, TownCamera, view, Preferences);
            settingsWindow?.Update();
        }

        private void OnApplicationQuit()
        {
            if (Simulation != null)
            {
                Save("on exit");
            }

            settingsWindow?.SaveNow();
        }

        private void TickOnce()
        {
            view.RememberPositions(Simulation);
            Simulation.Tick();
            Sound?.Hear(Simulation);
            WriteLegacyIfEveryoneDied();
            if (Simulation.Time.TotalMinutes - lastAutosaveMinute >= AutosaveEveryGameMinutes)
            {
                Save("autosave");
            }
        }

        /// <summary>Once everyone has died, the world's legacy log is written for the next world.</summary>
        private void WriteLegacyIfEveryoneDied()
        {
            if (legacyWritten || !TinyCity.Simulation.History.Legacy.EveryoneHasDied(Simulation))
            {
                return;
            }

            legacyWritten = true;
            try
            {
                var path = LegacyStore.Write(CurrentSlot, Simulation);
                if (path != null)
                {
                    Status = $"Everyone has died. Their story is written down for the next world: {path}";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TinyCity] Couldn't write the legacy log: {e.Message}");
            }
        }

        private bool legacyWritten;

        public void StartNewTown(string message)
        {
            var profiles = ProfilesLoader.Load(out var problems);
            var lessons = LegacyStore.LatestLessons();
            SetSimulation(FlatLand.CreateNewGame(profiles, lessons, ProfilesLoader.LoadBrief()));
            message = lessons.Count > 0 ? $"{message} They remember what the last world left behind.".Trim() : message;
            Status = problems.Count == 0 ? message : $"{message} residents.json: {problems.Count} problem(s), see Player.log.".Trim();
        }

        private void SetSimulation(TownSimulation simulation)
        {
            Simulation = simulation;
            legacyWritten = false;
            Ai?.Attach(simulation);
            // A town with another layout (the city in Play Mode tests) needs its maps drawn afresh.
            if (view.World == null || view.World.TownId != simulation.World.TownId)
            {
                view.Build(simulation.World, TinyCity.Simulation.Time.Seasons.Of(simulation.Time));
            }

            view.ForgetResidents();
            Sound?.Forget();
            TownCamera.ClearSpeech();
            Notifications.Reset(simulation);
            if (simulation.GetResident(selectedId) == null)
            {
                selectedId = simulation.Residents[0].Id;
            }

            // A camera over a map this town doesn't have, or following someone who isn't in it, shows the whole land.
            if (TownCamera.IsFree ? TownCamera.MapId != null && !simulation.World.TryGetMap(TownCamera.MapId, out _) : simulation.GetResident(TownCamera.FollowId) == null)
            {
                TownCamera.ShowWholeLand(view, simulation);
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
            var world = FlatLand.CreateWorld();
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

                UseSlot(slot);
                SetSimulation(result.Simulation);
                foreach (var warning in result.Warnings)
                {
                    Debug.LogWarning($"[TinyCity] Save repaired: {warning}");
                }

                Status = (useBackup ? $"The save of {CurrentTownName} was damaged, so its backup was loaded. " : $"Loaded {CurrentTownName}. ")
                    + (result.Warnings.Count > 0 ? $"{result.Warnings.Count} problem(s) were repaired." : string.Empty);
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
            view.ShowBlocks(Simulation.Construction);
            view.UpdateResidents(Simulation, Mathf.Clamp01(tickProgress));
            TownCamera.Update(CameraArea(), view, Simulation);
        }

        /// <summary>The part of the window left of the inspector, in GUI coordinates (origin top left).</summary>
        private Rect CameraArea() => new Rect(0f, 0f, Mathf.Max(1f, Screen.width - (SidebarHidden ? 0f : Inspector.Width)), Screen.height);

        public void ToggleSidebar()
        {
            SidebarHidden = !SidebarHidden;
            PlayerPrefs.SetInt(SidebarPrefsKey, SidebarHidden ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (SpeedIndex > 0)
            {
                lastSpeedIndex = SpeedIndex;
            }
        }

        /// <summary>Shows <paramref name="r"/> in the inspector, and optionally points the camera at them.</summary>
        public void Select(Resident r, bool follow)
        {
            if (r == null)
            {
                return;
            }

            selectedId = r.Id;
            if (follow && r.IsAlive)
            {
                TownCamera.Follow(r.Id, Simulation);
            }
        }

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
            Select(r, follow: false);
            journal.ShowOnly(r.Id);
        }

        /// <summary>Shows whoever a journal entry or notification is about, and points the camera at them if they're alive.</summary>
        public void GoTo(TownEvent entry)
        {
            var r = entry.ResidentIds.Select(id => Simulation.GetResident(id)).FirstOrDefault(x => x != null);
            Select(r, follow: true);
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

            HandleKeys(e);
            TownCamera.DrawOverlays(view, Simulation, styles, !OverWindow(e.mousePosition));
            DrawSidebarButton();
            journal.Draw(CameraArea(), styles);
            saves.Draw(CameraArea(), styles);
            settingsWindow.Draw(CameraArea(), styles);
            Notifications.Draw(CameraArea(), styles, GoTo);
            if (!SidebarHidden)
            {
                inspector.Draw(styles);
            }

            if ((e.isMouse || e.type == EventType.ScrollWheel) && !OverWindow(e.mousePosition))
            {
                // After the buttons have had their chance, so they and the inspector win over the camera.
                TownCamera.HandleMouse(e, view);
            }
        }

        /// <summary>While the side panel is hidden, a button in the top right corner brings it back.</summary>
        private void DrawSidebarButton()
        {
            if (!SidebarHidden)
            {
                return;
            }

            var area = CameraArea();
            if (GUI.Button(new Rect(area.xMax - 66f, area.y + 4f, 62f, 20f), "◀ Panel", styles.HeaderButton))
            {
                ToggleSidebar();
            }
        }

        /// <summary>Follows the next or previous resident, and shows them in the inspector.</summary>
        private void StepResident(int step)
        {
            TownCamera.CycleResident(step, Simulation);
            selectedId = TownCamera.FollowId ?? selectedId;
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
                case KeyCode.F:
                    TownCamera.Follow(selectedId, Simulation);
                    break;
                case KeyCode.N:
                    StepResident(1);
                    break;
                case KeyCode.B:
                    StepResident(-1);
                    break;
                case KeyCode.H:
                    ToggleSidebar();
                    break;
                case KeyCode.J:
                    ToggleJournal();
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
