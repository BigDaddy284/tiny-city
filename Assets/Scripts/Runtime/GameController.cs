using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation;
using TinyTown.Simulation.AI;
using TinyTown.Simulation.Configuration;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Persistence;
using TinyTown.Simulation.Residents;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Runs the town: advances the simulation with the clock, saves and loads, and hands drawing to the
    /// <see cref="TownView"/> (the maps and residents), the <see cref="CameraPanels"/> (what each camera watches) and the
    /// <see cref="Inspector"/> (the panel on the right). All rules live in <see cref="TownSimulation"/>.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public const string SaveSlot = "town";

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
        private GuiStyles styles;

        public TownSimulation Simulation { get; private set; }

        public GameSettings Settings { get; set; } = new GameSettings();

        /// <summary>Talks to Ollama; residents fall back to their built-in routine whenever it can't help.</summary>
        public AiService Ai { get; private set; }

        /// <summary>The camera panels, from one to ten. Panel 0 uses the main camera.</summary>
        public CameraPanels Panels { get; private set; }

        /// <summary>Where the maps and residents are drawn in the world.</summary>
        public TownView View => view;

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
            Panels = new CameraPanels(Camera.main) { ResidentClicked = id => Select(Simulation.GetResident(id), followInActivePanel: false) };
            inspector = new Inspector(this);
            Ai = new AiService(Settings.ollama, new UnityAiTransport()) { Log = message => Debug.Log($"[TinyTown] AI: {message}") };

            if (!SaveStore.Exists(SaveSlot) || !TryLoad())
            {
                StartNewTown(Status.Length == 0 ? "Started a new town." : Status + " Started a new town instead.");
            }

            Panels.LoadLayout(view, Simulation);
            UpdateView(1f);
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
        }

        private void OnApplicationQuit()
        {
            if (Simulation != null)
            {
                Save("on exit");
                Panels?.SaveLayout();
            }
        }

        private void TickOnce()
        {
            view.RememberPositions(Simulation);
            Simulation.Tick();
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
                view.Build(simulation.World);
            }

            view.ForgetResidents();
            Panels.ClearSpeech();
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
                SaveStore.Write(SaveSlot, SaveSerializer.Save(Simulation));
                lastAutosaveMinute = Simulation.Time.TotalMinutes;
                Status = $"Saved ({reason}) at {Simulation.Time}.";
            }
            catch (Exception e)
            {
                Status = $"Save failed: {e.Message}";
                Debug.LogError($"[TinyTown] {Status}");
            }
        }

        public bool TryLoad()
        {
            var world = StarterTown.CreateWorld();
            var problems = new List<string>();
            foreach (var useBackup in new[] { false, true })
            {
                if (!SaveStore.TryRead(SaveSlot, useBackup, out var json, out var readError))
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

                SetSimulation(result.Simulation);
                foreach (var warning in result.Warnings)
                {
                    Debug.LogWarning($"[TinyTown] Save repaired: {warning}");
                }

                Status = (useBackup ? "Main save was damaged, so the backup was loaded. " : "Loaded saved town. ")
                    + (result.Warnings.Count > 0 ? $"{result.Warnings.Count} problem(s) were repaired. " : string.Empty)
                    + (newcomers > 0 ? $"{newcomers} new neighbour(s) moved in." : string.Empty);
                return true;
            }

            foreach (var problem in problems)
            {
                Debug.LogWarning($"[TinyTown] Load failed: {problem}");
            }

            Status = $"Couldn't load the save: {problems.LastOrDefault()}";
            return false;
        }

        private void UpdateView(float tickProgress)
        {
            if (Simulation == null || view == null)
            {
                return;
            }

            view.UpdateResidents(Simulation, Mathf.Clamp01(tickProgress));
            Panels.Update(CameraArea(), view, Simulation);
        }

        /// <summary>The part of the window left of the inspector, in GUI coordinates (origin top left).</summary>
        private static Rect CameraArea() => new Rect(0f, 0f, Mathf.Max(1f, Screen.width - Inspector.Width), Screen.height);

        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (SpeedIndex > 0)
            {
                lastSpeedIndex = SpeedIndex;
            }
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

        public void WatchEveryone() => Panels.WatchEveryone(view, Simulation);

        private void OnGUI()
        {
            if (Simulation == null)
            {
                return;
            }

            styles ??= new GuiStyles();
            var e = Event.current;
            HandleKeys(e);
            Panels.DrawOverlays(view, Simulation, styles, DrawPanelHeader);
            inspector.Draw(styles);
            if (e.isMouse || e.type == EventType.ScrollWheel)
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

            if (Panels.Count > 1)
            {
                x -= 22f;
                if (GUI.Button(new Rect(x, y, 20f, 18f), "×", styles.HeaderButton))
                {
                    Panels.Remove(index);
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

            GUI.Label(new Rect(rect.x + 6f, y, Mathf.Max(0f, x - rect.x - 10f), 18f), Panels.Describe(index, Simulation), styles.Header);
        }

        /// <summary>A following panel moves to the next or previous resident; a free one to the next map.</summary>
        private void Step(int index, int step)
        {
            if (Panels.All[index].IsFree)
            {
                Panels.CycleMap(index, view, Simulation);
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
                default:
                    return;
            }

            e.Use();
        }
    }
}
