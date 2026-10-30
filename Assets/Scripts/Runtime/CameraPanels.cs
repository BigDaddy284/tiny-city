using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.World;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// One to ten camera panels sharing the screen to the left of the inspector. The layout adapts to how many
    /// there are; each panel follows a resident or roams freely, and the active one is the one the player last
    /// clicked. All panels look at the same <see cref="TownView"/>, so they always agree.
    /// </summary>
    public sealed class CameraPanels
    {
        /// <summary>
        /// Sixteen, so Watch Everyone can show all fifteen starting residents. The owner's camera panel test (RTX 3090,
        /// 2558 × 1360, 30 October 2026) stayed above 60 fps at every count up to 16.
        /// </summary>
        public const int MaxPanels = 16;

        /// <summary>How many panels may be shown: <see cref="MaxPanels"/>, raised only while the panel test runs.</summary>
        public int Limit { get; set; } = MaxPanels;
        public const float HeaderHeight = 22f;
        private const string PrefsKey = "TinyCity.Panels";
        private const float SpeechSeconds = 8f;

        /// <summary>Object names are only drawn when the camera is close enough for them to fit.</summary>
        private const float LabelPixelsPerUnit = 42f;


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
            count = Mathf.Clamp(count, 1, PanelTest.Largest);
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
            if (panels.Count >= Limit)
            {
                return false;
            }

            var go = new GameObject($"Panel Camera {panels.Count + 1}");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = mainCamera != null ? mainCamera.backgroundColor : UiTheme.ToColor(UiTheme.Palette.Canvas);
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
            count = Mathf.Clamp(count, 1, Limit);
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

        /// <summary>One panel for each living resident (up to <see cref="MaxPanels"/>), each following them.</summary>
        public void WatchEveryone(TownView view, TownSimulation simulation)
        {
            townViewReturn = null;
            var living = simulation.Residents.Where(r => r.IsAlive).Take(Limit).ToList();
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

        /// <summary>Every place a panel can show: the village first, then each building by name.</summary>
        public static List<TileMap> Places(TownWorld world) =>
            world.Maps.OrderBy(m => m.IsExterior ? 0 : 1).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Points a free panel at the next (or, with a negative <paramref name="step"/>, previous) place, or turns a
        /// following panel free over its current map.
        /// </summary>
        public void CycleMap(int index, TownView view, TownSimulation simulation, int step = 1)
        {
            var panel = panels[index];
            var ids = Places(simulation.World).Select(m => m.Id).ToList();
            var next = panel.IsFree ? ids[(((ids.IndexOf(panel.MapId) + step) % ids.Count) + ids.Count) % ids.Count] : panel.MapId;
            panel.LookAtMap(next, view);
        }

        /// <summary>Turns a panel into a free camera over <paramref name="mapId"/>, whoever it was following.</summary>
        public void ShowPlace(int index, string mapId, TownView view)
        {
            if (index < 0 || index >= panels.Count || view.World == null || !view.World.TryGetMap(mapId, out _))
            {
                return;
            }

            panels[index].LookAtMap(mapId, view);
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

        /// <summary>
        /// Clicks select a panel and the resident under the pointer; a double click on a building shows its inside in
        /// that panel; dragging pans a free panel; the wheel zooms.
        /// </summary>
        public void HandleMouse(Event e, TownView view, TownSimulation simulation)
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
                    else if (e.clickCount == 2 && BuildingUnder(panel, e.mousePosition, view, simulation) is PlaceInfo place)
                    {
                        ShowPlace(index, place.EnterMapId, view);
                    }

                    e.Use();
                    break;
                case EventType.MouseDrag when panel.IsFree && index == ActiveIndex:
                    panel.Pan(e.delta, view);
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    panel.ZoomAt(e.delta.y, e.mousePosition, view);
                    e.Use();
                    break;
            }
        }

        /// <summary>The building drawn under a point of a panel (GUI coordinates), described, or null.</summary>
        private static PlaceInfo BuildingUnder(CameraPanel panel, Vector2 gui, TownView view, TownSimulation simulation)
        {
            var world = panel.Camera.ScreenToWorldPoint(new Vector3(gui.x, Screen.height - gui.y, 0f));
            var mapId = view.MapAt(world);
            if (mapId == null || !simulation.World.TryGetMap(mapId, out var map) || !map.IsExterior)
            {
                return null;
            }

            var building = view.BuildingAt(mapId, world);
            return building == null ? null : PlaceInfo.Of(simulation, building.Value.inside);
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

        /// <summary>
        /// Draws names, door signs and speech bubbles inside each panel, then each panel's header bar, then the card
        /// about the building under the pointer unless <paramref name="pointerFree"/> is false (it's over a window).
        /// </summary>
        public void DrawOverlays(TownView view, TownSimulation simulation, GuiStyles styles, Action<int> headerButtons, bool pointerFree)
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

            if (pointerFree && Event.current.type == EventType.Repaint)
            {
                DrawBuildingCard(Event.current.mousePosition, view, simulation, styles);
            }
        }

        private void DrawBuildingCard(Vector2 mouse, TownView view, TownSimulation simulation, GuiStyles styles)
        {
            var panel = panels.FirstOrDefault(p => p.ScreenRect.Contains(mouse));
            if (panel == null || mouse.y - panel.ScreenRect.y < HeaderHeight || ResidentAt(panel, mouse, view) != null)
            {
                return;
            }

            var place = BuildingUnder(panel, mouse, view, simulation);
            if (place == null)
            {
                return;
            }

            var content = new GUIContent($"<b>{place.Title}</b>\n{string.Join("\n", place.Lines)}\n<size=11><i>Double-click to look inside</i></size>");
            var width = Mathf.Min(260f, panel.ScreenRect.width - 16f);
            var height = styles.Card.CalcHeight(content, width);
            var bounds = panel.ScreenRect;
            var x = Mathf.Clamp(mouse.x + 16f, bounds.x + 4f, bounds.xMax - width - 4f);
            var y = mouse.y + 18f + height <= bounds.yMax - 4f ? mouse.y + 18f : Mathf.Max(bounds.y + HeaderHeight, mouse.y - height - 8f);
            GUI.Box(new Rect(x, y, width, height), content, styles.Card);
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

        /// <summary>
        /// True while the panels show Town View: one free camera over the whole city, put up by <see cref="ToggleTownView"/>
        /// and left again by pressing it a second time, by Watch Everyone, or by changing the panels.
        /// </summary>
        public bool InTownView => townViewReturn != null && panels.Count == 1 && panels[0].IsFree && panels[0].MapId == townViewMapId;

        private string townViewReturn;
        private string townViewMapId;

        /// <summary>
        /// Switches to Town View, remembering the panels as they were, or, if Town View is showing, back to those panels.
        /// </summary>
        public void ToggleTownView(TownView view, TownSimulation simulation)
        {
            if (InTownView)
            {
                var back = townViewReturn;
                townViewReturn = null;
                ApplyLayout(back, view, simulation);
                return;
            }

            var city = simulation.World.Maps.Where(m => m.IsExterior).OrderByDescending(m => m.Width * m.Height).FirstOrDefault();
            if (city == null)
            {
                return;
            }

            townViewReturn = LayoutText();
            townViewMapId = city.Id;
            SetCount(1, view, simulation);
            panels[0].LookAtMap(city.Id, view);
            ActiveIndex = 0;
        }

        public void SaveLayout()
        {
            PlayerPrefs.SetString(PrefsKey, LayoutText());
            PlayerPrefs.Save();
        }

        /// <summary>The panels as they are, to put back later with <see cref="Restore"/>.</summary>
        public string Snapshot() => LayoutText();

        public void Restore(string snapshot, TownView view, TownSimulation simulation) => ApplyLayout(snapshot, view, simulation);

        private string LayoutText() => string.Join("|", panels.Select(p => p.IsFree ? "map:" + p.MapId : "res:" + p.FollowId));

        /// <summary>Restores the panels from the last session, skipping anyone or anywhere that no longer exists.</summary>
        public void LoadLayout(TownView view, TownSimulation simulation) => ApplyLayout(PlayerPrefs.GetString(PrefsKey, string.Empty), view, simulation);

        private void ApplyLayout(string saved, TownView view, TownSimulation simulation)
        {
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
                    Label(panel, origin, view.ToWorld(obj.Location) + new Vector3(0f, 0.32f, 0f), obj.Name, styles.Sign, GuiStyles.ThingGlow);
                }

                // Each door outside is labelled with the building it leads into.
                foreach (var portal in simulation.World.Portals)
                {
                    if (simulation.World.TryGetMap(portal.From.MapId, out var outside) && outside.IsExterior
                        && simulation.World.TryGetMap(portal.To.MapId, out var inside) && !inside.IsExterior)
                    {
                        Label(panel, origin, view.ToWorld(portal.From) + new Vector3(0f, 0.5f, 0f), inside.Name, styles.Sign, GuiStyles.DoorGlow);
                    }
                }
            }

            foreach (var r in simulation.Residents)
            {
                if (!view.TryGetPosition(r.Id, out var position) || !panel.TryScreenPoint(position, out _))
                {
                    continue;
                }

                Label(panel, origin, position + new Vector3(0f, 0.86f, 0f), r.FirstName, styles.Name, GuiStyles.PersonGlow);

                // A bubble appears for each new line a resident says, and fades after a few seconds.
                if (speech.TryGetValue(r.Id, out var bubble) && Time.unscaledTime < bubble.until && panel.TryScreenPoint(position + new Vector3(0f, 1.05f, 0f), out var above))
                {
                    var content = new GUIContent($"“{bubble.line.Text}”");
                    var width = Mathf.Min(220f, panel.ScreenRect.width * 0.8f);
                    var height = styles.Bubble.CalcHeight(content, width);
                    GUI.Box(new Rect(above.x - origin.x - (width / 2f), above.y - origin.y - height, width, height), content, styles.Bubble);
                }
            }
        }

        /// <summary>The offsets the glow is drawn at around a label: a soft ring, then a tighter, brighter one.</summary>
        private static readonly (float x, float y, float alpha)[] GlowPasses = MakeGlowPasses();

        /// <summary>A neon label: the text over a soft halo of <paramref name="glow"/>, with a thin dark shadow.</summary>
        private static void Label(CameraPanel panel, Vector2 origin, Vector3 world, string text, GUIStyle style, Color glow)
        {
            if (Event.current.type != EventType.Repaint || !panel.TryScreenPoint(world, out var gui))
            {
                return;
            }

            var rect = new Rect(gui.x - origin.x - 90f, gui.y - origin.y - 10f, 180f, 20f);
            var content = new GUIContent(text);
            var before = GUI.color;
            foreach (var (x, y, alpha) in GlowPasses)
            {
                GUI.color = new Color(glow.r, glow.g, glow.b, alpha);
                style.Draw(new Rect(rect.x + x, rect.y + y, rect.width, rect.height), content, false, false, false, false);
            }

            GUI.color = GuiStyles.LabelShadow;
            style.Draw(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, false, false, false, false);
            GUI.color = GuiStyles.LabelText;
            style.Draw(rect, content, false, false, false, false);
            GUI.color = before;
        }

        private static (float, float, float)[] MakeGlowPasses()
        {
            var passes = new List<(float, float, float)>();
            foreach (var (radius, alpha) in new[] { (3f, 0.12f), (1.5f, 0.35f) })
            {
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Mathf.PI / 4f;
                    passes.Add((Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, alpha));
                }
            }

            return passes.ToArray();
        }

        private static void DrawFrame(CameraPanel panel, bool active)
        {
            var r = panel.ScreenRect;
            var p = UiTheme.Palette;
            GUI.color = UiTheme.ToColor(p.Bar, 0.9f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, HeaderHeight), Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(active ? p.AccentA : p.BarBorder);
            var thickness = active ? 3f : 1f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
