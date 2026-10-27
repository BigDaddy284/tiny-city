using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyTown.Simulation.World
{
    /// <summary>Static layout of a town: maps, furniture and doors. Not saved; saves refer to it by stable ids.</summary>
    public sealed class TownWorld
    {
        private readonly Dictionary<string, TileMap> maps;
        private readonly Dictionary<string, WorldObject> objectsById;
        private readonly List<WorldObject> objects;
        private readonly List<Portal> portals;

        public TownWorld(string townId, string townName, IEnumerable<TileMap> maps, IEnumerable<WorldObject> objects, IEnumerable<Portal> portals)
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

            this.portals = portals.ToList();
            foreach (var portal in this.portals)
            {
                if (!IsWalkable(portal.From) || !IsWalkable(portal.To))
                {
                    throw new ArgumentException($"Portal '{portal.Id}' does not join two walkable tiles.");
                }
            }
        }

        public string TownId { get; }

        public string TownName { get; }

        public IEnumerable<TileMap> Maps => maps.Values;

        public IReadOnlyList<WorldObject> Objects => objects;

        public IReadOnlyList<Portal> Portals => portals;

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

        public bool IsWalkable(Location location) => TryGetMap(location.MapId, out var map) && map.IsWalkable(location.Position);

        public IEnumerable<WorldObject> ObjectsOfType(ObjectType type) => objects.Where(o => o.Type == type);

        public IEnumerable<Portal> PortalsFrom(string mapId) => portals.Where(p => p.From.MapId == mapId);
    }
}
