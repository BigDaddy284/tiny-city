using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// The whole town on screen at once: every map is drawn side by side at its own origin, and every living
    /// resident stands on whichever map they are on. Camera panels then simply look at different places.
    /// </summary>
    public sealed class TownView : MonoBehaviour
    {
        /// <summary>Space left between maps, in world units.</summary>
        private const float MapGap = 6f;

        private readonly Dictionary<string, Vector3> origins = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, MapView> maps = new Dictionary<string, MapView>();
        private readonly Dictionary<string, SpriteRenderer> residents = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, Location> previous = new Dictionary<string, Location>();

        public TownWorld World { get; private set; }

        public IReadOnlyDictionary<string, SpriteRenderer> ResidentRenderers => residents;

        /// <summary>Draws every map of <paramref name="world"/>, the village first. Does nothing if it is already drawn.</summary>
        public void Build(TownWorld world)
        {
            if (World == world)
            {
                return;
            }

            Clear();
            World = world;
            var cursor = 0f;
            foreach (var map in world.Maps.OrderBy(m => m.IsExterior ? 0 : 1).ThenBy(m => m.Id, System.StringComparer.Ordinal))
            {
                // A map's left edge is half its height left of tile (0, 0); see IsoGrid.TileToWorld.
                var origin = new Vector3(cursor + ((map.Height - 1) * IsoGrid.TileWidth * 0.5f) + 1f, 0f, 0f);
                origins[map.Id] = origin;
                var view = new GameObject($"Map {map.Name}").AddComponent<MapView>();
                view.transform.SetParent(transform, false);
                view.Show(world, map, origin);
                maps[map.Id] = view;
                cursor += ((map.Width + map.Height) * IsoGrid.TileWidth * 0.5f) + MapGap;
            }
        }

        public void Clear()
        {
            foreach (var view in maps.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            ForgetResidents();
            maps.Clear();
            origins.Clear();
            World = null;
        }

        /// <summary>Removes resident figures, e.g. when a different town is loaded.</summary>
        public void ForgetResidents()
        {
            foreach (var renderer in residents.Values)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }

            residents.Clear();
            previous.Clear();
        }

        public Vector3 Origin(string mapId) => origins.TryGetValue(mapId ?? string.Empty, out var o) ? o : Vector3.zero;

        public Vector3 TileToWorld(string mapId, float x, float y) => Origin(mapId) + IsoGrid.TileToWorld(x, y);

        public Vector3 ToWorld(Location location) => TileToWorld(location.MapId, location.Position.X, location.Position.Y);

        /// <summary>The centre of a map and the half-height and half-width an orthographic camera needs to show all of it.</summary>
        public (Vector3 centre, float halfHeight, float halfWidth) Bounds(string mapId)
        {
            if (World == null || !World.TryGetMap(mapId, out var map))
            {
                return (Vector3.zero, 5f, 5f);
            }

            var left = TileToWorld(mapId, 0, map.Height - 1).x - 0.6f;
            var right = TileToWorld(mapId, map.Width - 1, 0).x + 0.6f;
            var top = TileToWorld(mapId, 0, 0).y + 0.9f;
            var bottom = TileToWorld(mapId, map.Width - 1, map.Height - 1).y - 0.4f;
            return (new Vector3((left + right) / 2f, (top + bottom) / 2f, 0f), (top - bottom) / 2f, (right - left) / 2f);
        }

        /// <summary>Which map a world position is over, or null if it is between maps.</summary>
        public string MapAt(Vector3 world)
        {
            foreach (var id in origins.Keys)
            {
                var (centre, halfHeight, halfWidth) = Bounds(id);
                if (Mathf.Abs(world.x - centre.x) <= halfWidth && Mathf.Abs(world.y - centre.y) <= halfHeight)
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>Remembers where everyone stands before a tick, so the figures can glide to their new tile.</summary>
        public void RememberPositions(TownSimulation simulation)
        {
            foreach (var r in simulation.Residents)
            {
                previous[r.Id] = r.Location;
            }
        }

        /// <summary>Places every living resident, <paramref name="tickProgress"/> of the way from their last tile to their current one.</summary>
        public void UpdateResidents(TownSimulation simulation, float tickProgress)
        {
            var t = Mathf.Clamp01(tickProgress);
            var sharing = new Dictionary<Location, int>();
            foreach (var r in simulation.Residents)
            {
                var renderer = RendererFor(r);
                renderer.enabled = r.IsAlive;
                if (!r.IsAlive)
                {
                    continue;
                }

                // Residents on the same tile stand side by side instead of on top of each other.
                var here = r.Location;
                sharing.TryGetValue(here, out var alreadyHere);
                sharing[here] = alreadyHere + 1;
                var offset = new Vector3((alreadyHere % 2 == 0 ? 1 : -1) * 0.16f * ((alreadyHere + 1) / 2), -0.05f, 0f);

                var from = previous.TryGetValue(r.Id, out var p) && p.MapId == here.MapId ? p.Position : here.Position;
                var x = Mathf.Lerp(from.X, here.Position.X, t);
                var y = Mathf.Lerp(from.Y, here.Position.Y, t);
                renderer.transform.position = TileToWorld(here.MapId, x, y) + offset;
                renderer.sortingOrder = IsoGrid.SortingOrder(Mathf.Max(from.X, here.Position.X), Mathf.Max(from.Y, here.Position.Y), 2);
            }
        }

        /// <summary>Where a resident's figure is drawn now.</summary>
        public bool TryGetPosition(string residentId, out Vector3 position)
        {
            if (residentId != null && residents.TryGetValue(residentId, out var renderer) && renderer != null && renderer.enabled)
            {
                position = renderer.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private SpriteRenderer RendererFor(Resident r)
        {
            if (!residents.TryGetValue(r.Id, out var renderer) || renderer == null)
            {
                renderer = new GameObject($"Resident {r.FirstName}").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(transform, false);
                renderer.sprite = PlaceholderArt.Person(PlaceholderArt.ParseColour(r.ColourHex, new Color(0.85f, 0.45f, 0.3f)));
                residents[r.Id] = renderer;
            }

            return renderer;
        }
    }
}
