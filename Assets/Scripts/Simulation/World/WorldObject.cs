using System;

namespace TinyTown.Simulation.World
{
    public enum ObjectType
    {
        Bed,
        Kitchen,
        Sink,
        Toilet,
        Shower,
        Armchair,
        GardenPlot,
        GardenTap,
        Bench,
        ExerciseSpot,
    }

    /// <summary>Fixed furniture or outdoor feature. A resident uses it by standing on its tile.</summary>
    public sealed class WorldObject
    {
        public WorldObject(string id, string name, ObjectType type, Location location)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Object id is required.", nameof(id));
            }

            Id = id;
            Name = name ?? id;
            Type = type;
            Location = location;
        }

        public string Id { get; }

        public string Name { get; }

        public ObjectType Type { get; }

        public Location Location { get; }
    }

    /// <summary>One-way link between two walkable tiles on different maps, such as a front door.</summary>
    public sealed class Portal
    {
        public Portal(string id, Location from, Location to)
        {
            if (from.MapId == to.MapId)
            {
                throw new ArgumentException($"Portal '{id}' must connect two different maps.");
            }

            Id = id;
            From = from;
            To = to;
        }

        public string Id { get; }

        public Location From { get; }

        public Location To { get; }
    }
}
