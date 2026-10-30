using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.World
{
    /// <summary>Static layout of a town: maps, furniture and doors. Not saved; saves refer to it by stable ids.</summary>
    public sealed class TownWorld
    {
        private readonly Dictionary<string, TileMap> maps;
        private readonly Dictionary<string, WorldObject> objectsById;
        private readonly List<WorldObject> objects;
        private readonly List<Portal> portals;
        private readonly HashSet<Location> blocked;

        public TownWorld(string townId, string townName, IEnumerable<TileMap> maps, IEnumerable<WorldObject> objects, IEnumerable<Portal> portals, IEnumerable<Business> businesses = null, Community.CommunityCalendar calendar = null, IEnumerable<Job> jobOpenings = null)
        {
            if (string.IsNullOrWhiteSpace(townId))
            {
                throw new ArgumentException("Town id is required.", nameof(townId));
            }

            TownId = townId;
            TownName = townName ?? townId;
            this.maps = new Dictionary<string, TileMap>();
            foreach (var map in maps)
            {
                if (this.maps.ContainsKey(map.Id))
                {
                    throw new ArgumentException($"Duplicate map id '{map.Id}'.");
                }

                this.maps.Add(map.Id, map);
            }

            this.objects = objects.ToList();
            objectsById = new Dictionary<string, WorldObject>();
            foreach (var obj in this.objects)
            {
                if (objectsById.ContainsKey(obj.Id))
                {
                    throw new ArgumentException($"Duplicate object id '{obj.Id}'.");
                }

                if (!IsWalkable(obj.Location))
                {
                    throw new ArgumentException($"Object '{obj.Id}' is not on a walkable tile.");
                }

                objectsById.Add(obj.Id, obj);
            }

            Businesses = (businesses ?? Enumerable.Empty<Business>()).ToList();
            foreach (var business in Businesses)
            {
                if (!this.maps.ContainsKey(business.MapId) || Businesses.Count(b => b.Id == business.Id || b.MapId == business.MapId) != 1)
                {
                    throw new ArgumentException($"Business '{business.Id}' needs its own existing map.");
                }
            }

            JobOpenings = (jobOpenings ?? Enumerable.Empty<Job>()).ToList();
            foreach (var job in JobOpenings)
            {
                if (!objectsById.ContainsKey(job.WorkObjectId) || JobOpenings.Count(j => j.WorkObjectId == job.WorkObjectId) != 1)
                {
                    throw new ArgumentException($"Job opening '{job.Title}' needs its own existing work object.");
                }
            }

            Calendar = calendar ?? new Community.CommunityCalendar(null);
            foreach (var e in Calendar.Events)
            {
                if (!objectsById.TryGetValue(e.PlaceObjectId, out var place) || place.Type != ObjectType.GatheringSpot)
                {
                    throw new ArgumentException($"Community event '{e.Id}' needs a gathering spot.");
                }
            }

            this.portals = portals.ToList();
            foreach (var portal in this.portals)
            {
                if (!IsWalkable(portal.From) || !IsWalkable(portal.To))
                {
                    throw new ArgumentException($"Portal '{portal.Id}' does not join two walkable tiles.");
                }
            }

            // Doorways stay open even if something was placed on one.
            var doorways = new HashSet<Location>(this.portals.SelectMany(p => new[] { p.From, p.To }));
            blocked = new HashSet<Location>(this.objects.Where(o => o.BlocksPassing && !doorways.Contains(o.Location)).Select(o => o.Location));
        }

        public string TownId { get; }

        public string TownName { get; }

        public IEnumerable<TileMap> Maps => maps.Values;

        public IReadOnlyList<WorldObject> Objects => objects;

        public IReadOnlyList<Portal> Portals => portals;

        /// <summary>Shops, café, bakery and clinic, each with its own interior map.</summary>
        public IReadOnlyList<Business> Businesses { get; }

        /// <summary>
        /// Jobs that wait for someone to take them, such as running the city's venues: each is filled by a jobseeker
        /// (see <c>TownSimulation.FillVacancies</c>) whenever nobody alive has it.
        /// </summary>
        public IReadOnlyList<Job> JobOpenings { get; }

        /// <summary>The village calendar of community events.</summary>
        public Community.CommunityCalendar Calendar { get; }

        /// <summary>The business whose building contains this map, or null.</summary>
        public Business BusinessOnMap(string mapId) => Businesses.FirstOrDefault(b => b.MapId == mapId);

        public bool TryGetMap(string mapId, out TileMap map)
        {
            if (mapId == null)
            {
                map = null;
                return false;
            }

            return maps.TryGetValue(mapId, out map);
        }

        public TileMap GetMap(string mapId)
        {
            if (!TryGetMap(mapId, out var map))
            {
                throw new KeyNotFoundException($"Unknown map '{mapId}'.");
            }

            return map;
        }

        public bool TryGetObject(string objectId, out WorldObject obj)
        {
            if (objectId == null)
            {
                obj = null;
                return false;
            }

            return objectsById.TryGetValue(objectId, out obj);
        }

        /// <summary>True where furniture stands: walkable as a destination, but never walked through.</summary>
        public bool IsBlocked(Location location) => blocked.Contains(location);

        public bool IsWalkable(Location location) => TryGetMap(location.MapId, out var map) && map.IsWalkable(location.Position);

        public IEnumerable<WorldObject> ObjectsOfType(ObjectType type) => objects.Where(o => o.Type == type);

        public IEnumerable<Portal> PortalsFrom(string mapId) => portals.Where(p => p.From.MapId == mapId);
    }
}
