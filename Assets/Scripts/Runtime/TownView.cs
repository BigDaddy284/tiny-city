using System.Collections.Generic;
using System.Linq;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The whole town on screen at once: every map is drawn side by side at its own origin, and every living
    /// resident stands on whichever map they are on. Camera panels then simply look at different places.
    /// The maps are redrawn when the season changes, and tinted by the time of day.
    /// </summary>
    public sealed class TownView : MonoBehaviour
    {
        /// <summary>Space left between maps, in world units.</summary>
        private const float MapGap = 6f;

        private readonly Dictionary<string, Vector3> origins = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, MapView> maps = new Dictionary<string, MapView>();
        private readonly Dictionary<string, SpriteRenderer> residents = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, Location> previous = new Dictionary<string, Location>();
        private readonly Dictionary<string, SpriteRenderer> moodIcons = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, Facing> facings = new Dictionary<string, Facing>();
        private readonly Dictionary<string, Sprite> residentSprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<Mood, Sprite> moodSprites = new Dictionary<Mood, Sprite>();
        private readonly SpriteFactory residentFactory = new SpriteFactory();

        /// <summary>Mood icons draw above everything else in the town.</summary>
        private const int MoodIconOrder = 32000;

        private ArtOverrides overrides;

        public TownWorld World { get; private set; }

        /// <summary>The season the maps are drawn for.</summary>
        public Season Season { get; private set; }

        public IReadOnlyDictionary<string, MapView> MapViews => maps;

        public IReadOnlyDictionary<string, SpriteRenderer> ResidentRenderers => residents;

        /// <summary>Draws every map of <paramref name="world"/>, the village first. Does nothing if it is already drawn for this season.</summary>
        public void Build(TownWorld world, Season season)
        {
            if (World == world && Season == season)
            {
                return;
            }

            if (World == world)
            {
                Redraw(season);
                return;
            }

            Clear();
            World = world;
            Season = season;
            overrides ??= ArtOverrides.FromStreamingAssets();
            var palette = Palette.For(season);
            var cursor = 0f;
            foreach (var map in world.Maps.OrderBy(m => m.IsExterior ? 0 : 1).ThenBy(m => m.Id, System.StringComparer.Ordinal))
            {
                // A map's left edge is half its height left of tile (0, 0); see IsoGrid.TileToWorld.
                var origin = new Vector3(cursor + ((map.Height - 1) * IsoGrid.TileWidth * 0.5f) + 1f, 0f, 0f);
                origins[map.Id] = origin;
                var view = new GameObject($"Map {map.Name}").AddComponent<MapView>();
                view.transform.SetParent(transform, false);
                view.Show(world, map, origin, palette, overrides);
                maps[map.Id] = view;
                cursor += ((map.Width + map.Height) * IsoGrid.TileWidth * 0.5f) + MapGap;
            }
        }

        /// <summary>Follows the clock: redraws the maps when the season turns and sets the light for the time of day.</summary>
        public void ApplyTime(GameTime time)
        {
            if (World == null)
            {
                return;
            }

            Build(World, Seasons.Of(time));
            foreach (var view in maps.Values)
            {
                view.ApplyLight(Lighting.At(time, view.Map.IsExterior));
            }
        }

        /// <summary>The colour sprites on a map are tinted with at the moment.</summary>
        public Color TintOf(string mapId)
        {
            return mapId != null && maps.TryGetValue(mapId, out var view) && view.CurrentLight.HasValue ? MapView.ToColor(view.CurrentLight.Value.Tint) : Color.white;
        }

        private void Redraw(Season season)
        {
            Season = season;
            var palette = Palette.For(season);
            foreach (var view in maps.Values)
            {
                view.Show(World, view.Map, origins[view.Map.Id], palette, overrides);
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
            moodIcons.Clear();
            previous.Clear();
            facings.Clear();
            residentSprites.Clear();
            moodSprites.Clear();
            residentFactory.Release();
        }

        /// <summary>The mood icon over each resident, hidden when there is nothing to show.</summary>
        public IReadOnlyDictionary<string, SpriteRenderer> MoodIcons => moodIcons;

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

        /// <summary>
        /// Places and animates every living resident, <paramref name="tickProgress"/> of the way from their last tile
        /// to their current one: walking between tiles, and otherwise sitting, lying or standing at whatever they are
        /// using, facing it, with their mood over their head.
        /// </summary>
        public void UpdateResidents(TownSimulation simulation, float tickProgress)
        {
            var t = Mathf.Clamp01(tickProgress);
            var sharing = new Dictionary<Location, int>();
            for (var i = 0; i < simulation.Residents.Count; i++)
            {
                var r = simulation.Residents[i];
                var renderer = RendererFor(r);
                renderer.enabled = r.IsAlive;
                var icon = moodIcons[r.Id];
                icon.enabled = false;
                if (!r.IsAlive)
                {
                    continue;
                }

                var here = r.Location;
                var from = previous.TryGetValue(r.Id, out var p) && p.MapId == here.MapId ? p.Position : here.Position;
                var moving = from != here.Position;
                var (action, placement) = ResidentPose.Choose(World, r, moving);
                var facing = FacingOf(simulation, r, from, here, action, placement);
                facings[r.Id] = facing;

                var look = ResidentLook.For(r.Id, r.AgeYears);
                var pose = new ResidentPose(action, placement.Stance, facing, FrameOf(action, t, i));
                renderer.sprite = SpriteFor(r.Id, look, pose);

                var offset = Vector3.zero;
                if (placement.Stance == Stance.Standing && placement.U == 0.5 && placement.V == 0.5)
                {
                    // Residents on the same tile stand side by side instead of on top of each other.
                    sharing.TryGetValue(here, out var alreadyHere);
                    sharing[here] = alreadyHere + 1;
                    offset = new Vector3((alreadyHere % 2 == 0 ? 1 : -1) * 0.16f * ((alreadyHere + 1) / 2), -0.05f * alreadyHere, 0f);
                }

                var x = Mathf.Lerp(from.X, here.Position.X, t) + (float)(placement.U - 0.5);
                var y = Mathf.Lerp(from.Y, here.Position.Y, t) + (float)(placement.V - 0.5);
                renderer.transform.position = TileToWorld(here.MapId, x, y) + offset + new Vector3(0f, (float)placement.Z / SpriteFactory.PixelsPerUnit, 0f);
                renderer.sortingOrder = IsoGrid.SortingOrder(Mathf.Max(from.X, here.Position.X), Mathf.Max(from.Y, here.Position.Y), IsoGrid.ResidentLayer);
                renderer.color = TintOf(here.MapId);

                var mood = Moods.Of(simulation, r);
                if (mood != Mood.None)
                {
                    icon.sprite = MoodSprite(mood);
                    icon.enabled = icon.sprite != null;
                    icon.transform.localPosition = new Vector3(0f, (ResidentArt.HeadTop(look, pose) + 2f) / SpriteFactory.PixelsPerUnit, 0f);
                }
            }
        }

        /// <summary>The frame to show: walking steps with the move between tiles, everything else with the clock.</summary>
        private static int FrameOf(ResidentAction action, float tickProgress, int index)
        {
            if (action == ResidentAction.Walk)
            {
                return Mathf.FloorToInt(tickProgress * 4f) % 4;
            }

            var rate = action == ResidentAction.Read ? 0.3f : action == ResidentAction.Exercise ? 3f : 2f;

            // Each resident is a little out of step with the others.
            return Mathf.FloorToInt((Time.time + (index * 0.37f)) * rate);
        }

        private Facing FacingOf(TownSimulation simulation, Resident r, GridPos from, Location here, ResidentAction action, Placement placement)
        {
            facings.TryGetValue(r.Id, out var last);
            if (action == ResidentAction.Walk)
            {
                return Facing.OfStep(here.Position.X - from.X, here.Position.Y - from.Y, last);
            }

            if (action == ResidentAction.Talk && r.ConversationId != null)
            {
                foreach (var other in simulation.Residents)
                {
                    if (other != r && other.IsAlive && other.ConversationId == r.ConversationId && other.Location.MapId == here.MapId)
                    {
                        return Facing.Towards(here.Position, other.Location.Position, last);
                    }
                }
            }

            return r.State == ResidentState.Performing ? placement.Facing : last;
        }

        private Sprite SpriteFor(string residentId, ResidentLook look, ResidentPose pose)
        {
            var key = $"{residentId}|{look.Hair}|{pose.Key}";
            if (!residentSprites.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = residentFactory.Create(ResidentArt.Render(look, pose), key);
                residentSprites[key] = sprite;
            }

            return sprite;
        }

        private Sprite MoodSprite(Mood mood)
        {
            if (!moodSprites.TryGetValue(mood, out var sprite) || sprite == null)
            {
                var canvas = MoodArt.Icon(mood);
                sprite = canvas == null ? null : residentFactory.Create(canvas, $"Mood {mood}");
                moodSprites[mood] = sprite;
            }

            return sprite;
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
                residents[r.Id] = renderer;

                var icon = new GameObject("Mood").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(renderer.transform, false);
                icon.sortingOrder = MoodIconOrder;
                icon.enabled = false;
                moodIcons[r.Id] = icon;
            }

            return renderer;
        }
    }
}
