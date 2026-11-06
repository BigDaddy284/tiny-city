using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The one camera (owner, 5 November: one camera only), filling the screen left of the inspector. It either follows
    /// a resident or is free over the land, panned by dragging and zoomed with the wheel; a middle click lets go of
    /// whoever it follows. It also draws the names and speech bubbles over the town.
    /// </summary>
    public sealed class TownCamera
    {
        /// <summary>How much of the land a following camera shows: half the view height, in world units.</summary>
        public const float FollowHalfHeight = 4.5f;

        /// <summary>The closest the camera gets: half the view height in world units (about six rows of tiles).</summary>
        public const float MinHalfHeight = 1.25f;

        private const float SpeechSeconds = 8f;

        /// <summary>Object names are only drawn when the camera is close enough for them to fit.</summary>
        private const float LabelPixelsPerUnit = 42f;

        private readonly Dictionary<string, (ConversationLine line, float until)> speech = new Dictionary<string, (ConversationLine, float)>();

        public TownCamera(Camera camera)
        {
            Camera = camera;
            Camera.orthographic = true;
        }

        /// <summary>The main camera.</summary>
        public Camera Camera { get; }

        /// <summary>The resident followed, or null for a free camera.</summary>
        public string FollowId { get; private set; }

        public bool IsFree => FollowId == null;

        /// <summary>The map shown: the followed resident's, or the one the free camera was put over.</summary>
        public string MapId { get; private set; }

        /// <summary>Where a free camera looks.</summary>
        public Vector3 FreeCentre { get; private set; }

        /// <summary>
        /// Above 1 shows more, below 1 shows less. Changed with the mouse wheel; <see cref="Aim"/> keeps it between
        /// <see cref="MinHalfHeight"/> and the whole map.
        /// </summary>
        public float Zoom { get; private set; } = 1f;

        /// <summary>The camera's area on screen, in GUI coordinates (origin top left).</summary>
        public Rect ScreenRect { get; private set; }

        /// <summary>Called with a resident's id when the player clicks on them.</summary>
        public Action<string> ResidentClicked { get; set; }

        public void Follow(string residentId, TownSimulation simulation)
        {
            var resident = simulation.GetResident(residentId);
            if (resident == null)
            {
                return;
            }

            FollowId = resident.Id;
            MapId = resident.Location.MapId;
            Zoom = 1f;
        }

        /// <summary>Moves the camera to the next (or previous) living resident.</summary>
        public void CycleResident(int step, TownSimulation simulation)
        {
            var living = simulation.Residents.Where(r => r.IsAlive).ToList();
            if (living.Count == 0)
            {
                return;
            }

            var current = living.FindIndex(r => r.Id == FollowId);
            var next = current < 0 ? 0 : (current + step + living.Count) % living.Count;
            Follow(living[next].Id, simulation);
        }

        /// <summary>A free camera over the whole of the largest outdoor map, as the game starts.</summary>
        public void ShowWholeLand(TownView view, TownSimulation simulation)
        {
            var land = simulation.World.Maps.Where(m => m.IsExterior).OrderByDescending(m => m.Width * m.Height).FirstOrDefault();
            LookAtMap(land?.Id ?? simulation.Residents[0].Location.MapId, view);
        }

        /// <summary>Turns this into a free camera over a whole map.</summary>
        public void LookAtMap(string mapId, TownView view)
        {
            FollowId = null;
            MapId = mapId;
            FreeCentre = view.Bounds(mapId).centre;
            Zoom = 1f;
        }

        /// <summary>Stops following anyone, leaving a free camera exactly where the view is now (owner, 5 November: middle click).</summary>
        public void Release(TownView view)
        {
            if (IsFree)
            {
                return;
            }

            var (_, halfHeight, halfWidth) = view.Bounds(MapId);
            var wholeMap = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.1f, Camera.aspect)) * 1.05f;
            FreeCentre = Camera.transform.position;
            Zoom = Camera.orthographicSize / Mathf.Max(0.01f, wholeMap);
            FollowId = null;
        }

        /// <summary>Fits the camera to <paramref name="screenArea"/> and aims it. Call once per frame.</summary>
        public void Update(Rect screenArea, TownView view, TownSimulation simulation)
        {
            var followed = IsFree ? null : simulation.GetResident(FollowId);
            if (!IsFree && (followed == null || !followed.IsAlive))
            {
                // Someone who has died can't be followed; the camera stays where they were, free.
                LookAtMap(MapId ?? simulation.Residents[0].Location.MapId, view);
            }
            else if (followed != null)
            {
                MapId = followed.Location.MapId;
            }

            ScreenRect = screenArea;
            var w = Mathf.Max(1, Screen.width);
            var h = Mathf.Max(1, Screen.height);
            Camera.rect = new Rect(screenArea.x / w, 1f - ((screenArea.y + screenArea.height) / h), screenArea.width / w, screenArea.height / h);
            Aim(view);
        }

        /// <summary>
        /// Points the camera at the followed resident or the free position. It never shows more than the whole map,
        /// and stays over it: once the map fits in the view it can't be dragged.
        /// </summary>
        private void Aim(TownView view)
        {
            var (centre, halfHeight, halfWidth) = view.Bounds(MapId);
            var aspect = Mathf.Max(0.1f, Camera.aspect);
            var wholeMap = Mathf.Max(halfHeight, halfWidth / aspect) * 1.05f;

            Vector3 target;
            float baseSize;
            if (!IsFree && view.TryGetPosition(FollowId, out var position))
            {
                baseSize = Mathf.Min(FollowHalfHeight, wholeMap);
                target = position;
            }
            else
            {
                baseSize = wholeMap;
                target = FreeCentre;
            }

            var size = Mathf.Clamp(baseSize * Zoom, Mathf.Min(MinHalfHeight, wholeMap), wholeMap);
            Zoom = size / Mathf.Max(0.01f, baseSize);
            target = Keep(target, centre, halfWidth, halfHeight, size * aspect, size);
            if (IsFree)
            {
                FreeCentre = target;
            }

            Camera.orthographic = true;
            Camera.orthographicSize = size;
            Camera.transform.position = new Vector3(target.x, target.y, -10f);
        }

        /// <summary>
        /// Where the view's centre may be so it shows as much of the map as it can: anywhere the view stays inside
        /// the map, or the map's centre on an axis where the map is smaller than the view.
        /// </summary>
        private static Vector3 Keep(Vector3 target, Vector3 centre, float mapHalfWidth, float mapHalfHeight, float viewHalfWidth, float viewHalfHeight)
        {
            var roomX = Mathf.Max(0f, mapHalfWidth - viewHalfWidth);
            var roomY = Mathf.Max(0f, mapHalfHeight - viewHalfHeight);
            return new Vector3(Mathf.Clamp(target.x, centre.x - roomX, centre.x + roomX), Mathf.Clamp(target.y, centre.y - roomY, centre.y + roomY), 0f);
        }

        /// <summary>Moves a free camera by a drag of <paramref name="screenDelta"/> pixels, kept over its map.</summary>
        public void Pan(Vector2 screenDelta, TownView view)
        {
            var unitsPerPixel = 2f * Camera.orthographicSize / Mathf.Max(1f, ScreenRect.height);
            FreeCentre += new Vector3(-screenDelta.x * unitsPerPixel, screenDelta.y * unitsPerPixel, 0f);
            Aim(view);
        }

        /// <summary>
        /// Zooms by one wheel step (<paramref name="steps"/> above 0 zooms out), keeping the point under
        /// <paramref name="gui"/> where it is for a free camera.
        /// </summary>
        public void ZoomAt(float steps, Vector2 gui, TownView view)
        {
            var before = Camera.ScreenToWorldPoint(new Vector3(gui.x, Screen.height - gui.y, 0f));
            Zoom *= 1f + (steps * 0.08f);
            Aim(view);
            if (IsFree)
            {
                var after = Camera.ScreenToWorldPoint(new Vector3(gui.x, Screen.height - gui.y, 0f));
                FreeCentre += new Vector3(before.x - after.x, before.y - after.y, 0f);
                Aim(view);
            }
        }

        /// <summary>A screen position (GUI coordinates) for a world position, and whether it falls inside the camera's area.</summary>
        public bool TryScreenPoint(Vector3 world, out Vector2 gui)
        {
            var screen = Camera.WorldToScreenPoint(world);
            gui = new Vector2(screen.x, Screen.height - screen.y);
            return screen.z >= 0 && ScreenRect.Contains(gui);
        }

        /// <summary>
        /// A click picks the resident under the pointer; a middle click stops following them; dragging pans a free
        /// camera; the wheel zooms.
        /// </summary>
        public void HandleMouse(Event e, TownView view)
        {
            if (!ScreenRect.Contains(e.mousePosition))
            {
                return;
            }

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 2:
                    Release(view);
                    e.Use();
                    break;
                case EventType.MouseDown:
                    var clicked = ResidentAt(e.mousePosition, view);
                    if (clicked != null)
                    {
                        ResidentClicked?.Invoke(clicked);
                    }

                    e.Use();
                    break;
                case EventType.MouseDrag when IsFree:
                    Pan(e.delta, view);
                    e.Use();
                    break;
                case EventType.ScrollWheel:
                    ZoomAt(e.delta.y, e.mousePosition, view);
                    e.Use();
                    break;
            }
        }

        private string ResidentAt(Vector2 gui, TownView view)
        {
            string best = null;
            var bestDistance = 24f;
            foreach (var pair in view.ResidentRenderers)
            {
                if (pair.Value == null || !pair.Value.enabled || !TryScreenPoint(pair.Value.transform.position + new Vector3(0f, 0.3f, 0f), out var point))
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
        /// Draws names and speech bubbles over the town. <paramref name="pointerFree"/> is false while the pointer is
        /// over a window, so things under it aren't named.
        /// </summary>
        public void DrawOverlays(TownView view, TownSimulation simulation, GuiStyles styles, bool pointerFree)
        {
            NoteNewSpeech(simulation);
            var mouse = Event.current.mousePosition;
            var inUse = new HashSet<string>(simulation.Residents
                .Where(r => r.IsAlive && r.State == ResidentState.Performing && r.TargetObjectId != null)
                .Select(r => r.TargetObjectId));
            var pointer = pointerFree && ScreenRect.Contains(mouse) ? mouse : (Vector2?)null;
            GUI.BeginGroup(ScreenRect);
            DrawLabels(view, simulation, styles, inUse, pointer);
            GUI.EndGroup();
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

        /// <summary>
        /// Names over residents and things. A thing is named only while someone is using it (when the camera is close
        /// enough) or while the pointer is over it (<paramref name="pointer"/>, in screen GUI coordinates).
        /// </summary>
        private void DrawLabels(TownView view, TownSimulation simulation, GuiStyles styles, HashSet<string> inUse, Vector2? pointer)
        {
            var origin = ScreenRect.position;
            var pixelsPerUnit = ScreenRect.height / (2f * Mathf.Max(0.1f, Camera.orthographicSize));
            var hovered = pointer.HasValue ? ObjectAt(pointer.Value, view, simulation, pixelsPerUnit) : null;
            if (hovered != null)
            {
                Label(origin, view.ToWorld(hovered.Location) + new Vector3(0f, 0.32f, 0f), hovered.Name, styles.Sign, GuiStyles.ThingGlow);
            }

            if (pixelsPerUnit >= LabelPixelsPerUnit)
            {
                foreach (var obj in simulation.World.Objects)
                {
                    if (obj != hovered && inUse.Contains(obj.Id))
                    {
                        Label(origin, view.ToWorld(obj.Location) + new Vector3(0f, 0.32f, 0f), obj.Name, styles.Sign, GuiStyles.ThingGlow);
                    }
                }
            }

            foreach (var r in simulation.Residents)
            {
                if (!view.TryGetPosition(r.Id, out var position) || !TryScreenPoint(position, out _))
                {
                    continue;
                }

                Label(origin, position + new Vector3(0f, 0.86f, 0f), string.IsNullOrEmpty(r.Role) ? r.FirstName : $"{r.FirstName} ({r.Role})", styles.Name, GuiStyles.PersonGlow);

                // A bubble appears for each new line a resident says, and fades after a few seconds.
                if (speech.TryGetValue(r.Id, out var bubble) && Time.unscaledTime < bubble.until && TryScreenPoint(position + new Vector3(0f, 1.05f, 0f), out var above))
                {
                    var content = new GUIContent($"“{bubble.line.Text}”");
                    var width = Mathf.Min(220f, ScreenRect.width * 0.8f);
                    var height = styles.Bubble.CalcHeight(content, width);
                    GUI.Box(new Rect(above.x - origin.x - (width / 2f), above.y - origin.y - height, width, height), content, styles.Bubble);
                }
            }
        }

        /// <summary>
        /// The thing whose tile (and a little above it, where the furniture stands) is under <paramref name="gui"/>, the
        /// nearest if several are, or null.
        /// </summary>
        private WorldObject ObjectAt(Vector2 gui, TownView view, TownSimulation simulation, float pixelsPerUnit)
        {
            WorldObject best = null;
            var bestDistance = 1f;
            foreach (var obj in simulation.World.Objects)
            {
                if (!TryScreenPoint(view.ToWorld(obj.Location) + new Vector3(0f, 0.12f, 0f), out var centre))
                {
                    continue;
                }

                // A diamond a tile wide, stretched upwards to cover the furniture standing on it.
                var distance = (Mathf.Abs(gui.x - centre.x) / (0.5f * pixelsPerUnit)) + (Mathf.Abs(gui.y - centre.y) / (0.37f * pixelsPerUnit));
                if (distance <= bestDistance)
                {
                    best = obj;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>The offsets the glow is drawn at around a label: a soft ring, then a tighter, brighter one.</summary>
        private static readonly (float x, float y, float alpha)[] GlowPasses = MakeGlowPasses();

        /// <summary>A neon label: the text over a soft halo of <paramref name="glow"/>, with a thin dark shadow.</summary>
        private void Label(Vector2 origin, Vector3 world, string text, GUIStyle style, Color glow)
        {
            if (Event.current.type != EventType.Repaint || !TryScreenPoint(world, out var gui))
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
    }
}
