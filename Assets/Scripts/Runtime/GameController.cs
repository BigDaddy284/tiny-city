using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation;
using TinyTown.Simulation.AI;
using TinyTown.Simulation.Configuration;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Persistence;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Runs the town: advances the simulation with the clock, saves and loads, draws the map and the inspector.
    /// All rules live in <see cref="TownSimulation"/>; this class only drives and displays it.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public const string SaveSlot = "town";

        /// <summary>Game minutes per real second. At 1x one in-game day lasts 24 real minutes.</summary>
        public static readonly int[] Speeds = { 0, 1, 2, 4, 10 };

        private const int AutosaveEveryGameMinutes = 6 * 60;
        private const int MaxTicksPerFrame = 600;
        private const float PanelWidth = 380f;
        private const float ConfirmSeconds = 4f;
        private const float SpeechSeconds = 8f;

        private int speedIndex = 1;
        private int lastSpeedIndex = 1;
        private float accumulator;
        private long lastAutosaveMinute;
        private string status = string.Empty;
        private float confirmNewUntil;
        private bool followResident = true;
        private string viewedMapId;
        private string selectedId;
        private Vector2 panelScroll;

        private readonly Dictionary<string, SpriteRenderer> residentRenderers = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, Location> previousLocations = new Dictionary<string, Location>();
        private readonly Dictionary<string, (ConversationLine line, float until)> speech = new Dictionary<string, (ConversationLine, float)>();

        private Camera cam;
        private MapView mapView;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
        private GUIStyle labelStyle;
        private GUIStyle bubbleStyle;

        public TownSimulation Simulation { get; private set; }

        public GameSettings Settings { get; set; } = new GameSettings();

        /// <summary>Talks to Ollama; residents fall back to their built-in routine whenever it can't help.</summary>
        public AiService Ai { get; private set; }

        public int Speed => Speeds[speedIndex];

        /// <summary>The resident shown in the inspector and followed by the camera.</summary>
        private Resident Resident => Simulation.GetResident(selectedId) ?? Simulation.Residents[0];

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
            cam = Camera.main;
            mapView = new GameObject("Map").AddComponent<MapView>();
            Ai = new AiService(Settings.ollama, new UnityAiTransport()) { Log = message => Debug.Log($"[TinyTown] AI: {message}") };

            if (!SaveStore.Exists(SaveSlot) || !TryLoad())
            {
                StartNewTown(status.Length == 0 ? "Started a new town." : status + " Started a new town instead.");
            }

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
            }
        }

        private void TickOnce()
        {
            foreach (var r in Simulation.Residents)
            {
                previousLocations[r.Id] = r.Location;
            }

            Simulation.Tick();
            if (Simulation.Time.TotalMinutes - lastAutosaveMinute >= AutosaveEveryGameMinutes)
            {
                Save("autosave");
            }
        }

        private void StartNewTown(string message)
        {
            SetSimulation(StarterTown.CreateNewGame());
            status = message;
        }

        private void SetSimulation(TownSimulation simulation)
        {
            Simulation = simulation;
            Ai?.Attach(simulation);
            speech.Clear();
            previousLocations.Clear();
            foreach (var renderer in residentRenderers.Values)
            {
                Destroy(renderer.gameObject);
            }

            residentRenderers.Clear();
            if (simulation.GetResident(selectedId) == null)
            {
                selectedId = simulation.Residents[0].Id;
            }

            lastAutosaveMinute = simulation.Time.TotalMinutes;
            accumulator = 0f;
            followResident = true;
            viewedMapId = null;
        }

        private void Save(string reason)
        {
            try
            {
                SaveStore.Write(SaveSlot, SaveSerializer.Save(Simulation));
                lastAutosaveMinute = Simulation.Time.TotalMinutes;
                status = $"Saved ({reason}) at {Simulation.Time}.";
            }
            catch (Exception e)
            {
                status = $"Save failed: {e.Message}";
                Debug.LogError($"[TinyTown] {status}");
            }
        }

        private bool TryLoad()
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

                // Saves from before Phase 3 had only Mabel; her new neighbours move in at home.
                var newcomers = StarterTown.AddMissingResidents(result.Simulation);

                SetSimulation(result.Simulation);
                foreach (var warning in result.Warnings)
                {
                    Debug.LogWarning($"[TinyTown] Save repaired: {warning}");
                }

                status = (useBackup ? "Main save was damaged, so the backup was loaded. " : "Loaded saved town. ")
                    + (result.Warnings.Count > 0 ? $"{result.Warnings.Count} problem(s) were repaired. " : string.Empty)
                    + (newcomers > 0 ? $"{newcomers} new neighbour(s) moved in." : string.Empty);
                return true;
            }

            foreach (var problem in problems)
            {
                Debug.LogWarning($"[TinyTown] Load failed: {problem}");
            }

            status = $"Couldn't load the save: {problems.LastOrDefault()}";
            return false;
        }

        private void UpdateView(float tickProgress)
        {
            if (Simulation == null || cam == null)
            {
                return;
            }

            var mapId = followResident || viewedMapId == null ? Resident.Location.MapId : viewedMapId;
            if (mapView.Map == null || mapView.Map.Id != mapId)
            {
                mapView.Show(Simulation.World, Simulation.World.GetMap(mapId));
                viewedMapId = mapId;
            }

            cam.rect = new Rect(0f, 0f, Mathf.Clamp01(1f - (PanelWidth / Mathf.Max(1, Screen.width))), 1f);
            FrameCamera(mapView.Map);

            var t = Mathf.Clamp01(tickProgress);
            var sharing = new Dictionary<Location, int>();
            foreach (var r in Simulation.Residents)
            {
                var renderer = RendererFor(r);
                var here = r.Location;
                renderer.enabled = here.MapId == mapId && r.IsAlive;
                if (!renderer.enabled)
                {
                    continue;
                }

                // Residents on the same tile stand side by side instead of on top of each other.
                sharing.TryGetValue(here, out var alreadyHere);
                sharing[here] = alreadyHere + 1;
                var offset = new Vector3((alreadyHere % 2 == 0 ? 1 : -1) * 0.16f * ((alreadyHere + 1) / 2), -0.05f, 0f);

                var previous = previousLocations.TryGetValue(r.Id, out var p) ? p : here;
                var from = previous.MapId == here.MapId ? previous.Position : here.Position;
                var x = Mathf.Lerp(from.X, here.Position.X, t);
                var y = Mathf.Lerp(from.Y, here.Position.Y, t);
                renderer.transform.position = IsoGrid.TileToWorld(x, y) + offset;
                renderer.sortingOrder = IsoGrid.SortingOrder(Mathf.Max(from.X, here.Position.X), Mathf.Max(from.Y, here.Position.Y), 2);
            }
        }

        private SpriteRenderer RendererFor(Resident r)
        {
            if (!residentRenderers.TryGetValue(r.Id, out var renderer))
            {
                renderer = new GameObject($"Resident {r.FirstName}").AddComponent<SpriteRenderer>();
                renderer.sprite = PlaceholderArt.Person(PlaceholderArt.ParseColour(r.ColourHex, new Color(0.85f, 0.45f, 0.3f)));
                residentRenderers.Add(r.Id, renderer);
            }

            return renderer;
        }

        private void FrameCamera(TileMap map)
        {
            var left = IsoGrid.TileToWorld(0, map.Height - 1).x - 0.6f;
            var right = IsoGrid.TileToWorld(map.Width - 1, 0).x + 0.6f;
            var top = IsoGrid.TileToWorld(0, 0).y + 0.9f;
            var bottom = IsoGrid.TileToWorld(map.Width - 1, map.Height - 1).y - 0.4f;
            cam.orthographic = true;
            cam.transform.position = new Vector3((left + right) / 2f, (top + bottom) / 2f, -10f);
            cam.orthographicSize = Mathf.Max((top - bottom) / 2f, (right - left) / (2f * Mathf.Max(0.1f, cam.aspect))) * 1.05f;
        }

        private void OnGUI()
        {
            if (Simulation == null)
            {
                return;
            }

            EnsureStyles();
            HandleKeys(Event.current);
            DrawWorldLabels();
            DrawPanel();
        }

        private void HandleKeys(Event e)
        {
            if (e.type != EventType.KeyDown)
            {
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.Space:
                    SetSpeed(speedIndex == 0 ? lastSpeedIndex : 0);
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
                    CycleMap();
                    break;
                case KeyCode.F:
                    followResident = true;
                    break;
                case KeyCode.N:
                    SelectNext();
                    break;
                default:
                    return;
            }

            e.Use();
        }

        private void SetSpeed(int index)
        {
            speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (speedIndex > 0)
            {
                lastSpeedIndex = speedIndex;
            }
        }

        private void Select(Resident r)
        {
            selectedId = r.Id;
            followResident = true;
        }

        private void SelectNext()
        {
            var list = Simulation.Residents;
            var index = list.ToList().IndexOf(Resident);
            Select(list[(index + 1) % list.Count]);
        }

        private void CycleMap()
        {
            var maps = Simulation.World.Maps.Select(m => m.Id).ToList();
            var current = maps.IndexOf(viewedMapId);
            viewedMapId = maps[(current + 1) % maps.Count];
            followResident = false;
        }

        private void DrawWorldLabels()
        {
            foreach (var obj in Simulation.World.Objects.Where(o => o.Location.MapId == viewedMapId))
            {
                Label(IsoGrid.TileToWorld(obj.Location.Position.X, obj.Location.Position.Y) + new Vector3(0f, 0.32f, 0f), obj.Name, smallStyle);
            }

            foreach (var r in Simulation.Residents)
            {
                if (!residentRenderers.TryGetValue(r.Id, out var renderer) || !renderer.enabled)
                {
                    continue;
                }

                var position = renderer.transform.position;
                Label(position + new Vector3(0f, 0.72f, 0f), r.FirstName, labelStyle);

                // A bubble appears for each new line a resident says, and fades after a few seconds.
                var line = Simulation.LastLineBy(r.Id);
                if (line != null && (!speech.TryGetValue(r.Id, out var shown) || shown.line != line))
                {
                    var fresh = Simulation.Time.TotalMinutes - line.Time.TotalMinutes <= 2;
                    speech[r.Id] = (line, fresh ? UnityEngine.Time.unscaledTime + SpeechSeconds : 0f);
                }

                if (speech.TryGetValue(r.Id, out var bubble) && UnityEngine.Time.unscaledTime < bubble.until)
                {
                    var screen = cam.WorldToScreenPoint(position + new Vector3(0f, 0.95f, 0f));
                    var content = new GUIContent($"“{bubble.line.Text}”");
                    var height = bubbleStyle.CalcHeight(content, 220f);
                    GUI.Box(new Rect(screen.x - 110f, Screen.height - screen.y - height, 220f, height), content, bubbleStyle);
                }
            }
        }

        private void Label(Vector3 world, string text, GUIStyle style)
        {
            var screen = cam.WorldToScreenPoint(world);
            var rect = new Rect(screen.x - 70f, Screen.height - screen.y - 10f, 140f, 20f);
            GUI.Label(rect, text, style);
        }

        private void DrawPanel()
        {
            var r = Resident;
            var area = new Rect(Screen.width - PanelWidth, 0f, PanelWidth, Screen.height);
            GUI.color = new Color(0.98f, 0.93f, 0.84f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 14f, 10f, PanelWidth - 28f, Screen.height - 20f));
            GUILayout.Label("The Tiny Town", titleStyle);
            GUILayout.Label($"{Simulation.Time.Weekday}, Day {Simulation.Time.Day}   {Simulation.Time.ClockText}", textStyle);

            GUILayout.BeginHorizontal();
            for (var i = 0; i < Speeds.Length; i++)
            {
                var label = Speeds[i] == 0 ? "Pause" : $"{Speeds[i]}x";
                if (GUILayout.Toggle(speedIndex == i, label, GUI.skin.button) && speedIndex != i)
                {
                    SetSpeed(i);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var other in Simulation.Residents)
            {
                if (GUILayout.Toggle(other == r, other.FirstName, GUI.skin.button) && other != r)
                {
                    Select(other);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            panelScroll = GUILayout.BeginScrollView(panelScroll);

            GUILayout.Label($"{r.FullName}, {r.AgeYears} ({Words(r.LifeStage.ToString())})", titleStyle);
            GUILayout.Label($"{r.Job?.Title ?? "No job"} · {r.Personality.Describe()}", smallStyle);
            GUILayout.Label($"Where: {DescribeLocation(r)}", textStyle);
            GUILayout.Label($"Doing: {DescribeActivity(r)}", textStyle);
            if (!string.IsNullOrEmpty(r.Goal))
            {
                var source = r.GoalSource == DecisionSource.Ai ? $"in {r.FirstName}'s words, via AI" : "routine";
                GUILayout.Label($"Why ({source}): {r.Goal}", smallStyle);
            }

            var said = Simulation.Conversations.LastLineBy(r.Id);
            if (said != null)
            {
                GUILayout.Label($"Last said ({said.Time.ClockText}): “{said.Text}”", smallStyle);
            }

            GUILayout.Space(6f);
            for (var i = 0; i < Needs.Count; i++)
            {
                NeedBar((NeedType)i, r.Needs[(NeedType)i]);
            }

            GUILayout.Space(6f);
            GUILayout.Label("People", textStyle);
            foreach (var other in Simulation.Residents.Where(o => o != r))
            {
                GUILayout.Label(DescribeRelationship(r, other), smallStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label("Recently", textStyle);
            foreach (var entry in r.Log.Reverse().Take(4))
            {
                GUILayout.Label($"{entry.Time.ClockText}  {entry.Text}", smallStyle);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Remembers", textStyle);
            foreach (var memory in Simulation.Memories.ForResident(r.Id).OrderByDescending(m => m.Time.TotalMinutes).ThenByDescending(m => m.Id).Take(3))
            {
                GUILayout.Label($"Day {memory.Time.Day}: {memory.Summary}", smallStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save (F5)"))
            {
                Save("manual");
            }

            if (GUILayout.Button("Load (F9)"))
            {
                TryLoad();
            }

            var confirming = UnityEngine.Time.unscaledTime < confirmNewUntil;
            if (GUILayout.Button(confirming ? "Click again to restart" : "Start over"))
            {
                if (confirming)
                {
                    StartNewTown("Started a new town. Your old save is replaced at the next save.");
                    confirmNewUntil = 0f;
                }
                else
                {
                    confirmNewUntil = UnityEngine.Time.unscaledTime + ConfirmSeconds;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(followResident ? $"View: following {r.FirstName} (N: next resident, Tab: other map)" : $"View: {Simulation.World.GetMap(viewedMapId).Name} (F: follow {r.FirstName})", smallStyle);
            GUILayout.Label(Ai.Summary(), smallStyle);
            var lastEvent = Ai.RecentEvents.LastOrDefault();
            if (lastEvent != null)
            {
                GUILayout.Label(lastEvent, smallStyle);
            }

            if (!string.IsNullOrEmpty(status))
            {
                GUILayout.Label(status, smallStyle);
            }

            GUILayout.EndArea();
        }

        private void NeedBar(NeedType need, double value)
        {
            var rect = GUILayoutUtility.GetRect(PanelWidth - 28f, 18f);
            GUI.Label(new Rect(rect.x, rect.y, 90f, rect.height), need.ToString(), smallStyle);
            var bar = new Rect(rect.x + 90f, rect.y + 3f, rect.width - 130f, rect.height - 6f);
            GUI.color = new Color(0.82f, 0.76f, 0.68f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            var fraction = (float)(value / Needs.Max);
            GUI.color = value < 20 ? new Color(0.85f, 0.3f, 0.25f) : value < 50 ? new Color(0.92f, 0.67f, 0.25f) : new Color(0.42f, 0.68f, 0.38f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 6f, rect.y, 40f, rect.height), $"{value:0}", smallStyle);
        }

        private string DescribeLocation(Resident r)
        {
            var map = Simulation.World.GetMap(r.Location.MapId);
            var at = Simulation.World.Objects.FirstOrDefault(o => o.Location == r.Location);
            return at != null ? $"{map.Name}, at the {at.Name.ToLowerInvariant()}" : map.Name;
        }

        private string DescribeActivity(Resident r)
        {
            TinyTown.Simulation.Activities.ActivityCatalog.TryGet(r.ActivityId, out var activity);
            switch (r.State)
            {
                case ResidentState.Performing:
                    return activity?.Verb ?? "something";
                case ResidentState.Travelling when r.TargetResidentId != null:
                    return $"looking for {Simulation.GetResident(r.TargetResidentId)?.FirstName ?? "someone"} for a chat";
                case ResidentState.Talking:
                    var chat = Simulation.GetConversation(r.ConversationId);
                    var partner = chat == null ? null : Simulation.GetResident(chat.OtherThan(r.Id));
                    var topic = string.IsNullOrEmpty(chat?.Topic) ? string.Empty : $" about {chat.Topic}";
                    return $"chatting with {partner?.FirstName ?? "someone"}{topic}";
                case ResidentState.Travelling:
                    Simulation.World.TryGetObject(r.TargetObjectId, out var target);
                    return $"walking to the {target?.Name.ToLowerInvariant()} to {activity?.Name.ToLowerInvariant()}";
                case ResidentState.Waiting:
                    return "taking it easy";
                case ResidentState.Deciding:
                    return "thinking about what to do";
                default:
                    return "deciding what to do";
            }
        }

        private string DescribeRelationship(Resident r, Resident other)
        {
            if (!Simulation.Relationships.TryGet(r.Id, other.Id, out var rel) || !rel.HaveMet)
            {
                return $"{other.FirstName}: hasn't met yet";
            }

            return $"{other.FirstName}: {rel.Describe()} (friendship {rel.Friendship:0}, trust {rel.Trust:0}{(rel.Dislike >= 10 ? $", dislike {rel.Dislike:0}" : string.Empty)}), {rel.Interactions} chat(s)";
        }

        private static string Words(string pascal) =>
            string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            var ink = new Color(0.3f, 0.2f, 0.12f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = textStyle.normal.textColor = smallStyle.normal.textColor = labelStyle.normal.textColor = ink;
            bubbleStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 6, 6) };
            bubbleStyle.normal.background = PlaceholderArt.Solid(new Color(1f, 0.99f, 0.95f, 0.95f));
            bubbleStyle.normal.textColor = ink;
        }
    }
}
