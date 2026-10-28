using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation;
using TinyTown.Simulation.Memory;
using TinyTown.Simulation.World;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// One to ten camera panels sharing the screen to the left of the inspector. The layout adapts to how many
    /// there are; each panel follows a resident or roams freely, and the active one is the one the player last
    /// clicked. All panels look at the same <see cref="TownView"/>, so they always agree.
    /// </summary>
    public sealed class CameraPanels
    {
        public const int MaxPanels = 10;
        public const float HeaderHeight = 22f;
        private const string PrefsKey = "TinyTown.Panels";
        private const float SpeechSeconds = 8f;

        /// <summary>Object names are only drawn when the camera is close enough for them to fit.</summary>
        private const float LabelPixelsPerUnit = 42f;

        private static readonly Color Background = new Color(0.98f, 0.91f, 0.78f);
        private static readonly Color ActiveBorder = new Color(0.85f, 0.45f, 0.2f);

        private readonly List<CameraPanel> panels = new List<CameraPanel>();
        private readonly Camera mainCamera;
        private readonly Dictionary<string, (ConversationLine line, float until)> speech = new Dictionary<string, (ConversationLine, float)>();
        private Rect area;

        public CameraPanels(Camera mainCamera)
        {
            this.mainCamera = mainCamera;
            panels.Add(new CameraPanel(mainCamera, ownsCamera: false));
        }

        public IReadOnlyList<CameraPanel> All => panels;

        public int Count => panels.Count;

        public int ActiveIndex { get; private set; }

        public CameraPanel Active => panels[Mathf.Clamp(ActiveIndex, 0, panels.Count - 1)];

        /// <summary>Called with a resident's id when the player clicks on them in a panel.</summary>
        public Action<string> ResidentClicked { get; set; }

        /// <summary>Splits <paramref name="area"/> into <paramref name="count"/> cells: a near-square grid whose last row stretches to fill the width.</summary>
        public static Rect[] Layout(int count, Rect area)
        {
            count = Mathf.Clamp(count, 1, MaxPanels);
            var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            var rows = Mathf.CeilToInt(count / (float)columns);
            var cells = new Rect[count];
            var height = area.height / rows;
            for (var i = 0; i < count; i++)
            {
                var row = i / columns;
                var inRow = row == rows - 1 ? count - (row * columns) : columns;
                var width = area.width / inRow;
                cells[i] = new Rect(area.x + ((i % columns) * width), area.y + (row * height), width, height);
            }

            return cells;
        }

        /// <summary>Adds a panel following <paramref name="residentId"/>, or a free one over the village. Returns false at the limit.</summary>
        public bool Add(TownView view, TownSimulation simulation, string residentId = null)
        {
            if (panels.Count >= MaxPanels)
            {
                return false;
            }

            var go = new GameObject($"Panel Camera {panels.Count + 1}");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = mainCamera != null ? mainCamera.backgroundColor : Background;
            camera.depth = (mainCamera != null ? mainCamera.depth : 0) + panels.Count;
            var panel = new CameraPanel(camera, ownsCamera: true);
            panels.Add(panel);
            if (residentId != null && simulation.GetResident(residentId) != null)
            {
                Follow(panels.Count - 1, residentId, simulation);
            }
            else
            {
                panel.LookAtMap(simulation.Residents[0].Location.MapId, view);
            }

            ActiveIndex = panels.Count - 1;
            return true;
        }

        /// <summary>Closes a panel. The last one always stays.</summary>
        public bool Remove(int index)
        {
            if (panels.Count <= 1 || index < 0 || index >= panels.Count)
            {
                return false;
            }

            var panel = panels[index];
            panels.RemoveAt(index);
            if (panel.OwnsCamera)
            {
                UnityEngine.Object.Destroy(panel.Camera.gameObject);
            }
            else
            {
                // The main camera can't go: hand it the first remaining panel's view instead.
                var replacement = panels[0];
                panels[0] = new CameraPanel(mainCamera, ownsCamera: false) { FollowId = replacement.FollowId, MapId = replacement.MapId, FreeCentre = replacement.FreeCentre, Zoom = replacement.Zoom };
                UnityEngine.Object.Destroy(replacement.Camera.gameObject);
            }

            ActiveIndex = Mathf.Clamp(ActiveIndex >= index ? ActiveIndex - 1 : ActiveIndex, 0, panels.Count - 1);
            return true;
        }

        /// <summary>Sets the number of panels, adding ones that follow residents nobody is watching yet.</summary>
        public void SetCount(int count, TownView view, TownSimulation simulation)
        {
            count = Mathf.Clamp(count, 1, MaxPanels);
            while (panels.Count > count)
            {
                Remove(panels.Count - 1);
            }

            while (panels.Count < count)
            {
                var unwatched = simulation.Residents.FirstOrDefault(r => r.IsAlive && panels.All(p => p.FollowId != r.Id));
                Add(view, simulation, unwatched?.Id);
            }
        }

        /// <summary>One panel for each living resident (up to ten), each following them.</summary>
        public void WatchEveryone(TownView view, TownSimulation simulation)
        {
            var living = simulation.Residents.Where(r => r.IsAlive).Take(MaxPanels).ToList();
            SetCount(Mathf.Max(1, living.Count), view, simulation);
            for (var i = 0; i < living.Count; i++)
            {
                Follow(i, living[i].Id, simulation);
            }
        }

        public void Follow(int index, string residentId, TownSimulation simulation)
        {
            var resident = simulation.GetResident(residentId);
            if (index < 0 || index >= panels.Count || resident == null)
            {
                return;
            }

            panels[index].FollowId = resident.Id;
            panels[index].MapId = resident.Location.MapId;
            panels[index].Zoom = 1f;
        }

        public void Activate(int index) => ActiveIndex = Mathf.Clamp(index, 0, panels.Count - 1);

        /// <summary>Moves a panel to the next (or previous) living resident.</summary>
        public void CycleResident(int index, int step, TownSimulation simulation)
        {
            var living = simulation.Residents.Where(r => r.IsAlive).ToList();
            if (living.Count == 0 || index < 0 || index >= panels.Count)
            {
                return;
            }

            var current = living.FindIndex(r => r.Id == panels[index].FollowId);
            var next = current < 0 ? 0 : (current + step + living.Count) % living.Count;
            Follow(index, living[next].Id, simulation);
        }

        /// <summary>Points a free panel at the next map, or turns a following panel free over its current map.</summary>
        public void CycleMap(int index, TownView view, TownSimulation simulation)
        {
            var panel = panels[index];
            var ids = simulation.World.Maps.OrderBy(m => m.IsExterior ? 0 : 1).ThenBy(m => m.Id, StringComparer.Ordinal).Select(m => m.Id).ToList();
            var next = panel.IsFree ? ids[(ids.IndexOf(panel.MapId) + 1) % ids.Count] : panel.MapId;
            panel.LookAtMap(next, view);
        }

        /// <summary>Lays the panels out in <paramref name="screenArea"/> and aims every camera. Call once per frame.</summary>
        public void Update(Rect screenArea, TownView view, TownSimulation simulation)
        {
            area = screenArea;
            var cells = Layout(panels.Count, area);
            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                var followed = panel.IsFree ? null : simulation.GetResident(panel.FollowId);
                if (!panel.IsFree && (followed == null || !followed.IsAlive))
                {
                    // Someone who has died can't be followed; the panel stays where they were, free.
                    panel.LookAtMap(panel.MapId ?? simulation.Residents[0].Location.MapId, view);
                }
                else if (followed != null)
                {
                    panel.MapId = followed.Location.MapId;
                }

                panel.SetScreenRect(cells[i]);
                panel.Aim(view);
            }
        }

        /// <summary>Clicks select a panel and the resident under the pointer; dragging pans a free panel; the wheel zooms.</summary>
        public void HandleMouse(Event e, TownView view)
        {
            var index = panels.FindIndex(p => p.ScreenRect.Contains(e.mousePosition));
            if (index < 0 || (e.mousePosition.y - panels[index].ScreenRect.y) < HeaderHeight)
            {
                return;
            }

            var panel = panels[index];
            switch (e.type)
            {
                case EventType.MouseDown:
                    ActiveIndex = index;
                    var clicked = ResidentAt(panel, e.mousePosition, view);
                    if (clicked != null)
                    {
                        ResidentClicked?.Invoke(clicked);
                    }

                    e.Use();
                    break;
                case EventType.MouseDrag when panel.IsFree && index == ActiveIndex:
                    panel.Pan(e.delta, view);
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    panel.Zoom = Mathf.Clamp(panel.Zoom * (1f + (e.delta.y * 0.08f)), CameraPanel.MinZoom, CameraPanel.MaxZoom);
                    e.Use();
                    break;
            }
        }

        private static string ResidentAt(CameraPanel panel, Vector2 gui, TownView view)
        {
            string best = null;
            var bestDistance = 24f;
            foreach (var pair in view.ResidentRenderers)
            {
                if (pair.Value == null || !pair.Value.enabled || !panel.TryScreenPoint(pair.Value.transform.position + new Vector3(0f, 0.3f, 0f), out var point))
                {
                    continue;
                }

                var distance = Vector2.Distance(point, gui);
                if (distance < bestDistance)
                {
                    best = pair.Key;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Draws names, door signs and speech bubbles inside each panel, then each panel's header bar.</summary>
        public void DrawOverlays(TownView view, TownSimulation simulation, GuiStyles styles, Action<int> headerButtons)
        {
            NoteNewSpeech(simulation);
            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                GUI.BeginGroup(panel.ScreenRect);
                DrawLabels(panel, view, simulation, styles);
                GUI.EndGroup();
                DrawFrame(panel, i == ActiveIndex && panels.Count > 1);
                headerButtons(i);
            }
        }

        public string Describe(int index, TownSimulation simulation)
        {
            var panel = panels[index];
            if (!panel.IsFree)
            {
                var r = simulation.GetResident(panel.FollowId);
                return r == null ? "Following nobody" : $"{r.FirstName} · {simulation.World.GetMap(r.Location.MapId).Name}";
            }

            return simulation.World.TryGetMap(panel.MapId, out var map) ? $"Free camera · {map.Name}" : "Free camera";
        }

        public void SaveLayout()
        {
            var parts = panels.Select(p => p.IsFree ? "map:" + p.MapId : "res:" + p.FollowId);
            PlayerPrefs.SetString(PrefsKey, string.Join("|", parts));
            PlayerPrefs.Save();
        }

        /// <summary>Restores the panels from the last session, skipping anyone or anywhere that no longer exists.</summary>
        public void LoadLayout(TownView view, TownSimulation simulation)
        {
            var saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            var entries = saved.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Take(MaxPanels).ToList();
            if (entries.Count == 0)
            {
                return;
            }

            SetCount(entries.Count, view, simulation);
            for (var i = 0; i < entries.Count; i++)
            {
                var value = entries[i].Length > 4 ? entries[i].Substring(4) : string.Empty;
                if (entries[i].StartsWith("res:") && simulation.GetResident(value)?.IsAlive == true)
                {
                    Follow(i, value, simulation);
                }
                else if (entries[i].StartsWith("map:") && simulation.World.TryGetMap(value, out _))
                {
                    panels[i].LookAtMap(value, view);
                }
            }

            ActiveIndex = 0;
        }

        /// <summary>Forgets speech bubbles, e.g. when another town is loaded.</summary>
        public void ClearSpeech() => speech.Clear();

        private void NoteNewSpeech(TownSimulation simulation)
        {
            foreach (var r in simulation.Residents)
            {
                var line = simulation.LastLineBy(r.Id);
                if (line != null && (!speech.TryGetValue(r.Id, out var shown) || shown.line != line))
                {
                    var fresh = simulation.Time.TotalMinutes - line.Time.TotalMinutes <= 2;
                    speech[r.Id] = (line, fresh ? Time.unscaledTime + SpeechSeconds : 0f);
                }
            }
        }

        private void DrawLabels(CameraPanel panel, TownView view, TownSimulation simulation, GuiStyles styles)
        {
            var origin = panel.ScreenRect.position;
            var pixelsPerUnit = panel.ScreenRect.height / (2f * Mathf.Max(0.1f, panel.Camera.orthographicSize));
            if (pixelsPerUnit >= LabelPixelsPerUnit)
            {
                foreach (var obj in simulation.World.Objects)
                {
                    Label(panel, origin, view.ToWorld(obj.Location) + new Vector3(0f, 0.32f, 0f), obj.Name, styles.Small);
                }

                // Each door outside is labelled with the building it leads into.
                foreach (var portal in simulation.World.Portals)
                {
                    if (simulation.World.TryGetMap(portal.From.MapId, out var outside) && outside.IsExterior
                        && simulation.World.TryGetMap(portal.To.MapId, out var inside) && !inside.IsExterior)
                    {
                        Label(panel, origin, view.ToWorld(portal.From) + new Vector3(0f, 0.5f, 0f), inside.Name, styles.Small);
                    }
                }
            }

            foreach (var r in simulation.Residents)
            {
                if (!view.TryGetPosition(r.Id, out var position) || !panel.TryScreenPoint(position, out _))
                {
                    continue;
                }

                Label(panel, origin, position + new Vector3(0f, 0.72f, 0f), r.FirstName, styles.Name);

                // A bubble appears for each new line a resident says, and fades after a few seconds.
                if (speech.TryGetValue(r.Id, out var bubble) && Time.unscaledTime < bubble.until && panel.TryScreenPoint(position + new Vector3(0f, 0.95f, 0f), out var above))
                {
                    var content = new GUIContent($"“{bubble.line.Text}”");
                    var width = Mathf.Min(220f, panel.ScreenRect.width * 0.8f);
                    var height = styles.Bubble.CalcHeight(content, width);
                    GUI.Box(new Rect(above.x - origin.x - (width / 2f), above.y - origin.y - height, width, height), content, styles.Bubble);
                }
            }
        }

        private static void Label(CameraPanel panel, Vector2 origin, Vector3 world, string text, GUIStyle style)
        {
            if (panel.TryScreenPoint(world, out var gui))
            {
                GUI.Label(new Rect(gui.x - origin.x - 70f, gui.y - origin.y - 10f, 140f, 20f), text, style);
            }
        }

        private static void DrawFrame(CameraPanel panel, bool active)
        {
            var r = panel.ScreenRect;
            GUI.color = new Color(0.3f, 0.2f, 0.12f, 0.75f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, HeaderHeight), Texture2D.whiteTexture);
            GUI.color = active ? ActiveBorder : new Color(0.55f, 0.45f, 0.35f);
            var thickness = active ? 3f : 1f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
