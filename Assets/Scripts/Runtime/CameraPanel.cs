using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// One view onto the town. A panel either follows a resident wherever they go, indoors and out, or is a free
    /// camera the player pans and zooms over one map.
    /// </summary>
    public sealed class CameraPanel
    {
        /// <summary>How much of the village a following camera shows: half the view height, in world units.</summary>
        public const float FollowHalfHeight = 4.5f;

        /// <summary>The closest a camera gets: half the view height in world units (about six rows of tiles).</summary>
        public const float MinHalfHeight = 1.25f;

        public CameraPanel(Camera camera, bool ownsCamera)
        {
            Camera = camera;
            OwnsCamera = ownsCamera;
            Camera.orthographic = true;
        }

        public Camera Camera { get; }

        /// <summary>False for the main camera, which the panel borrows and must not destroy.</summary>
        public bool OwnsCamera { get; }

        /// <summary>The resident followed, or null for a free camera.</summary>
        public string FollowId { get; set; }

        public bool IsFree => FollowId == null;

        /// <summary>The map shown: the followed resident's, or the one the free camera was put over.</summary>
        public string MapId { get; set; }

        /// <summary>Where a free camera looks.</summary>
        public Vector3 FreeCentre { get; set; }

        /// <summary>
        /// Above 1 shows more, below 1 shows less. Changed with the mouse wheel; <see cref="Aim"/> keeps it between
        /// <see cref="MinHalfHeight"/> and the whole map.
        /// </summary>
        public float Zoom { get; set; } = 1f;

        /// <summary>The panel's area on screen, in GUI coordinates (origin top left).</summary>
        public Rect ScreenRect { get; private set; }

        public void SetScreenRect(Rect rect)
        {
            ScreenRect = rect;
            var w = Mathf.Max(1, Screen.width);
            var h = Mathf.Max(1, Screen.height);
            Camera.rect = new Rect(rect.x / w, 1f - ((rect.y + rect.height) / h), rect.width / w, rect.height / h);
        }

        /// <summary>
        /// Points the camera at the followed resident or the free position, framing small interiors whole. The camera
        /// never shows more than the whole map, and stays over it: once the map fits in the view it can't be dragged.
        /// </summary>
        public void Aim(TownView view)
        {
            var (centre, halfHeight, halfWidth) = view.Bounds(MapId);
            var aspect = Mathf.Max(0.1f, Camera.aspect);
            var wholeMap = Mathf.Max(halfHeight, halfWidth / aspect) * 1.05f;
            var exterior = view.World != null && view.World.TryGetMap(MapId, out var map) && map.IsExterior;

            Vector3 target;
            float baseSize;
            if (!IsFree && view.TryGetPosition(FollowId, out var position))
            {
                // Indoors the room fits in the panel; outdoors the camera stays close and moves with them.
                baseSize = exterior ? Mathf.Min(FollowHalfHeight, wholeMap) : wholeMap;
                target = exterior ? position : centre;
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
        /// Moves a free camera by a drag of <paramref name="screenDelta"/> pixels, kept over its map (see
        /// <see cref="Aim"/>).
        /// </summary>
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

        /// <summary>Turns this into a free camera over a whole map.</summary>
        public void LookAtMap(string mapId, TownView view)
        {
            FollowId = null;
            MapId = mapId;
            FreeCentre = view.Bounds(mapId).centre;
            Zoom = 1f;
        }

        /// <summary>A screen position (GUI coordinates) for a world position, and whether it falls inside this panel.</summary>
        public bool TryScreenPoint(Vector3 world, out Vector2 gui)
        {
            var screen = Camera.WorldToScreenPoint(world);
            gui = new Vector2(screen.x, Screen.height - screen.y);
            return screen.z >= 0 && ScreenRect.Contains(gui);
        }
    }
}
